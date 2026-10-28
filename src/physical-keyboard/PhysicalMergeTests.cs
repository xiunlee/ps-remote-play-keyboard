using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using PSKeyboard;

// Native ABI + real key-lifecycle adapter checks only. No hooks, devices,
// services, Sony processes or network connections are opened by these tests.
internal static class PhysicalMergeTests
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks++;
    }
    private static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    private static byte[] Sample(int seed)
    {
        byte[] b = new byte[64];
        for (int i = 0; i < b.Length; i++) b[i] = (byte)((i * 37 + seed) & 255);
        b[0] = 1; b[1] = 102; b[2] = 153; b[5] = 0x88;
        b[6] = 0; b[7] &= 0xFC; b[8] = 17; b[9] = 29;
        return b;
    }
    private static int Main()
    {
        PhysicalKeyboardOptions empty = PhysicalKeyboardOptions.Parse(new string[0]);
        Check(empty.BusId == "" && !empty.StartRelay, "no arguments leave relay stopped");
        PhysicalKeyboardOptions prefill = PhysicalKeyboardOptions.Parse(new string[] { "1-12" });
        Check(prefill.BusId == "1-12" && !prefill.StartRelay, "single argument only prefills bus");
        PhysicalKeyboardOptions automatic = PhysicalKeyboardOptions.Parse(new string[] { "--start-relay", "2-3.1" });
        Check(automatic.BusId == "2-3.1" && automatic.StartRelay, "explicit flag requests relay startup");
        PhysicalKeyboardOptions statusOptions = PhysicalKeyboardOptions.Parse(new string[] {
            "--start-relay", "1-1", "--status-file", @"C:\Temp\status.json" });
        Check(statusOptions.StartRelay && statusOptions.StatusFile == @"C:\Temp\status.json", "explicit local status path");
        PhysicalKeyboardOptions statusFirst = PhysicalKeyboardOptions.Parse(new string[] {
            "--status-file", @"C:\Temp\status.json", "--start-relay", "1-1" });
        Check(statusFirst.StartRelay && statusFirst.BusId == "1-1", "status option can precede startup");
        Check(empty.StatusFile == null && prefill.StatusFile == null, "logging disabled by default");
        string[][] invalidArguments = new string[][] {
            new string[] { "--start-relay" }, new string[] { "--start-relay", "" },
            new string[] { "--unknown", "1-1" }, new string[] { "--start-relay", "1-1", "extra" },
            new string[] { "--start-relay", " 1-1" }, new string[] { "--start-relay", "1-1 " },
            new string[] { "--start-relay", "1-1;stop" }, new string[] { "--start-relay", "--all" },
            new string[] { "--start-relay", new string('1', 32) },
            new string[] { "--start-relay", "一-1" }, new string[] { "--start-relay", "." },
            new string[] { "--status-file" }, new string[] { "--status-file", "status.json" },
            new string[] { "--status-file", @"C:status.json" }, new string[] { "--status-file", @"\\server\share\status.json" },
            new string[] { "--status-file", @"C:\Temp\status.json:hidden" },
            new string[] { "--status-file", @"C:\Temp\app.exe" },
            new string[] { "--status-file", @"C:\Temp\status.json", "--status-file", @"C:\Temp\other.json" }
        };
        foreach (string[] arguments in invalidArguments)
        {
            bool invalid = false;
            try { PhysicalKeyboardOptions.Parse(arguments); } catch (ArgumentException) { invalid = true; }
            Check(invalid, "invalid startup arguments rejected");
        }
        RelayStatusSnapshot telemetry = new RelayStatusSnapshot { Pid = 123, Enabled = false, Attached = true,
            EndpointVerified = true, InputReports = 4000, MergedReports = 12, Failure = "unexpected secret raw payload" };
        Dictionary<string, object> parsed = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(telemetry.ToJson());
        Check(parsed.Count == 7, "status contains only seven approved fields");
        Check((int)parsed["pid"] == 123 && !(bool)parsed["enabled"] && (bool)parsed["attached"], "status primitive fields parse");
        Check((int)parsed["inputReports"] == 4000 && (int)parsed["mergedReports"] == 12, "status counters parse");
        Check((string)parsed["failure"] == "relay_error" && !telemetry.ToJson().Contains("secret"), "unknown failure text never logged");
        telemetry.Failure = null;
        Check(new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(telemetry.ToJson())["failure"] == null, "healthy status has null failure");
        string statusTestDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "status-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(statusTestDirectory);
        string statusTestPath = Path.Combine(statusTestDirectory, "status.json");
        try
        {
            OptionalStatusFile.WriteAtomic(statusTestPath, telemetry.ToJson());
            Check(File.ReadAllText(statusTestPath) == telemetry.ToJson(), "first status file atomically created");
            telemetry.InputReports = 5000;
            OptionalStatusFile.WriteAtomic(statusTestPath, telemetry.ToJson());
            Check(File.ReadAllText(statusTestPath) == telemetry.ToJson(), "existing status file atomically replaced");
            Check(Directory.GetFiles(statusTestDirectory).Length == 1, "no temporary file remains");
        }
        finally { File.Delete(statusTestPath); Directory.Delete(statusTestDirectory); }
        KeyboardInputState input = new KeyboardInputState();
        byte[] first = Sample(19), snapshot = (byte[])first.Clone();
        Check(Equal(first, PhysicalReportMerge.Merge(first, 0, false)), "disabled physical passthrough");
        Check(input.Process(0x4B, true, false, false, true), "capture physical K");
        byte[] pressed = PhysicalReportMerge.Merge(first, input.Held, true);
        Check((pressed[5] & 0xA0) == 0xA0, "keyboard Cross OR physical Triangle");
        Check(Equal(first, snapshot), "source never mutated");
        Check(pressed[3] == first[3] && pressed[4] == first[4], "physical right stick preserved");
        for (int i = 10; i < 64; i++) Check(pressed[i] == first[i], "opaque physical byte " + i);
        Check(input.Process(0x4B, true, false, false, true), "repeat captured");
        byte[] second = Sample(113);
        byte[] held = PhysicalReportMerge.Merge(second, input.Held, true);
        Check((held[5] & 0x20) != 0 && held[10] == second[10], "hold uses fresh physical data");
        Check(input.Process(0x4B, false, true, false, true), "release captured");
        Check(Equal(second, PhysicalReportMerge.Merge(second, input.Held, true)), "release returns latest physical sample");
        input.Process(0x57, true, false, false, true);
        byte[] movement = PhysicalReportMerge.Merge(second, input.Held, true);
        Check(movement[2] == 0, "W full forward");
        Check(movement[3] == second[3] && movement[4] == second[4], "W keeps physical right stick");
        input.Reset();
        Check(Equal(second, PhysicalReportMerge.Merge(second, input.Held, true)), "focus loss reset physical passthrough");
        input.Process(0x57, true, false, false, true);
        Check(input.Held == 0, "held key cannot rearm after focus loss");
        input.Process(0x57, false, true, false, true);
        input.Process(0x57, true, false, false, true);
        Check(input.Held != 0, "fresh press rearms");
        Check(Equal(second, PhysicalReportMerge.Merge(second, input.Held, false)), "pause ignores captured keys");
        input.Process(0x57, false, true, true, true);
        Check(input.Held != 0, "injected release cannot alter physical held state");
        input.Process(0x57, false, true, false, true);
        input.Process(0x55, true, false, false, true);
        byte[] trigger = PhysicalReportMerge.Merge(second, input.Held, true);
        Check(trigger[8] == 255 && (trigger[6] & 4) != 0, "L2 analog and button merged together");
        Check(trigger[9] == second[9], "other physical trigger preserved");
        input.Process(0x55, false, true, false, true);
        Check(Equal(second, PhysicalReportMerge.Merge(second, input.Held, true)), "L2 release restores physical analog");
        bool rejected = false;
        try { PhysicalReportMerge.Merge(new byte[63], 0, true); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "short report rejected");
        foreach (byte reportId in new byte[] { 0xF0, 0xF1, 0xF2, 0x11 })
        {
            byte[] feature = Sample(10); feature[0] = reportId; rejected = false;
            try { PhysicalReportMerge.Merge(feature, 0, true); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "non-input report rejected " + reportId);
        }
        Console.WriteLine("PHYSICAL_MERGE_PASS checks=" + checks + " native_x64=true devices_opened=0");
        return 0;
    }
}
