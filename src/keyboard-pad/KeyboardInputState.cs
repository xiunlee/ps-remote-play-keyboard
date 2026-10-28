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
        private uint held;

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
            uint action = KeyboardMapping.ActionForKey(virtualKey);
            lock (gate)
            {
                if (!canCapture) ResetCore();
                if (virtualKey < 0 || virtualKey >= physicalDown.Length || action == 0)
                    return false;

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

                physicalDown[virtualKey] = true;
                capturedDown[virtualKey] = canCapture;
                disarmed[virtualKey] = !canCapture;
                actionByKey[virtualKey] = action;
                RecomputeHeld();
                return canCapture;
            }
        }
    }
}
