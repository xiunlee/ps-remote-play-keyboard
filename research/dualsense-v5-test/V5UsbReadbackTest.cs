// Isolated V5 candidate -> userspace USB/IP readback. No Windows USB import.
// References pinned upstream report layout; expected USB bytes are independent
// of the application's DS4-to-DualSense conversion implementation.
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
using PSKeyboard.Research;

internal static class V5UsbReadbackTest
{
    const int ApiPort = 33442, UsbPort = 33441;
    const int OperationLimitMs = 3000, TotalLimitMs = 90000;
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static uint sequence;
    static int passed;

    static int Remaining(int operationStarted)
    {
        int total = TotalLimitMs - (int)Clock.ElapsedMilliseconds;
        int operation = OperationLimitMs - ((int)Clock.ElapsedMilliseconds - operationStarted);
        if (total <= 0 || operation <= 0) throw new TimeoutException("Bounded test deadline exceeded.");
        return Math.Min(total, operation);
    }

    static TcpClient Connect(int port)
    {
        var client = new TcpClient(AddressFamily.InterNetwork);
        client.NoDelay = true; client.ReceiveTimeout = client.SendTimeout = OperationLimitMs;
        try
        {
            int start = (int)Clock.ElapsedMilliseconds;
            var pending = client.BeginConnect(IPAddress.Loopback, port, null, null);
            using (pending.AsyncWaitHandle)
            {
                if (!pending.AsyncWaitHandle.WaitOne(Remaining(start))) throw new TimeoutException("Loopback connection timed out.");
                client.EndConnect(pending);
            }
            return client;
        }
        catch { client.Close(); throw; }
    }

    static byte[] ReadExact(NetworkStream stream, int length)
    {
        if (length < 0 || length > 65536) throw new InvalidDataException("Invalid response size.");
        var result = new byte[length]; int offset = 0, start = (int)Clock.ElapsedMilliseconds;
        while (offset < length)
        {
            stream.ReadTimeout = Remaining(start);
            int count = stream.Read(result, offset, length - offset);
            Remaining(start);
            if (count == 0) throw new EndOfStreamException("Truncated protocol response.");
            offset += count;
        }
        return result;
    }

