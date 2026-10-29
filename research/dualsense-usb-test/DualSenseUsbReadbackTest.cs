// Dedicated loopback VIIPER -> userspace USB/IP readback. No Windows USB import.
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

internal static class DualSenseUsbReadbackTest
{
    const int ApiPort = 33342, UsbPort = 33341;
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

    static void VerifyServer()
    {
        using (var client = Connect(ApiPort))
        using (var stream = client.GetStream())
        using (var response = new MemoryStream())
        {
            Write(stream, Encoding.ASCII.GetBytes("ping\0"));
            var buffer = new byte[4096]; int start = (int)Clock.ElapsedMilliseconds;
            for (;;)
            {
                stream.ReadTimeout = Remaining(start);
                int count = stream.Read(buffer, 0, buffer.Length);
                if (count == 0) break;
                if (response.Length + count > 16384) throw new InvalidDataException("API response too large.");
                response.Write(buffer, 0, count);
            }
            var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(response.ToArray()));
            if (result == null || !result.ContainsKey("server") || !result.ContainsKey("version") ||
                Convert.ToString(result["server"], CultureInfo.InvariantCulture) != "VIIPER" ||
                Convert.ToString(result["version"], CultureInfo.InvariantCulture).TrimStart('v') != "0.9.0")
                throw new InvalidDataException("Expected dedicated VIIPER v0.9.0 server.");
            Pass("pinned server version=0.9.0; transport=loopback; Windows import disabled by operator");
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
        // HID GET_REPORT(feature), interface 0, little-endian setup fields.
        byte[] setup = { 0xa1, 1, reportId, 3, 0, 0, length, 0 };
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

    static void Expect(ViiperTransport feeder, NetworkStream usb, uint device, string label, uint held, bool enabled, Expected expected)
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
        if (args.Length != 4 || args[0] != "--isolated-loopback-server" || args[1] != "33342" ||
            args[2] != "33341" || args[3] != "--auto-attach-disabled")
        {
            Console.Error.WriteLine("Usage: DualSenseUsbReadbackTest.exe --isolated-loopback-server 33342 33341 --auto-attach-disabled");
            Console.Error.WriteLine("Requires a separately verified dedicated VIIPER v0.9.0 server bound only to loopback, with auto-attach disabled.");
            return 2;
        }
        ViiperTransport feeder = null; bool success = false, cleanup = true;
        try
        {
            VerifyServer();
            feeder = new ViiperTransport(ApiPort, "dualsense"); feeder.Connect();
            using (var usbClient = Connect(UsbPort))
            using (var usb = usbClient.GetStream())
            {
                uint device = Import(usb, feeder.UsbBusId);
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
