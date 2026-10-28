using System;
using System.IO;
using System.Text;
using PsKeyboard.PhysicalRelay;

internal static class RelayProtocolTests
{
    const uint Device = 0x00010002;
    static int checks;
    static void Check(bool yes, string name) { if (!yes) throw new Exception("FAIL: " + name); ++checks; }
    static void Throws(Action action, string name)
    {
        try { action(); } catch (InvalidDataException) { ++checks; return; } catch (EndOfStreamException) { ++checks; return; }
        throw new Exception("FAIL (accepted): " + name);
    }
    static byte[] Join(params byte[][] parts)
    { using (MemoryStream s = new MemoryStream()) { foreach (byte[] p in parts) s.Write(p, 0, p.Length); return s.ToArray(); } }
    static void Equal(byte[] a, byte[] b, string name)
    { bool same = a.Length == b.Length; if (same) for (int i = 0; i < a.Length; ++i) if (a[i] != b[i]) same = false; Check(same, name); }
    static byte[] Bytes(Packet p) { using (MemoryStream s = new MemoryStream()) { p.Write(s); return s.ToArray(); } }
    static byte[] Submit(uint seq, uint direction, uint ep, int length, uint packets)
    {
        byte[] b = new byte[48]; Wire.Put32(b, 0, 1); Wire.Put32(b, 4, seq); Wire.Put32(b, 8, Device);
        Wire.Put32(b, 12, direction); Wire.Put32(b, 16, ep); Wire.Put32(b, 24, (uint)length); Wire.Put32(b, 32, packets); Wire.Put32(b, 36, 5); return b;
    }
    static byte[] Reply(uint seq, int length, int status, uint packets)
    {
        byte[] b = new byte[48]; Wire.Put32(b, 0, 3); Wire.Put32(b, 4, seq);
        Wire.Put32(b, 20, unchecked((uint)status)); Wire.Put32(b, 24, (uint)length); Wire.Put32(b, 32, packets); return b;
    }
    static byte[] Report()
    { byte[] b = new byte[64]; for (int i = 0; i < b.Length; ++i) b[i] = (byte)(i * 3); b[0] = 1; b[5] = 8; b[7] = 0xa0; return b; }
    static Packet Client(Protocol p, byte[] b) { return p.ReadClient(new Chunks(b)); }
    static Packet Server(Protocol p, byte[] b) { return p.ReadServer(new Chunks(b)); }
    static byte[] Unlink(uint seq, uint target)
    { byte[] b = new byte[48]; Wire.Put32(b, 0, 2); Wire.Put32(b, 4, seq); Wire.Put32(b, 8, Device); Wire.Put32(b, 20, target); return b; }
    static byte[] Unlinked(uint seq, int status)
    { byte[] b = new byte[48]; Wire.Put32(b, 0, 4); Wire.Put32(b, 4, seq); Wire.Put32(b, 20, unchecked((uint)status)); return b; }
    static byte[] Configuration(byte attributes, byte classCode, byte packetSize, byte alternate)
    {
        return new byte[] { 9, 2, 25, 0, 1, 1, 0, 0x80, 50, 9, 4, 0, alternate, 1, classCode, 0, 0, 0, 7, 5, 0x84, attributes, packetSize, 0, 5 };
    }
    static void Describe(Protocol p, byte[] descriptor, uint seq)
    {
        byte[] h = Submit(seq, 1, 0, descriptor.Length, 0); h[40] = 0x80; h[41] = 6; h[43] = 2;
        Equal(h, Bytes(Client(p, h)), "descriptor request preserved");
        byte[] response = Join(Reply(seq, descriptor.Length, 0, 0), descriptor);
        Equal(response, Bytes(Server(p, response)), "descriptor response preserved");
    }
    sealed class Chunks : MemoryStream
    {
        internal Chunks(byte[] b) : base(b) { }
        public override int Read(byte[] buffer, int offset, int count) { return base.Read(buffer, offset, Math.Min(count, 3)); }
    }

