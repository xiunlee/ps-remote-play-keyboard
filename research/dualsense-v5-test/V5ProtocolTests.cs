// Offline protocol tests only. Fixtures are synthetic, never controller captures.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using PSKeyboard.Research;

internal static class V5ProtocolTests
{
    static int passed;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        passed++; Console.WriteLine("PASS " + label);
    }
    static void Throws<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, label); return; }
        throw new Exception("Expected " + typeof(T).Name + ": " + label);
    }
    static byte[] Neutral()
    { byte[] state = LegacyNeutral(); state[15] = 0x80; state[20] = 0x80; return state; }
    static byte[] LegacyNeutral()
    { byte[] state = new byte[33]; state[32] = 0xE0; return state; }
    static bool Equal(byte[] first, byte[] second)
    {
        if (first == null || second == null || first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }
    static byte[] Pattern(int count)
    { byte[] bytes = new byte[count]; for (int i = 0; i < count; i++) bytes[i] = (byte)i; return bytes; }
    static byte[] Join(params byte[][] parts)
    {
        using (var buffer = new MemoryStream())
        { foreach (byte[] part in parts) buffer.Write(part, 0, part.Length); return buffer.ToArray(); }
    }

    // Fixture generator uses an independently built CRC table. The fixed
    // golden CRC values below were also calculated outside this C# codec;
    // the standard ASCII "123456789" IEEE vector checks the fixture generator.
    static uint FixtureCrc(byte[] bytes)
    {
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint entry = i;
            for (int bit = 0; bit < 8; bit++) entry = (entry & 1) == 0 ? entry >> 1 : (entry >> 1) ^ 0xEDB88320u;
            table[i] = entry;
        }
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in bytes) crc = table[(crc ^ value) & 255] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
    static byte[] Packet(byte type, uint sequence, byte[] payload)
    {
        byte[] header = { 0x56, 0x50, 0x43, 0x4D, 5, type,
            (byte)payload.Length, (byte)(payload.Length >> 8),
            (byte)sequence, (byte)(sequence >> 8), (byte)(sequence >> 16), (byte)(sequence >> 24), 0, 0, 0, 0 };
        byte[] fields = new byte[8]; Buffer.BlockCopy(header, 4, fields, 0, 8);
        uint crc = FixtureCrc(Join(fields, payload));
        header[12] = (byte)crc; header[13] = (byte)(crc >> 8);
        header[14] = (byte)(crc >> 16); header[15] = (byte)(crc >> 24);
        return Join(header, payload);
    }
    static byte[] Atomic()
    {
        byte[] payload = new byte[V5Protocol.AtomicSize]; payload[0] = 0xDA; payload[1] = 1;
        Buffer.BlockCopy(Pattern(474), 0, payload, 2, 474);
        Buffer.BlockCopy(Pattern(1920), 0, payload, 476, 1920);
        return payload;
    }
    static void Reject(byte[] packet, string label, bool events)
    {
        var stream = new MemoryStream(packet);
        using (var reader = new V5FeedbackReader(stream, events))
        {
            Throws<InvalidDataException>(delegate { reader.Read(1000); }, label);
            if (stream.CanRead) throw new Exception("Failed reader left stream open: " + label);
            Throws<ObjectDisposedException>(delegate { reader.Read(1000); }, label + " cannot resume");
        }
    }
    sealed class FragmentedStream : MemoryStream
    {
        readonly int chunk, delay;
        internal readonly List<int> Timeouts = new List<int>();
        int timeout;
        internal FragmentedStream(byte[] data, int chunk, int delay) : base(data)
        { this.chunk = chunk; this.delay = delay; }
        public override bool CanTimeout { get { return true; } }
        public override int ReadTimeout
        { get { return timeout; } set { timeout = value; Timeouts.Add(value); } }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (delay > 0) Thread.Sleep(delay);
            return base.Read(buffer, offset, Math.Min(chunk, count));
        }
    }

    static void Run()
    {
        Check(FixtureCrc(System.Text.Encoding.ASCII.GetBytes("123456789")) == 0xCBF43926u, "independent fixture IEEE CRC check vector");
        byte[] legacy = LegacyNeutral();
        Check(Equal(V5Protocol.ConvertLegacyKeyboardState(legacy), Neutral()), "legacy bool inactive contacts become candidate packed 80 status");
        Check(Equal(legacy, LegacyNeutral()), "legacy touch conversion preserves caller source");
        byte[] heldLegacy = LegacyNeutral();
        heldLegacy[0] = 128; heldLegacy[1] = 90; heldLegacy[4] = 0xF0; heldLegacy[5] = 0xFF;
        heldLegacy[6] = 3; heldLegacy[8] = 9; heldLegacy[9] = 255; heldLegacy[10] = 255;
        byte[] heldExpected = (byte[])heldLegacy.Clone(); heldExpected[15] = 128; heldExpected[20] = 128;
        Check(Equal(V5Protocol.ConvertLegacyKeyboardState(heldLegacy), heldExpected), "conversion preserves sticks all keyboard buttons dpad triggers and gravity");
        Throws<ArgumentNullException>(delegate { V5Protocol.ConvertLegacyKeyboardState(null); }, "legacy converter rejects null");
        foreach (int length in new int[] { 31, 32, 34, 53 })
        {
            int n = length;
            Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(new byte[n]); }, "legacy converter rejects length/raw metadata " + n);
        }
        for (int index = 11; index < 31; index++)
        {
            byte[] nonKeyboard = LegacyNeutral(); nonKeyboard[index] = 1;
            Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(nonKeyboard); }, "legacy converter rejects touch/motion byte " + index);
        }
        byte[] badGravity = LegacyNeutral(); badGravity[31] = 1;
        Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(badGravity); }, "legacy converter rejects gravity low byte");
        badGravity = LegacyNeutral(); badGravity[32] = 0;
        Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(badGravity); }, "legacy converter rejects gravity high byte");
        Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(Neutral()); }, "legacy converter rejects already packed candidate state instead of guessing");
        byte[] extraControls = LegacyNeutral(); extraControls[6] = 4;
        Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(extraControls); }, "legacy converter rejects unsupported mic button");
        extraControls = LegacyNeutral(); extraControls[8] = 16;
        Throws<ArgumentException>(delegate { V5Protocol.ConvertLegacyKeyboardState(extraControls); }, "legacy converter rejects unsupported dpad bit");
        byte[] input = Neutral();
        byte[] neutral = V5Protocol.EncodeInput(input, 0);
        byte[] neutralHeader = { 0x56, 0x50, 0x43, 0x4D, 5, 1, 33, 0, 0, 0, 0, 0, 0x21, 0xB8, 0x8D, 0xB8 };
        Check(Equal(neutral, Join(neutralHeader, Neutral())), "candidate inactive-touch neutral golden frame and CRC B88DB821");
        byte[] cross = Neutral(); cross[4] = 0x20;
        byte[] crossHeader = { 0x56, 0x50, 0x43, 0x4D, 5, 1, 33, 0, 0x78, 0x56, 0x34, 0x12, 0x84, 0xAC, 0x4F, 0x68 };
        Check(Equal(V5Protocol.EncodeInput(cross, 0x12345678), Join(crossHeader, cross)), "Cross input golden CRC and little-endian sequence");
        Check(Equal(input, Neutral()), "input encoding does not mutate candidate source state");
        Throws<ArgumentNullException>(delegate { V5Protocol.EncodeInput(null, 0); }, "reject null input");
        foreach (int length in new int[] { 0, 31, 32, 34, 53 })
        {
            int n = length;
            Throws<ArgumentException>(delegate { V5Protocol.EncodeInput(new byte[n], 0); }, "reject input length " + n);
        }
        byte[] invalid = Neutral(); invalid[4] = 1;
        Throws<ArgumentException>(delegate { V5Protocol.EncodeInput(invalid, 0); }, "reject unknown button bits");
        invalid = Neutral(); invalid[8] = 16;
        Throws<ArgumentException>(delegate { V5Protocol.EncodeInput(invalid, 0); }, "reject unknown D-pad bits");

        byte[] feedbackHeader = { 0x56, 0x50, 0x43, 0x4D, 5, 0x81, 0xDA, 1, 0, 0, 0, 0, 0xFF, 0x6C, 0xC4, 0x6C };
        byte[] golden = Join(feedbackHeader, Pattern(474));
        using (var reader = new V5FeedbackReader(new FragmentedStream(golden, 1, 0), false))
        {
            V5Frame f = reader.Read(1000);
            Check(f.Type == 0x81 && f.Sequence == 0 && Equal(f.Feedback, Pattern(474)), "read fixed feedback CRC 6CC46CFF through one-byte fragments");
            Check(f.RumbleSmall == 0 && f.RumbleLarge == 1 && f.LedRed == 2 && f.LedGreen == 3 && f.LedBlue == 4 && f.PlayerLeds == 5, "parse six compact feedback fields");
            Check(f.SpeakerPcm == null && f.MicrophoneActive == null, "controller feedback has no synthetic media or event");
        }
        byte[] eventPayload = { 1, 8, 7, 6, 5, 4, 3, 2, 1 };
        byte[] combined = Join(golden, Packet(0x83, 1, Atomic()), Packet(0x84, 2, Pattern(474)), Packet(0x85, 3, eventPayload));
        using (var source = new MemoryStream(combined))
        using (var reader = new V5FeedbackReader(source, true))
        {
            reader.Read(1000);
            Check(source.Position == golden.Length, "coalesced TCP buffer retains next frame");
            V5Frame audio = reader.Read(1000);
            Check(audio.Sequence == 1 && Equal(audio.Feedback, Pattern(474)) && Equal(audio.SpeakerPcm, Pattern(1920)), "atomic feedback and exact stereo PCM boundary");
            V5Frame haptics = reader.Read(1000);
            Check(haptics.Type == 0x84 && haptics.Sequence == 2 && Equal(haptics.Feedback, Pattern(474)), "source-defined realtime haptics lane shares sequence");
            V5Frame evt = reader.Read(1000);
            Check(evt.Type == 0x85 && evt.Sequence == 3 && evt.MicrophoneActive == true && evt.MicrophoneGeneration == 0x0102030405060708UL && evt.Feedback == null, "negotiated microphone event and 64-bit little-endian generation");
            Throws<InvalidOperationException>(delegate { byte unused = evt.RumbleSmall; }, "event is not compact controller feedback");
            Check(source.Position == source.Length, "all coalesced frames consumed exactly");
        }
        byte[] corrupt = (byte[])golden.Clone(); corrupt[0] ^= 1;
        Reject(corrupt, "reject bad magic", false);
        corrupt = (byte[])golden.Clone(); corrupt[4] = 4;
        Reject(corrupt, "reject V4 without downgrade", false);
        corrupt = (byte[])golden.Clone(); corrupt[5] = 0x82;
        Reject(corrupt, "reject unknown server type", false);
        Reject(Packet(1, 0, Neutral()), "reject client-direction input as feedback", false);
        foreach (int n in new int[] { 0, 6, 473, 475, 65535 })
            Reject(Packet(0x81, 0, new byte[n]), "reject feedback length " + n, false);
        Reject(Packet(0x83, 0, new byte[2395]), "reject short atomic payload", false);
        byte[] badAtomic = Atomic(); badAtomic[0] = 0xD9;
        Reject(Packet(0x83, 0, badAtomic), "reject atomic internal boundary despite valid CRC", false);
        corrupt = (byte[])golden.Clone(); corrupt[20] ^= 1;
        Reject(corrupt, "reject payload CRC mismatch", false);
        corrupt = (byte[])golden.Clone(); corrupt[12] ^= 1;
        Reject(corrupt, "reject CRC field mismatch", false);
        Reject(Packet(0x81, 1, Pattern(474)), "reject nonzero fresh server sequence", false);
        foreach (uint next in new uint[] { 0, 2 })
        {
            using (var reader = new V5FeedbackReader(new MemoryStream(Join(golden, Packet(0x84, next, Pattern(474)))), false))
            {
                reader.Read(1000);
                Throws<InvalidDataException>(delegate { reader.Read(1000); }, next == 0 ? "reject replay across frame types" : "reject sequence gap across frame types");
            }
        }
        Reject(Packet(0x85, 0, eventPayload), "reject unnegotiated microphone event", false);
        eventPayload[0] = 2;
        Reject(Packet(0x85, 0, eventPayload), "reject invalid microphone active flag", true);
        Reject(Packet(0x85, 0, new byte[8]), "reject short microphone event", true);

        foreach (int n in new int[] { 0, 1, 4, 12, 15, 16, 17, 489 })
        {
            byte[] truncated = new byte[n]; Buffer.BlockCopy(golden, 0, truncated, 0, n);
            using (var reader = new V5FeedbackReader(new FragmentedStream(truncated, 3, 0), false))
                Throws<EndOfStreamException>(delegate { reader.Read(1000); }, "EOF at frame byte " + n);
        }
        using (var source = new FragmentedStream(golden, 1, 25))
        using (var reader = new V5FeedbackReader(source, false))
        {
            Stopwatch clock = Stopwatch.StartNew();
            Throws<TimeoutException>(delegate { reader.Read(80); }, "slow trickle obeys one absolute frame deadline");
            Check(clock.ElapsedMilliseconds < 1000 && source.Timeouts.Count < 20 && !source.CanRead, "deadline closes stream before header completes");
            Check(source.Timeouts.Count > 1 && source.Timeouts[1] < source.Timeouts[0], "remaining timeout decreases rather than resets");
        }
        using (var source = new FragmentedStream(golden, 1000, 50))
        using (var reader = new V5FeedbackReader(source, false))
            Throws<TimeoutException>(delegate { reader.Read(80); }, "header and final payload share deadline");
        using (var reader = new V5FeedbackReader(new MemoryStream(golden), false))
        {
            Throws<ArgumentOutOfRangeException>(delegate { reader.Read(0); }, "reject unbounded read timeout");
            Check(reader.Read(1000).Sequence == 0, "invalid timeout does not consume stream");
        }
    }

    static int Main()
    {
        bool success = false;
        try { Run(); success = true; }
        catch (Exception e) { Console.Error.WriteLine("FAIL " + e.GetType().Name + ": " + e.Message); }
        Console.WriteLine("RESULT passed=" + passed + " success=" + success.ToString().ToLowerInvariant() +
            " windows_attached=false sony_tested=false keyboard_captured=false backend_started=false");
        return success ? 0 : 1;
    }
}
