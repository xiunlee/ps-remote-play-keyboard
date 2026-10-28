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
    enum Mode { Normal, AuthRequired, AddRejected, StreamRejected, StreamDropped, FeedbackTruncated, FeedbackStalled, PingNeverCloses, MalformedCreate, RemoveRejected }
    sealed class FakeServer : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly List<TcpClient> clients = new List<TcpClient>();
        readonly object gate = new object();
        readonly Mode mode;
        volatile bool stopped;
        public readonly List<string> Requests = new List<string>();
        public readonly List<byte[]> Frames = new List<byte[]>();
        public int Port { get; private set; }
        public FakeServer(Mode mode)
        {
            this.mode = mode; listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
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
        static byte[] ReadFrame(NetworkStream stream)
        {
            byte[] frame = new byte[31]; int offset = 0;
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
                    else if (request == "bus/7/add {\"type\":\"dualshock4\"}") Reply(stream,
                        mode == Mode.AddRejected ? "{\"status\":400,\"title\":\"Bad Request\",\"detail\":\"rejected\"}" :
                        "{\"busId\":7,\"devId\":\"11\",\"type\":\"dualshock4\"}");
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
                                byte[] feedback = new byte[] { 7, 1, 2, 3, 4, 5, 6, 7 };
                                stream.Write(feedback, 0, 3); Thread.Sleep(10); stream.Write(feedback, 3, 5);
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
    {
        using (FakeServer server = new FakeServer(mode))
        using (ViiperTransport transport = new ViiperTransport(server.Port, 300, 50))
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
        if (args.Length == 2 && args[0] == "--integration")
        {
            using (ViiperTransport transport = new ViiperTransport(Int32.Parse(args[1])))
            {
                transport.Connect(); Console.WriteLine("INTEGRATION connected version=" + transport.ServerVersion + " device=" + transport.UsbBusId);
                byte[] frame = ViiperTransport.CreateNeutralFrame(); frame[0] = 64; frame[4] = 0x20;
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
        foreach (Mode mode in new Mode[] { Mode.StreamDropped, Mode.FeedbackTruncated, Mode.FeedbackStalled })
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
        Console.WriteLine("SUMMARY checks=" + checks + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
