using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using PSKeyboard;

// Pure profile and physical-key lifecycle tests. No hooks, devices or user settings.
static class BindingProfileTests
{
    const int K = 0x4B, W = 0x57, Space = 0x20;
    const uint Cross = 1u << 8, Circle = 1u << 9;
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

    static bool Down(KeyboardInputState state, int key)
    { return state.Process(key, true, false, false, true); }
    static bool Up(KeyboardInputState state, int key)
    { return state.Process(key, false, true, false, true); }

    static bool Same(BindingProfile a, BindingProfile b)
    {
        for (int i = 0; i < BindingProfile.ActionCount; ++i)
            if (a.KeyForAction(i) != b.KeyForAction(i) ||
                a.RapidFireModeForAction(i) != b.RapidFireModeForAction(i) ||
                a.IntervalMillisecondsForAction(i) != b.IntervalMillisecondsForAction(i)) return false;
        return true;
    }

    static int[] Keys(BindingProfile profile)
    {
        int[] result = new int[BindingProfile.ActionCount];
        for (int i = 0; i < result.Length; ++i) result[i] = profile.KeyForAction(i);
        return result;
    }

    static string Json(object version, object keys)
    {
        return new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
            { "version", version }, { "keys", keys }
        });
    }

    static string Json2(object modes, object intervals)
    {
        return new JavaScriptSerializer().Serialize(new Dictionary<string, object> {
            { "version", 2 }, { "keys", Keys(BindingProfile.Default()) },
            { "rapidFireModes", modes }, { "intervalMilliseconds", intervals }
        });
    }

    static int[] AllIntervals(int value)
    {
        int[] result = new int[BindingProfile.ActionCount];
        for (int i = 0; i < result.Length; ++i) result[i] = value;
        return result;
    }

    static void RapidFireSettings()
    {
        BindingProfile original = BindingProfile.Default();
        for (int i = 0; i < BindingProfile.ActionCount; ++i)
            Check(original.RapidFireModeForAction(i) == RapidFireMode.Off &&
                original.IntervalMillisecondsForAction(i) == 100, "all actions default to ordinary input: " + i);
        BindingProfile custom = original.WithRapidFire(8, RapidFireMode.Hold, 20)
            .WithRapidFire(9, RapidFireMode.Toggle, 5000).WithBinding(8, Space);
        Check(custom.RapidFireModeForAction(8) == RapidFireMode.Hold && custom.IntervalMillisecondsForAction(8) == 20 &&
            custom.KeyForAction(8) == Space, "remapping preserves per-action rapid-fire settings");
        Check(custom.RapidFireModeForAction(9) == RapidFireMode.Toggle && custom.IntervalMillisecondsForAction(9) == 5000,
            "independent action stores its own mode and interval");
        Check(original.RapidFireModeForAction(8) == RapidFireMode.Off && original.KeyForAction(8) == K,
            "rapid-fire edits preserve original immutable settings and bindings");
        Check(custom.WithBinding(9, 0).RapidFireModeForAction(9) == RapidFireMode.Toggle,
            "unassignment preserves a user's rapid-fire setting for later reassignment");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(22, RapidFireMode.Hold, 100); }, "invalid turbo action rejected");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(8, (RapidFireMode)(-1), 100); }, "negative mode rejected");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(8, (RapidFireMode)3, 100); }, "unknown mode rejected");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(8, RapidFireMode.Hold, 19); }, "too short interval rejected");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(8, RapidFireMode.Toggle, 5001); }, "too long interval rejected");
        Throws<ArgumentOutOfRangeException>(delegate { original.WithRapidFire(8, RapidFireMode.Off, 0); }, "interval validated even while off");
        Throws<ArgumentOutOfRangeException>(delegate { original.RapidFireModeForAction(-1); }, "mode getter validates action");
        Throws<ArgumentOutOfRangeException>(delegate { original.IntervalMillisecondsForAction(22); }, "interval getter validates action");
    }

    static void DefaultAndValidation()
    {
        BindingProfile profile = BindingProfile.Default();
        bool sameMapping = true;
        for (int i = 0; i < 256; ++i)
            if (profile.ActionForKey(i) != KeyboardMapping.ActionForKey(i)) sameMapping = false;
        Check(sameMapping, "default profile preserves all original native action bits");
        Check(profile.ActionForKey(-1) == 0 && profile.ActionForKey(256) == 0 && profile.ActionForKey(0) == 0,
            "unassigned and out-of-range keys have no action");
        for (int i = 0; i < BindingProfile.ActionCount; ++i)
            Check(profile.ActionForKey(profile.KeyForAction(i)) == (1u << i), "stable native action index " + i);

        BindingProfile changed = profile.WithBinding(8, Space);
        Check(changed.ActionForKey(Space) == Cross && changed.ActionForKey(K) == 0,
            "remapping removes the previous key and activates the replacement");
        Check(profile.ActionForKey(K) == Cross && profile.ActionForKey(Space) == 0,
            "WithBinding preserves the original immutable profile");
        Check(Same(changed, changed.WithBinding(8, Space)), "assigning the existing key is valid");
        BindingProfile cleared = changed.WithBinding(8, 0).WithBinding(9, 0);
        Check(cleared.KeyForAction(8) == 0 && cleared.KeyForAction(9) == 0 && cleared.ActionForKey(Space) == 0,
            "multiple actions may be unassigned");
        Check(Same(BindingProfile.Default(), profile), "a fresh default resets all edits");
        Throws<ArgumentException>(delegate { profile.WithBinding(8, W); }, "duplicate key rejected");
        Throws<ArgumentOutOfRangeException>(delegate { profile.WithBinding(-1, Space); }, "negative action rejected");
        Throws<ArgumentOutOfRangeException>(delegate { profile.KeyForAction(22); }, "out-of-range action rejected");
        Throws<ArgumentNullException>(delegate { new KeyboardInputState(null); }, "null resolver rejected");

        int[] reserved = { -1, 1, 2, 4, 5, 6, 0x10, 0x11, 0x12, 0x5B, 0x5C, 0x77,
            0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xE7, 0xFF, 256, 0x10041 };
        foreach (int key in reserved)
        {
            int capturedKey = key;
            Check(!BindingProfile.IsAllowedKey(key), "unsupported key classified: " + key);
            Throws<ArgumentException>(delegate { profile.WithBinding(8, capturedKey); }, "unsupported key rejected: " + key);
        }
        int[] ordinary = { 0, 0x30, 0x39, 0x41, 0x5A, 0x60, 0x69, 0x6A, 0x6F,
            0x70, 0x76, 0x78, 0x7B, 0x08, 0x09, 0x0D, 0x1B, 0x20, 0x21, 0x28,
            0x2D, 0x2E, 0xBA, 0xC0, 0xDB, 0xDF, 0xE2 };
        foreach (int key in ordinary) Check(BindingProfile.IsAllowedKey(key), "ordinary key supported: " + key);
    }

    static void RemappingHeldKeys()
    {
        BindingProfile profile = BindingProfile.Default();
        KeyboardInputState state = new KeyboardInputState(delegate(int key) { return profile.ActionForKey(key); });
        Check(Down(state, K) && state.Held == Cross, "original binding captures Cross");
        state.Reset();
        profile = profile.WithBinding(8, 0);
        Check(Down(state, K) && state.Held == 0, "unassigned captured key repeat remains swallowed and disarmed");
        Check(Up(state, K) && state.Held == 0, "unassigned captured key release is still swallowed");
        Check(!Down(state, K) && !Up(state, K), "fresh unassigned press passes to Windows");

        profile = BindingProfile.Default();
        Check(Down(state, K) && state.Held == Cross, "original binding can be restored");
        state.Reset();
        profile = profile.WithBinding(8, Space).WithBinding(9, K);
        Check(Down(state, K) && state.Held == 0, "changing a held key to another action cannot re-arm it");
        Check(!state.Process(K, false, true, true, true), "injected release is ignored during remap");
        Check(Down(state, K) && state.Held == 0, "injected release cannot permit a repeat to re-arm");
        Check(Up(state, K), "real original release remains paired");
        Check(Down(state, K) && state.Held == Circle, "fresh press uses changed Circle binding");
        Check(Down(state, Space) && state.Held == (Cross | Circle), "custom bindings combine independent held actions");
        Check(Up(state, K) && state.Held == Cross, "releasing one custom binding preserves another");
        Check(Up(state, Space) && state.Held == 0, "custom inputs release fully");
    }

    static void MappingPreviouslyUnmappedKey()
    {
        BindingProfile profile = BindingProfile.Default();
        KeyboardInputState state = new KeyboardInputState(delegate(int key) { return profile.ActionForKey(key); });
        Check(!Down(state, Space), "initial unmapped down passes to Windows");
        state.Reset();
        profile = profile.WithBinding(8, Space);
        Check(!Down(state, Space) && state.Held == 0, "unmapped held key cannot capture after it becomes mapped");
        Check(!state.Process(Space, false, true, true, true), "injected up cannot end an unmapped physical press");
        Check(!Down(state, Space) && state.Held == 0, "repeat after injected up remains passed");
        Check(!Up(state, Space), "release of original passed down remains passed");
        Check(Down(state, Space) && state.Held == Cross, "new physical press uses the custom binding");
        Check(Up(state, Space) && state.Held == 0, "new captured press has paired swallowed up");

        profile = BindingProfile.Default();
        Check(!state.Process(Space, true, false, false, false), "startup snapshot records unmapped held keys");
        profile = profile.WithBinding(8, Space);
        Check(!Down(state, Space) && state.Held == 0, "snapshot prevents capture until initially held key is released");
        Check(!Up(state, Space), "snapshot release passes to Windows");
        Check(Down(state, Space) && Up(state, Space), "fresh post-snapshot custom press works");
    }

    static void Persistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "pskeyboard-binding-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "profile.json");
        try
        {
            BindingProfile original = BindingProfile.Default();
            Check(Same(original, BindingProfile.Load(path)), "missing file loads defaults");
            Check(Same(original, BindingProfile.Load(Path.Combine(root, "missing", "profile.json"))),
                "missing directory loads defaults without creating it");
            Check(!Directory.Exists(Path.Combine(root, "missing")), "loading missing profile makes no filesystem changes");
            BindingProfile custom = original.WithBinding(8, Space).WithBinding(21, 0).WithBinding(18, 0x70)
                .WithRapidFire(8, RapidFireMode.Hold, 20).WithRapidFire(9, RapidFireMode.Toggle, 5000)
                .WithRapidFire(21, RapidFireMode.Toggle, 301);
            custom.Save(path);
            Check(Same(custom, BindingProfile.Load(path)), "custom keys, modes and independent intervals survive save/load");
            Dictionary<string, object> written = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(path)) as Dictionary<string, object>;
            Check(written != null && (int)written["version"] == 2, "new saves use schema 2");
            BindingProfile legacy = original.WithBinding(8, Space).WithBinding(21, 0);
            File.WriteAllText(path, Json(1, Keys(legacy)));
            Check(Same(legacy, BindingProfile.Load(path)), "schema 1 preserves keys with all rapid-fire settings disabled");
            original.Save(path);
            Check(Same(original, BindingProfile.Load(path)), "atomic replacement saves over an existing profile");

            string prior = File.ReadAllText(path);
            using (FileStream heldFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Throws<IOException>(delegate { custom.Save(path); }, "locked destination rejects replacement");
            Check(File.ReadAllText(path) == prior, "failed save preserves the complete previous profile");
            Check(Directory.GetFiles(root).Length == 1, "failed save removes its temporary file");

            int[] duplicate = Keys(original); duplicate[9] = duplicate[8];
            int[] reserved = Keys(original); reserved[8] = 0x77;
            int[] unsupported = Keys(original); unsupported[8] = -1;
            object[] nonInteger = new object[BindingProfile.ActionCount];
            for (int i = 0; i < nonInteger.Length; ++i) nonInteger[i] = original.KeyForAction(i);
            nonInteger[8] = "75";
            int[] invalidMode = new int[22]; invalidMode[8] = 3;
            int[] negativeMode = new int[22]; negativeMode[8] = -1;
            int[] shortInterval = AllIntervals(100); shortInterval[9] = 19;
            int[] longInterval = AllIntervals(100); longInterval[9] = 5001;
            object[] invalidIntervalType = new object[22];
            for (int i = 0; i < invalidIntervalType.Length; ++i) invalidIntervalType[i] = 100;
            invalidIntervalType[0] = 100.5;
            string[] bad = {
                "", "{", "null", "[]", "{}",
                Json(2, Keys(original)), Json(3, Keys(original)), Json("1", Keys(original)), Json(1.5, Keys(original)),
                Json(1, new int[21]), Json(1, new int[23]), Json(1, null),
                Json(1, duplicate), Json(1, reserved), Json(1, unsupported), Json(1, nonInteger),
                "{\"version\":1,\"keys\":[],\"extra\":true}", new string(' ', 16385),
                Json2(invalidMode, AllIntervals(100)), Json2(negativeMode, AllIntervals(100)),
                Json2(new int[21], AllIntervals(100)), Json2(new int[22], new int[23]),
                Json2(new int[22], shortInterval), Json2(new int[22], longInterval),
                Json2(new int[22], invalidIntervalType), Json2(nonInteger, AllIntervals(100)),
                Json2(null, AllIntervals(100)), Json2(new int[22], null)
            };
            for (int i = 0; i < bad.Length; ++i)
            {
                File.WriteAllText(path, bad[i]);
                Throws<InvalidDataException>(delegate { BindingProfile.Load(path); }, "invalid persisted profile rejected: " + i);
                Check(File.ReadAllText(path) == bad[i], "failed load preserves original profile for recovery: " + i);
            }
        }
        finally
        {
            // Only files in this unique test-created directory; no recursive delete.
            foreach (string file in Directory.GetFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }

    static int Main()
    {
        DefaultAndValidation(); RapidFireSettings(); RemappingHeldKeys(); MappingPreviouslyUnmappedKey(); Persistence();
        Console.WriteLine("BINDING_PROFILE_PASS checks=" + checks + "; isolated temporary files and pure key events only");
        return 0;
    }
}
