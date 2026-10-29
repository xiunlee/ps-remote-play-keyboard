using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using PSKeyboard;

static class TransportTests
{
    enum Mode { Normal, AuthRequired, AddRejected, WrongDeviceType, StreamRejected, StreamDropped, FeedbackTruncated, FeedbackStalled, FullRangeFeedback, InvalidFeedbackFlags, PingNeverCloses, MalformedCreate, RemoveRejected }
    sealed class FakeServer : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly List<TcpClient> clients = new List<TcpClient>();
        readonly object gate = new object();
        readonly Mode mode;
        readonly string deviceType;
        volatile bool stopped;
        public readonly List<string> Requests = new List<string>();
        public readonly List<byte[]> Frames = new List<byte[]>();
        public int Port { get; private set; }
        public FakeServer(Mode mode) : this(mode, "dualshock4") { }
        public FakeServer(Mode mode, string deviceType)
        {
            this.mode = mode; this.deviceType = deviceType; listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Thread accept = new Thread(Accept); accept.IsBackground = true; accept.Start();
        }
        void Accept()
        {
            while (!stopped)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    lock (gate) clients.Add(client);
                    Thread worker = new Thread(delegate() { Handle(client); }); worker.IsBackground = true; worker.Start();
                }
                catch { if (!stopped) throw; }
            }
        }
        static void Reply(NetworkStream stream, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text + "\n");
            int split = Math.Min(3, bytes.Length); stream.Write(bytes, 0, split);
            Thread.Sleep(5); stream.Write(bytes, split, bytes.Length - split);
        }
        byte[] ReadFrame(NetworkStream stream)
        {
            byte[] frame = new byte[deviceType == "dualsense" ? 33 : 31]; int offset = 0;
            while (offset < frame.Length)
            {
                int count = stream.Read(frame, offset, frame.Length - offset);
                if (count == 0) return null;
                offset += count;
            }
            return frame;
        }
        void Handle(TcpClient client)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 3000;
                    StringBuilder command = new StringBuilder();
                    while (true)
                    {
                        int b = stream.ReadByte(); if (b < 0) return; if (b == 0) break;
                        command.Append((char)b); if (command.Length > 2048) return;
                    }
                    string request = command.ToString(); lock (gate) Requests.Add(request);
                    if (request == "ping")
                    {
                        Reply(stream, mode == Mode.AuthRequired ? "{\"status\":401,\"title\":\"Unauthorized\",\"detail\":\"authentication required\"}" : "{\"server\":\"VIIPER\",\"version\":\"0.9.0\"}");
                        if (mode == Mode.PingNeverCloses) Thread.Sleep(1000);
                    }
                    else if (request == "bus/create") Reply(stream, mode == Mode.MalformedCreate ? "{\"busId\":" : "{\"busId\":7}");
                    else if (request == "bus/7/add {\"type\":\"" + deviceType + "\"}") Reply(stream,
                        mode == Mode.AddRejected ? "{\"status\":400,\"title\":\"Bad Request\",\"detail\":\"rejected\"}" :
                        "{\"busId\":7,\"devId\":\"11\",\"type\":\"" + (mode == Mode.WrongDeviceType ? "wrong-device" : deviceType) + "\"}");
                    else if (request == "bus/remove 7") Reply(stream, mode == Mode.RemoveRejected ?
                        "{\"status\":500,\"title\":\"Error\",\"detail\":\"remove failed\"}" : "{\"busId\":7}");
                    else if (request == "bus/7/11")
                    {
                        if (mode == Mode.StreamRejected)
                        {
                            Reply(stream, "{\"status\":401,\"title\":\"Unauthorized\",\"detail\":\"stream denied\"}"); return;
                        }
                        while (!stopped)
                        {
                            byte[] frame = ReadFrame(stream); if (frame == null) return;
                            lock (gate) Frames.Add(frame);
                            if (mode == Mode.StreamDropped) { Thread.Sleep(250); return; }
                            if (mode == Mode.FeedbackTruncated) { stream.Write(new byte[] { 1, 2, 3 }, 0, 3); return; }
                            if (mode == Mode.FeedbackStalled) { stream.Write(new byte[] { 1, 2, 3 }, 0, 3); Thread.Sleep(1000); return; }
                            if (FrameCount == 1)
                            {
                                byte[] feedback = deviceType == "dualsense" ? new byte[] { 255, 128, 127, 1, 0, 31 } : new byte[] { 7, 1, 2, 3, 4, 5, 6, 7 };
                                if (mode == Mode.FullRangeFeedback) feedback = new byte[] { 123, 255, 254, 253, 252, 251, 255, 123, 128, 127, 0, 31 };
                                if (mode == Mode.InvalidFeedbackFlags) feedback[0] = 0x80;
                                stream.Write(feedback, 0, 3); Thread.Sleep(10); stream.Write(feedback, 3, feedback.Length - 3);
                            }
                        }
                    }
                    else Reply(stream, "{\"status\":404,\"title\":\"Not Found\"}");
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
        public int FrameCount { get { lock (gate) return Frames.Count; } }
        public byte[] Frame(int index) { lock (gate) return (byte[])Frames[index].Clone(); }
        public bool Saw(string command) { lock (gate) return Requests.Contains(command); }
        public void Dispose()
        {
            stopped = true; listener.Stop();
            lock (gate) foreach (TcpClient client in clients) client.Close();
        }
    }
    static int failures, checks;
    static void Check(bool result, string label) { checks++; if (!result) failures++; Console.WriteLine((result ? "PASS " : "FAIL ") + label); }
    static bool Until(Func<bool> condition)
    {
        Stopwatch time = Stopwatch.StartNew();
        while (time.ElapsedMilliseconds < 2500) { if (condition()) return true; Thread.Sleep(10); }
        return condition();
    }
    static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    static void FailureCase(Mode mode, string expected, bool cleanup)
    { FailureCase(mode, expected, cleanup, "dualshock4"); }
    static void FailureCase(Mode mode, string expected, bool cleanup, string deviceType)
    {
        using (FakeServer server = new FakeServer(mode, deviceType))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50, deviceType))
        {
            bool threw = false; Stopwatch time = Stopwatch.StartNew();
            try { transport.Connect(); } catch (IOException) { threw = true; }
            Check(threw && !transport.IsConnected && transport.LastFailure != null, mode + " rejects false success");
            Check(time.ElapsedMilliseconds < 2000, mode + " bounded failure");
            if (expected != null) Check(transport.LastFailure.Contains(expected), mode + " preserves reason");
            if (cleanup) Check(server.Saw("bus/remove 7") && transport.BusId == 0, mode + " removes only own allocated bus");
            else Check(!server.Saw("bus/remove 7"), mode + " does not remove unowned bus");
        }
    }
    static int Main(string[] args)
    {
        if ((args.Length == 2 || args.Length == 3) && args[0] == "--integration")
        {
            using (ViiperTransport transport = new ViiperTransport(Int32.Parse(args[1]), args.Length == 3 ? args[2] : "dualshock4"))
            {
                transport.Connect(); Console.WriteLine("INTEGRATION connected version=" + transport.ServerVersion + " type=" + transport.DeviceType + " device=" + transport.UsbBusId);
                byte[] frame = transport.NeutralFrame(); frame[0] = 64; frame[4] = 0x20;
                transport.Send(frame); Thread.Sleep(250); Check(transport.IsConnected, "actual local VIIPER stream remains open");
                transport.Disconnect(); Check(!transport.IsConnected && transport.BusId == 0 && transport.LastFailure == null, "actual own bus removed");
            }
            Console.WriteLine("No USBIP attach or Windows device installation was requested.");
            return failures == 0 ? 0 : 1;
        }
        using (FakeServer server = new FakeServer(Mode.Normal))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50))
        {
            transport.Connect(); Check(transport.IsConnected && transport.UsbBusId == "7-11", "connect validates bus and device IDs");
            Check(Until(delegate { return transport.FeedbackPackets == 1; }), "fragmented 8-byte feedback reassembled");
            Thread.Sleep(700); Check(transport.IsConnected, "idle feedback is not a disconnect");
            byte[] first = ViiperTransport.CreateNeutralFrame(); first[0] = 0x7f; first[4] = 0x20;
            transport.Send(first); Check(Until(delegate { return server.FrameCount >= 2; }) && Equal(server.Frame(1), first), "31-byte state delivered without rewriting");
            bool rejected = false; try { transport.Send(new byte[30]); } catch (ArgumentException) { rejected = true; }
            Check(rejected && transport.IsConnected, "wrong frame length rejected without corrupting stream");
            Thread[] senders = new Thread[4];
            for (int i = 0; i < senders.Length; i++)
            {
                int marker = i + 1;
                senders[i] = new Thread(delegate() { for (int n = 0; n < 20; n++) { byte[] f = ViiperTransport.CreateNeutralFrame(); f[0] = (byte)marker; f[1] = (byte)marker; transport.Send(f); } });
                senders[i].Start();
            }
            foreach (Thread sender in senders) sender.Join();
            Check(Until(delegate { return server.FrameCount == 82; }), "concurrent writers deliver complete frame count");
            bool intact = true; for (int i = 2; i < server.FrameCount; i++) { byte[] f = server.Frame(i); if (f[0] != f[1] || f[30] != 0xe0) intact = false; }
            Check(intact, "concurrent writes do not interleave frame bytes");
            transport.Disconnect();
            Check(Until(delegate { return server.FrameCount == 83; }) && Equal(server.Frame(82), ViiperTransport.CreateNeutralFrame()), "disconnect sends neutral with AccelZ minus8192");
            Check(!transport.IsConnected && transport.BusId == 0 && server.Saw("bus/remove 7"), "disconnect removes own bus");
            transport.Disconnect(); Check(transport.LastFailure == null, "disconnect is idempotent");
        }
        FailureCase(Mode.AuthRequired, "401", false);
        FailureCase(Mode.AddRejected, "400", true);
        FailureCase(Mode.StreamRejected, "401", true);
        FailureCase(Mode.PingNeverCloses, null, false);
        FailureCase(Mode.MalformedCreate, null, false);
        FailureCase(Mode.WrongDeviceType, "different bus or device type", true);
        foreach (Mode mode in new Mode[] { Mode.StreamDropped, Mode.FeedbackTruncated, Mode.FeedbackStalled, Mode.InvalidFeedbackFlags })
        {
            using (FakeServer server = new FakeServer(mode))
            using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50))
            {
                transport.Connect();
                Check(Until(delegate { return !transport.IsConnected && transport.BusId == 0; }), mode + " invalidates stream and cleans owned bus");
                Check(transport.LastFailure != null, mode + " exposes background failure");
                bool rejected = false; try { transport.Send(ViiperTransport.CreateNeutralFrame()); } catch (IOException) { rejected = true; }
                Check(rejected, mode + " prevents sends after feedback failure");
            }
        }
        using (FakeServer server = new FakeServer(Mode.RemoveRejected))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50))
        {
            transport.Connect(); transport.Disconnect();
            Check(!transport.IsConnected && transport.BusId == 7 && transport.LastFailure.Contains("cleanup failed"), "cleanup failure retains owned ID and reports failure");
            bool rejected = false; try { transport.Connect(); } catch (IOException) { rejected = true; }
            Check(rejected, "unconfirmed old cleanup prevents creating another bus");
        }
        TestDualSense();
        TestKeyboardConversion();
        Console.WriteLine("SUMMARY checks=" + checks + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }

    static void TestDualSense()
    {
        using (FakeServer server = new FakeServer(Mode.Normal, "dualsense"))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50, "dualsense"))
        {
            Check(transport.DeviceType == "dualsense" && transport.InputFrameSize == 33, "DualSense metadata uses explicit device type and 33-byte state");
            transport.Connect();
            Check(server.Saw("bus/7/add {\"type\":\"dualsense\"}") && transport.IsConnected, "DualSense creation requests exact device type");
            Check(Until(delegate { return transport.FeedbackPackets == 1; }), "fragmented 6-byte DualSense feedback accepts rumble above DS4 flag range");
            Check(Equal(server.Frame(0), ViiperTransport.CreateNeutralFrame("dualsense")), "DualSense handshake sends 33-byte neutral");
            byte[] pressed = transport.NeutralFrame(); pressed[6] = 3; pressed[0] = 127;
            transport.Send(pressed);
            Check(Until(delegate { return server.FrameCount == 2; }) && Equal(server.Frame(1), pressed), "DualSense input includes PS and touchpad high button bits");
            bool rejected = false; try { transport.Send(ViiperTransport.CreateNeutralFrame()); } catch (ArgumentException) { rejected = true; }
            Check(rejected && transport.IsConnected, "DS4-sized state rejected on DualSense without disconnect");
            transport.Disconnect();
            Check(Until(delegate { return server.FrameCount == 3; }) && Equal(server.Frame(2), ViiperTransport.CreateNeutralFrame("dualsense")), "DualSense disconnect releases all controls with its own frame size");
            Check(transport.BusId == 0 && server.Saw("bus/remove 7") && transport.LastFailure == null, "DualSense cleanup removes own bus");
        }
        using (FakeServer server = new FakeServer(Mode.FullRangeFeedback, "dualsense"))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50, "dualsense"))
        {
            transport.Connect();
            Check(Until(delegate { return transport.FeedbackPackets == 2; }) && transport.IsConnected,
                "DualSense arbitrary 6-byte feedback including leading brace is not misread as JSON or 8-byte DS4 feedback");
        }
        FailureCase(Mode.WrongDeviceType, "different bus or device type", true, "dualsense");
        FailureCase(Mode.StreamRejected, "401", true, "dualsense");
        foreach (Mode mode in new Mode[] { Mode.StreamDropped, Mode.FeedbackTruncated, Mode.FeedbackStalled })
        using (FakeServer server = new FakeServer(mode, "dualsense"))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50, "dualsense"))
        {
            transport.Connect();
            Check(Until(delegate { return !transport.IsConnected && transport.BusId == 0; }), "DualSense " + mode + " invalidates stream and cleans only owned bus");
            Check(transport.LastFailure != null, "DualSense " + mode + " reports feedback failure");
        }
        foreach (string type in new string[] { null, "", "DualSense", "dualsenseedge", "dualshock4\"}" })
        {
            bool rejected = false;
            try { using (ViiperTransport unused = new ViiperTransport(33242, type)) { } } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Unsupported device type rejected without network use");
        }
    }

    static void TestKeyboardConversion()
    {
        byte[] neutral = ViiperTransport.CreateNeutralFrame();
        byte[] same = ViiperTransport.ConvertKeyboardFrame(neutral, "dualshock4");
        Check(Equal(same, neutral) && !Object.ReferenceEquals(same, neutral), "Default DS4 conversion preserves state and returns independent storage");
        Check(Equal(ViiperTransport.ConvertKeyboardFrame(neutral, "dualsense"), ViiperTransport.CreateNeutralFrame("dualsense")), "Neutral keyboard conversion rests with AccelZ minus8192");
        // Independent fixtures for all 22 keyboard actions, using protocol
        // constants rather than the conversion bit-shift expression.
        string[] labels = { "W", "S", "A", "D", "Up", "Down", "Left", "Right", "Cross", "Circle", "Square", "Triangle", "L1", "R1", "L2", "R2", "Create", "Options", "L3", "R3", "PS", "Touchpad" };
        int[] positions = { 1, 1, 0, 0, 6, 6, 6, 6, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 4, 4 };
        byte[] values = { 128, 127, 128, 127, 1, 2, 4, 8, 0x20, 0x40, 0x10, 0x80, 1, 2, 4, 8, 0x10, 0x20, 0x40, 0x80, 1, 2 };
        int[] expectedPositions = { 1, 1, 0, 0, 8, 8, 8, 8, 4, 4, 4, 4, 5, 5, 5, 5, 5, 5, 5, 5, 6, 6 };
        for (int i = 0; i < labels.Length; i++)
        {
            byte[] source = ViiperTransport.CreateNeutralFrame(); source[positions[i]] = values[i];
            byte[] expected = ViiperTransport.CreateNeutralFrame("dualsense"); expected[expectedPositions[i]] = values[i];
            if (i == 14) { source[7] = 255; expected[9] = 255; }
            if (i == 15) { source[8] = 255; expected[10] = 255; }
            byte[] original = (byte[])source.Clone();
            Check(Equal(ViiperTransport.ConvertKeyboardFrame(source, "dualsense"), expected) && Equal(source, original), "Keyboard action " + labels[i] + " has exact DualSense bytes without modifying input");
        }
        byte[] combined = ViiperTransport.CreateNeutralFrame(); combined[4] = 0xF3; combined[5] = 0xFF; combined[6] = 9;
        byte[] all = ViiperTransport.ConvertKeyboardFrame(combined, "dualsense");
        Check(all[4] == 0xF0 && all[5] == 0xFF && all[6] == 3 && all[7] == 0 && all[8] == 9, "Combined buttons retain PS/touchpad without enabling microphone or Edge-only buttons");
        byte[] badTouch = ViiperTransport.CreateNeutralFrame(); badTouch[13] = 1;
        byte[] badGyro = ViiperTransport.CreateNeutralFrame(); badGyro[19] = 1;
        byte[] badGravity = ViiperTransport.CreateNeutralFrame(); badGravity[30] = 0;
        byte[] badButtons = ViiperTransport.CreateNeutralFrame(); badButtons[4] = 4;
        foreach (byte[] invalid in new byte[][] { null, new byte[30], new byte[33], badTouch, badGyro, badGravity, badButtons })
        {
            bool rejected = false; try { ViiperTransport.ConvertKeyboardFrame(invalid, "dualsense"); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Non-keyboard or invalid input is rejected instead of silently converting motion/touch formats");
        }
    }
}
