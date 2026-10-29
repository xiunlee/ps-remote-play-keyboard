// Hosted Windows guest acceptance only. Never run this on the user's PC.
// No Sony process access, keyboard capture, installation, driver changes, or
// blanket detach. Only the bus and verified USB/IP port created here are owned.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;
using PSKeyboard;
using PSKeyboard.Research;

internal static class WindowsDualSenseDriverTest
{
    const string UsbIpExe = @"C:\Program Files\USBip\usbip.exe";
    const string SourceCommit = "b78e31e93e84b4cb7c4b4c15b8aace10749106d2";
    static string DeviceType, TargetId;
    static ushort ProductId;
    const int ApiPort = 33542, UsbPort = 33541, MainLimitMs = 85000;
    static readonly Stopwatch Clock = Stopwatch.StartNew();
    static readonly List<string> Errors = new List<string>();
    static bool guard, marker, cliVersion, attached, portVerified, hidOpened, hidCaps, inputPassed;
    static bool neutralSent, portDetached, busRemoved, hidRemoved, cleanupComplete;
    static bool commandTerminationUnconfirmed;
    static int passed, pressed, released;

    // These are accidental-execution guards, not attestation of an untrusted host.
    static void ValidateGuard(string[] args)
    {
        if (args.Length != 3 || args[0] != "--isolated-driver-test-only" || args[1] != "--persona")
            throw new InvalidOperationException("Usage: WindowsDualSenseDriverTest.exe --isolated-driver-test-only --persona <allowed-v5-persona>");
        switch (args[2]) {
            case "dualsensecombinedaudioduplexv5":
            case "dualsensegamepadv5": ProductId = 0x0ce6; break;
            case "dualsenseedgecombinedaudioduplexv5":
            case "dualsenseedgegamepadv5": ProductId = 0x0df2; break;
            default: throw new InvalidOperationException("Persona is not one of the four explicitly allowed V5 candidates.");
        }
        DeviceType = args[2]; TargetId = "vid_054c&pid_" + ProductId.ToString("x4", CultureInfo.InvariantCulture);
        string[,] required = {
            { "GITHUB_ACTIONS", "true" }, { "RUNNER_ENVIRONMENT", "github-hosted" },
            { "GITHUB_REPOSITORY", "xiunlee/ps-remote-play-keyboard" }, { "RUNNER_OS", "Windows" }
        };
        for (int i = 0; i < required.GetLength(0); i++)
            if (!String.Equals(Environment.GetEnvironmentVariable(required[i, 0]), required[i, 1], StringComparison.Ordinal))
                throw new InvalidOperationException("Hosted-only guard refused: " + required[i, 0]);
        if (Environment.OSVersion.Platform != PlatformID.Win32NT || IntPtr.Size != 8)
            throw new InvalidOperationException("Requires a Windows x64 hosted guest.");
    }

