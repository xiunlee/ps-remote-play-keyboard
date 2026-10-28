using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace PsKeyboard.PhysicalRelay
{
    // USB/IP 1.1.1 framing. No device management, HID opens, or keyboard capture.
    internal static class Wire
    {
        internal const int HeaderSize = 48, MaxTransfer = 1048576, MaxIsoPackets = 1024;
        internal static uint U32(byte[] b, int p) { return ((uint)b[p] << 24) | ((uint)b[p + 1] << 16) | ((uint)b[p + 2] << 8) | b[p + 3]; }
        internal static ushort U16(byte[] b, int p) { return (ushort)((b[p] << 8) | b[p + 1]); }
        internal static void Put32(byte[] b, int p, uint n) { b[p] = (byte)(n >> 24); b[p + 1] = (byte)(n >> 16); b[p + 2] = (byte)(n >> 8); b[p + 3] = (byte)n; }
        internal static byte[] Read(Stream s, int count)
        {
            byte[] b = new byte[count];
            for (int p = 0; p < count; ) { int n = s.Read(b, p, count - p); if (n == 0) throw new EndOfStreamException(); p += n; }
            return b;
        }
        internal static void Write(Stream s, byte[] b) { s.Write(b, 0, b.Length); }
        internal static int Length(uint n) { if (n > MaxTransfer) throw new InvalidDataException("transfer length"); return (int)n; }
        internal static int IsoCount(uint n)
        {
            if (n == UInt32.MaxValue) return 0;
            if (n > MaxIsoPackets) throw new InvalidDataException("iso count");
            return (int)n;
        }
        internal static string AsciiZ(byte[] b, int start, int length)
        {
            int end = start;
            while (end < start + length && b[end] != 0) { if (b[end] < 33 || b[end] > 126) throw new InvalidDataException("bus id"); ++end; }
            if (end == start + length) throw new InvalidDataException("bus id terminator");
            for (int p = end; p < start + length; ++p) if (b[p] != 0) throw new InvalidDataException("bus id padding");
            return Encoding.ASCII.GetString(b, start, end - start);
        }
    }

    internal sealed class Pending
    {
        internal uint Command, Direction, Endpoint, Target;
        internal int Length, IsoPackets;
        internal byte[] Setup;
    }

    internal sealed class EndpointProof
    {
        internal int Configuration, Interface, Alternate;
    }

    internal sealed class Packet
    {
        internal byte[] Header, Data, Iso;
        internal Packet(byte[] h, byte[] d, byte[] i) { Header = h; Data = d; Iso = i; }
        internal void Write(Stream output) { Wire.Write(output, Header); Wire.Write(output, Data); Wire.Write(output, Iso); }
    }

    internal sealed class Protocol
    {
        internal const int MaxPending = 4096;
        readonly object gate = new object();
        readonly Dictionary<uint, Pending> pending = new Dictionary<uint, Pending>();
        readonly uint device, endpoint;
        readonly Func<byte[], byte[]> merge;
        readonly List<EndpointProof> proofs = new List<EndpointProof>();
        readonly Dictionary<int, int> alternates = new Dictionary<int, int>();
        int configuration;
        volatile bool endpointVerified;
        long inputReports, mergedReports;
        internal long InputReports { get { return Interlocked.Read(ref inputReports); } }
        internal long MergedReports { get { return Interlocked.Read(ref mergedReports); } }
        internal int PendingCount { get { lock (gate) return pending.Count; } }
        internal bool EndpointVerified { get { return endpointVerified; } }
        internal Protocol(uint deviceId, int inputEndpoint, Func<byte[], byte[]> merger, int initialConfiguration = 1)
        { device = deviceId; endpoint = (uint)inputEndpoint; merge = merger; configuration = initialConfiguration; }

        internal Packet ReadClient(Stream input)
        {
            byte[] h = Wire.Read(input, Wire.HeaderSize);
            uint command = Wire.U32(h, 0), seq = Wire.U32(h, 4), direction = Wire.U32(h, 12), ep = Wire.U32(h, 16);
            if (Wire.U32(h, 8) != device || direction > 1 || ep > 15) throw new InvalidDataException("request identity");
            Pending request = new Pending(); request.Command = command; request.Direction = direction; request.Endpoint = ep;
            byte[] data = new byte[0], iso = new byte[0];
            if (command == 1)
            {
                request.Length = Wire.Length(Wire.U32(h, 24)); request.IsoPackets = Wire.IsoCount(Wire.U32(h, 32));
                request.Setup = new byte[8]; Array.Copy(h, 40, request.Setup, 0, 8);
                data = Wire.Read(input, direction == 0 ? request.Length : 0);
                iso = Wire.Read(input, request.IsoPackets * 16);
            }
            else if (command == 2)
            {
                if (direction != 0 || ep != 0) throw new InvalidDataException("unlink header");
                request.Target = Wire.U32(h, 20);
            }
            else throw new InvalidDataException("request command");
            lock (gate)
            {
                if (pending.Count >= MaxPending || pending.ContainsKey(seq)) throw new InvalidDataException("pending requests");
                // A late UNLINK after an already completed SUBMIT is valid.
                pending.Add(seq, request);
            }
            return new Packet(h, data, iso);
        }

        internal Packet ReadServer(Stream input)
        {
            byte[] h = Wire.Read(input, Wire.HeaderSize);
            uint command = Wire.U32(h, 0), seq = Wire.U32(h, 4);
            Pending request;
            lock (gate) { if (!pending.TryGetValue(seq, out request)) throw new InvalidDataException("unknown response"); }
            byte[] data = new byte[0], iso = new byte[0];
            if (command == 3 && request.Command == 1)
            {
                int length = Wire.Length(Wire.U32(h, 24)), packets = Wire.IsoCount(Wire.U32(h, 32));
                if (length > request.Length || packets != request.IsoPackets) throw new InvalidDataException("response length");
                // Server direction and endpoint fields are zero by specification.
                data = Wire.Read(input, request.Direction == 1 ? length : 0);
                iso = Wire.Read(input, packets * 16);
                if (Wire.U32(h, 20) == 0 && request.Endpoint == 0 && packets == 0) ObserveControl(request, data);
                if (Wire.U32(h, 20) == 0 && request.Direction == 1 && request.Endpoint == endpoint &&
                    request.Length == 64 && length == 64 && packets == 0 && data[0] == 1)
                {
                    Interlocked.Increment(ref inputReports);
                    if (merge != null && endpointVerified)
                    {
                        byte[] result = merge((byte[])data.Clone());
                        ValidateMerge(data, result);
                        bool changed = false;
                        for (int i = 0; i < data.Length; ++i) if (data[i] != result[i]) changed = true;
                        if (changed) Interlocked.Increment(ref mergedReports);
                        data = (byte[])result.Clone();
                    }
                }
            }
            else if (command == 4 && request.Command == 2)
            {
                // -ECONNRESET means the cancellation won: no RET_SUBMIT follows.
                if (unchecked((int)Wire.U32(h, 20)) == -104)
                {
                    lock (gate)
                    {
                        Pending target;
                        if (pending.TryGetValue(request.Target, out target) && target.Command == 1) pending.Remove(request.Target);
                    }
                }
            }
            else throw new InvalidDataException("response command");
            lock (gate) pending.Remove(seq);
            return new Packet(h, data, iso);
        }

        void ObserveControl(Pending request, byte[] data)
        {
            byte[] setup = request.Setup;
            if (request.Direction == 1 && setup[0] == 0x80 && setup[1] == 6 && setup[3] == 2)
            {
                // Initial nine-byte descriptor reads provide no endpoint evidence.
                if (data.Length >= 9 && data[0] == 9 && data[1] == 2 && (data[2] | data[3] << 8) == data.Length)
                {
                    int cfg = data[5], currentInterface = -1, currentAlternate = -1, currentClass = -1;
                    List<EndpointProof> found = new List<EndpointProof>();
                    bool valid = cfg != 0;
                    for (int p = 0; p < data.Length && valid; )
                    {
                        if (p + 2 > data.Length || data[p] < 2 || p + data[p] > data.Length) { valid = false; break; }
                        if (data[p + 1] == 4)
                        {
                            if (data[p] < 9) { valid = false; break; }
                            currentInterface = data[p + 2]; currentAlternate = data[p + 3]; currentClass = data[p + 5];
                        }
                        else if (data[p + 1] == 5)
                        {
                            if (data[p] < 7) { valid = false; break; }
                            if (currentClass == 3 && currentInterface >= 0 && data[p + 2] == (0x80 | endpoint) &&
                                (data[p + 3] & 3) == 3 && (data[p + 4] | data[p + 5] << 8) == 64)
                                found.Add(new EndpointProof { Configuration = cfg, Interface = currentInterface, Alternate = currentAlternate });
                        }
                        p += data[p];
                    }
                    proofs.RemoveAll(delegate(EndpointProof proof) { return proof.Configuration == cfg; });
                    if (valid) proofs.AddRange(found);
                }
            }
            else if (request.Direction == 0 && setup[0] == 0 && setup[1] == 9 && request.Length == 0 && setup[3] == 0)
            {
                configuration = setup[2]; alternates.Clear();
            }
            else if (request.Direction == 0 && setup[0] == 1 && setup[1] == 11 && request.Length == 0 && setup[3] == 0 && setup[5] == 0)
            {
                alternates[setup[4]] = setup[2];
            }
            bool verified = false;
            foreach (EndpointProof proof in proofs)
            {
                int alternate; if (!alternates.TryGetValue(proof.Interface, out alternate)) alternate = 0;
                if (proof.Configuration == configuration && proof.Alternate == alternate) verified = true;
            }
            endpointVerified = verified;
        }

        internal static void ValidateMerge(byte[] source, byte[] result)
        {
            if (result == null || result.Length != 64) throw new InvalidDataException("merge size");
            for (int i = 0; i < 64; ++i)
            {
                if (i == 1 || i == 2 || i == 5 || i == 6 || i == 8 || i == 9) continue;
                if (i == 7) { if ((source[i] & 0xfc) != (result[i] & 0xfc)) throw new InvalidDataException("merge counter"); }
                else if (source[i] != result[i]) throw new InvalidDataException("merge unrelated field");
            }
        }
    }

    public sealed class UsbIpRelay : IDisposable
    {
        readonly object gate = new object();
        readonly int listenPort, upstreamPort, endpoint;
        readonly string busId;
        readonly ushort product;
        readonly Func<byte[], byte[]> merge;
        TcpListener listener;
        TcpClient client, upstream;
        Thread worker;
        Protocol protocol;
        volatile bool disposed, attached, started;
        string failure;
        public bool IsAttached { get { return attached; } }
        public bool EndpointVerified { get { Protocol p = protocol; return attached && p != null && p.EndpointVerified; } }
        public string Failure { get { lock (gate) return failure; } }
        public long InputReports { get { Protocol p = protocol; return p == null ? 0 : p.InputReports; } }
        public long MergedReports { get { Protocol p = protocol; return p == null ? 0 : p.MergedReports; } }

        public UsbIpRelay(int listenPort, int upstreamPort, string busId, ushort productId, int inputEndpoint, Func<byte[], byte[]> merge)
        {
            if (listenPort < 1024 || listenPort > 65535 || upstreamPort < 1 || upstreamPort > 65535 || listenPort == upstreamPort) throw new ArgumentOutOfRangeException("listenPort");
            if (String.IsNullOrEmpty(busId) || busId.Length > 31) throw new ArgumentException("busId");
            foreach (char c in busId) if (!((c >= '0' && c <= '9') || c == '-' || c == '.')) throw new ArgumentException("busId");
            if (productId != 0x05c4 && productId != 0x09cc) throw new ArgumentException("productId");
            if (inputEndpoint < 1 || inputEndpoint > 15) throw new ArgumentOutOfRangeException("inputEndpoint");
            this.listenPort = listenPort; this.upstreamPort = upstreamPort; this.busId = busId;
            product = productId; endpoint = inputEndpoint; this.merge = merge;
        }

        public void Start()
        {
            lock (gate)
            {
                if (disposed || started) throw new InvalidOperationException("Relay instance cannot restart.");
                listener = new TcpListener(IPAddress.Loopback, listenPort);
                listener.Server.ExclusiveAddressUse = true;
                listener.Start(1); started = true;
                worker = new Thread(Run); worker.IsBackground = true; worker.Name = "Physical USB relay"; worker.Start();
            }
        }

        void Run()
        {
            Thread receive = null;
            try
            {
                TcpClient accepted = listener.AcceptTcpClient();
                lock (gate) { if (disposed) { accepted.Close(); return; } client = accepted; }
                listener.Stop();
                client.NoDelay = true; client.ReceiveTimeout = 5000;
                byte[] request = Wire.Read(client.GetStream(), 40);
                ValidateImportRequest(request, busId);
                TcpClient remote = new TcpClient(AddressFamily.InterNetwork);
                lock (gate) { if (disposed) { remote.Close(); return; } upstream = remote; }
                IAsyncResult connection = remote.BeginConnect(IPAddress.Loopback, upstreamPort, null, null);
                using (WaitHandle wait = connection.AsyncWaitHandle)
                { if (!wait.WaitOne(3000)) throw new IOException("upstream timeout"); remote.EndConnect(connection); }
                remote.NoDelay = true; remote.ReceiveTimeout = 5000;
                NetworkStream cs = client.GetStream(), us = remote.GetStream();
                Wire.Write(us, request);
                byte[] response = Wire.Read(us, 8);
                if (Wire.U16(response, 0) != 0x0111 || Wire.U16(response, 2) != 3) throw new InvalidDataException("import reply");
                if (Wire.U32(response, 4) != 0) { Wire.Write(cs, response); throw new IOException("import rejected"); }
                byte[] device = Wire.Read(us, 312);
                uint deviceId = ValidateImportDevice(device, busId, product);
                protocol = new Protocol(deviceId, endpoint, merge, device[309]);
                Wire.Write(cs, response); Wire.Write(cs, device);
                client.ReceiveTimeout = 0; remote.ReceiveTimeout = 0; attached = true;
                receive = new Thread(delegate()
                {
                    try { while (!disposed) protocol.ReadServer(us).Write(cs); }
                    catch (Exception e) { RecordFailure(e); }
                    finally { attached = false; CloseSockets(); }
                });
                receive.IsBackground = true; receive.Name = "Physical USB replies"; receive.Start();
                while (!disposed) protocol.ReadClient(cs).Write(us);
            }
            catch (Exception e) { RecordFailure(e); }
            finally { attached = false; CloseSockets(); if (receive != null && receive != Thread.CurrentThread) receive.Join(2000); }
        }

        internal static void ValidateImportRequest(byte[] request, string bus)
        {
            if (request.Length != 40 || Wire.U16(request, 0) != 0x0111 || Wire.U16(request, 2) != 0x8003 || Wire.U32(request, 4) != 0 || Wire.AsciiZ(request, 8, 32) != bus)
                throw new InvalidDataException("import request");
        }
        internal static uint ValidateImportDevice(byte[] device, string bus, ushort expectedProduct)
        {
            if (device.Length != 312 || Wire.AsciiZ(device, 256, 32) != bus || Wire.U16(device, 300) != 0x054c || Wire.U16(device, 302) != expectedProduct)
                throw new InvalidDataException("import device identity");
            uint busnum = Wire.U32(device, 288), devnum = Wire.U32(device, 292);
            if (busnum == 0 || busnum > 65535 || devnum == 0 || devnum > 65535) throw new InvalidDataException("import device number");
            return (busnum << 16) | devnum;
        }
        void RecordFailure(Exception e)
        {
            lock (gate)
            {
                if (disposed || failure != null) return;
                // Never expose payload bytes, bus paths, serials, or arbitrary callback exception text.
                failure = e is InvalidDataException ? "USB/IP protocol validation failed" :
                    e is EndOfStreamException ? "USB/IP peer disconnected" :
                    e is SocketException || e is IOException ? "USB/IP connection ended" : "USB/IP relay stopped";
            }
        }
        void CloseSockets()
        {
            lock (gate)
            {
                if (client != null) client.Close();
                if (upstream != null) upstream.Close();
                if (listener != null) listener.Stop();
            }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; attached = false; }
            CloseSockets();
            Thread t = worker; if (t != null && t != Thread.CurrentThread) t.Join(2500);
        }
    }
}
