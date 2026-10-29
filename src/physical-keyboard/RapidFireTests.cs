using System;
using System.Threading;
using PSKeyboard;

// Fake monotonic timestamps and action masks only. No timers, hooks or devices.
static class RapidFireTests
{
    const uint Cross = 1u << 8, Circle = 1u << 9, Forward = 1u;
    static int checks;

    static void Check(bool condition, string description)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(description);
    }

    static void Throws<T>(Action action, string description) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, description); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + description);
    }

    static void OrdinaryAndHold()
    {
        BindingProfile ordinary = BindingProfile.Default();
        RapidFireEngine engine = new RapidFireEngine();
        engine.UpdateInput(Cross | Forward, ordinary, 0);
        Check(engine.Evaluate(ordinary, 0) == (Cross | Forward), "Off mode preserves ordinary simultaneous held input");
        Check(engine.Evaluate(ordinary, 10000) == (Cross | Forward), "Off mode does not pulse after elapsed time");
        engine.UpdateInput(Cross, ordinary, 10001);
        Check(engine.Evaluate(ordinary, 10002) == Cross, "ordinary actions release independently");
        engine.UpdateInput(0, ordinary, 10003);
        Check(engine.Evaluate(ordinary, 10004) == 0, "ordinary release returns neutral");

        BindingProfile hold = ordinary.WithRapidFire(8, RapidFireMode.Hold, 100);
        engine = new RapidFireEngine();
        engine.UpdateInput(Cross | Forward, hold, 1000);
        Check(engine.Evaluate(hold, 1000) == (Cross | Forward), "Hold starts with immediate press phase");
        Check(engine.Evaluate(hold, 1049) == (Cross | Forward), "Hold remains down during first half period");
        Check(engine.Evaluate(hold, 1050) == Forward, "Hold releases exactly at half period while ordinary action stays held");
        engine.UpdateInput(Cross | Forward, hold, 1075);
        Check(engine.Evaluate(hold, 1075) == Forward, "OS repeat does not restart Hold phase");
        Check(engine.Evaluate(hold, 1099) == Forward, "Hold remains released until complete interval");
        Check(engine.Evaluate(hold, 1100) == (Cross | Forward), "Hold starts next press at full interval");
        engine.UpdateInput(Forward, hold, 1101);
        Check(engine.Evaluate(hold, 1101) == Forward, "physical keyboard release immediately stops Hold");
        engine.UpdateInput(Cross | Forward, hold, 1125);
        Check(engine.Evaluate(hold, 1125) == (Cross | Forward), "fresh Hold press restarts phase at its own timestamp");
        Check(engine.Evaluate(hold, 1000099) == Forward, "long report gap samples current off phase without catch-up burst");
        Check(engine.Evaluate(hold, 1000099) == Forward, "repeated evaluation at same instant produces no queued pulse");
    }

    static void ToggleAndFastTaps()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Toggle, 100);
        RapidFireEngine engine = new RapidFireEngine();
        engine.UpdateInput(Cross, profile, 0);
        engine.UpdateInput(0, profile, 1);
        Check(engine.Evaluate(profile, 25) == Cross, "fast down/up between reports still starts Toggle");
        Check(engine.Evaluate(profile, 50) == 0, "released Toggle continues alternating phase");
        Check(engine.Evaluate(profile, 100) == Cross, "released Toggle survives into next interval");
        engine.UpdateInput(Cross, profile, 110);
        Check(engine.Evaluate(profile, 110) == 0, "second fresh press turns Toggle off immediately");
        engine.UpdateInput(Cross, profile, 111);
        Check(engine.Evaluate(profile, 111) == 0, "held repeat cannot toggle back on");
        engine.UpdateInput(0, profile, 112);
        engine.UpdateInput(Cross, profile, 120);
        engine.UpdateInput(Cross, profile, 121);
        Check(engine.Evaluate(profile, 121) == Cross, "one new press starts exactly one Toggle latch");
        Check(engine.Evaluate(profile, 170) == 0, "repeat does not move Toggle phase origin");

        engine = new RapidFireEngine();
        engine.UpdateInput(Cross, profile, 10); engine.UpdateInput(0, profile, 11);
        engine.UpdateInput(Cross, profile, 12); engine.UpdateInput(0, profile, 13);
        Check(engine.Evaluate(profile, 14) == 0, "two complete taps before one report cancel each other");
        engine.UpdateInput(Cross, profile, 15); engine.UpdateInput(0, profile, 16);
        Check(engine.Evaluate(profile, 64) == Cross && engine.Evaluate(profile, 65) == 0,
            "third tap starts a new independent phase");
    }

    static void IndependentActionsAndLimits()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Hold, 100)
            .WithRapidFire(9, RapidFireMode.Toggle, 200);
        RapidFireEngine engine = new RapidFireEngine();
        engine.UpdateInput(Cross, profile, 0);
        engine.UpdateInput(Cross | Circle, profile, 25);
        engine.UpdateInput(Cross, profile, 26);
        Check(engine.Evaluate(profile, 60) == Circle, "each action has its own interval and start time");
        Check(engine.Evaluate(profile, 125) == Cross, "one pulse can be down while another is released");
        Check(engine.Evaluate(profile, 225) == (Cross | Circle), "independent phases may overlap");

        for (int index = 0; index < BindingProfile.ActionCount; ++index)
        {
            uint bit = 1u << index;
            BindingProfile allActions = BindingProfile.Default().WithRapidFire(index, RapidFireMode.Hold, 20);
            RapidFireEngine one = new RapidFireEngine(); one.UpdateInput(bit, allActions, 0);
            Check(one.Evaluate(allActions, 0) == bit && one.Evaluate(allActions, 10) == 0 && one.Evaluate(allActions, 20) == bit,
                "all native actions support minimum interval: " + index);
            BindingProfile toggleAction = BindingProfile.Default().WithRapidFire(index, RapidFireMode.Toggle, 20);
            one = new RapidFireEngine(); one.UpdateInput(bit, toggleAction, 0); one.UpdateInput(0, toggleAction, 1);
            Check(one.Evaluate(toggleAction, 1) == bit && one.Evaluate(toggleAction, 10) == 0 && one.Evaluate(toggleAction, 20) == bit,
                "all native actions support Toggle after release: " + index);
        }
        BindingProfile longest = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Hold, 5000);
        engine = new RapidFireEngine(); engine.UpdateInput(Cross, longest, 0);
        Check(engine.Evaluate(longest, 2499) == Cross && engine.Evaluate(longest, 2500) == 0 && engine.Evaluate(longest, 5000) == Cross,
            "maximum interval uses equal press and release halves");
        BindingProfile odd = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Hold, 21);
        engine = new RapidFireEngine(); engine.UpdateInput(Cross, odd, 0);
        Check(engine.Evaluate(odd, 10) == Cross && engine.Evaluate(odd, 11) == 0 && engine.Evaluate(odd, 21) == Cross,
            "odd interval samples exact half boundary without integer truncation");
    }

    static void ResetAndProfileChanges()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Toggle, 100)
            .WithRapidFire(9, RapidFireMode.Hold, 100);
        RapidFireEngine engine = new RapidFireEngine();
        engine.UpdateInput(Cross | Circle | Forward, profile, 0);
        Check(engine.Evaluate(profile, 0) == (Cross | Circle | Forward), "mixed inputs start before reset");
        engine.Reset();
        Check(engine.Evaluate(profile, 1) == 0, "focus loss or explicit pause clears latch and all held actions");
        engine.UpdateInput(Cross | Circle | Forward, profile, 2);
        Check(engine.Evaluate(profile, 2) == 0, "held keys through reset cannot restart any mode");
        engine.Reset(); engine.Reset();
        Check(engine.Evaluate(profile, 100) == 0, "repeated reset never resurrects Toggle");
        engine.UpdateInput(0, profile, 101);
        Check(engine.Evaluate(profile, 200) == 0, "returning focus and releasing keys does not resume Toggle");
        engine.UpdateInput(Cross, profile, 201);
        engine.UpdateInput(0, profile, 202);
        Check(engine.Evaluate(profile, 210) == Cross, "fresh press after release can explicitly restart Toggle");
        engine.Reset();
        Check(engine.Evaluate(profile, 300) == 0, "reset also clears released-but-latched Toggle");

        engine.UpdateInput(Cross, profile, 301); engine.UpdateInput(0, profile, 302);
        BindingProfile changed = profile.WithRapidFire(8, RapidFireMode.Toggle, 200);
        Check(engine.Evaluate(changed, 303) == 0, "profile replacement clears a previous latch");
        engine.UpdateInput(Cross, changed, 304);
        Check(engine.Evaluate(changed, 304) == Cross, "fresh key works with newly published profile");
        BindingProfile remapped = changed.WithBinding(8, 0x20);
        Check(engine.Evaluate(remapped, 305) == 0, "profile replacement while held disarms previous physical action");
        engine.UpdateInput(Cross, remapped, 306);
        Check(engine.Evaluate(remapped, 306) == 0, "held repeat does not activate changed profile");
        engine.UpdateInput(0, remapped, 307); engine.UpdateInput(Cross, remapped, 308);
        Check(engine.Evaluate(remapped, 308) == Cross, "released remapped action starts on a new press");

        BindingProfile unassigned = BindingProfile.Default().WithBinding(8, 0).WithRapidFire(8, RapidFireMode.Toggle, 100);
        engine = new RapidFireEngine(); engine.UpdateInput(Cross | 0x80000000u, unassigned, 0);
        Check(engine.Evaluate(unassigned, 0) == 0, "unassigned actions and unknown mask bits cannot generate output");

        engine = new RapidFireEngine(); engine.UpdateInput(0, profile, 0); engine.Reset();
        engine.UpdateInput(Cross, changed, 1);
        Check(engine.Evaluate(changed, 1) == Cross, "first fresh press after an idle profile change is not lost");
    }

    static void PhysicalEventIntegration()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Toggle, 100);
        KeyboardInputState input = new KeyboardInputState(profile.ActionForKey);
        RapidFireEngine engine = new RapidFireEngine();
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 0);
        input.Process(0x4B, false, true, false, true); engine.UpdateInput(input.Held, profile, 1);
        Check(engine.Evaluate(profile, 2) == Cross, "hook event updates retain a tap shorter than one USB report interval");
        input.Reset(); engine.Reset();
        input.Process(0x4B, true, false, false, false); engine.UpdateInput(input.Held, profile, 3);
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 4);
        Check(engine.Evaluate(profile, 4) == 0, "key pressed outside foreground cannot start Toggle on focus return");
        input.Process(0x4B, false, true, false, true); engine.UpdateInput(input.Held, profile, 5);
        input.Process(0x4B, true, false, true, true); engine.UpdateInput(input.Held, profile, 6);
        Check(engine.Evaluate(profile, 6) == 0, "injected down does not start Toggle");
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 7);
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 8);
        Check(engine.Evaluate(profile, 8) == Cross, "real fresh down starts Toggle and OS repeat does not switch it off");
        input.Reset(); engine.Reset();
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 9);
        Check(engine.Evaluate(profile, 9) == 0, "held captured press through focus reset cannot resume Toggle");
        input.Process(0x4B, false, true, false, true); engine.UpdateInput(input.Held, profile, 10);
        input.Process(0x4B, true, false, false, true); engine.UpdateInput(input.Held, profile, 11);
        Check(engine.Evaluate(profile, 11) == Cross, "real release then fresh press is required after focus reset");
    }

    static void ClockAndConcurrency()
    {
        BindingProfile profile = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Hold, 100);
        RapidFireEngine engine = new RapidFireEngine(); engine.UpdateInput(Cross, profile, 100);
        Check(engine.Evaluate(profile, 160) == 0, "clock enters released phase");
        Check(engine.Evaluate(profile, 110) == 0, "older concurrent clock sample cannot rewind phase");
        Check(engine.Evaluate(profile, 200) == Cross, "clock continues normally after older sample");
        engine = new RapidFireEngine(); engine.UpdateInput(Cross, profile, Int64.MaxValue - 100);
        Check(engine.Evaluate(profile, Int64.MaxValue) == Cross, "large monotonic timestamps do not overflow phase math");
        Throws<ArgumentNullException>(delegate { engine.UpdateInput(0, null, 0); }, "null input profile rejected");
        Throws<ArgumentNullException>(delegate { engine.Evaluate(null, 0); }, "null evaluation profile rejected");
        Throws<ArgumentOutOfRangeException>(delegate { engine.Evaluate(profile, -1); }, "negative clock rejected");

        BindingProfile toggle = BindingProfile.Default().WithRapidFire(8, RapidFireMode.Toggle, 100);
        RapidFireEngine concurrent = new RapidFireEngine(); Exception failure = null;
        Thread events = new Thread(delegate() {
            try { for (int i = 0; i < 1000; ++i) { concurrent.UpdateInput(Cross, toggle, i * 2); concurrent.UpdateInput(0, toggle, i * 2 + 1); } }
            catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
        });
        Thread reports = new Thread(delegate() {
            try { for (int i = 0; i < 1000; ++i) if ((concurrent.Evaluate(toggle, i * 3) & ~Cross) != 0) throw new InvalidOperationException("Unexpected action bit."); }
            catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
        });
        Thread resets = new Thread(delegate() {
            try { for (int i = 0; i < 1000; ++i) concurrent.Reset(); }
            catch (Exception ex) { Interlocked.CompareExchange(ref failure, ex, null); }
        });
        events.Start(); reports.Start(); resets.Start(); events.Join(); reports.Join(); resets.Join();
        concurrent.Reset();
        Check(failure == null && concurrent.Evaluate(toggle, 10000) == 0, "concurrent events/evaluation/reset complete safely and final reset clears output");
        concurrent.UpdateInput(0, toggle, 10001); concurrent.UpdateInput(Cross, toggle, 10002);
        Check(concurrent.Evaluate(toggle, 10002) == Cross, "fresh input still works after concurrent operations");
    }

    static int Main()
    {
        OrdinaryAndHold(); ToggleAndFastTaps(); IndependentActionsAndLimits(); ResetAndProfileChanges();
        PhysicalEventIntegration(); ClockAndConcurrency();
        Console.WriteLine("RAPID_FIRE_PASS checks=" + checks + "; fake clocks and pure key/action events only");
        return 0;
    }
}