    static int Budget(Stopwatch operation, int limit, bool cleanup)
    {
        int left = limit - (int)operation.ElapsedMilliseconds;
        if (!cleanup) left = Math.Min(left, MainLimitMs - (int)Clock.ElapsedMilliseconds);
        if (left <= 0) throw new TimeoutException("Test operation deadline exceeded.");
        return left;
    }
    static void Pass(string label) { passed++; Console.WriteLine("PASS " + label); }
    static void Fail(string stage, Exception error)
    {
        string message = stage + ": " + error.GetType().Name + ": " + error.Message;
        Errors.Add(message); Console.Error.WriteLine("FAIL " + message);
    }
    static string Text(Dictionary<string, object> data, string key)
    { object value; return data.TryGetValue(key, out value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : null; }

    static TcpClient Connect(int port, Stopwatch timer, bool cleanup)
    {
        var client = new TcpClient(AddressFamily.InterNetwork); client.NoDelay = true;
        try {
            var pending = client.BeginConnect(IPAddress.Loopback, port, null, null);
            using (pending.AsyncWaitHandle) {
                if (!pending.AsyncWaitHandle.WaitOne(Budget(timer, 3000, cleanup)))
                    throw new TimeoutException("Loopback connection timeout.");
                client.EndConnect(pending);
            }
            Budget(timer, 3000, cleanup); return client;
        } catch { client.Close(); throw; }
    }
    static Dictionary<string, object> Request(string command, bool cleanup)
    {
        var timer = Stopwatch.StartNew();
        using (var client = Connect(ApiPort, timer, cleanup))
        using (var stream = client.GetStream())
        using (var output = new MemoryStream()) {
            byte[] bytes = Encoding.ASCII.GetBytes(command + "\0");
            stream.WriteTimeout = Budget(timer, 3000, cleanup); stream.Write(bytes, 0, bytes.Length);
            byte[] buffer = new byte[2048];
            for (;;) {
                stream.ReadTimeout = Budget(timer, 3000, cleanup);
                int count = stream.Read(buffer, 0, buffer.Length); Budget(timer, 3000, cleanup);
                if (count == 0) break;
                if (output.Length + count > 16384) throw new InvalidDataException("Lab response too large.");
                output.Write(buffer, 0, count);
            }
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(
                new UTF8Encoding(false, true).GetString(output.ToArray()));
            if (data == null || data.ContainsKey("status")) throw new InvalidDataException("Lab API rejected request.");
            return data;
        }
    }
    static void VerifyLab()
    {
        var ping = Request("ping", false);
        if (Text(ping, "labMarker") != "pskeyboard-dualsense-v5-driver-test" ||
            Text(ping, "sourceCommit") != SourceCommit || Text(ping, "autoAttach") != "False" ||
            Text(ping, "hostedRunnerOnly") != "True" || Text(ping, "apiAddress") != "127.0.0.1:33542" ||
            Text(ping, "usbAddress") != "127.0.0.1:33541")
            throw new InvalidDataException("Unexpected lab identity, endpoints, or auto-attach setting.");
        marker = true; Pass("dedicated lab marker; source integrity is checked separately by the build runner");
    }
    sealed class Feeder
    {
        internal uint BusId;
        internal bool CreateAttempted, BusAllocated;
        uint deviceId, sequence;
        TcpClient client; NetworkStream stream;
        internal string UsbBusId { get { return BusId.ToString(CultureInfo.InvariantCulture) + "-" + deviceId.ToString(CultureInfo.InvariantCulture); } }
        internal bool Connected { get { return stream != null; } }
        internal void Open()
        {
            CreateAttempted = true;
            var created = Request("bus/create", false);
            uint id;
            if (!UInt32.TryParse(Text(created, "busId"), NumberStyles.None, CultureInfo.InvariantCulture, out id) || id == 0)
                throw new InvalidDataException("Invalid allocated bus.");
            BusId = id; BusAllocated = true; // Retain ownership immediately, even if add/connect fails.
            var added = Request("bus/" + BusId + "/add {\"type\":\"" + DeviceType + "\"}", false);
            if (Text(added, "busId") != BusId.ToString(CultureInfo.InvariantCulture) || Text(added, "type") != DeviceType ||
                !UInt32.TryParse(Text(added, "devId"), NumberStyles.None, CultureInfo.InvariantCulture, out deviceId) || deviceId == 0)
                throw new InvalidDataException("Allocated device ownership mismatch.");
            var timer = Stopwatch.StartNew(); client = Connect(ApiPort, timer, false); stream = client.GetStream();
            byte[] bytes = Encoding.ASCII.GetBytes("bus/" + BusId + "/" + deviceId + "\0");
            stream.WriteTimeout = Budget(timer, 3000, false); stream.Write(bytes, 0, bytes.Length);
            Budget(timer, 3000, false); Send(ViiperTransport.CreateNeutralFrame("dualsense"), false);
        }
        internal void Send(byte[] legacy, bool cleanup)
        {
            var timer = Stopwatch.StartNew();
            byte[] frame = V5Protocol.EncodeInput(V5Protocol.ConvertLegacyKeyboardState(legacy), sequence++);
            stream.WriteTimeout = Budget(timer, 3000, cleanup); stream.Write(frame, 0, frame.Length);
            Budget(timer, 3000, cleanup);
        }
        internal void CloseStream()
        { if (client != null) client.Close(); client = null; stream = null; }
        internal void RemoveBus()
        {
            if (BusId == 0) {
                if (CreateAttempted && !BusAllocated)
                    throw new IOException("Bus-create outcome unknown; refusing to claim cleanup or remove an unowned bus.");
                return;
            }
            var response = Request("bus/remove " + BusId, true);
            if (Text(response, "busId") != BusId.ToString(CultureInfo.InvariantCulture))
                throw new InvalidDataException("Owned bus removal not confirmed.");
            BusId = 0;
        }
    }

    sealed class CommandResult { internal int Code; internal string Output, Error; }
    static CommandResult Run(string arguments, bool cleanup)
    {
        var timer = Stopwatch.StartNew();
        var start = new ProcessStartInfo(UsbIpExe, arguments) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using (var process = new Process { StartInfo = start }) {
            var output = new StringBuilder(); var error = new StringBuilder();
            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (output) { if (output.Length < 65536) output.AppendLine(e.Data); } };
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) lock (error) { if (error.Length < 65536) error.AppendLine(e.Data); } };
            Budget(timer, cleanup ? 5000 : 10000, cleanup);
            if (!process.Start()) throw new IOException("Could not start the fixed USBip command.");
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            try {
                if (!process.WaitForExit(Budget(timer, cleanup ? 5000 : 10000, cleanup)))
                    throw new TimeoutException("USBip command timed out.");
                process.WaitForExit(); // Flush redirected event handlers after process exit.
                return new CommandResult { Code = process.ExitCode, Output = output.ToString(), Error = error.ToString() };
            } catch {
                // This is only the child usbip.exe started by this invocation.
                try {
                    if (!process.HasExited) {
                        process.Kill();
                        if (!process.WaitForExit(1000)) commandTerminationUnconfirmed = true;
                    }
                } catch { commandTerminationUnconfirmed = true; }
                throw;
            }
        }
    }
    static void RequireCommand(CommandResult result, string operation)
    { if (result.Code != 0) throw new IOException(operation + " exited " + result.Code + ": " + result.Error.Trim()); }
    static bool PortMatches(int port, string url, bool cleanup)
    {
        var result = Run("port " + port.ToString(CultureInfo.InvariantCulture), cleanup);
        if (result.Code != 0) return false;
        foreach (string line in result.Output.Split('\n'))
            if (String.Equals(line.Trim(), "-> " + url, StringComparison.Ordinal)) return true;
        return false;
    }
    static int FindOwnedPort(string url, bool cleanup)
    {
        var result = Run("port", cleanup); RequireCommand(result, "Read imported ports");
        int current = 0, found = 0;
        foreach (string line in result.Output.Split('\n')) {
            Match match = Regex.Match(line, @"^Port\s+(\d+):");
            if (match.Success) {
                if (!Int32.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out current) || current < 1 || current > 255)
                    throw new InvalidDataException("Invalid imported port number.");
            }
            if (String.Equals(line.Trim(), "-> " + url, StringComparison.Ordinal)) {
                if (current == 0 || found != 0) throw new InvalidDataException("Ambiguous owned USBip port.");
                found = current;
            }
        }
        return found;
    }

    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] struct Attributes { public uint Size; public ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Sequential)] struct Caps {
        public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort LinkNodes, InputButtons, InputValues, InputIndices, OutputButtons, OutputValues, OutputIndices, FeatureButtons, FeatureValues, FeatureIndices;
    }
    [StructLayout(LayoutKind.Sequential)] struct Overlapped { public UIntPtr Internal, InternalHigh; public uint Offset, OffsetHigh; public IntPtr Event; }
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_Interface_List_SizeW(out uint size, ref Guid guid, string id, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_Interface_ListW(ref Guid guid, string id, [Out] char[] buffer, uint size, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_Interface_PropertyW(string path, ref PropertyKey key, out uint type, byte[] buffer, ref uint size, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent, uint node, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint node, StringBuilder id, int length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] static extern int CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, byte[] buffer, ref uint size, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string path, uint access, uint sharing, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetAttributes(SafeFileHandle device, ref Attributes attributes);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)] static extern bool HidD_FlushQueue(SafeFileHandle device);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, out Caps caps);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr CreateEvent(IntPtr attributes, bool manual, bool initial, string name);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadFile(SafeFileHandle handle, IntPtr buffer, uint length, IntPtr bytesRead, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetOverlappedResult(SafeFileHandle handle, IntPtr overlapped, out uint transferred, bool wait);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CancelIoEx(SafeFileHandle handle, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr handle, uint timeout);

    static HashSet<string> Paths()
    {
        Guid guid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        for (int retry = 0; retry < 3; retry++) {
            uint size; int result = CM_Get_Device_Interface_List_SizeW(out size, ref guid, null, 0);
            if (result != 0 || size > 1048576) throw new IOException("HID enumeration size failed: " + result);
            char[] buffer = new char[size];
            result = CM_Get_Device_Interface_ListW(ref guid, null, buffer, size, 0);
            if (result == 26) continue;
            if (result != 0) throw new IOException("HID interface enumeration failed: " + result);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in new string(buffer).Split('\0'))
                if (path.IndexOf(TargetId, StringComparison.OrdinalIgnoreCase) >= 0) paths.Add(path);
            return paths;
        }
        throw new IOException("HID interface enumeration kept changing.");
    }
    static bool IsUsbIpDescendant(string path)
    {
        var key = new PropertyKey { Format = new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), Id = 256 };
        byte[] bytes = new byte[4096]; uint size = (uint)bytes.Length, type;
        if (CM_Get_Device_Interface_PropertyW(path, ref key, out type, bytes, ref size, 0) != 0 ||
            type != 0x12 || size < 2 || size > bytes.Length || size % 2 != 0) return false;
        uint node;
        if (CM_Locate_DevNodeW(out node, Encoding.Unicode.GetString(bytes, 0, (int)size).TrimEnd('\0'), 0) != 0) return false;
        bool usbIdentity = false;
        for (int depth = 0; depth < 16; depth++) {
            var id = new StringBuilder(1024);
            if (CM_Get_Device_IDW(node, id, id.Capacity, 0) != 0) return false;
            if (id.ToString().StartsWith("USB\\" + TargetId, StringComparison.OrdinalIgnoreCase)) usbIdentity = true;
            byte[] hardware = new byte[4096]; uint length = (uint)hardware.Length, hardwareType;
            if (CM_Get_DevNode_Registry_PropertyW(node, 2, out hardwareType, hardware, ref length, 0) == 0 &&
                hardwareType == 7 && length <= hardware.Length && length % 2 == 0)
                foreach (string value in Encoding.Unicode.GetString(hardware, 0, (int)length).Split('\0'))
                    if (String.Equals(value, @"ROOT\USBIP_WIN2\UDE", StringComparison.OrdinalIgnoreCase)) return usbIdentity;
            uint parent; if (CM_Get_Parent(out parent, node, 0) != 0) return false; node = parent;
        }
        return false;
    }
    static string AwaitNewPath(HashSet<string> before)
    {
        var timer = Stopwatch.StartNew();
        for (;;) {
            Budget(timer, 20000, false); string found = null;
            foreach (string path in Paths()) {
                if (before.Contains(path) || !IsUsbIpDescendant(path)) continue;
                if (found != null) throw new IOException("Multiple new USBIP DualSense HID collections; refusing ambiguous access.");
                found = path;
            }
            if (found != null) return found;
            Thread.Sleep(100);
        }
    }
    static void ValidateHid(SafeFileHandle handle)
    {
        var attributes = new Attributes { Size = (uint)Marshal.SizeOf(typeof(Attributes)) };
        if (!HidD_GetAttributes(handle, ref attributes) || attributes.Vendor != 0x054c || attributes.Product != ProductId)
            throw new IOException("New HID attributes do not match DualSense.");
        IntPtr data;
        if (!HidD_GetPreparsedData(handle, out data)) throw new Win32Exception(Marshal.GetLastWin32Error(), "HID preparsed data");
        try {
            Caps caps;
            if (HidP_GetCaps(data, out caps) != 0x00110000 || caps.InputLength != 64 || caps.UsagePage != 1 || caps.Usage != 5)
                throw new IOException("Expected a 64-byte gamepad HID input collection.");
        } finally { HidD_FreePreparsedData(data); }
        hidCaps = true; Pass("Windows HID attributes and 64-byte gamepad capability");
    }
    static byte[] ReadReport(SafeFileHandle handle, uint timeout)
    {
        IntPtr buffer = IntPtr.Zero, overlapped = IntPtr.Zero, ev = IntPtr.Zero; bool completed = true;
        try {
            buffer = Marshal.AllocHGlobal(64); overlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(Overlapped)));
            ev = CreateEvent(IntPtr.Zero, true, false, null);
            if (ev == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            Marshal.StructureToPtr(new Overlapped { Event = ev }, overlapped, false);
            bool immediate = ReadFile(handle, buffer, 64, IntPtr.Zero, overlapped);
            int error = immediate ? 0 : Marshal.GetLastWin32Error();
            if (!immediate && error != 997) throw new Win32Exception(error, "HID ReadFile submission");
            completed = false;
            if (!immediate && WaitForSingleObject(ev, timeout) != 0) {
                CancelIoEx(handle, overlapped);
                if (WaitForSingleObject(ev, 1000) != 0)
                    throw new TimeoutException("HID cancellation still pending; unmanaged request storage retained until process exit.");
                uint cancelled;
                bool done = GetOverlappedResult(handle, overlapped, out cancelled, false);
                int terminal = done ? 0 : Marshal.GetLastWin32Error(); completed = done || terminal != 996;
                throw new TimeoutException("HID read timed out; cancellation drained=" + completed);
            }
            uint length; bool ok = GetOverlappedResult(handle, overlapped, out length, false);
            int completionError = ok ? 0 : Marshal.GetLastWin32Error(); completed = ok || completionError != 996;
            if (!ok) throw new Win32Exception(completionError, "HID ReadFile completion");
            if (length != 64) throw new InvalidDataException("Expected exactly 64 HID input bytes, got " + length);
            byte[] result = new byte[64]; Marshal.Copy(buffer, result, 0, result.Length);
            if (result[0] != 1) throw new InvalidDataException("Unexpected HID input report ID.");
            return result;
        } finally {
            // CancelIoEx is not completion. Never free live OVERLAPPED storage.
            if (completed) {
                if (ev != IntPtr.Zero) CloseHandle(ev);
                if (overlapped != IntPtr.Zero) Marshal.FreeHGlobal(overlapped);
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            }
        }
    }
    sealed class Expected
    {
        internal readonly string Label; internal readonly int LX, LY; internal readonly uint Buttons; internal readonly byte Hat, L2, R2;
        internal Expected(string label, int lx, int ly, uint buttons, byte hat, byte l2, byte r2)
        { Label = label; LX = lx; LY = ly; Buttons = buttons; Hat = hat; L2 = l2; R2 = r2; }
    }
    static readonly Expected Neutral = new Expected("neutral", 0, 0, 0, 8, 0, 0);
    static readonly Expected[] Actions = {
        new Expected("left stick up",0,-128,0,8,0,0), new Expected("left stick down",0,127,0,8,0,0),
        new Expected("left stick left",-128,0,0,8,0,0), new Expected("left stick right",127,0,0,8,0,0),
        new Expected("D-pad up",0,0,0,0,0,0), new Expected("D-pad down",0,0,0,4,0,0),
        new Expected("D-pad left",0,0,0,6,0,0), new Expected("D-pad right",0,0,0,2,0,0),
        new Expected("Cross",0,0,0x20,8,0,0), new Expected("Circle",0,0,0x40,8,0,0),
        new Expected("Square",0,0,0x10,8,0,0), new Expected("Triangle",0,0,0x80,8,0,0),
        new Expected("L1",0,0,0x100,8,0,0), new Expected("R1",0,0,0x200,8,0,0),
        new Expected("L2 digital and analog",0,0,0x400,8,255,0), new Expected("R2 digital and analog",0,0,0x800,8,0,255),
        new Expected("Create",0,0,0x1000,8,0,0), new Expected("Options",0,0,0x2000,8,0,0),
        new Expected("L3",0,0,0x4000,8,0,0), new Expected("R3",0,0,0x8000,8,0,0),
        new Expected("PS",0,0,0x10000,8,0,0), new Expected("touchpad click",0,0,0x20000,8,0,0)
    };
    static bool Matches(byte[] report, Expected expected)
    {
        uint buttons = (uint)(report[8] & 0xf0) | ((uint)report[9] << 8) | ((uint)report[10] << 16);
        if (report[1] != expected.LX + 128 || report[2] != expected.LY + 128 || report[3] != 128 || report[4] != 128 ||
            report[5] != expected.L2 || report[6] != expected.R2 || (report[8] & 15) != expected.Hat || buttons != expected.Buttons ||
            report[27] != 0xe0 || (report[33] & 0x80) == 0 || (report[37] & 0x80) == 0) return false;
        for (int i = 16; i < 27; i++) if (report[i] != 0) return false;
        return true;
    }
    static void Expect(Feeder feeder, SafeFileHandle handle, string label, uint held, bool enabled, Expected expected)
    {
        byte[] frame = ViiperTransport.ConvertKeyboardFrame(KeyboardMapping.Frame(held, enabled), "dualsense");
        var timer = Stopwatch.StartNew(); int reads = 0;
        // Flush only the verified new handle, then tolerate in-flight old reports.
        // Continuous HID reports otherwise leave a full Windows ring of old state.
        if (!HidD_FlushQueue(handle)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Flush owned HID queue");
        feeder.Send(frame, false);
        while (reads++ < 512) {
            int remaining = Budget(timer, 1500, false);
            byte[] report = ReadReport(handle, (uint)Math.Min(remaining, 500));
            Budget(timer, 1500, false);
            if (Matches(report, expected)) { Pass(label); return; }
        }
        throw new InvalidDataException("Expected Windows input state not observed: " + label);
    }
    static void CheckInput(Feeder feeder, SafeFileHandle hid)
    {
        Expect(feeder, hid, "initial neutral including inactive touch", 0, true, Neutral);
        for (int i = 0; i < Actions.Length; i++) {
            Expect(feeder, hid, "action " + i + " " + Actions[i].Label, 1u << i, true, Actions[i]); pressed++;
            Expect(feeder, hid, "release action " + i, 0, true, Neutral); released++;
        }
        Expect(feeder, hid, "Cross held sample 1", 1u << 8, true, Actions[8]);
        Expect(feeder, hid, "Cross held sample 2", 1u << 8, true, Actions[8]);
        Expect(feeder, hid, "normalized diagonal", (1u << 0) | (1u << 3), true, new Expected("",90,-90,0,8,0,0));
        Expect(feeder, hid, "opposite stick cancellation", 15, true, Neutral);
        Expect(feeder, hid, "D-pad diagonal", (1u << 4) | (1u << 7), true, new Expected("",0,0,0,1,0,0));
        Expect(feeder, hid, "opposite D-pad cancellation", 15u << 4, true, Neutral);
        Expect(feeder, hid, "Cross and Triangle", (1u << 8) | (1u << 11), true, new Expected("",0,0,0xa0,8,0,0));
        Expect(feeder, hid, "PS and touchpad click", (1u << 20) | (1u << 21), true, new Expected("",0,0,0x30000,8,0,0));
        Expect(feeder, hid, "both triggers", (1u << 14) | (1u << 15), true, new Expected("",0,0,0xc00,8,255,255));
        Expect(feeder, hid, "disabled all actions neutral", (1u << 22) - 1, false, Neutral);
        Expect(feeder, hid, "final release", 0, true, Neutral);
        inputPassed = true;
    }
    static void PrintResult(bool success, bool watchdog)
    {
        var data = new Dictionary<string, object> {
            {"success",success}, {"persona",DeviceType}, {"target_pid",ProductId.ToString("X4", CultureInfo.InvariantCulture)},
            {"guard_passed",guard}, {"lab_marker_verified",marker}, {"usbip_cli_0981_verified",cliVersion},
            {"attach_command_succeeded",attached}, {"owned_port_identity_verified",portVerified},
            {"windows_attached",portVerified}, {"windows_hid_opened",hidOpened}, {"windows_hid_capability_verified",hidCaps},
            {"windows_input_readback_passed",inputPassed}, {"actions_pressed_verified",pressed}, {"actions_released_verified",released},
            {"neutral_cleanup_sent",neutralSent}, {"owned_port_detached",portDetached}, {"owned_bus_removed",busRemoved},
            {"owned_hid_disappeared",hidRemoved}, {"cleanup_complete",cleanupComplete}, {"watchdog_expired",watchdog},
            {"usbip_child_exit_unconfirmed",commandTerminationUnconfirmed},
            {"passed_checks",passed}, {"elapsed_ms",Clock.ElapsedMilliseconds}, {"source_commit_marker",SourceCommit},
            {"sony_tested",false}, {"keyboard_captured",false}, {"foreground_gate_tested",false},
            {"audio_streaming_tested",false}, {"feature_report_tested",false}, {"errors",Errors.ToArray()}
        };
        Console.WriteLine("RESULT_JSON " + new JavaScriptSerializer().Serialize(data));
    }
    static int Main(string[] args)
    {
        // Guard precedes process creation, networking, native HID calls and writes.
        try { ValidateGuard(args); guard = true; }
        catch (Exception error) { Fail("hosted-only guard", error); PrintResult(false, false); return 2; }
        using (var watchdog = new Timer(delegate {
            cleanupComplete = false;
            Console.Error.WriteLine("FAIL process deadline expired; cleanup was not confirmed.");
            PrintResult(false, true); Environment.Exit(3);
        }, null, 115000, Timeout.Infinite)) {
            Feeder feeder = null; int port = 0; string url = null, ownedPath = null;
            bool completed = false, cleanup = true, attachAttempted = false, attachReturned = false;
            try {
                if (!File.Exists(UsbIpExe)) throw new FileNotFoundException("Requires preinstalled USBip 0.9.8.1.", UsbIpExe);
                var version = Run("--version", false); RequireCommand(version, "USBip version");
                if (!Regex.IsMatch(version.Output + "\n" + version.Error, @"(?<![0-9.])0\.9\.8\.1(?![0-9.])"))
                    throw new InvalidDataException("The fixed usbip.exe did not report version 0.9.8.1.");
                cliVersion = true; Pass("fixed USBip CLI version 0.9.8.1 (installed driver provenance belongs to runner)");
                VerifyLab();
                HashSet<string> before = Paths();
                Console.WriteLine("Existing matching HID collections=" + before.Count + "; all excluded from access");
                feeder = new Feeder(); feeder.Open();
                url = "usbip://127.0.0.1:" + UsbPort + "/" + feeder.UsbBusId;
                attachAttempted = true;
                var attach = Run("--tcp-port " + UsbPort + " attach --remote 127.0.0.1 --bus-id " + feeder.UsbBusId + " --once --terse", false);
                attachReturned = true;
                RequireCommand(attach, "Attach this bus once"); attached = true;
                if (!Int32.TryParse(attach.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port) || port < 1 || port > 255)
                    throw new InvalidDataException("Attach did not return a single valid port number.");
                if (!PortMatches(port, url, false)) throw new IOException("Attached port URL is not the test-owned bus.");
                portVerified = true; Pass("exact loopback port URL identifies this allocated bus");
                ownedPath = AwaitNewPath(before);
                if (before.Contains(ownedPath) || !IsUsbIpDescendant(ownedPath) || !PortMatches(port, url, false))
                    throw new IOException("Ownership changed before HID open.");
                using (SafeFileHandle hid = CreateFile(ownedPath, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero)) {
                    if (hid.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Open new virtual DualSense HID");
                    hidOpened = true; ValidateHid(hid); CheckInput(feeder, hid);
                }
                completed = true;
            } catch (Exception error) { Fail("driver test", error); }
            finally {
                if (feeder != null && feeder.Connected) {
                    try { feeder.Send(ViiperTransport.CreateNeutralFrame("dualsense"), true); neutralSent = true; }
                    catch (Exception error) { cleanup = false; Fail("cleanup neutral", error); }
                }
                if (url != null) {
                    try {
                        // Even an attach failure may have completed at the driver.
                        // Discover the exact URL again; never trust an invalid/stale terse result.
                        int owned = FindOwnedPort(url, true);
                        if (owned != 0) {
                            if (!PortMatches(owned, url, true)) throw new IOException("Port identity changed; refusing detach.");
                            RequireCommand(Run("detach --port " + owned.ToString(CultureInfo.InvariantCulture), true), "Detach owned port");
                            if (FindOwnedPort(url, true) != 0) throw new IOException("Owned port remains imported.");
                        }
                        portDetached = true; Pass("test-owned port absent; no blanket detach");
                    } catch (Exception error) { cleanup = false; Fail("cleanup port", error); }
                }
                if (attachAttempted && !attachReturned) {
                    cleanup = false;
                    Fail("cleanup attach disposition", new IOException("Attach command did not complete; a late kernel attach cannot be excluded."));
                }
                if (feeder != null) {
                    try { feeder.CloseStream(); feeder.RemoveBus(); busRemoved = feeder.BusAllocated; Pass("removed only the allocated bus"); }
                    catch (Exception error) { cleanup = false; Fail("cleanup bus", error); }
                }
                if (ownedPath != null) {
                    try {
                        var timer = Stopwatch.StartNew();
                        while (Paths().Contains(ownedPath)) { Budget(timer, 5000, true); Thread.Sleep(100); }
                        hidRemoved = true; Pass("owned HID interface disappeared");
                    } catch (Exception error) { cleanup = false; Fail("cleanup HID disappearance", error); }
                }
                if (commandTerminationUnconfirmed) {
                    cleanup = false;
                    Fail("cleanup child process", new IOException("An owned usbip.exe child was not confirmed stopped."));
                }
                cleanupComplete = cleanup;
            }
            PrintResult(completed && cleanup, false);
            return completed && cleanup ? 0 : 1;
        }
    }
}
