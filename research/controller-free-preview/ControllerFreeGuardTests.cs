using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace PSKeyboard
{
    internal static class ControllerFreeGuardTests
    {
        private static int checks;
        private static void Check(bool valid, string message)
        { checks++; if (!valid) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        { try { action(); } catch (ArgumentException) { checks++; return; } throw new InvalidOperationException("Invalid argument was accepted."); }
        private static int Main()
        {
            bool connect; string path;
            ControllerFreeKeyboard.ParseOptions(new string[0], out connect, out path);
            Check(!connect && path == null, "Default does not connect or create logs.");
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "example-status.json");
            ControllerFreeKeyboard.ParseOptions(new string[] { "--connect", "--status-file", output }, out connect, out path);
            Check(connect && path == output && !File.Exists(output), "Explicit args parse without writing a file.");
            string[][] bad = { new string[] { "--connect", "--connect" }, new string[] { "--status-file" },
                new string[] { "--unknown" }, new string[] { "--status-file", "relative.json" },
                new string[] { "--status-file", @"\\server\share\status.json" },
                new string[] { "--status-file", @"C:\status.json:hidden" }, new string[] { "--status-file", @"C:\program.exe" } };
            foreach (string[] args in bad) Reject(delegate { bool c; string p; ControllerFreeKeyboard.ParseOptions(args, out c, out p); });
            Check(ControllerFreeKeyboard.OwnPortUrl("Port 01:\n -> usbip://127.0.0.1:33241/1-2\n", "1-2"), "Exact owned virtual URL accepted.");
            Check(!ControllerFreeKeyboard.OwnPortUrl(" -> usbip://127.0.0.1:33241/1-20", "1-2"), "Prefix bus mismatch rejected.");
            Check(!ControllerFreeKeyboard.OwnPortUrl(" -> usbip://127.0.0.1:33251/1-2", "1-2"), "Physical relay URL rejected.");
            Check(!ControllerFreeKeyboard.OwnPortUrl(" -> usbip://example.net:33241/1-2", "1-2"), "Remote server rejected.");
            Check(!ControllerFreeKeyboard.OwnPortUrl(" -> usbip://127.0.0.1:33241/1-2 extra", "1-2"), "Trailing data rejected.");
            Check(!ControllerFreeKeyboard.OwnPortUrl(" -> usbip://127.0.0.1:33241/1-2", "1-2.*"), "Untrusted bus pattern rejected.");
            string owned = "Port 07: device in use\n -> usbip://127.0.0.1:33241/1-2\n";
            Check(ControllerFreeKeyboard.ResolveOwnedPort(owned, "1-2") == 7, "Cleanup rediscovers exact own port after missing attach output.");
            Check(ControllerFreeKeyboard.ResolveOwnedPort("Port 01:\n -> usbip://127.0.0.1:33251/1-2\n" + owned, "1-2") == 7, "Cleanup ignores unrelated physical relay.");
            Check(ControllerFreeKeyboard.ResolveOwnedPort("", "1-2") == 0, "No import requires no detach.");
            Check(ControllerFreeKeyboard.ResolveOwnedPort(owned, "1-20") == 0, "Cleanup does not match a bus prefix.");
            bool ambiguous = false;
            try { ControllerFreeKeyboard.ResolveOwnedPort(owned + "Port 08:\n -> usbip://127.0.0.1:33241/1-2\n", "1-2"); }
            catch (InvalidDataException) { ambiguous = true; }
            Check(ambiguous, "Two matching ports prevent destructive guessing.");
            bool noSection = false;
            try { ControllerFreeKeyboard.ResolveOwnedPort(" -> usbip://127.0.0.1:33241/1-2\n", "1-2"); }
            catch (InvalidDataException) { noSection = true; }
            Check(noSection, "Matching URL without validated port section is rejected.");
            Check(ControllerFreeKeyboard.IsNeutralFrame(ViiperTransport.CreateNeutralFrame()), "Neutral frame classification agrees with transport.");
            byte[] nonNeutral = ViiperTransport.CreateNeutralFrame(); nonNeutral[0] = 1;
            Check(!ControllerFreeKeyboard.IsNeutralFrame(nonNeutral), "Output counter distinguishes changed frames.");
            Dictionary<string, object> sanitized = ControllerFreeKeyboard.SanitizeServerLine("stderr",
                "WARN unhandled reportType=3 reportID=0xF0 wLength=64 payload=private-payload account=private-account");
            string json = new JavaScriptSerializer().Serialize(sanitized);
            Check(!json.Contains("private-payload") && !json.Contains("private-account") && !json.Contains("payload"), "Raw line/payload never persisted.");
            Check((string)sanitized["reportID"] == "0xF0" && (string)sanitized["wLength"] == "64", "Only request metadata retained.");
            Check((string)sanitized["category"] == "unhandled_request", "Unhandled category retained without arbitrary text.");
            Check(!ControllerFreeKeyboard.SanitizeServerLine("stdout", "payload-only-secret").ContainsKey("text"), "No raw text field.");
            Console.WriteLine("CONTROLLER_FREE_GUARDS_PASS checks=" + checks + "; no GUI, process, device or network operations");
            return 0;
        }
    }
}
