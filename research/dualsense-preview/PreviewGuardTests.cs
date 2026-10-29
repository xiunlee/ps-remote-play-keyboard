using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace PSKeyboard
{
    // Offline argument/ownership/diagnostic and synthetic-profile checks. No Form,
    // process, keyboard hook, driver, Sony, network, or personal profile is opened.
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
            Check(!connect && path == null && persona == "dualsensegamepadv5", "Opening defaults to the selected pure gamepad without connecting or writing metadata.");
            Check(DualSensePreview.Persona == "dualsensegamepadv5", "The sole mode is the user's alpha.9 second choice.");
            DualSensePreview.ParseOptions(new string[] { "--persona", "dualsensegamepadv5" }, out connect, out path, out persona);
            Check(persona == "dualsensegamepadv5" && !connect && path == null, "Explicit fixed mode cannot auto-connect.");
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guard-example-status.json");
            DualSensePreview.ParseOptions(new string[] { "--status-file", output }, out connect, out path, out persona);
            Check(!connect && path == output && !File.Exists(output), "Metadata argument parsing does not create files.");
            foreach (string[] invalid in new string[][] {
                new string[] { "--connect" }, new string[] { "--controller", "dualsense" },
                new string[] { "--persona" }, new string[] { "--persona", "dualsense" },
                new string[] { "--persona", "dualshock4" }, new string[] { "--persona", "dualsenseedge" },
                new string[] { "--persona", "dualsensecombinedaudioduplexv5" },
                new string[] { "--persona", "dualsenseedgecombinedaudioduplexv5" },
                new string[] { "--persona", "dualsenseedgegamepadv5" },
                new string[] { "--persona", "DUALSENSEGAMEPADV5" },
                new string[] { "--persona", "dualsensegamepadv5", "--persona", "dualsensegamepadv5" },
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
            SupportDestinations();
            ProfileEditing();
            Console.WriteLine("DUALSENSE_PREVIEW_GUARDS_PASS checks=" + checks + "; synthetic profile only; no GUI, personal profile, process, driver, keyboard, Sony or network operations");
            return 0;
        }

        private static void SupportDestinations()
        {
            string version = BuildInfo.Version;
            string originalLanguage = UiText.LanguageTag;
            UiText.Initialize(CultureInfo.GetCultureInfo("zh-Hant"));
            string feedback = SupportLinks.FeedbackUrl(version);
            Check(feedback.StartsWith("mailto:xiun1019@gmail.com?subject=", StringComparison.Ordinal) &&
                Uri.UnescapeDataString(feedback.Substring(feedback.IndexOf("?subject=", StringComparison.Ordinal) + 9)) == "上班不要玩 " + version + " 意見回饋" &&
                feedback.IndexOf('&') < 0, "Feedback has only the fixed recipient and encoded version subject, without body, attachments or extra recipients.");
            Check(SupportLinks.IsAllowedLaunchUrl(feedback, version) &&
                !SupportLinks.IsAllowedLaunchUrl(feedback + "&cc=another@example.com", version), "Only the exact feedback destination can launch.");
            Reject(delegate { SupportLinks.FeedbackUrl(version + "\r\nBcc:another@example.com"); });
            foreach(string compatibleVersion in new string[] { "0.1.0", "0.1.1", "0.1.9", "0.2.0", "65534.65534.9", "0.1.0-alpha.11", "0.1.0-alpha.12" })
                Check(Uri.UnescapeDataString(SupportLinks.FeedbackUrl(compatibleVersion)).Contains(compatibleVersion),
                    "Feedback supports the release version and preserved public alpha versions.");
            foreach(string invalidVersion in new string[] { "v0.1.0", "0.1.0.0", "0.1.10", "65535.0.0", "0.65535.0", "00.1.0", "0.1.0-alpha.10",
                "0.1.0-alpha.65535", "0.1.0-beta.1", "0.1.0?cc=another@example.com", "0.1.0\n" })
                Reject(delegate { SupportLinks.FeedbackUrl(invalidVersion); });
            Check(SupportLinks.IsAllowedSponsorUrl("https://www.paypal.com/ncp/payment/AAAAAAAAAAAAA"), "The precise PayPal HTTPS host, payment path and token format are supported.");
            foreach (string invalid in new string[] { null, "", "http://www.paypal.com/ncp/payment/HAW9YBJXNEMFW", "https://www.paypal.com.evil.example/ncp/payment/HAW9YBJXNEMFW",
                "https://user@www.paypal.com/ncp/payment/HAW9YBJXNEMFW", "https://www.paypal.com:8443/ncp/payment/HAW9YBJXNEMFW", "https://www.paypal.com:443/ncp/payment/HAW9YBJXNEMFW",
                "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW?amount=1", "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW#fragment", "https://www.paypal.com\\ncp/payment/HAW9YBJXNEMFW",
                "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW\r\n", " https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW", "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW ",
                "https://www.paypal.com/ncp/payment/haw9ybjxnemfw", "https://www.paypal.com/ncp/payment/ABC123", "https://www.paypal.com/ncp/payment/AAAAAAAAAAAAAA",
                "https://www.paypal.com/ncp/payment/%48AW9YBJXNEMFW", "https://www.paypal.com/other/../ncp/payment/HAW9YBJXNEMFW", "https://paypal.com/ncp/payment/HAW9YBJXNEMFW",
                "file:///C:/example.exe", "https://evil.example/ncp/payment/HAW9YBJXNEMFW" })
                Check(!SupportLinks.IsAllowedSponsorUrl(invalid), "Malformed, altered or untrusted sponsor target is rejected.");
            Check(SupportLinks.SponsorUrl == "https://www.paypal.com/ncp/payment/HAW9YBJXNEMFW" && SupportLinks.SponsorAvailable &&
                SupportLinks.IsAllowedLaunchUrl(SupportLinks.SponsorUrl, version), "Only the user's latest designated PayPal destination can launch.");
            Check(!SupportLinks.IsAllowedLaunchUrl("https://www.paypal.com/ncp/payment/AAAAAAAAAAAAA", version),
                "A valid PayPal payment token format does not authorize another payment page.");
            foreach(string language in UiText.SupportedLanguageTags)
            {
                UiText.Initialize(CultureInfo.GetCultureInfo(language));
                string localized = SupportLinks.FeedbackUrl(version);
                const string prefix = "mailto:xiun1019@gmail.com?subject=";
                Check(localized.StartsWith(prefix, StringComparison.Ordinal) && localized.IndexOf('&') < 0 && localized.IndexOf('#') < 0,
                    "Localized feedback remains a subject-only draft to the fixed recipient: " + language);
                string subject = Uri.UnescapeDataString(localized.Substring(prefix.Length));
                Check(subject.Contains(version) && subject.Contains("上班不要玩") && subject.IndexOf('\r') < 0 && subject.IndexOf('\n') < 0,
                    "Localized subject preserves brand/version without header injection: " + language);
                Check(SupportLinks.IsAllowedLaunchUrl(localized, version) && !SupportLinks.IsAllowedLaunchUrl(localized + "&body=unapproved", version),
                    "Only the exact localized draft can launch: " + language);
            }
            UiText.Initialize(CultureInfo.GetCultureInfo(originalLanguage));
        }

        private static void ProfileEditing()
        {
            string fixtureRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "guard-profile-" + Guid.NewGuid().ToString("N"));
            string fixture = Path.Combine(fixtureRoot, "synthetic-bindings.json");
            // This directory is new, package-local, and contains synthetic data only.
            if (Directory.Exists(fixtureRoot)) throw new IOException("Fixture must be new.");
            Directory.CreateDirectory(fixtureRoot);
            try
            {
                BindingProfile original = BindingProfile.Load(fixture);
                Check(BindingProfile.ActionCount == 22 && !File.Exists(fixture), "Opening absent settings loads all 22 defaults without writing a file.");
                original.Save(fixture);
                string previous = File.ReadAllText(fixture);
                BindingProfile current = BindingProfile.Load(fixture);
                Check(File.ReadAllText(fixture) == previous, "Opening existing settings does not rewrite them.");
                BindingProfile draft = current.WithBinding(8, 0x5A).WithRapidFire(8, RapidFireMode.Hold, 20)
                    .WithRapidFire(9, RapidFireMode.Toggle, 5000).WithRapidFire(10, RapidFireMode.Off, 250);
                Check(current.KeyForAction(8) == original.KeyForAction(8) && File.ReadAllText(fixture) == previous,
                    "An edited or discarded draft cannot alter active settings or persisted data.");
                Reject(delegate { draft.WithRapidFire(8, RapidFireMode.Hold, 19); });
                Reject(delegate { draft.WithRapidFire(9, RapidFireMode.Toggle, 5001); });
                Reject(delegate { draft.WithBinding(8, 0x77); });

                KeyboardControlPipeline pipeline = new KeyboardControlPipeline(delegate { return current; });
                int originalKey = current.KeyForAction(8);
                pipeline.Process(originalKey, true, false, false, true, 0);
                Check(pipeline.Evaluate(true, 1) == (1u << 8), "Baseline synthetic input is active before opening settings.");
                pipeline.Reset();
                Check(pipeline.Evaluate(false, 2) == 0, "Opening settings clears existing output while editing is paused.");
                BindingProfile beforeFailure = current; bool failed = false;
                using (FileStream locked = new FileStream(fixture, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    try { current = DualSensePreview.SaveEditedProfile(draft, fixture); }
                    catch (IOException) { failed = true; }
                }
                Check(failed && Object.ReferenceEquals(current, beforeFailure) && current.KeyForAction(8) == originalKey,
                    "Failed persistence never publishes the edited mapping.");
                Check(File.ReadAllText(fixture) == previous && Directory.GetFiles(fixtureRoot).Length == 1,
                    "Failed save preserves the previous file and cleans its own temporary file.");
                Check(pipeline.Evaluate(true, 3) == 0, "A save error cannot resume a key held before editing.");

                current = DualSensePreview.SaveEditedProfile(draft, fixture); pipeline.Reset();
                Check(Object.ReferenceEquals(current, draft), "Only a successful save publishes the immutable edited profile.");
                BindingProfile restored = BindingProfile.Load(fixture);
                for (int action = 0; action < BindingProfile.ActionCount; action++)
                    Check(restored.KeyForAction(action) == draft.KeyForAction(action) &&
                        restored.RapidFireModeForAction(action) == draft.RapidFireModeForAction(action) &&
                        restored.IntervalMillisecondsForAction(action) == draft.IntervalMillisecondsForAction(action),
                        "Saved mapping and independent repeat settings survive reload for action " + action);
                Check(restored.RapidFireModeForAction(8) == RapidFireMode.Hold && restored.IntervalMillisecondsForAction(8) == 20 &&
                    restored.RapidFireModeForAction(9) == RapidFireMode.Toggle && restored.IntervalMillisecondsForAction(9) == 5000 &&
                    restored.RapidFireModeForAction(10) == RapidFireMode.Off, "All three repeat modes and both supported timing limits persist.");
                Check(pipeline.Evaluate(false, 4) == 0, "A successful save leaves output paused until explicitly enabled.");
                pipeline.Process(originalKey, false, true, false, false, 5);
                pipeline.Process(0x5A, true, false, false, true, 6);
                Check(pipeline.Evaluate(true, 6) == (1u << 8), "A fresh accepted press uses the newly saved mapping.");
                Check(pipeline.Evaluate(false, 7) == 0 && pipeline.Evaluate(true, 8) == 0,
                    "Focus loss cancels repeated output and returning cannot resume it.");
            }
            finally
            {
                // No recursive deletion: remove only this test's exact file and empty directory.
                if (File.Exists(fixture)) File.Delete(fixture);
                Directory.Delete(fixtureRoot);
            }
        }
    }
}