    static void Write(NetworkStream stream, byte[] bytes)
    {
        stream.WriteTimeout = Remaining((int)Clock.ElapsedMilliseconds);
        stream.Write(bytes, 0, bytes.Length);
    }
    static void Put16(byte[] bytes, int offset, ushort value)
    { bytes[offset] = (byte)(value >> 8); bytes[offset + 1] = (byte)value; }
    static void Put32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24); bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8); bytes[offset + 3] = (byte)value;
    }
    static ushort Get16(byte[] bytes, int offset)
    { return (ushort)((bytes[offset] << 8) | bytes[offset + 1]); }
    static uint Get32(byte[] bytes, int offset)
    { return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3]; }
    static void Pass(string label) { passed++; Console.WriteLine("PASS " + label); }

    const string SourceCommit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2";
    const string DeviceType = "dualsensecombinedaudioduplexv5";

    static Dictionary<string, object> Request(string command, bool cleanup)
    {
        // Cleanup gets its own bounded deadline even after the main deadline.
        var timer = Stopwatch.StartNew(); int start = (int)Clock.ElapsedMilliseconds;
        Func<int> budget = delegate {
            int remaining = OperationLimitMs - (int)timer.ElapsedMilliseconds;
            if (remaining <= 0) throw new TimeoutException("Lab API deadline exceeded.");
            return cleanup ? remaining : Math.Min(remaining, Remaining(start));
        };
        using (var client = new TcpClient(AddressFamily.InterNetwork))
        {
            var pending = client.BeginConnect(IPAddress.Loopback, ApiPort, null, null);
            using (pending.AsyncWaitHandle)
            {
                if (!pending.AsyncWaitHandle.WaitOne(budget())) throw new TimeoutException("Lab API connect timeout.");
                client.EndConnect(pending);
            }
            using (var stream = client.GetStream())
            using (var output = new MemoryStream())
            {
                stream.WriteTimeout = budget();
                byte[] bytes = Encoding.ASCII.GetBytes(command + "\0");
                stream.Write(bytes, 0, bytes.Length);
                var buffer = new byte[2048];
                for (;;)
                {
                    stream.ReadTimeout = budget();
                    int count = stream.Read(buffer, 0, buffer.Length);
                    budget();
                    if (count == 0) break;
                    if (output.Length + count > 16384) throw new InvalidDataException("Lab API response too large.");
                    output.Write(buffer, 0, count);
                }
                var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(output.ToArray()));
                if (data == null || data.ContainsKey("status")) throw new InvalidDataException("Lab API rejected request.");
                return data;
            }
        }
    }

    static string Text(Dictionary<string, object> data, string key)
    { object value; return data.TryGetValue(key, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null; }

    static void VerifyLabServer()
    {
        var ping = Request("ping", false);
        if (Text(ping, "labMarker") != "pskeyboard-dualsense-v5-userspace-only" ||
            Text(ping, "sourceCommit") != SourceCommit || Text(ping, "autoAttach") != "False")
            throw new InvalidDataException("Expected the dedicated source-pinned lab runner with auto-attach disabled.");
        Pass("lab runner source marker matches; autoAttach=false (source integrity is verified separately at build)");
    }

    sealed class Feeder : IDisposable
    {
        internal uint BusId;
        string deviceId;
        TcpClient client;
        NetworkStream stream;
        uint inputSequence;
        internal V5FeedbackReader Feedback;
        internal string UsbBusId { get { return BusId.ToString(CultureInfo.InvariantCulture) + "-" + deviceId; } }
        internal void Connect()
        {
            var created = Request("bus/create", false);
            BusId = UInt32.Parse(Text(created, "busId"), CultureInfo.InvariantCulture);
            if (BusId == 0) throw new InvalidDataException("Invalid owned bus.");
            var added = Request("bus/" + BusId + "/add {\"type\":\"" + DeviceType + "\"}", false);
            if (Text(added, "busId") != BusId.ToString(CultureInfo.InvariantCulture) || Text(added, "type") != DeviceType)
                throw new InvalidDataException("Device ownership mismatch.");
            uint id;
            if (!UInt32.TryParse(Text(added, "devId"), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                throw new InvalidDataException("Invalid device ID.");
            deviceId = id.ToString(CultureInfo.InvariantCulture);
            client = V5UsbReadbackTest.Connect(ApiPort); stream = client.GetStream();
            Write(stream, Encoding.ASCII.GetBytes("bus/" + BusId + "/" + deviceId + "\0"));
            Feedback = new V5FeedbackReader(stream, false);
            Send(ViiperTransport.CreateNeutralFrame("dualsense"));
        }
        internal void Send(byte[] frame)
        { Write(stream, V5Protocol.EncodeInput(V5Protocol.ConvertLegacyKeyboardState(frame), inputSequence++)); }
        internal void Disconnect()
        {
            if (client != null) { client.Close(); client = null; stream = null; }
            if (BusId == 0) return;
            var removed = Request("bus/remove " + BusId, true);
            if (Text(removed, "busId") != BusId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Owned bus cleanup mismatch.");
            BusId = 0;
        }
        public void Dispose() { Disconnect(); }
    }

    static void CheckDescriptors(NetworkStream usb, uint device)
    {
        byte[] deviceDesc = TransferIn(usb, device, 0, 18, new byte[] { 0x80, 6, 0, 1, 0, 0, 18, 0 });
        if (deviceDesc[0] != 18 || deviceDesc[1] != 1 || deviceDesc[8] != 0x4c || deviceDesc[9] != 5 ||
            deviceDesc[10] != 0xe6 || deviceDesc[11] != 0x0c) throw new InvalidDataException("USB device descriptor mismatch.");
        Pass("USB device descriptor VID/PID");
        byte[] header = TransferIn(usb, device, 0, 9, new byte[] { 0x80, 6, 0, 2, 0, 0, 9, 0 });
        int size = header[2] | (header[3] << 8);
        if (size < 9 || size > 4096 || header[4] != 4) throw new InvalidDataException("Expected four composite interfaces.");
        byte[] config = TransferIn(usb, device, 0, size, new byte[] { 0x80, 6, 0, 2, 0, 0, (byte)size, (byte)(size >> 8) });
        int offset = 0, iface = -1, alternate = -1, reportSize = 0;
        bool[] baseInterfaces = new bool[4]; bool audioOut = false, audioIn = false, hidIn = false, hidOut = false;
        while (offset < config.Length)
        {
            int length = config[offset];
            if (length < 2 || offset + length > config.Length) throw new InvalidDataException("Malformed configuration descriptor.");
            int type = config[offset + 1];
            if (type == 4)
            {
                if (length < 9) throw new InvalidDataException("Short interface descriptor.");
                iface = config[offset + 2]; alternate = config[offset + 3];
                if (iface < 0 || iface > 3 || config[offset + 5] != (iface == 3 ? 3 : 1))
                    throw new InvalidDataException("Composite interface class mismatch.");
                if (alternate == 0) baseInterfaces[iface] = true;
            }
            else if (type == 0x21 && iface == 3)
            {
                if (length != 9 || config[offset + 6] != 0x22) throw new InvalidDataException("HID descriptor mismatch.");
                reportSize = config[offset + 7] | (config[offset + 8] << 8);
            }
            else if (type == 5)
            {
                if (length < 7) throw new InvalidDataException("Short endpoint descriptor.");
                int endpoint = config[offset + 2], transfer = config[offset + 3] & 3;
                audioOut |= iface == 1 && alternate == 1 && endpoint == 1 && transfer == 1;
                audioIn |= iface == 2 && alternate == 1 && endpoint == 0x82 && transfer == 1;
                hidIn |= iface == 3 && endpoint == 0x84 && transfer == 3;
                hidOut |= iface == 3 && endpoint == 3 && transfer == 3;
            }
            offset += length;
        }
        for (int i = 0; i < 4; i++) if (!baseInterfaces[i]) throw new InvalidDataException("Missing interface.");
        if (!audioOut || !audioIn || !hidIn || !hidOut) throw new InvalidDataException("Missing composite endpoint.");
        Pass("audio interfaces 0/1/2 and HID 3; ISO OUT/IN and interrupt OUT/IN");
        if (reportSize < 1 || reportSize > 4096) throw new InvalidDataException("Invalid HID report descriptor size.");
        byte[] report = TransferIn(usb, device, 0, reportSize, new byte[] { 0x81, 6, 0, 0x22, 3, 0, (byte)reportSize, (byte)(reportSize >> 8) });
        if (report[0] != 5 || report[1] != 1) throw new InvalidDataException("HID report usage page mismatch.");
        Pass("HID report descriptor returned at interface 3");
        TransferOut(usb, device, 0, new byte[0], new byte[] { 0, 9, 1, 0, 0, 0, 0, 0 });
        Pass("SET_CONFIGURATION 1");
    }

    static void TransferOut(NetworkStream usb, uint device, uint endpoint, byte[] payload, byte[] setup)
    {
        var request = new byte[48 + payload.Length]; uint current = ++sequence;
        Put32(request, 0, 1); Put32(request, 4, current); Put32(request, 8, device);
        Put32(request, 16, endpoint); Put32(request, 24, (uint)payload.Length); Put32(request, 32, 0xffffffff);
        if (setup != null) Buffer.BlockCopy(setup, 0, request, 40, 8);
        Buffer.BlockCopy(payload, 0, request, 48, payload.Length); Write(usb, request);
        byte[] header = ReadExact(usb, 48);
        if (Get32(header, 0) != 3 || Get32(header, 4) != current || Get32(header, 20) != 0 ||
            Get32(header, 24) != payload.Length || (Get32(header, 32) != 0 && Get32(header, 32) != UInt32.MaxValue))
            throw new InvalidDataException("USB/IP OUT transfer rejected.");
    }

    static void CheckControlsAndFeedback(Feeder feeder, NetworkStream usb, uint device)
    {
        byte[] calibration = TransferIn(usb, device, 0, 41, new byte[] { 0xa1, 1, 5, 3, 3, 0, 41, 0 });
        if (calibration[19] != 0 || calibration[20] != 2 || calibration[21] != 0 || calibration[22] != 2)
            throw new InvalidDataException("Candidate canonical gyro calibration mismatch.");
        Pass("feature 05 gyro calibration uses 512+512");
        byte[] rate = TransferIn(usb, device, 0, 3, new byte[] { 0xa2, 0x81, 0, 1, 1, 0, 3, 0 });
        if (rate[0] != 0x80 || rate[1] != 0xbb || rate[2] != 0) throw new InvalidDataException("Audio rate mismatch.");
        Pass("audio endpoint GET_CUR 48000 Hz (no audio streaming claim)");
        var feedback = new V5FeedbackReaderProxy(feeder);
        byte[] output = new byte[48]; output[0] = 2; output[1] = 3; output[2] = 0x14;
        output[3] = 17; output[4] = 29; output[44] = 3; output[45] = 41; output[46] = 53; output[47] = 67;
        TransferOut(usb, device, 3, output, null);
        feedback.Expect(17, 29, 41, 53, 67, 3);
        Pass("interrupt OUT report 02 -> V5 81 CRC/sequence and 474-byte feedback");
        output[3] = 19; output[4] = 31; output[45] = 43;
        TransferOut(usb, device, 0, output, new byte[] { 0x21, 9, 2, 2, 3, 0, 48, 0 });
        feedback.Expect(19, 31, 43, 53, 67, 3);
        Pass("HID SET_REPORT at interface 3 -> next V5 feedback sequence");
    }

    sealed class V5FeedbackReaderProxy
    {
        readonly Feeder feeder;
        internal V5FeedbackReaderProxy(Feeder feeder) { this.feeder = feeder; }
        internal void Expect(byte small, byte large, byte r, byte g, byte b, byte leds)
        {
            V5Frame frame = feeder.Feedback.Read(Remaining((int)Clock.ElapsedMilliseconds));
            if (frame.Type != 0x81 || frame.Feedback == null || frame.Feedback.Length != 474 ||
                frame.RumbleSmall != small || frame.RumbleLarge != large || frame.LedRed != r ||
                frame.LedGreen != g || frame.LedBlue != b || frame.PlayerLeds != leds)
                throw new InvalidDataException("V5 feedback state mismatch.");
        }
    }

    static uint Import(NetworkStream usb, string busId)
    {
        var request = new byte[40]; Put16(request, 0, 0x0111); Put16(request, 2, 0x8003);
        byte[] name = Encoding.ASCII.GetBytes(busId);
        if (name.Length == 0 || name.Length > 31) throw new InvalidDataException("Invalid owned bus ID.");
        Buffer.BlockCopy(name, 0, request, 8, name.Length); Write(usb, request);
        byte[] header = ReadExact(usb, 8);
        if (Get16(header, 0) != 0x0111 || Get16(header, 2) != 0x0003 || Get32(header, 4) != 0)
            throw new InvalidDataException("Userspace USB/IP import rejected.");
        byte[] descriptor = ReadExact(usb, 312);
        string returnedBus = Encoding.ASCII.GetString(descriptor, 256, 32).TrimEnd('\0');
        if (returnedBus != busId || Get16(descriptor, 300) != 0x054c || Get16(descriptor, 302) != 0x0ce6)
            throw new InvalidDataException("Imported device identity or owned bus mismatch.");
        uint bus = Get32(descriptor, 288), device = Get32(descriptor, 292);
        if (bus == 0 || bus > 65535 || device == 0 || device > 65535)
            throw new InvalidDataException("Invalid USB/IP device address.");
        Pass("owned virtual DualSense identity VID=054C PID=0CE6");
        return (bus << 16) | device;
    }

    static byte[] TransferIn(NetworkStream usb, uint device, uint endpoint, int length, byte[] setup)
    {
        var request = new byte[48]; uint current = ++sequence;
        Put32(request, 0, 1); Put32(request, 4, current); Put32(request, 8, device);
        Put32(request, 12, 1); Put32(request, 16, endpoint); Put32(request, 24, (uint)length);
        Put32(request, 32, 0xffffffff); Put32(request, 36, endpoint == 4 ? 4u : 0u);
        if (setup != null) Buffer.BlockCopy(setup, 0, request, 40, 8);
        Write(usb, request);
        byte[] header = ReadExact(usb, 48);
        if (Get32(header, 0) != 3 || Get32(header, 4) != current || Get32(header, 20) != 0)
            throw new InvalidDataException("USB/IP transfer failed or sequence mismatch.");
        uint actual = Get32(header, 24), packets = Get32(header, 32);
        if (actual != length || (packets != 0 && packets != 0xffffffff))
            throw new InvalidDataException("Unexpected USB/IP response length or packet type.");
        return ReadExact(usb, length);
    }

    static byte[] ReadReport(NetworkStream usb, uint device)
    {
        byte[] result = TransferIn(usb, device, 4, 64, null);
        if (result[0] != 1) throw new InvalidDataException("Expected DualSense input report 01.");
        return result;
    }

    static void CheckFeature(NetworkStream usb, uint device, byte reportId, byte length)
    {
        // HID GET_REPORT(feature), interface 3, little-endian setup fields.
        byte[] setup = { 0xa1, 1, reportId, 3, 3, 0, length, 0 };
        byte[] report = TransferIn(usb, device, 0, length, setup);
        if (report[0] != reportId) throw new InvalidDataException("Unexpected feature report ID.");
        Pass("feature " + reportId.ToString("X2") + " size=" + length + "; payload not logged");
    }

    sealed class Expected
    {
        internal readonly string Label;
        internal readonly sbyte LX, LY;
        internal readonly uint Buttons;
        internal readonly byte Hat, L2, R2;
        internal Expected(string label, sbyte lx, sbyte ly, uint buttons, byte hat, byte l2, byte r2)
        { Label = label; LX = lx; LY = ly; Buttons = buttons; Hat = hat; L2 = l2; R2 = r2; }
    }

    static readonly Expected Neutral = new Expected("neutral", 0, 0, 0, 8, 0, 0);
    static readonly Expected[] Actions = {
        new Expected("left stick up", 0, -128, 0, 8, 0, 0),
        new Expected("left stick down", 0, 127, 0, 8, 0, 0),
        new Expected("left stick left", -128, 0, 0, 8, 0, 0),
        new Expected("left stick right", 127, 0, 0, 8, 0, 0),
        new Expected("D-pad up", 0, 0, 0, 0, 0, 0),
        new Expected("D-pad down", 0, 0, 0, 4, 0, 0),
        new Expected("D-pad left", 0, 0, 0, 6, 0, 0),
        new Expected("D-pad right", 0, 0, 0, 2, 0, 0),
        new Expected("Cross", 0, 0, 0x0020, 8, 0, 0),
        new Expected("Circle", 0, 0, 0x0040, 8, 0, 0),
        new Expected("Square", 0, 0, 0x0010, 8, 0, 0),
        new Expected("Triangle", 0, 0, 0x0080, 8, 0, 0),
        new Expected("L1", 0, 0, 0x0100, 8, 0, 0),
        new Expected("R1", 0, 0, 0x0200, 8, 0, 0),
        new Expected("L2 digital and analog", 0, 0, 0x0400, 8, 255, 0),
        new Expected("R2 digital and analog", 0, 0, 0x0800, 8, 0, 255),
        new Expected("Create (DS4 Share mapping)", 0, 0, 0x1000, 8, 0, 0),
        new Expected("Options", 0, 0, 0x2000, 8, 0, 0),
        new Expected("L3", 0, 0, 0x4000, 8, 0, 0),
        new Expected("R3", 0, 0, 0x8000, 8, 0, 0),
        new Expected("PS at DualSense bit16", 0, 0, 0x10000, 8, 0, 0),
        new Expected("touchpad click at DualSense bit17", 0, 0, 0x20000, 8, 0, 0)
    };

    static void Expect(Feeder feeder, NetworkStream usb, uint device, string label, uint held, bool enabled, Expected expected)
    {
        byte[] frame = ViiperTransport.ConvertKeyboardFrame(KeyboardMapping.Frame(held, enabled), "dualsense");
        if (frame.Length != 33) throw new InvalidDataException("DualSense feeder must use 33-byte states.");
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Remaining((int)Clock.ElapsedMilliseconds);
            feeder.Send(frame); Thread.Sleep(10);
            byte[] report = ReadReport(usb, device);
            uint buttons = (uint)(report[8] & 0xf0) | ((uint)report[9] << 8) | ((uint)report[10] << 16);
            bool match = report[1] == expected.LX + 128 && report[2] == expected.LY + 128 &&
                report[3] == 128 && report[4] == 128 && report[5] == expected.L2 && report[6] == expected.R2 &&
                (report[8] & 0x0f) == expected.Hat && buttons == expected.Buttons &&
                report[16] == 0 && report[17] == 0 && report[18] == 0 && report[19] == 0 &&
                report[20] == 0 && report[21] == 0 && report[22] == 0 && report[23] == 0 &&
                report[24] == 0 && report[25] == 0 && report[26] == 0 && report[27] == 0xe0 &&
                (report[33] & 0x80) != 0 && (report[37] & 0x80) != 0;
            if (match) { Pass(label); return; }
        }
        throw new InvalidDataException("Expected state not observed: " + label);
    }

    static int Main(string[] args)
    {
        if (args.Length != 4 || args[0] != "--isolated-loopback-server" || args[1] != "33442" ||
            args[2] != "33441" || args[3] != "--auto-attach-disabled")
        {
            Console.Error.WriteLine("Usage: V5UsbReadbackTest.exe --isolated-loopback-server 33442 33441 --auto-attach-disabled");
            Console.Error.WriteLine("Requires the pinned source-built lab server; never use the production VIIPER server.");
            return 2;
        }
        Feeder feeder = null; bool success = false, cleanup = true;
        try
        {
            VerifyLabServer();
            feeder = new Feeder(); feeder.Connect();
            using (var usbClient = Connect(UsbPort))
            using (var usb = usbClient.GetStream())
            {
                uint device = Import(usb, feeder.UsbBusId);
                CheckDescriptors(usb, device);
                Expect(feeder, usb, device, "initial mapped neutral", 0, true, Neutral);
                for (int i = 0; i < Actions.Length; i++)
                {
                    Expect(feeder, usb, device, "action " + i + " " + Actions[i].Label, 1u << i, true, Actions[i]);
                    Expect(feeder, usb, device, "release action " + i, 0, true, Neutral);
                }
                Expect(feeder, usb, device, "Cross held sample1", 1u << 8, true, Actions[8]);
                Expect(feeder, usb, device, "Cross held sample2", 1u << 8, true, Actions[8]);
                Expect(feeder, usb, device, "normalized diagonal", (1u << 0) | (1u << 3), true, new Expected("", 90, -90, 0, 8, 0, 0));
                Expect(feeder, usb, device, "opposite stick directions cancel", 15, true, Neutral);
                Expect(feeder, usb, device, "D-pad up-right diagonal", (1u << 4) | (1u << 7), true, new Expected("", 0, 0, 0, 1, 0, 0));
                Expect(feeder, usb, device, "opposite D-pad directions cancel", 15u << 4, true, Neutral);
                Expect(feeder, usb, device, "Cross and Triangle together", (1u << 8) | (1u << 11), true, new Expected("", 0, 0, 0xa0, 8, 0, 0));
                Expect(feeder, usb, device, "PS and touchpad together", (1u << 20) | (1u << 21), true, new Expected("", 0, 0, 0x30000, 8, 0, 0));
                Expect(feeder, usb, device, "both triggers digital and analog", (1u << 14) | (1u << 15), true, new Expected("", 0, 0, 0x0c00, 8, 255, 255));
                Expect(feeder, usb, device, "disabled all held actions are neutral", (1u << 22) - 1, false, Neutral);
                Expect(feeder, usb, device, "final release", 0, true, Neutral);
                CheckFeature(usb, device, 0x05, 41);
                CheckFeature(usb, device, 0x09, 20);
                CheckFeature(usb, device, 0x20, 64);
                CheckControlsAndFeedback(feeder, usb, device);
            }
            success = true;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error.GetType().Name + ": " + error.Message); }
        finally
        {
            if (feeder != null)
            {
                try
                {
                    feeder.Disconnect();
                    if (feeder.BusId != 0) throw new IOException("Test-owned bus removal not confirmed.");
                    feeder.Dispose(); Pass("removed only the test-owned virtual bus");
                }
                catch (Exception error) { cleanup = false; Console.Error.WriteLine("FAIL cleanup: " + error.Message); }
            }
        }
        Console.WriteLine("RESULT passed=" + passed + " success=" + (success && cleanup).ToString().ToLowerInvariant() +
            " elapsed_ms=" + Clock.ElapsedMilliseconds + " windows_attached=false sony_tested=false keyboard_captured=false");
        return success && cleanup ? 0 : 1;
    }
}
