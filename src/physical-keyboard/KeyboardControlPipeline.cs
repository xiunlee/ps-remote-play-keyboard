using System;

namespace PSKeyboard
{
    // Serializes physical key ownership and repeat timing. It does not install
    // hooks, inspect windows, send input, access files or open a device.
    public sealed class KeyboardControlPipeline
    {
        private readonly object gate = new object();
        private readonly KeyboardInputState input;
        private readonly RapidFireEngine repeat = new RapidFireEngine();
        private readonly Func<BindingProfile> readProfile;

        public KeyboardControlPipeline(Func<BindingProfile> readProfile)
        {
            if (readProfile == null) throw new ArgumentNullException("readProfile");
            this.readProfile = readProfile;
            input = new KeyboardInputState(delegate(int key) { return this.readProfile().ActionForKey(key); });
        }

        public bool Process(int key, bool down, bool up, bool injected, bool canCapture, long nowMilliseconds)
        {
            if (injected || down == up) return false;
            lock (gate)
            {
                if (!canCapture) ResetCore();
                bool consumed = input.Process(key, down, up, injected, canCapture);
                // Called for every physical event: short taps between USB
                // reports still toggle once, and OS key repeats do not toggle.
                repeat.UpdateInput(input.Held, readProfile(), nowMilliseconds);
                return consumed;
            }
        }

        public uint Evaluate(bool active, long nowMilliseconds)
        {
            lock (gate)
            {
                if (!active) { ResetCore(); return 0; }
                return repeat.Evaluate(readProfile(), nowMilliseconds);
            }
        }

        public void Reset() { lock (gate) { ResetCore(); } }
        private void ResetCore() { input.Reset(); repeat.Reset(); }
    }
}
