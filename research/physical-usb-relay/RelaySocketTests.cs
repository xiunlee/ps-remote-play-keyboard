using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using PsKeyboard.PhysicalRelay;

internal static class RelaySocketTests
{
    static int checks;
    static void Check(bool yes, string name) { if (!yes) throw new Exception("SOCKET FAIL: " + name); ++checks; }
    static void Equal(byte[] a, byte[] b, string name)
    { bool same = a.Length == b.Length; if (same) for (int i = 0; i < a.Length; ++i) if (a[i] != b[i]) same = false; Check(same, name); }
    static void Wait(Func<bool> condition, string name)
    { Stopwatch timer = Stopwatch.StartNew(); while (!condition()) { if (timer.ElapsedMilliseconds > 4000) throw new Exception("SOCKET TIMEOUT: " + name); Thread.Sleep(5); } ++checks; }
    static int FreePort()
    { TcpListener listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    static TcpClient Connect(int port)
    { TcpClient client = new TcpClient(AddressFamily.InterNetwork); client.Connect(IPAddress.Loopback, port); client.NoDelay = true; client.ReceiveTimeout = 4000; client.SendTimeout = 4000; return client; }
    static TcpClient Accept(TcpListener listener)
    { Wait(delegate { return listener.Pending(); }, "fake upstream connection"); TcpClient client = listener.AcceptTcpClient(); client.NoDelay = true; client.ReceiveTimeout = 4000; client.SendTimeout = 4000; return client; }
    static void Fragment(Stream stream, byte[] data)
    { for (int p = 0; p < data.Length; p += 7) stream.Write(data, p, Math.Min(7, data.Length - p)); }
    static byte[] Join(params byte[][] arrays)
    { using (MemoryStream s = new MemoryStream()) { foreach (byte[] b in arrays) Wire.Write(s, b); return s.ToArray(); } }
    static byte[] Import()
    { byte[] b = new byte[40]; b[0] = 1; b[1] = 0x11; b[2] = 0x80; b[3] = 3; Array.Copy(Encoding.ASCII.GetBytes("1-2"), 0, b, 8, 3); return b; }
    static byte[] Imported()
    {
        byte[] b = new byte[320]; b[0] = 1; b[1] = 0x11; b[3] = 3;
        Array.Copy(Encoding.ASCII.GetBytes("1-2"), 0, b, 264, 3); Wire.Put32(b, 296, 1); Wire.Put32(b, 300, 2);
        b[308] = 5; b[309] = 0x4c; b[310] = 5; b[311] = 0xc4; b[317] = 1; return b;
    }
    static byte[] Submit(uint seq, uint direction, uint ep, int length)
    {
        byte[] b = new byte[48]; Wire.Put32(b, 0, 1); Wire.Put32(b, 4, seq); Wire.Put32(b, 8, 0x00010002);
        Wire.Put32(b, 12, direction); Wire.Put32(b, 16, ep); Wire.Put32(b, 24, (uint)length); Wire.Put32(b, 36, 5); return b;
    }
    static byte[] Reply(uint seq, int length)
    { byte[] b = new byte[48]; Wire.Put32(b, 0, 3); Wire.Put32(b, 4, seq); Wire.Put32(b, 24, (uint)length); return b; }
    static byte[] Report()
    { byte[] b = new byte[64]; b[0] = 1; b[1] = 128; b[2] = 128; b[3] = 101; b[4] = 115; b[5] = 8; b[7] = 0xbc; b[50] = 33; return b; }

    internal static int Run()
    {
        TcpListener fakeServer = new TcpListener(IPAddress.Loopback, 0); fakeServer.Start();
        int upstreamPort = ((IPEndPoint)fakeServer.LocalEndpoint).Port, localPort = FreePort();
        UsbIpRelay relay = null; TcpClient client = null, upstream = null;
        try
        {
            relay = new UsbIpRelay(localPort, upstreamPort, "1-2", 0x05c4, 4, delegate(byte[] b) { b[5] |= 0x20; return b; });
            relay.Start(); client = Connect(localPort); Fragment(client.GetStream(), Import()); upstream = Accept(fakeServer);
            Equal(Import(), Wire.Read(upstream.GetStream(), 40), "fragmented import request exact");
            Fragment(upstream.GetStream(), Imported()); Equal(Imported(), Wire.Read(client.GetStream(), 320), "fragmented import response exact");
            Wait(delegate { return relay.IsAttached; }, "attached state"); Check(!relay.EndpointVerified, "not ready from import alone");
            byte[] descriptor = new byte[] { 9, 2, 25, 0, 1, 1, 0, 0x80, 50, 9, 4, 0, 0, 1, 3, 0, 0, 0, 7, 5, 0x84, 3, 64, 0, 5 };
            byte[] getDescriptor = Submit(1, 1, 0, descriptor.Length); getDescriptor[40] = 0x80; getDescriptor[41] = 6; getDescriptor[43] = 2;
            Fragment(client.GetStream(), getDescriptor); Equal(getDescriptor, Wire.Read(upstream.GetStream(), 48), "descriptor request exact over TCP");
            byte[] descReply = Join(Reply(1, descriptor.Length), descriptor); Fragment(upstream.GetStream(), descReply);
            Equal(descReply, Wire.Read(client.GetStream(), descReply.Length), "descriptor response exact over TCP");
            Wait(delegate { return relay.EndpointVerified; }, "descriptor readiness");

            byte[] first = Submit(2, 1, 4, 64), second = Submit(3, 1, 4, 64);
            Fragment(client.GetStream(), Join(first, second)); Equal(Join(first, second), Wire.Read(upstream.GetStream(), 96), "pipelined submits over TCP");
            byte[] original = Report(), expected = Report(); expected[5] |= 0x20;
            Fragment(upstream.GetStream(), Join(Reply(3, 64), original, Reply(2, 64), original));
            Equal(Join(Reply(3, 64), expected, Reply(2, 64), expected), Wire.Read(client.GetStream(), 224), "out of order responses merged using request direction");
            Check(relay.InputReports == 2 && relay.MergedReports == 2, "live aggregate counters");

            byte[] feature = Submit(4, 1, 0, 64); feature[40] = 0xa1; feature[41] = 1; feature[42] = 0xf1; feature[43] = 3;
            Fragment(client.GetStream(), feature); Equal(feature, Wire.Read(upstream.GetStream(), 48), "feature request exact over TCP");
            byte[] featureReply = Join(Reply(4, 64), original); Fragment(upstream.GetStream(), featureReply);
            Equal(featureReply, Wire.Read(client.GetStream(), 112), "feature bytes unchanged over TCP");
            byte[] output = Join(Submit(5, 0, 3, 64), original); Fragment(client.GetStream(), output);
            Equal(output, Wire.Read(upstream.GetStream(), output.Length), "output report unchanged over TCP");
            Fragment(upstream.GetStream(), Reply(5, 64)); Equal(Reply(5, 64), Wire.Read(client.GetStream(), 48), "OUT reply has no payload");
            Check(relay.MergedReports == 2, "control/output do not invoke merge");
            client.Close(); Wait(delegate { return !relay.IsAttached && relay.Failure != null; }, "peer disconnect shuts session");
            Check(relay.Failure.IndexOf("USB/IP", StringComparison.Ordinal) == 0, "sanitized failure category");
        }
        finally { if (relay != null) relay.Dispose(); if (client != null) client.Close(); if (upstream != null) upstream.Close(); fakeServer.Stop(); }

        // Dispose while AcceptTcpClient is pending, and reuse the same listener port.
        localPort = FreePort(); relay = new UsbIpRelay(localPort, FreePort(), "1-2", 0x05c4, 4, null);
        relay.Start(); Stopwatch disposeTimer = Stopwatch.StartNew(); relay.Dispose();
        Check(disposeTimer.ElapsedMilliseconds < 3000, "dispose interrupts accept");
        TcpListener reuse = new TcpListener(IPAddress.Loopback, localPort); reuse.Start(); reuse.Stop(); Check(true, "listener port released");
        Check(relay.Failure == null && !relay.IsAttached, "intentional disposal has no failure");
        relay.Dispose(); Check(true, "idempotent disposal");

        // Dispose while a client has sent only part of the import request.
        localPort = FreePort(); relay = new UsbIpRelay(localPort, FreePort(), "1-2", 0x05c4, 4, null);
        try
        {
            relay.Start(); client = Connect(localPort); client.GetStream().Write(Import(), 0, 5); Thread.Sleep(30);
            disposeTimer.Restart(); relay.Dispose(); Check(disposeTimer.ElapsedMilliseconds < 3000, "dispose interrupts partial import read");
            Check(relay.Failure == null, "partial-handshake intentional disposal has no failure");
        }
        finally { relay.Dispose(); client.Close(); }

        // Refused upstream connection must stop cleanly. A genuinely pending loopback
        // connect cannot be made deterministic without changing system networking.
        localPort = FreePort(); int unavailable = FreePort();
        relay = new UsbIpRelay(localPort, unavailable, "1-2", 0x05c4, 4, null);
        try
        {
            relay.Start(); client = Connect(localPort); Fragment(client.GetStream(), Import());
            Wait(delegate { return relay.Failure != null; }, "refused upstream handled");
            Check(!relay.IsAttached, "refused connection never attached");
        }
        finally { relay.Dispose(); client.Close(); }

        // Successful import with both URB readers blocked; Dispose must close both.
        fakeServer = new TcpListener(IPAddress.Loopback, 0); fakeServer.Start(); upstreamPort = ((IPEndPoint)fakeServer.LocalEndpoint).Port;
        localPort = FreePort(); relay = new UsbIpRelay(localPort, upstreamPort, "1-2", 0x05c4, 4, null);
        try
        {
            relay.Start(); client = Connect(localPort); Fragment(client.GetStream(), Import()); upstream = Accept(fakeServer);
            Wire.Read(upstream.GetStream(), 40); Fragment(upstream.GetStream(), Imported()); Wire.Read(client.GetStream(), 320);
            Wait(delegate { return relay.IsAttached; }, "second session imported");
            disposeTimer.Restart(); relay.Dispose(); Check(disposeTimer.ElapsedMilliseconds < 3000, "dispose interrupts both URB reads");
            Check(!relay.IsAttached && relay.Failure == null, "imported session disposed intentionally");
            Check(upstream.GetStream().ReadByte() == -1 && client.GetStream().ReadByte() == -1, "both peers observe close");
        }
        finally { relay.Dispose(); client.Close(); upstream.Close(); fakeServer.Stop(); }
        Console.WriteLine("PASS: " + checks + " real loopback TCP checks with fake peers only; no USB/IP driver, device, or Sony process used.");
        return checks;
    }
}
