using System;
using PSKeyboard;

internal static class KeyboardControlPipelineTests
{
    private const int K = 0x4b, J = 0x4a;
    private const uint Cross = 1u << 8, Square = 1u << 10;
    private static int checks;
    private static void Check(bool passed, string message)
    { checks++; if (!passed) throw new InvalidOperationException(message); }

    private static int Main()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Toggle, 100);
        KeyboardControlPipeline input = new KeyboardControlPipeline(delegate { return profile; });
        Check(input.Process(K, true, false, false, true, 0), "Game owns accepted key down");
        Check(input.Process(K, false, true, false, true, 1), "Fast tap release remains paired");
        Check(input.Evaluate(true, 2) == Cross, "Fast tap before next report still enables Toggle");
        Check(input.Evaluate(true, 50) == 0, "Toggle release phase is sampled");
        Check(input.Evaluate(true, 100) == Cross, "Toggle continues without any key held");
        Check(!input.Process(0x11, true, false, true, false, 100), "Injected modifier never owns the physical event");
        Check(input.Evaluate(true, 100) == Cross, "Injected modifier cannot reset a live Toggle");
        Check(!input.Process(K, false, false, false, false, 100), "Invalid event ignored before a reset");
        Check(input.Evaluate(true, 100) == Cross, "Invalid event cannot clear a live Toggle");
        Check(input.Evaluate(false, 101) == 0, "Focus loss stops repeats even without a new key event");
        Check(input.Evaluate(true, 200) == 0, "Returning to Remote Play never resumes Toggle");
        Check(!input.Process(K, true, false, false, false, 210), "Other windows receive their own key down");
        Check(!input.Process(K, true, false, false, true, 220), "Holding an outside key on focus entry stays outside");
        Check(input.Evaluate(true, 220) == 0, "Outside typing cannot start a repeat");
        Check(!input.Process(K, false, true, false, true, 221), "Outside release stays paired");
        Check(input.Process(K, true, false, false, true, 230), "Fresh game press re-arms Toggle");
        Check(input.Evaluate(true, 230) == Cross, "New game press starts repeat");
        input.Reset();
        Check(input.Process(K, true, false, false, true, 231), "OS repeat remains consumed after pause");
        Check(input.Evaluate(true, 232) == 0, "Held key cannot re-arm through pause");
        Check(input.Process(K, false, true, false, false, 233), "Captured release is paired even after focus loss");
        Check(input.Process(K, true, false, false, true, 240), "New key starts after outside release");
        Check(input.Evaluate(true, 240) == Cross, "Outside release clears disarming");
        Check(input.Process(K, true, false, false, true, 241), "OS repeat is consumed");
        Check(input.Evaluate(true, 242) == Cross, "OS repeat cannot toggle off");
        input.Process(K, false, true, false, true, 243);
        input.Process(K, true, false, false, true, 244);
        Check(input.Evaluate(true, 244) == 0, "Second separate press stops Toggle");
        input.Process(K, false, true, false, true, 245);
        Check(!input.Process(K, true, false, true, true, 246), "Injected key ignored");
        Check(input.Evaluate(true, 247) == 0, "Injected key never starts Toggle");

        profile = profile.WithRapidFire(8, RapidFireMode.Hold, 100);
        input.Reset();
        input.Process(K, true, false, false, true, 300);
        Check(input.Evaluate(true, 300) == Cross, "Hold starts on press");
        Check(input.Evaluate(true, 350) == 0, "Hold releases each cycle");
        Check(input.Evaluate(true, 400) == Cross, "Hold presses next cycle");
        input.Process(J, true, false, false, true, 401);
        Check(input.Evaluate(true, 450) == Square, "Normal action remains held during repeat release");
        byte[] physical = new byte[64]; physical[0] = 1; physical[1] = physical[2] = 128;
        physical[3] = 43; physical[4] = 217; physical[5] = 0x88; physical[20] = 77;
        byte[] merged = PhysicalReportMerge.Merge(physical, input.Evaluate(true, 450), true);
        Check(merged[5] == 0x98, "Repeat off phase releases keyboard Cross but preserves physical Triangle and normal Square");
        Check(merged[3] == 43 && merged[4] == 217 && merged[20] == 77, "Real controller right stick and opaque data remain intact");
        physical[5] |= 0x20;
        merged = PhysicalReportMerge.Merge(physical, input.Evaluate(true, 450), true);
        Check((merged[5] & 0x20) != 0, "Repeat never cancels a physically held controller button");
        input.Process(K, false, true, false, true, 451);
        Check(input.Evaluate(true, 500) == Square, "Hold release stops immediately");
        input.Reset();
        Check(input.Evaluate(true, 600) == 0, "Disconnect or explicit pause resets normal and repeated actions");
        Console.WriteLine("KEYBOARD_PIPELINE_PASS checks=" + checks + "; fake clock, native merge only; no live input or devices");
        return 0;
    }
}
