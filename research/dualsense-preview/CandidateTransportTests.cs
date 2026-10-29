// Offline loopback fakes only. This executable has no live-server mode.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PSKeyboard;

internal static class CandidateTransportTests
{
    const string Persona = "dualsensecombinedaudioduplexv5";
    static readonly string[] Personas = { Persona, "dualsensegamepadv5", "dualsenseedgecombinedaudioduplexv5", "dualsenseedgegamepadv5" };
    enum Mode { Normal, WrongMarker, WrongCommit, WrongVersion, AutoAttach, AutoAttachString, WrongApi, WrongUsb,
        AuthRequired, PingTooLarge, PingNeverCloses, PingTrickle, MalformedCreate, AddRejected, WrongPersona,
        WrongBus, InvalidDeviceId, StreamRejected, StreamDropped, BadCrc, BadSequence, TruncatedFeedback,
        StalledFeedback, TrickleFeedback, UnknownFeedback, RemoveRejected, RemoveMismatch, Remove404 }
    sealed class FakeServer : IDisposable
    {
        readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        readonly object gate = new object();
        readonly List<TcpClient> clients = new List<TcpClient>();
        readonly List<string> requests = new List<string>();
        readonly List<byte[]> frames = new List<byte[]>();
        readonly List<string> faults = new List<string>();
        readonly Mode mode; readonly string persona;
        volatile bool stopped;
        internal volatile bool AllowRemoval;
        internal int Port { get; private set; }
        internal FakeServer(Mode mode) : this(mode, Persona) { }
        internal FakeServer(Mode mode, string persona)
        {
            this.mode = mode; this.persona = persona; listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var accept = new Thread(Accept); accept.IsBackground = true; accept.Start();
        }
        void Accept()
        {
            while (!stopped) {
                try {
                    TcpClient client = listener.AcceptTcpClient(); lock (gate) clients.Add(client);
                    var worker = new Thread(delegate() { Handle(client); }); worker.IsBackground = true; worker.Start();
                } catch (Exception error) { if (!stopped) lock (gate) faults.Add("accept: " + error.Message); return; }
            }
        }
        static void Reply(NetworkStream stream, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json); int first = Math.Min(3, bytes.Length);
            stream.Write(bytes, 0, first); Thread.Sleep(2); stream.Write(bytes, first, bytes.Length - first);
        }
        string Ping()
        {
            var ping = new Dictionary<string, object> {
                {"labMarker",mode == Mode.WrongMarker ? "pskeyboard-dualsense-v5-userspace-only" : "pskeyboard-dualsense-v5-candidate"},
                {"sourceCommit",mode == Mode.WrongCommit ? "not-the-pinned-commit" : "b78e31e93e84b4cb7c4b4c15b8aace10749106d2"},
                {"version",mode == Mode.WrongVersion ? "research.preview.2" : "research.preview.1"},
                {"apiAddress",mode == Mode.WrongApi ? "127.0.0.1:33242" : "127.0.0.1:33642"},
                {"usbAddress",mode == Mode.WrongUsb ? "127.0.0.1:33241" : "127.0.0.1:33641"},
                {"autoAttach",mode == Mode.AutoAttachString ? (object)"false" : (object)(mode == Mode.AutoAttach)}
            };
            return new JavaScriptSerializer().Serialize(ping);
        }
        void Handle(TcpClient client)
        {
            try {
                using (client)
                using (NetworkStream stream = client.GetStream()) {
                    stream.ReadTimeout = 4000; stream.WriteTimeout = 1000;
                    var command = new StringBuilder();
                    for (;;) {
                        int value = stream.ReadByte(); if (value < 0) return; if (value == 0) break;
                        if (command.Length >= 2048) throw new InvalidDataException("Oversized command.");
                        command.Append((char)value);
                    }
                    string request = command.ToString(); lock (gate) requests.Add(request);
                    if (request == "ping") {
                        if (mode == Mode.AuthRequired) { Reply(stream, "{\"status\":401}"); return; }
                        if (mode == Mode.PingTooLarge) { Reply(stream, "{\"padding\":\"" + new string('x', 17000) + "\"}"); return; }
                        if (mode == Mode.PingTrickle) {
                            byte[] bytes = Encoding.UTF8.GetBytes(Ping());
                            foreach (byte value in bytes) { stream.WriteByte(value); Thread.Sleep(15); } return;
                        }
                        Reply(stream, Ping()); if (mode == Mode.PingNeverCloses) Thread.Sleep(1200);
                    } else if (request == "bus/create") Reply(stream, mode == Mode.MalformedCreate ? "{\"busId\":" : "{\"busId\":7}");
                    else if (request == "bus/7/add {\"type\":\"" + persona + "\"}") {
                        if (mode == Mode.AddRejected) Reply(stream, "{\"status\":400}");
                        else Reply(stream, "{\"busId\":" + (mode == Mode.WrongBus ? "8" : "7") + ",\"devId\":\"" +
                            (mode == Mode.InvalidDeviceId ? "11/injected" : "11") + "\",\"type\":\"" +
                            (mode == Mode.WrongPersona ? "dualsenseraw" : persona) + "\"}");
                    } else if (request == "bus/remove 7") {
                        Reply(stream, mode == Mode.Remove404 ? "{\"status\":404}" :
                            !AllowRemoval && mode == Mode.RemoveRejected ? "{\"status\":500}" :
                            !AllowRemoval && mode == Mode.RemoveMismatch ? "{\"busId\":8}" : "{\"busId\":7}");
                    } else if (request == "bus/7/11") {
                        if (mode == Mode.StreamRejected) { Reply(stream, "{\"status\":401,\"detail\":\"stream denied\"}"); return; }
                        uint expected = 0;
                        while (!stopped) {
                            byte[] packet = ReadInput(stream, expected); if (packet == null) return; expected++;
                            lock (gate) frames.Add(packet);
                            if (expected != 1) continue;
                            if (mode == Mode.StreamDropped) { Thread.Sleep(100); return; }
                            byte[] feedback = ServerFrame(0x81, 0);
                            if (mode == Mode.BadCrc) feedback[feedback.Length - 1] ^= 1;
                            if (mode == Mode.BadSequence) feedback = ServerFrame(0x81, 1);
                            if (mode == Mode.UnknownFeedback) feedback = ServerFrame(0x82, 0);
                            if (mode == Mode.TruncatedFeedback || mode == Mode.StalledFeedback) {
                                stream.Write(feedback, 0, 18);
                                if (mode == Mode.StalledFeedback) Thread.Sleep(1200); return;
                            }
                            if (mode == Mode.TrickleFeedback) {
                                foreach (byte value in feedback) { stream.WriteByte(value); Thread.Sleep(15); } return;
                            }
                            // Partial first frame, then coalesced frames of every allowed type.
                            stream.Write(feedback, 0, 3); Thread.Sleep(5); stream.Write(feedback, 3, feedback.Length - 3);
                            if (mode == Mode.BadCrc || mode == Mode.BadSequence || mode == Mode.UnknownFeedback) continue;
                            using (var combined = new MemoryStream()) {
                                foreach (byte type in new byte[] { 0x83, 0x84, 0x85 }) {
                                    byte[] next = ServerFrame(type, (uint)(type == 0x83 ? 1 : type == 0x84 ? 2 : 3));
                                    combined.Write(next, 0, next.Length);
                                }
                                byte[] bytes = combined.ToArray(); stream.Write(bytes, 0, bytes.Length);
                            }
                        }
                    } else { lock (gate) faults.Add("Unexpected request: " + request); Reply(stream, "{\"status\":404}"); }
                }
            } catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            catch (Exception error) { lock (gate) faults.Add(error.ToString()); }
        }
        byte[] ReadInput(NetworkStream stream, uint expected)
        {
            var packet = new byte[49]; int offset = 0;
            while (offset < packet.Length) {
                int count = stream.Read(packet, offset, packet.Length - offset);
                if (count == 0) { if (offset != 0) lock (gate) faults.Add("Truncated client input."); return null; }
                offset += count;
            }
            if (packet[0] != 0x56 || packet[1] != 0x50 || packet[2] != 0x43 || packet[3] != 0x4d || packet[4] != 5 ||
                packet[5] != 1 || packet[6] != 33 || packet[7] != 0 || Read32(packet, 8) != expected || Read32(packet, 12) != Crc(packet))
                lock (gate) faults.Add("Malformed V5 input, CRC or sequence at " + expected);
            return packet;
        }
        internal int FrameCount { get { lock (gate) return frames.Count; } }
        internal byte[] Frame(int index) { lock (gate) return (byte[])frames[index].Clone(); }
        internal int Count(string request) { lock (gate) { int count = 0; foreach (string item in requests) if (item == request) count++; return count; } }
        internal string Faults { get { lock (gate) return String.Join(" | ", faults.ToArray()); } }
        public void Dispose()
        { stopped = true; listener.Stop(); lock (gate) foreach (TcpClient client in clients) client.Close(); }
    }

    // Independent table-based IEEE CRC reference, not V5Protocol.FrameCrc.
    static readonly uint[] CrcTable = MakeCrcTable();
    static uint[] MakeCrcTable()
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < table.Length; i++) {
            uint value = i; for (int bit = 0; bit < 8; bit++) value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0);
            table[i] = value;
        }
        return table;
    }
    static uint Crc(byte[] packet)
    {
        uint crc = 0xffffffff;
        for (int i = 4; i < packet.Length; i++) {
            if (i >= 12 && i < 16) continue;
            crc = (crc >> 8) ^ CrcTable[(crc ^ packet[i]) & 255];
        }
        return ~crc;
    }
    static uint Read32(byte[] bytes, int offset)
    { return bytes[offset] | ((uint)bytes[offset + 1] << 8) | ((uint)bytes[offset + 2] << 16) | ((uint)bytes[offset + 3] << 24); }
    static void Put32(byte[] bytes, int offset, uint value)
    { for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (8 * i)); }
    static byte[] ServerFrame(byte type, uint sequence)
    {
        int length = type == 0x83 ? 2396 : type == 0x85 ? 9 : 474;
        byte[] packet = new byte[16 + length]; packet[0] = 0x56; packet[1] = 0x50; packet[2] = 0x43; packet[3] = 0x4d;
        packet[4] = 5; packet[5] = type; packet[6] = (byte)length; packet[7] = (byte)(length >> 8); Put32(packet, 8, sequence);
        for (int i = 16; i < packet.Length; i++) packet[i] = (byte)(i * 37 + 11);
        if (type == 0x83) { packet[16] = 0xda; packet[17] = 1; }
        if (type == 0x85) packet[16] = 1;
        Put32(packet, 12, Crc(packet)); return packet;
    }
    static int checks, failures;
    static void Check(bool condition, string label)
    { checks++; if (!condition) failures++; Console.WriteLine((condition ? "PASS " : "FAIL ") + label); }
    static bool Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 2200) { if (condition()) return true; Thread.Sleep(10); }
        return condition();
    }
    static CandidateTransport Transport(FakeServer server, string persona)
    { return new CandidateTransport(server.Port, persona, 350, 50); }
    static void FailureCase(Mode mode, bool allocated)
    {
        using (var server = new FakeServer(mode))
        using (var transport = Transport(server, Persona)) {
            bool threw = false; var timer = Stopwatch.StartNew();
            try { transport.Connect(); } catch (IOException) { threw = true; }
            Check(threw && !transport.IsConnected && !String.IsNullOrEmpty(transport.LastFailure), mode + " refuses success");
            Check(timer.ElapsedMilliseconds < 1800, mode + " bounded deadline");
            Check(allocated ? server.Count("bus/remove 7") == 1 && transport.BusId == 0 : server.Count("bus/remove 7") == 0,
                mode + " only confirmed owned bus can be removed");
            if (!allocated && mode != Mode.MalformedCreate) Check(server.Count("bus/create") == 0, mode + " identity checked before mutation");
        }
    }
    static void FeedbackFailureCase(Mode mode)
    {
        using (var server = new FakeServer(mode))
        using (var transport = Transport(server, Persona)) {
            transport.Connect(); var timer = Stopwatch.StartNew();
            Check(Until(delegate { return !transport.IsConnected && transport.BusId == 0; }), mode + " disconnects and removes owned bus");
            Check(!String.IsNullOrEmpty(transport.LastFailure) && transport.FeedbackPackets == 0, mode + " error recorded; invalid frame never counted");
            Check(timer.ElapsedMilliseconds < 1800 && server.Count("bus/remove 7") == 1, mode + " bounded cleanup once");
            bool rejected = false; try { transport.SendNeutral(); } catch (IOException) { rejected = true; }
            Check(rejected, mode + " later sends fail");
        }
    }
    static void NormalCase(string persona)
    {
        using (var server = new FakeServer(Mode.Normal, persona))
        using (var transport = Transport(server, persona)) {
            transport.Connect();
            Check(transport.IsConnected && transport.UsbBusId == "7-11" && transport.DeviceType == "dualsense" &&
                transport.Persona == persona && transport.ServerVersion == "research.preview.1", persona + " identity and device allocation");
            Check(Until(delegate { return transport.FeedbackPackets == 4; }), persona + " fragmented/coalesced 81/83/84/85 drained with shared sequence");
            Check(Until(delegate { return server.FrameCount >= 1; }), persona + " initial neutral delivered");
            byte[] initial = server.Frame(0);
            Check(Read32(initial, 12) == 0xb88db821u && initial[31] == 0x80 && initial[36] == 0x80,
                persona + " fixed golden neutral CRC and native inactive touch");
            Thread.Sleep(800); Check(transport.IsConnected, persona + " idle exceeds frame timeout without disconnect");
            byte[] legacy = transport.NeutralFrame(); legacy[4] = 0x20; transport.Send(legacy);
            Check(legacy[15] == 0 && legacy[20] == 0, persona + " caller legacy array not mutated");
            bool rejected = false; try { transport.Send(new byte[53]); } catch (ArgumentException) { rejected = true; }
            Check(rejected && transport.IsConnected, persona + " raw/invalid length refused without losing stream");
            transport.SendNeutral(); transport.Dispose();
            Check(Until(delegate { return server.FrameCount == 4; }), persona + " explicit and dispose neutral delivered");
            Check(server.Frame(1)[20] == 0x20 && server.Frame(2)[20] == 0 && Read32(server.Frame(3), 8) == 3,
                persona + " controls, neutral and sequence advance without invalid-input gap");
            Check(!transport.IsConnected && transport.BusId == 0 && transport.LastFailure == null && server.Count("bus/remove 7") == 1 && server.Faults == "",
                persona + " clean close and exact bus removal with valid CRCs");
        }
    }
    static void ConcurrentAndReconnect()
    {
        using (var server = new FakeServer(Mode.Normal))
        using (var transport = Transport(server, Persona)) {
            transport.Connect(); var errors = new List<Exception>(); var senders = new Thread[4];
            for (int i = 0; i < senders.Length; i++) {
                senders[i] = new Thread(delegate() { try { for (int n = 0; n < 12; n++) transport.SendNeutral(); } catch (Exception error) { lock (errors) errors.Add(error); } });
                senders[i].Start();
            }
            foreach (Thread sender in senders) sender.Join(2000);
            Check(Until(delegate { return server.FrameCount == 49; }) && errors.Count == 0 && server.Faults == "", "concurrent sends preserve single input sequence and complete frames");
            transport.Disconnect(); transport.Connect();
            Check(Until(delegate { return server.FrameCount >= 51; }) && transport.IsConnected && Read32(server.Frame(50), 8) == 0,
                "new session resets input sequence; old reader cannot disconnect it");
            Check(server.Count("bus/create") == 2 && server.Count("bus/remove 7") == 1, "reconnect cleans previous owned bus before allocating");
        }
    }
    static void CleanupRetry(Mode mode)
    {
        using (var server = new FakeServer(mode))
        using (var transport = Transport(server, Persona)) {
            transport.Connect(); transport.Dispose();
            Check(!transport.IsConnected && transport.BusId == 7 && !String.IsNullOrEmpty(transport.LastFailure), mode + " preserves failed cleanup ownership");
            server.AllowRemoval = true; transport.Dispose();
            Check(transport.BusId == 0 && server.Count("bus/remove 7") == 2, mode + " repeated dispose retries only known bus");
        }
    }
    static void PendingReadAndFailedCleanup()
    {
        using (var server = new FakeServer(Mode.StalledFeedback))
        using (var transport = new CandidateTransport(server.Port, Persona, 2500, 50)) {
            transport.Connect(); Check(Until(delegate { return server.FrameCount == 1; }), "partial-feedback fixture received initial input");
            Thread.Sleep(60); var timer = Stopwatch.StartNew(); transport.Dispose();
            Check(timer.ElapsedMilliseconds < 1000 && !transport.IsConnected && transport.BusId == 0 && transport.LastFailure == null,
                "dispose closes socket to interrupt partial-frame read before its 2500ms deadline");
        }
        using (var server = new FakeServer(Mode.RemoveRejected))
        using (var transport = Transport(server, Persona)) {
            transport.Connect(); transport.Disconnect(); bool refused = false;
            try { transport.Connect(); } catch (IOException) { refused = true; }
            Check(refused && transport.BusId == 7 && server.Count("bus/create") == 1,
                "unconfirmed cleanup blocks reconnect before another bus is created");
            server.AllowRemoval = true; transport.Connect();
            Check(transport.IsConnected && server.Count("bus/create") == 2,
                "reconnect resumes only after previous owned-bus removal is confirmed");
        }
    }
    static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathRooted(args[0])) { Console.Error.WriteLine("Usage: CandidateTransportTests.exe <absolute-result-json-path>"); return 2; }
        var timer = Stopwatch.StartNew();
        try {
            foreach (string invalid in new[] { "dualsense", Persona + "raw", Persona + "events", "dualsenseaudioonlyv5", "DualSenseGamepadV5", "" }) {
                bool refused = false; try { CandidateTransport.ValidatePersona(invalid); } catch (ArgumentException) { refused = true; }
                Check(refused, "persona rejects exact-name mismatch: " + invalid);
            }
            bool wrongPort = false; try { new CandidateTransport(33242, Persona); } catch (ArgumentOutOfRangeException) { wrongPort = true; }
            Check(wrongPort, "public constructor cannot target working PS4 port");
            foreach (string persona in Personas) NormalCase(persona);
            foreach (Mode mode in new[] { Mode.WrongMarker, Mode.WrongCommit, Mode.WrongVersion, Mode.AutoAttach, Mode.AutoAttachString,
                Mode.WrongApi, Mode.WrongUsb, Mode.AuthRequired, Mode.PingTooLarge, Mode.PingNeverCloses, Mode.PingTrickle, Mode.MalformedCreate }) FailureCase(mode, false);
            foreach (Mode mode in new[] { Mode.AddRejected, Mode.WrongPersona, Mode.WrongBus, Mode.InvalidDeviceId, Mode.StreamRejected }) FailureCase(mode, true);
            foreach (Mode mode in new[] { Mode.StreamDropped, Mode.BadCrc, Mode.BadSequence, Mode.TruncatedFeedback, Mode.StalledFeedback, Mode.TrickleFeedback, Mode.UnknownFeedback }) FeedbackFailureCase(mode);
            ConcurrentAndReconnect(); CleanupRetry(Mode.RemoveRejected); CleanupRetry(Mode.RemoveMismatch); PendingReadAndFailedCleanup();
            using (var server = new FakeServer(Mode.Remove404))
            using (var transport = Transport(server, Persona)) {
                transport.Connect(); var close = Stopwatch.StartNew(); transport.Dispose();
                Check(close.ElapsedMilliseconds < 1000 && transport.BusId == 0 && transport.LastFailure == null, "idle reader close bounded; known bus 404 means already removed");
            }
        } catch (Exception error) { Check(false, "unexpected test exception: " + error); }
        var result = new Dictionary<string, object> { {"success",failures == 0}, {"checks",checks}, {"failures",failures},
            {"elapsed_ms",timer.ElapsedMilliseconds}, {"fake_loopback_only",true}, {"driver_operations",false}, {"sony_tested",false}, {"keyboard_captured",false} };
        string json = new JavaScriptSerializer().Serialize(result); File.WriteAllText(args[0], json + Environment.NewLine, new UTF8Encoding(false));
        Console.WriteLine("RESULT_JSON " + json); return failures == 0 ? 0 : 1;
    }
}
