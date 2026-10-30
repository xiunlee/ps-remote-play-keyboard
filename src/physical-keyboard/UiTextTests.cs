using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using PSKeyboard;

// Pure catalogue tests: no UI, hooks, devices, file writes or OS setting changes.
static class UiTextTests
{
    private static int checks;
    private static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(description);
    }

    private static void CultureRouting()
    {
        string[,] cases = {
            { "en-US", "en" }, { "en-GB", "en" },
            { "zh-TW", "zh-Hant" }, { "zh-HK", "zh-Hant" }, { "zh-MO", "zh-Hant" },
            { "zh-Hant", "zh-Hant" }, { "zh-CN", "zh-Hans" }, { "zh-SG", "zh-Hans" },
            { "zh-Hans", "zh-Hans" }, { "zh", "zh-Hans" },
            { "ja-JP", "ja" }, { "ko-KR", "ko" }, { "de-AT", "de" },
            { "fr-CA", "fr" }, { "es-MX", "es" }, { "pt-PT", "pt-BR" }, { "pt-BR", "pt-BR" },
            { "it-IT", "en" }, { "ar-SA", "en" }, { "", "en" }
        };
        for (int i = 0; i < cases.GetLength(0); ++i)
        {
            UiText.Initialize(CultureInfo.GetCultureInfo(cases[i, 0]));
            Check(UiText.LanguageTag == cases[i, 1], "Culture route " + cases[i, 0]);
        }
        UiText.Initialize(null);
        Check(UiText.LanguageTag == "en", "Null culture has an English fallback");
        Check(UiText.T("ControllerDisconnected") == "Not connected", "English fallback has an actual English translation");
    }

    private static void FullCatalogueCoverage()
    {
        string[] tags = UiText.SupportedLanguageTags;
        string[] keys = UiText.ResourceKeys;
        Check(tags.Length == 9, "Nine languages available");
        HashSet<string> distinct = new HashSet<string>(tags, StringComparer.Ordinal);
        Check(distinct.Count == 9, "No duplicate language tags");
        string[] required = {
            "AppTitle", "AppSubtitle", "ControllerTitle", "ControllerDisconnected", "ControllerWaiting",
            "ControllerVerifying", "ControllerReady", "ControllerStale", "ControllerFailure", "KeyboardTitle",
            "KeyboardPaused", "KeyboardActive", "KeyboardFocusPaused", "EnableKeyboard", "PauseKeyboard",
            "ConfigureKeys", "AdvancedShow", "AdvancedHide", "BusId", "Product", "Endpoint", "ProductConfirmed",
            "ProductExperimental", "StartRelay", "StopRelay", "StopConfirmTitle", "StopConfirmMessage", "ReadyHint",
            "ExperimentalHint", "ProfileLoadWarning", "KeyboardSetupFailure", "RelayStartupFailure", "AlreadyRunning",
            "InvalidArguments", "BindingsTitle", "BindingsSubtitle", "ActionColumn", "KeyColumn", "CaptureKey",
            "CapturePrompt", "ClearBinding", "RestoreDefaults", "Save", "Cancel", "BindingConflict", "BindingInvalid",
            "BindingsPausedHint", "ProfileSaveFailure", "LanguageLabel", "Unassigned",
            "RepeatTitle", "RepeatModeLabel", "RepeatOff", "RepeatHold", "RepeatToggle",
            "RepeatIntervalLabel", "RepeatHint", "RepeatColumn"
        };
        foreach (string tag in tags)
        {
            UiText.Initialize(CultureInfo.GetCultureInfo(tag));
            Check(UiText.LanguageTag == tag, "Each advertised language can be selected: " + tag);
                bool chinese = tag == "zh-Hant" || tag == "zh-Hans";
                Check(UiText.T("AppTitle") == (chinese ? "上班不要玩" : "Three Cats Keybridge"),
                    "Brand follows the selected UI language: " + tag);
            foreach (string key in keys)
            {
                Check(UiText.HasTranslation(tag, key), "Actual translation present: " + tag + "/" + key);
                Check(!String.IsNullOrWhiteSpace(UiText.T(key)), "Nonempty translation: " + tag + "/" + key);
                    if (!chinese) Check(!UiText.T(key).Contains("上班不要玩"), "Non-Chinese resources do not leak the Chinese brand: " + tag + "/" + key);
            }
            foreach (string key in required)
                Check(UiText.HasTranslation(tag, key), "Visible UI coverage: " + tag + "/" + key);
            for (int index = 0; index < 22; ++index)
                Check(!String.IsNullOrWhiteSpace(UiText.ActionName(index)), "All actions localized: " + tag + "/" + index);
            Check(UiText.KeyName(0) == UiText.T("Unassigned"), "Localized unassigned key: " + tag);
            Check(UiText.T("NeverDefinedKey") == "NeverDefinedKey", "Unknown key fallback: " + tag);
        }
        tags[0] = "broken";
        keys[0] = "broken";
        Check(UiText.SupportedLanguageTags[0] == "en", "Caller cannot mutate language storage");
        Check(UiText.ResourceKeys[0] != "broken", "Caller cannot mutate resource storage");
        Check(UiText.T(null) == String.Empty && UiText.T(String.Empty) == String.Empty, "Empty keys are safe");
    }

    private static void TranslationAndKeyNames()
    {
        UiText.Initialize(CultureInfo.GetCultureInfo("en-US"));
        Check(UiText.ActionName(0) == "Left stick ↑", "ABI action 0 is left stick up");
        Check(UiText.ActionName(4) == "D-pad ↑", "ABI action 4 is D-pad up");
        Check(UiText.ActionName(8) == "×", "ABI action 8 is Cross");
        Check(UiText.ActionName(9) == "○", "ABI action 9 is Circle");
        Check(UiText.ActionName(12) == "L1", "ABI action 12 is L1");
        Check(UiText.ActionName(21) == "Touchpad click", "ABI action 21 is touchpad click");
        Check(UiText.KeyName(0x41) == "A" && UiText.KeyName(0x5a) == "Z", "Letter key labels");
        Check(UiText.KeyName(0x30) == "0" && UiText.KeyName(0x39) == "9", "Number key labels");
        Check(UiText.KeyName(0x70) == "F1" && UiText.KeyName(0x87) == "F24", "Function key labels");
        Check(UiText.KeyName(0x60) == "Numpad 0" && UiText.KeyName(0x69) == "Numpad 9", "Numpad labels");
        Check(UiText.KeyName(0x25) == "←" && UiText.KeyName(0x28) == "↓", "Arrow labels");
        Check(UiText.KeyName(0x20) == "Space", "English space key");
        Check(UiText.KeyName(0xba) == "Key 186", "OEM key label does not assume a keyboard layout");
        bool lowRejected = false, highRejected = false;
        try { UiText.ActionName(-1); } catch (ArgumentOutOfRangeException) { lowRejected = true; }
        try { UiText.ActionName(22); } catch (ArgumentOutOfRangeException) { highRejected = true; }
        Check(lowRejected && highRejected, "Out-of-range action indexes rejected");
        UiText.Initialize(CultureInfo.GetCultureInfo("zh-TW"));
        Check(UiText.T("AppTitle") == "上班不要玩", "Exact brand preserved in Traditional Chinese");
        Check(UiText.KeyName(0x20) == "空白鍵", "Traditional Chinese special key name");
        UiText.Initialize(CultureInfo.GetCultureInfo("zh-CN"));
        Check(UiText.T("AppTitle") == "上班不要玩", "Exact Traditional Chinese brand preserved in Simplified Chinese UI");
        UiText.Initialize(CultureInfo.GetCultureInfo("de-DE"));
        Check(UiText.T("Save") == "Änderungen speichern", "German accented text preserved");
        UiText.Initialize(CultureInfo.GetCultureInfo("ja-JP"));
        Check(UiText.T("AppTitle") == "Three Cats Keybridge", "Japanese UI uses the shared international brand");
        UiText.Initialize(CultureInfo.GetCultureInfo("fr-FR"));
        Check(UiText.KeyName(0x0d) == "Entrée", "French key name preserved");
    }

    private static int Main()
    {
        CultureInfo initialCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo initialUiCulture = Thread.CurrentThread.CurrentUICulture;
        Check(UiText.LanguageTag == UiText.ResolveLanguage(initialUiCulture), "Default follows current OS UI culture");
        try
        {
            CultureRouting();
            FullCatalogueCoverage();
            TranslationAndKeyNames();
            Check(Object.ReferenceEquals(initialCulture, Thread.CurrentThread.CurrentCulture), "Formatting culture not changed");
            Check(Object.ReferenceEquals(initialUiCulture, Thread.CurrentThread.CurrentUICulture), "OS/thread UI culture not changed");
            Console.WriteLine("UI_TEXT_PASS checks=" + checks + "; languages=9; resources=" + UiText.ResourceKeys.Length + "; actions=22; no UI or device access");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("UI_TEXT_FAIL type=" + error.GetType().FullName);
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally { UiText.Initialize(initialUiCulture); }
    }
}
