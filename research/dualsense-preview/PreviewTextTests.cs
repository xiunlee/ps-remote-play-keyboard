using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace PSKeyboard
{
    // Pure resource checks: no windows, user settings, keyboard, devices or network.
    internal static class PreviewTextTests
    {
        private static int checks;
        private static void Check(bool condition, string description)
        { checks++; if(!condition) throw new InvalidOperationException(description); }

        private static string Slots(string value)
        {
            List<string> indexes = new List<string>();
            foreach(Match match in Regex.Matches(value, @"(?<!\{)\{([0-9]+)(?:[^{}]*)\}(?!\})")) indexes.Add(match.Groups[1].Value);
            indexes.Sort(StringComparer.Ordinal);
            return String.Join(",", indexes.ToArray());
        }

        private static int Main()
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            CultureInfo uiCulture = Thread.CurrentThread.CurrentUICulture;
            string previous = UiText.LanguageTag;
            try
            {
                string[] keys = PreviewText.ResourceKeys;
                string[] languages = PreviewText.SupportedLanguageTags;
                Check(keys.Length >= 50, "Main screen, game suitability, connection failures and feedback subject resources are included.");
                Check(String.Join(",", languages) == String.Join(",", UiText.SupportedLanguageTags), "Main screen and settings use the same nine languages.");
                Check(new HashSet<string>(keys, StringComparer.Ordinal).Count == keys.Length, "Resource keys are unique.");
                Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.Ordinal);
                UiText.Initialize(CultureInfo.GetCultureInfo("en-US"));
                foreach(string key in keys) english.Add(key, PreviewText.T(key));
                foreach(string language in languages)
                {
                    UiText.Initialize(CultureInfo.GetCultureInfo(language));
                    bool chinese = language == "zh-Hant" || language == "zh-Hans";
                    string brand = chinese ? "上班不要玩" : "PS Remote Play Keyboard";
                    Check(UiText.T("AppTitle") == brand && DualSensePreview.WindowTitle == brand + " V " + BuildInfo.Version,
                        "Main brand and window title match the language and version: " + language);
                    foreach(string key in keys)
                    {
                        Check(PreviewText.HasTranslation(language, key), "Actual translation: " + language + "/" + key);
                        string value = PreviewText.T(key);
                        Check(!String.IsNullOrWhiteSpace(value) && value != key, "Nonempty displayed translation: " + language + "/" + key);
                        Check(Slots(value) == Slots(english[key]), "Formatting arguments preserved: " + language + "/" + key);
                        Check(value.IndexOf('\ufffd') < 0 && value.IndexOf('\0') < 0 && value.Length < 2000, "Valid resource text: " + language + "/" + key);
                        if(!chinese) Check(!value.Contains("上班不要玩"), "Non-Chinese UI messages do not contain the Chinese brand: " + language + "/" + key);
                        if(language == "en") Check(!Regex.IsMatch(value, @"\p{IsCJKUnifiedIdeographs}"), "English UI resources contain no Chinese text: " + key);
                        // Rendering is exercised separately; this catches malformed composite formats.
                        Check(!String.IsNullOrEmpty(String.Format(CultureInfo.InvariantCulture, value, "sample-version", "sample-address", "sample-detail")),
                            "Resource format is valid: " + language + "/" + key);
                    }
                    string feedback = PreviewText.F("FeedbackSubjectFmt", BuildInfo.Version);
                    Check(feedback.StartsWith(brand + " V " + BuildInfo.Version, StringComparison.Ordinal), "Feedback preserves the localized brand and release identity: " + language);
                    Check(PreviewText.T("AlreadyRunning").Contains(brand), "Startup warning identifies the localized product name: " + language);
                    Check(PreviewText.T("SponsorButton").Contains("US$1"), "Donation currency and amount are unchanged: " + language);
                    Check(PreviewText.HasTranslation(language, "GameSuitability") && PreviewText.T("GameSuitability").Contains("2D") &&
                        PreviewText.T("GameSuitability").Contains("JRPG") && PreviewText.T("GameSuitability").Contains("3D"),
                        "Every language includes the 2D/JRPG suitability and 3D camera limitation: " + language);
                    foreach(string failure in new string[] { "keyboard_setup_failed", "usbip_missing", "usbip_invalid", "existing_import",
                        "backend_invalid", "ports_unavailable", "usb_attach_failed", "send_failed", "cancelled", "backend_start_failed",
                        "backend_exited", "usbip_timeout", "usbip_output_limit", "unrecognized-private-detail" })
                    {
                        string message = DualSensePreview.FailureMessage(failure);
                        Check(!String.IsNullOrWhiteSpace(message) && !message.Contains("unrecognized-private-detail"),
                            "Connection failures display a localized message without raw diagnostic details: " + language + "/" + failure);
                        if(language == "en") Check(!Regex.IsMatch(message, @"\p{IsCJKUnifiedIdeographs}"), "English errors contain English text: " + failure);
                    }
                }
                UiText.Initialize(CultureInfo.GetCultureInfo("it-IT"));
                foreach(string key in keys) Check(PreviewText.T(key) == english[key], "Unsupported languages fall back to English: " + key);
                keys[0] = "changed"; languages[0] = "changed";
                Check(PreviewText.ResourceKeys[0] != "changed" && PreviewText.SupportedLanguageTags[0] == "en", "Resource metadata cannot mutate the catalogue.");
                Check(Object.ReferenceEquals(culture, Thread.CurrentThread.CurrentCulture) && Object.ReferenceEquals(uiCulture, Thread.CurrentThread.CurrentUICulture),
                    "Selecting application resources does not change OS/thread cultures.");
                Console.WriteLine("PREVIEW_TEXT_PASS checks=" + checks + "; languages=9; resources=" + PreviewText.ResourceKeys.Length + "; no UI or device access");
                return 0;
            }
            catch(Exception error) { Console.Error.WriteLine("PREVIEW_TEXT_FAIL " + error.GetType().Name + ": " + error.Message); return 1; }
            finally { UiText.Initialize(CultureInfo.GetCultureInfo(previous)); }
        }
    }
}
