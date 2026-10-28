using System;

namespace Ds4Research
{
    // Offline experiment: does not read hardware, create a controller, or send
    // data to Remote Play. USB report 0x01 only; Bluetooth reports differ.
    public sealed class Overlay
    {
        public bool LeftStickActive, RightStickActive, DpadActive;
        public byte LeftX = 128, LeftY = 128, RightX = 128, RightY = 128;
        public byte Dpad = 8;
        public byte FaceButtons, OtherButtons, SpecialButtons, L2, R2;
    }

    public static class Ds4UsbReport
    {
        public const int Length = 64;
        public const byte ReportId = 0x01;

        // Source is the unmodified input report for this sample, never a prior
        // merged result. This keeps released synthetic keys from sticking.
        public static byte[] Merge(byte[] source, Overlay overlay)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (overlay == null) throw new ArgumentNullException("overlay");
            if (source.Length != Length || source[0] != ReportId)
                throw new ArgumentException("Expected a 64-byte DS4 USB input report with report ID 0x01.", "source");
            if ((source[5] & 0x0f) > 8)
                throw new ArgumentException("Invalid physical D-pad value.", "source");
            if (overlay.DpadActive && overlay.Dpad > 8)
                throw new ArgumentOutOfRangeException("overlay", "D-pad must be in 0..8.");
            if ((overlay.FaceButtons & 0x0f) != 0 || (overlay.SpecialButtons & 0xfc) != 0)
                throw new ArgumentException("Button masks contain unsupported bits.", "overlay");
            if ((overlay.OtherButtons & 0x0c) != 0)
                throw new ArgumentException("Use analog L2/R2 fields for synthetic trigger presses.", "overlay");

            byte[] merged = (byte[])source.Clone();
            if (overlay.LeftStickActive) { merged[1] = overlay.LeftX; merged[2] = overlay.LeftY; }
            if (overlay.RightStickActive) { merged[3] = overlay.RightX; merged[4] = overlay.RightY; }
            if (overlay.DpadActive) merged[5] = (byte)((merged[5] & 0xf0) | overlay.Dpad);
            merged[5] |= overlay.FaceButtons;
            merged[6] |= overlay.OtherButtons;
            merged[7] |= overlay.SpecialButtons;
            // Analog triggers and their digital button bits must agree.
            if (overlay.L2 > 0) { merged[8] = Math.Max(merged[8], overlay.L2); merged[6] |= 0x04; }
            if (overlay.R2 > 0) { merged[9] = Math.Max(merged[9], overlay.R2); merged[6] |= 0x08; }
            return merged;
        }

        // A sample mouse delta is a deflection; no motion yields center. The
        // caller controls activation, frame timing, sensitivity and capture.
        public static byte MouseAxis(int delta, int sensitivity)
        {
            if (sensitivity < 1 || sensitivity > 10) throw new ArgumentOutOfRangeException("sensitivity");
            long value = 128L + (long)delta * sensitivity * 2;
            return (byte)Math.Max(0L, Math.Min(255L, value));
        }

        // Opposite held keys cancel; callers decide whether to override the
        // physical stick whenever either key is held.
        public static byte KeyAxis(bool negative, bool positive)
        {
            return negative == positive ? (byte)128 : negative ? (byte)0 : (byte)255;
        }

        public static byte DpadFromKeys(bool up, bool down, bool left, bool right)
        {
            int x = (right ? 1 : 0) - (left ? 1 : 0);
            int y = (down ? 1 : 0) - (up ? 1 : 0);
            return DpadFromAxes(x, y);
        }

        // -1, 0, +1 on each axis map to the DS4's hat value (0..8).
        public static byte DpadFromAxes(int x, int y)
        {
            if (x < -1 || x > 1 || y < -1 || y > 1) throw new ArgumentOutOfRangeException("x/y");
            return (byte)(y < 0 ? (x < 0 ? 7 : x > 0 ? 1 : 0) :
                          y > 0 ? (x < 0 ? 5 : x > 0 ? 3 : 4) :
                                  (x < 0 ? 6 : x > 0 ? 2 : 8));
        }
    }
}