    static int Main()
    {
        try { Run(); RelaySocketTests.Run(); return 0; }
        catch (Exception e) { Console.WriteLine("TEST FAILURE after " + checks + " checks: " + e.Message); return 1; }
    }
    static void Run()
    {
        int merges = 0;
        Protocol p = new Protocol(Device, 4, delegate(byte[] b) { ++merges; b[5] |= 0x20; return b; });
        Client(p, Submit(900, 1, 4, 64, 0));
        byte[] beforeProof = Join(Reply(900, 64, 0, 0), Report());
        Equal(beforeProof, Bytes(Server(p, beforeProof)), "input untouched before descriptor proof");
        Check(!p.EndpointVerified && merges == 0, "no endpoint inference from data shape");
        Describe(p, Configuration(3, 3, 64, 0), 901);
        Check(p.EndpointVerified, "verified interrupt IN HID endpoint");
        byte[] first = Submit(1, 1, 4, 64, 0), second = Submit(2, 1, 4, 64, UInt32.MaxValue);
        Equal(first, Bytes(Client(p, first)), "first submit unchanged");
        Equal(second, Bytes(Client(p, second)), "second submit unchanged");
        byte[] report = Report(), expected = (byte[])report.Clone(); expected[5] |= 0x20;
        Equal(Join(Reply(2, 64, 0, UInt32.MaxValue), expected), Bytes(Server(p, Join(Reply(2, 64, 0, UInt32.MaxValue), report))), "out of order response uses original IN direction");
        Equal(Join(Reply(1, 64, 0, 0), expected), Bytes(Server(p, Join(Reply(1, 64, 0, 0), report))), "interleaved first response");
        Check(merges == 2 && p.PendingCount == 0 && p.InputReports == 3 && p.MergedReports == 2, "matched request lifecycle counters");
        Equal(Report(), report, "callback never mutates source buffer");

        // Authentication feature requests use EP0; even a 64-byte report beginning01 must remain unchanged.
        uint seq = 10;
        foreach (byte feature in new byte[] { 0xf0, 0xf1, 0xf2 })
        {
            foreach (uint direction in new uint[] { 0, 1 })
            {
                byte[] h = Submit(seq, direction, 0, 64, 0); h[40] = direction == 1 ? (byte)0xa1 : (byte)0x21;
                h[41] = direction == 1 ? (byte)1 : (byte)9; h[42] = feature; h[43] = 3; h[46] = 64;
                byte[] request = direction == 0 ? Join(h, report) : h;
                Equal(request, Bytes(Client(p, request)), "feature request preserved " + feature + "/" + direction);
                byte[] response = direction == 1 ? Join(Reply(seq, 64, 0, 0), report) : Reply(seq, 64, 0, 0);
                Equal(response, Bytes(Server(p, response)), "feature reply preserved " + feature + "/" + direction);
                ++seq;
            }
        }
        Check(merges == 2, "no feature merge callbacks");
        byte[] descriptor = Submit(seq, 1, 0, 64, 0); descriptor[40] = 0x80; descriptor[41] = 6; descriptor[43] = 2;
        Equal(descriptor, Bytes(Client(p, descriptor)), "configuration descriptor request exact");
        byte[] descResponse = Join(Reply(seq++, 64, 0, 0), report);
        Equal(descResponse, Bytes(Server(p, descResponse)), "configuration descriptor response exact");

        // Ordinary output, wrong endpoint/report, short response, and failed transfer bypass merge.
        byte[] output = Join(Submit(seq, 0, 4, 64, 0), report);
        Equal(output, Bytes(Client(p, output)), "interrupt output exact");
        byte[] outputReply = Reply(seq++, 64, 0, 0); Equal(outputReply, Bytes(Server(p, outputReply)), "OUT actual length has no payload");
        foreach (int mode in new int[] { 0, 1, 2, 3 })
        {
            Client(p, Submit(seq, 1, mode == 0 ? 3u : 4u, 64, 0));
            byte[] data = mode == 2 ? new byte[12] : Report(); if (mode == 1) data[0] = 0x11;
            byte[] r = Join(Reply(seq++, data.Length, mode == 3 ? -32 : 0, 0), data);
            Equal(r, Bytes(Server(p, r)), "non-candidate bypass " + mode);
        }
        Check(merges == 2, "only exact candidates merged");

        // ISO framing: payload followed by descriptors; never treated as a controller report.
        byte[] iso = new byte[16]; iso[7] = 64;
        byte[] isoOut = Join(Submit(seq, 0, 4, 64, 1), report, iso);
        Equal(isoOut, Bytes(Client(p, isoOut)), "ISO OUT framing exact");
        byte[] isoReply = Join(Reply(seq++, 64, 0, 1), iso); Equal(isoReply, Bytes(Server(p, isoReply)), "ISO OUT reply exact");
        Client(p, Join(Submit(seq, 1, 4, 64, 1), iso));
        byte[] isoIn = Join(Reply(seq++, 64, 0, 1), report, iso); Equal(isoIn, Bytes(Server(p, isoIn)), "ISO IN exact and not merged");
        Check(merges == 2, "ISO bypass");

        Protocol proofTest = new Protocol(Device, 4, delegate(byte[] b) { b[5] |= 0x20; return b; });
        Describe(proofTest, Configuration(2, 3, 64, 0), 200);
        Check(!proofTest.EndpointVerified, "bulk endpoint cannot enable merge");
        Describe(proofTest, Configuration(3, 0xff, 64, 0), 201);
        Check(!proofTest.EndpointVerified, "non HID interface cannot enable merge");
        Describe(proofTest, Configuration(3, 3, 32, 0), 202);
        Check(!proofTest.EndpointVerified, "wrong maxpacket cannot enable merge");
        byte[] outEndpoint = Configuration(3, 3, 64, 0); outEndpoint[20] = 4;
        Describe(proofTest, outEndpoint, 203); Check(!proofTest.EndpointVerified, "OUT endpoint cannot enable merge");
        Describe(proofTest, Configuration(3, 3, 64, 1), 204);
        Check(!proofTest.EndpointVerified, "inactive alternate cannot enable merge");
        byte[] selectAlternate = Submit(205, 0, 0, 0, 0); selectAlternate[40] = 1; selectAlternate[41] = 11; selectAlternate[42] = 1;
        Client(proofTest, selectAlternate); Server(proofTest, Reply(205, 0, 0, 0));
        Check(proofTest.EndpointVerified, "successful interface selection enables matching descriptor");
        byte[] clearConfig = Submit(206, 0, 0, 0, 0); clearConfig[41] = 9;
        Client(proofTest, clearConfig); Server(proofTest, Reply(206, 0, 0, 0));
        Check(!proofTest.EndpointVerified, "unconfigured device disables merge");
        byte[] setConfig = Submit(207, 0, 0, 0, 0); setConfig[41] = 9; setConfig[42] = 1;
        Client(proofTest, setConfig); Server(proofTest, Reply(207, 0, 0, 0));
        Check(!proofTest.EndpointVerified, "configuration selection resets alternate settings");
        Describe(proofTest, Configuration(3, 3, 64, 0), 208);
        Check(proofTest.EndpointVerified, "active default alternate verified");
        byte[] malformedDescriptor = Configuration(3, 3, 64, 0); malformedDescriptor[18] = 1;
        Describe(proofTest, malformedDescriptor, 209);
        Check(!proofTest.EndpointVerified, "malformed complete descriptor clears proof but passes unchanged");
        byte[] shortDescriptor = new byte[] { 9, 2, 25, 0, 1, 1, 0, 0x80, 50 };
        Describe(proofTest, shortDescriptor, 210); Check(!proofTest.EndpointVerified, "partial descriptor cannot prove endpoint");

        Client(p, Submit(100, 1, 4, 64, 0));
        byte[] unlink = Unlink(101, 100); Equal(unlink, Bytes(Client(p, unlink)), "unlink request exact");
        byte[] cancelled = Unlinked(101, -104); Equal(cancelled, Bytes(Server(p, cancelled)), "winning cancellation exact");
        Check(p.PendingCount == 0, "winning unlink removes pending submit");
        Client(p, Submit(102, 1, 4, 64, 0)); Client(p, Unlink(103, 102));
        Server(p, Join(Reply(102, 64, 0, 0), report)); Server(p, Unlinked(103, 0));
        Check(p.PendingCount == 0, "submit winning cancellation race");
        Client(p, Unlink(104, 999)); Server(p, Unlinked(104, 0));
        Check(p.PendingCount == 0, "late unlink allowed");

        Protocol bad = new Protocol(Device, 4, null);
        byte[] wrongDevice = Submit(1, 1, 4, 64, 0); Wire.Put32(wrongDevice, 8, 42);
        Throws(delegate { Client(bad, wrongDevice); }, "wrong device");
        Throws(delegate { Client(bad, Submit(1, 2, 4, 64, 0)); }, "wrong direction");
        Throws(delegate { Client(bad, Submit(1, 1, 16, 64, 0)); }, "wrong endpoint");
        Throws(delegate { Client(bad, Submit(1, 1, 4, Wire.MaxTransfer + 1, 0)); }, "oversize request");
        Throws(delegate { Client(bad, Submit(1, 1, 4, -1, 0)); }, "negative request length");
        Throws(delegate { Client(bad, Submit(1, 1, 4, 64, Wire.MaxIsoPackets + 1)); }, "oversize iso count");
        Throws(delegate { Client(bad, new byte[12]); }, "truncated header disconnect");
        Throws(delegate { Client(bad, Join(Submit(1, 0, 4, 64, 0), new byte[12])); }, "truncated OUT data disconnect");
        Check(bad.PendingCount == 0, "partial request not registered");
        Client(bad, Submit(1, 1, 4, 64, 0));
        Throws(delegate { Client(bad, Submit(1, 1, 4, 64, 0)); }, "duplicate sequence");
        Throws(delegate { Server(bad, Reply(99, 0, 0, 0)); }, "unknown response");
        Throws(delegate { Server(bad, Join(Reply(1, 65, 0, 0), new byte[65])); }, "reply exceeds request");
        Throws(delegate { Server(bad, Reply(1, -1, 0, 0)); }, "negative response length");
        Throws(delegate { Server(bad, Reply(1, 64, 0, 1)); }, "ISO response mismatch");
        Throws(delegate { Server(bad, Join(Reply(1, 64, 0, 0), new byte[10])); }, "truncated reply disconnect");
        Throws(delegate { Server(bad, Unlinked(1, 0)); }, "mismatched response command");
        byte[] unknown = Submit(2, 1, 4, 64, 0); Wire.Put32(unknown, 0, 9);
        Throws(delegate { Client(bad, unknown); }, "unsupported command");
        Protocol flood = new Protocol(Device, 4, null);
        for (uint n = 1; n <= Protocol.MaxPending; ++n) Client(flood, Submit(n, 1, 4, 64, 0));
        Throws(delegate { Client(flood, Submit(5000, 1, 4, 64, 0)); }, "pending bound");

        Protocol disabled = new Protocol(Device, 4, null); Client(disabled, Submit(1, 1, 4, 64, 0));
        byte[] unchanged = Join(Reply(1, 64, 0, 0), report);
        Equal(unchanged, Bytes(Server(disabled, unchanged)), "null callback passthrough");
        Check(disabled.InputReports == 1 && disabled.MergedReports == 0, "disabled counters");
        foreach (int index in new int[] { 0, 3, 4, 10, 63 })
        {
            byte[] altered = (byte[])report.Clone(); altered[index] ^= 1;
            Throws(delegate { Protocol.ValidateMerge(report, altered); }, "unmapped byte guard " + index);
        }
        byte[] counter = (byte[])report.Clone(); counter[7] ^= 4;
        Throws(delegate { Protocol.ValidateMerge(report, counter); }, "report counter guard");
        Throws(delegate { Protocol.ValidateMerge(report, null); }, "null callback guard");
        Throws(delegate { Protocol.ValidateMerge(report, new byte[63]); }, "callback size guard");
        byte[] permitted = (byte[])report.Clone(); foreach (int index in new int[] { 1, 2, 5, 6, 8, 9 }) permitted[index] ^= 1; permitted[7] ^= 3;
        Protocol.ValidateMerge(report, permitted); Check(true, "mapped bytes allowed");

        byte[] import = new byte[40]; import[0] = 1; import[1] = 0x11; import[2] = 0x80; import[3] = 3;
        Array.Copy(Encoding.ASCII.GetBytes("1-2"), 0, import, 8, 3);
        UsbIpRelay.ValidateImportRequest(import, "1-2"); Check(true, "exact import request");
        Throws(delegate { UsbIpRelay.ValidateImportRequest(import, "1-3"); }, "other bus denied");
        byte[] devlist = (byte[])import.Clone(); devlist[3] = 5;
        Throws(delegate { UsbIpRelay.ValidateImportRequest(devlist, "1-2"); }, "device listing unsupported");
        byte[] nonAscii = (byte[])import.Clone(); nonAscii[9] = 0xff;
        Throws(delegate { UsbIpRelay.ValidateImportRequest(nonAscii, "1-2"); }, "non ASCII bus denied");
        byte[] padding = (byte[])import.Clone(); padding[14] = 1;
        Throws(delegate { UsbIpRelay.ValidateImportRequest(padding, "1-2"); }, "bus padding denied");
        byte[] device = new byte[312]; Array.Copy(Encoding.ASCII.GetBytes("1-2"), 0, device, 256, 3);
        Wire.Put32(device, 288, 1); Wire.Put32(device, 292, 2); device[300] = 5; device[301] = 0x4c; device[302] = 5; device[303] = 0xc4;
        Check(UsbIpRelay.ValidateImportDevice(device, "1-2", 0x05c4) == Device, "import identity and device id");
        Throws(delegate { UsbIpRelay.ValidateImportDevice(device, "1-2", 0x09cc); }, "wrong physical product denied");
        Throws(delegate { UsbIpRelay.ValidateImportDevice(device, "1-3", 0x05c4); }, "wrong imported bus denied");
        device[301] ^= 1; Throws(delegate { UsbIpRelay.ValidateImportDevice(device, "1-2", 0x05c4); }, "non Sony vendor denied");
        Console.WriteLine("PASS: " + checks + " offline USB/IP protocol checks; no sockets, drivers, devices, or Sony process used.");
    }
}
