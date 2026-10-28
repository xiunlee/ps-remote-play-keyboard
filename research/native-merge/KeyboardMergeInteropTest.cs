using System;
using System.Runtime.InteropServices;

// Tests the exported native ABI in this test process only. No input capture,
// hardware, drivers, Remote Play, or transport are involved.
static class KeyboardMergeInteropTest
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Keyboard { public uint Held; public int Enabled; }

    [DllImport("pskb-keyboard-merge.dll", CallingConvention=CallingConvention.Cdecl,
        ExactSpelling=true)]
    static extern int pskb_merge_keyboard(
        [In] byte[] source, UIntPtr sourceLength, ref Keyboard keyboard,
        [Out] byte[] output, UIntPtr outputLength);

    static int Main()
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf(typeof(Keyboard)) != 8)
            throw new InvalidOperationException("Expected x64 and an 8-byte keyboard struct.");
        byte[] source = new byte[64], output = new byte[64];
        source[0] = 1; source[1] = 128; source[2] = 128;
        source[3] = 73; source[4] = 199; source[5] = 8;
        Keyboard keyboard = new Keyboard { Held = 1u << 8, Enabled = 1 };
        int status = pskb_merge_keyboard(source,(UIntPtr)64,ref keyboard,output,(UIntPtr)64);
        if (status != 0 || output[5] != 0x28 || output[3] != 73 || output[4] != 199)
            throw new InvalidOperationException("Native Cross overlay or right-stick preservation failed.");
        keyboard.Held = 0;
        status = pskb_merge_keyboard(source,(UIntPtr)64,ref keyboard,output,(UIntPtr)64);
        for (int i = 0; i < 64; i++)
            if (status != 0 || output[i] != source[i])
                throw new InvalidOperationException("Native release did not restore physical report.");
        Console.WriteLine("KEYBOARD_MERGE_INTEROP_PASS x64 cdecl struct_size=8 cross=0x20 release=physical right_stick=preserved");
        return 0;
    }
}
