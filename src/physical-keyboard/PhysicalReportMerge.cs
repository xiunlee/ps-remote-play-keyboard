using System;
using System.Runtime.InteropServices;

namespace PSKeyboard
{
    // The caller supplies one fresh physical interrupt-IN report per call.
    // This adapter never constructs a neutral device or resends an old report.
    public static class PhysicalReportMerge
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct HeldKeys { public uint Held; public int Enabled; }

        [DllImport("pskb-keyboard-merge.dll", CallingConvention = CallingConvention.Cdecl,
            ExactSpelling = true)]
        private static extern int pskb_merge_keyboard([In] byte[] source, UIntPtr sourceLength,
            ref HeldKeys keys, [Out] byte[] output, UIntPtr outputLength);

        public static byte[] Merge(byte[] physical, uint held, bool enabled)
        {
            if (physical == null || physical.Length != 64 || physical[0] != 1)
                throw new ArgumentException("Expected a fresh 64-byte DS4 USB input report.", "physical");
            byte[] output = new byte[64];
            HeldKeys keys = new HeldKeys { Held = enabled ? held : 0, Enabled = enabled ? 1 : 0 };
            int result = pskb_merge_keyboard(physical, (UIntPtr)64, ref keys, output, (UIntPtr)64);
            if (result != 0)
                throw new InvalidOperationException("Physical keyboard merge failed: " + result);
            return output;
        }
    }
}
