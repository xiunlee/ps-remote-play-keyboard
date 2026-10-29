using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace PSKeyboard
{
    // Pure argument/ownership/diagnostic checks. No Form, process, keyboard hook,
    // driver, Sony, network, or personal profile is opened by this executable.
    internal static class PreviewGuardTests
    {
        private static int checks;
        private static void Check(bool valid, string message)
        { checks++; if (!valid) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        { try { action(); } catch (ArgumentException) { checks++; return; } throw new InvalidOperationException("Invalid argument was accepted."); }
        private static int Main()
        {
            bool connect; string path, persona;
            DualSensePreview.ParseOptions(new string[0], out connect, out path, out persona);
            Check(!connect && path == null && persona == "dualsensecombinedaudioduplexv5", "Opening the package does not connect, write a status file, or choose a legacy device.");
            string[] allowed = { "dualsensecombinedaudioduplexv5", "dualsensegamepadv5", "dualsenseedgecombinedaudioduplexv5", "dualsenseedgegamepadv5" };
            Check(DualSensePreview.Personas.Length == 4, "Exactly four reviewed choices.");
            foreach (string choice in allowed)
            {
                DualSensePreview.ParseOptions(new string[] { "--persona", choice }, out connect, out path, out persona);
                Check(persona == choice && !connect && path == null, "Selecting a persona alone cannot attach: " + choice);
            }
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guard-example-status.json");
            DualSensePreview.ParseOptions(new string[] { "--status-file", output }, out connect, out path, out persona);
            Check(!connect && path == output && !File.Exists(output), "Metadata argument parsing does not create files.");
            foreach (string[] invalid in new string[][] {
                new string[] { "--connect" }, new string[] { "--controller", "dualsense" },
                new string[] { "--persona" }, new string[] { "--persona", "dualsense" },
                new string[] { "--persona", "dualshock4" }, new string[] { "--persona", "dualsenseedge" },
                new string[] { "--persona", allowed[0].ToUpperInvariant() },
                new string[] { "--persona", allowed[0], "--persona", allowed[1] },
                new string[] { "--status-file" }, new string[] { "--unknown" },
                new string[] { "--status-file", "relative.json" },
                new string[] { "--status-file", @"\\server\share\status.json" },
                new string[] { "--status-file", @"C:\status.json:hidden" },
                new string[] { "--status-file", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.exe") },
                new string[] { "--status-file", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "outside.json") },
                new string[] { "--status-file", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PSKeyboard", "bindings.json") }
            }) Reject(delegate { bool c; string p, d; DualSensePreview.ParseOptions(invalid, out c, out p, out d); });

            const string own = "Port 07: device in use\n -> usbip://127.0.0.1:33641/1-2\n";
            Check(DualSensePreview.OwnPortUrl(own, "1-2"), "Exact owned candidate URL accepted.");
            Check(DualSensePreview.ResolveOwnedPort(own, "1-2") == 7, "Only the exact owned port is selected.");
            Check(DualSensePreview.ResolveOwnedPort("Port 01:\n -> usbip://127.0.0.1:33241/1-2\n" + own, "1-2") == 7, "Working alpha.8 port is excluded.");
            Check(DualSensePreview.ResolveOwnedPort("", "1-2") == 0, "Absent import requires no detach.");
            Check(DualSensePreview.ResolveOwnedPort(own, "1-20") == 0, "Bus prefix is never ownership.");
            foreach (string notOwned in new string[] {
                " -> usbip://127.0.0.1:33641/1-20", " -> usbip://127.0.0.1:33541/1-2",
                " -> usbip://127.0.0.1:33241/1-2", " -> usbip://example.net:33641/1-2",
                " -> usbip://127.0.0.1:33641/1-2 extra"
            }) Check(!DualSensePreview.OwnPortUrl(notOwned, "1-2"), "Unowned URL rejected.");
            Check(!DualSensePreview.OwnPortUrl(own, "1-2.*"), "Bus cannot inject a pattern.");
            bool ambiguous = false;
            try { DualSensePreview.ResolveOwnedPort(own + "Port 08:\n -> usbip://127.0.0.1:33641/1-2\n", "1-2"); }
            catch (InvalidDataException) { ambiguous = true; }
            Check(ambiguous, "Duplicate URL refuses destructive guessing.");
            bool noSection = false;
            try { DualSensePreview.ResolveOwnedPort(" -> usbip://127.0.0.1:33641/1-2\n", "1-2"); }
            catch (InvalidDataException) { noSection = true; }
            Check(noSection, "URL without a validated port section is rejected.");
            Check(DualSensePreview.HasCandidateImport(own), "Stale candidate import is detected before launching another backend.");
            Check(DualSensePreview.HasCandidateImport(" -> usbip://127.0.0.1:33641/99-1"), "Any previous candidate bus blocks reuse of the fixed server port.");
            Check(!DualSensePreview.HasCandidateImport(" -> usbip://127.0.0.1:33241/1-2"), "Working PS4 import is not treated as a candidate import.");
            for (int flags = 0; flags < 8; flags++)
                Check(DualSensePreview.CanReconnect((flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0) == (flags == 0),
                    "Any unknown cleanup, unresolved attach, or unconfirmed child exit blocks reconnect.");

            byte[] neutral = ViiperTransport.CreateNeutralFrame("dualsense");
            Check(DualSensePreview.IsNeutralFrame(neutral), "Legacy 33-byte neutral accounting matches the V5 adapter input boundary.");
            neutral[6] = 1;
            Check(!DualSensePreview.IsNeutralFrame(neutral), "Non-neutral action accounting does not record the raw bytes.");
            Dictionary<string, object> metadata = DualSensePreview.SanitizeServerLine("stderr", "WARN unhandled reportType=3 reportID=0xF0 wLength=64 payload=secret-payload account=secret-account");
            string json = new JavaScriptSerializer().Serialize(metadata);
            Check(!json.Contains("secret-payload") && !json.Contains("secret-account") && !json.Contains("payload"), "Diagnostics do not persist arbitrary input or raw tool text.");
            Check((string)metadata["reportID"] == "0xF0" && (string)metadata["wLength"] == "64", "Only bounded request metadata is retained.");
            Check(!DualSensePreview.SanitizeServerLine("stdout", "private-input").ContainsKey("text"), "No raw-text diagnostic field.");
            Console.WriteLine("DUALSENSE_PREVIEW_GUARDS_PASS checks=" + checks + "; no GUI, profile, process, driver, keyboard, Sony or network operations");
            return 0;
        }
    }
}
