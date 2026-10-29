using System;

namespace PSKeyboard
{
    // Only keyboard action bits enter/leave this class. Physical controller
    // reports stay untouched until the existing native merger combines them.
    public sealed class RapidFireEngine
    {
        private readonly object gate = new object();
        private readonly long[] phaseStarts = new long[BindingProfile.ActionCount];
        private BindingProfile currentProfile;
        private uint held, disarmed, latched, assigned;
        private long lastTime;

        // Call for every real accepted key transition, including releases, not
        // just when a USB report arrives. This preserves fast Toggle taps.
        public void UpdateInput(uint heldActions, BindingProfile profile, long nowMilliseconds)
        {
            Validate(profile, nowMilliseconds);
            lock (gate)
            {
                long now = MonotonicTime(nowMilliseconds);
                UseProfile(profile);
                uint next = heldActions & assigned;
                disarmed &= next;
                uint pressed = next & ~held & ~disarmed;
                held = next;
                for (int i = 0; i < BindingProfile.ActionCount; ++i)
                {
                    uint bit = 1u << i;
                    if ((pressed & bit) == 0) continue;
                    RapidFireMode mode = profile.RapidFireModeForAction(i);
                    if (mode == RapidFireMode.Toggle)
                    {
                        latched ^= bit;
                        if ((latched & bit) != 0) phaseStarts[i] = now;
                    }
                    else if (mode == RapidFireMode.Hold) phaseStarts[i] = now;
                }
            }
        }

        // Samples the current half-on/half-off phase. Delayed reports never
        // produce catch-up bursts, queued inputs or synthetic keyboard events.
        public uint Evaluate(BindingProfile profile, long nowMilliseconds)
        {
            Validate(profile, nowMilliseconds);
            lock (gate)
            {
                long now = MonotonicTime(nowMilliseconds);
                UseProfile(profile);
                uint result = 0, activeHeld = held & ~disarmed & assigned;
                for (int i = 0; i < BindingProfile.ActionCount; ++i)
                {
                    uint bit = 1u << i;
                    RapidFireMode mode = profile.RapidFireModeForAction(i);
                    if (mode == RapidFireMode.Off) { result |= activeHeld & bit; continue; }
                    bool active = mode == RapidFireMode.Toggle ? (latched & bit) != 0 : (activeHeld & bit) != 0;
                    if (!active || (assigned & bit) == 0) continue;
                    long phase = (now - phaseStarts[i]) % profile.IntervalMillisecondsForAction(i);
                    if (phase * 2 < profile.IntervalMillisecondsForAction(i)) result |= bit;
                }
                return result;
            }
        }

        // Focus loss, modifiers, pause, edits, disconnect and shutdown clear
        // every latch. Returning to the game never automatically resumes it.
        // A still-held press cannot re-arm until its release is observed.
        public void Reset()
        {
            lock (gate) { ResetCore(); }
        }

        private void ResetCore()
        {
            disarmed |= held;
            latched = 0;
            Array.Clear(phaseStarts, 0, phaseStarts.Length);
        }

        private void UseProfile(BindingProfile profile)
        {
            if (Object.ReferenceEquals(profile, currentProfile)) return;
            if (currentProfile != null) ResetCore();
            currentProfile = profile; assigned = 0;
            for (int i = 0; i < BindingProfile.ActionCount; ++i)
                if (profile.KeyForAction(i) != 0) assigned |= 1u << i;
        }

        private long MonotonicTime(long now)
        {
            // Concurrent callers can acquire this lock in a different order
            // from sampling the clock. Older samples must not rewind a phase.
            if (now > lastTime) lastTime = now;
            return lastTime;
        }

        private static void Validate(BindingProfile profile, long now)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            if (now < 0) throw new ArgumentOutOfRangeException("nowMilliseconds");
        }
    }
}
