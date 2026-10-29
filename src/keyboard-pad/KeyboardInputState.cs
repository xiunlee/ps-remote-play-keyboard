using System;

namespace PSKeyboard
{
    // Physical key lifecycle only. This class neither installs a hook nor polls
    // the keyboard. A caller decides focus, modifiers and enabled state.
    public sealed class KeyboardInputState
    {
        private readonly object gate = new object();
        private readonly bool[] physicalDown = new bool[256];
        private readonly bool[] capturedDown = new bool[256];
        private readonly bool[] disarmed = new bool[256];
        private readonly uint[] actionByKey = new uint[256];
        private readonly Func<int, uint> resolver;
        private uint held;

        public KeyboardInputState() : this(KeyboardMapping.ActionForKey) { }

        public KeyboardInputState(Func<int, uint> resolver)
        {
            if (resolver == null) throw new ArgumentNullException("resolver");
            this.resolver = resolver;
        }

        public uint Held { get { lock (gate) { return held; } } }

        // Release game actions immediately, while retaining which physical
        // presses were swallowed. Their repeats/up must also be swallowed;
        // otherwise Windows could receive an unmatched down or up.
        public void Reset()
        {
            lock (gate) { ResetCore(); }
        }

        private void ResetCore()
        {
            held = 0;
            for (int i = 0; i < physicalDown.Length; i++)
                if (physicalDown[i]) disarmed[i] = true;
        }

        private void RecomputeHeld()
        {
            uint current = 0;
            for (int i = 0; i < physicalDown.Length; i++)
                if (physicalDown[i] && capturedDown[i] && !disarmed[i])
                    current |= actionByKey[i];
            held = current;
        }

        // Return true only when the caller should suppress this physical event.
        // Synthetic input never changes the physical state, including key-up.
        // A down first passed to Windows stays passed through all repeats/up,
        // even if the caller enables capture in the middle of that press.
        public bool Process(int virtualKey, bool down, bool up, bool injected,
                            bool canCapture)
        {
            if (injected || down == up) return false;
            lock (gate)
            {
                if (!canCapture) ResetCore();
                if (virtualKey < 0 || virtualKey >= physicalDown.Length)
                    return false;

                // A captured release still belongs to its original down even
                // if that key has been removed from the current profile.
                if (up)
                {
                    bool swallow = capturedDown[virtualKey];
                    physicalDown[virtualKey] = false;
                    capturedDown[virtualKey] = false;
                    disarmed[virtualKey] = false;
                    actionByKey[virtualKey] = 0;
                    RecomputeHeld();
                    return swallow;
                }

                if (physicalDown[virtualKey])
                {
                    // Repeats cannot arm a key that was held during Reset or
                    // before enable/focus entry. A captured press stays consumed
                    // until physical release, even when it is now disarmed.
                    return capturedDown[virtualKey];
                }

                uint action = resolver(virtualKey);
                bool capture = canCapture && action != 0;
                // Track unmapped downs too: changing a mapping must not claim
                // a press which Windows already received before that change.
                physicalDown[virtualKey] = true;
                capturedDown[virtualKey] = capture;
                disarmed[virtualKey] = !capture;
                actionByKey[virtualKey] = action;
                RecomputeHeld();
                return capture;
            }
        }
    }
}
