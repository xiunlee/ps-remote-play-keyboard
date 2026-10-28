using System;
using Ds4Research;

static class Ds4UsbReportTests
{
    static int assertions;
    static void Assert(bool value, string name) { assertions++; if (!value) throw new Exception(name); }
    static void Throws<T>(Action action, string name) where T : Exception
    {
        try { action(); } catch (T) { assertions++; return; }
        throw new Exception(name);
    }
    static byte[] Sample()
    {
        byte[] bytes = new byte[64];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 3);
        bytes[0] = 1; bytes[1] = 130; bytes[2] = 125; bytes[3] = 140; bytes[4] = 100;
        bytes[5] = 0x28; bytes[6] = 0x81; bytes[7] = 0xfc; bytes[8] = 64; bytes[9] = 96;
        return bytes;
    }
    static void Main()
    {
        byte[] original = Sample(); byte[] pristine = (byte[])original.Clone();
        byte[] identity = Ds4UsbReport.Merge(original, new Overlay());
        Assert(!Object.ReferenceEquals(original, identity), "must clone input");
        for (int i = 0; i < 64; i++) Assert(identity[i] == original[i], "no keys must preserve byte " + i);
        var keys = new Overlay { LeftStickActive = true, LeftX = 0, LeftY = 255,
            RightStickActive = true, RightX = Ds4UsbReport.MouseAxis(3, 5), RightY = Ds4UsbReport.MouseAxis(-4, 5),
            DpadActive = true, Dpad = Ds4UsbReport.DpadFromAxes(1, -1),
            FaceButtons = 0x90, OtherButtons = 0x20, SpecialButtons = 0x03, L2 = 255, R2 = 50 };
        byte[] combined = Ds4UsbReport.Merge(original, keys);
        Assert(combined[1] == 0 && combined[2] == 255, "left stick");
        Assert(combined[3] == 158 && combined[4] == 88, "mouse axes");
        Assert(combined[5] == 0xb1, "dpad and face masks");
        Assert(combined[6] == 0xad, "physical and synthetic buttons, trigger flags");
        Assert(combined[7] == 0xff, "special and counter bits preserved");
        Assert(combined[8] == 255 && combined[9] == 96, "trigger max retains physical pressure");
        for (int i = 10; i < 64; i++) Assert(combined[i] == original[i], "sensor/touch/status unchanged " + i);
        for (int i = 0; i < 64; i++) Assert(original[i] == pristine[i], "source immutable " + i);
        byte[] released = Ds4UsbReport.Merge(original, new Overlay());
        for (int i = 0; i < 64; i++) Assert(released[i] == original[i], "release returns physical " + i);
        Assert(Ds4UsbReport.MouseAxis(int.MaxValue, 10) == 255 && Ds4UsbReport.MouseAxis(int.MinValue, 10) == 0, "mouse clamp without overflow");
        Assert(Ds4UsbReport.MouseAxis(0, 5) == 128, "mouse neutral");
        Assert(Ds4UsbReport.KeyAxis(true, true) == 128 && Ds4UsbReport.KeyAxis(false, false) == 128, "opposed keys cancel");
        Assert(Ds4UsbReport.KeyAxis(true, false) == 0 && Ds4UsbReport.KeyAxis(false, true) == 255, "key axis signs");
        Assert(Ds4UsbReport.DpadFromKeys(true, true, true, true) == 8, "opposed dpad directions cancel");
        Assert(Ds4UsbReport.DpadFromKeys(true, true, false, true) == 2, "remaining dpad axis retained");
        byte[] hats = {7,0,1,6,8,2,5,4,3}; int n = 0;
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) Assert(Ds4UsbReport.DpadFromAxes(x,y) == hats[n++], "hat directions");
        Throws<ArgumentNullException>(() => Ds4UsbReport.Merge(null, new Overlay()), "null report");
        Throws<ArgumentNullException>(() => Ds4UsbReport.Merge(original, null), "null overlay");
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(new byte[63], new Overlay()), "invalid length");
        byte[] bluetooth = Sample(); bluetooth[0] = 0x11;
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(bluetooth, new Overlay()), "bluetooth report");
        byte[] invalidHat = Sample(); invalidHat[5] |= 0x0f;
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(invalidHat, new Overlay()), "invalid physical hat");
        Throws<ArgumentOutOfRangeException>(() => Ds4UsbReport.Merge(original, new Overlay { DpadActive = true, Dpad = 9 }), "invalid synthetic hat");
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(original, new Overlay { FaceButtons = 1 }), "invalid face mask");
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(original, new Overlay { SpecialButtons = 4 }), "counter mask");
        Throws<ArgumentException>(() => Ds4UsbReport.Merge(original, new Overlay { OtherButtons = 4 }), "use analog trigger field");
        Throws<ArgumentOutOfRangeException>(() => Ds4UsbReport.MouseAxis(1, 0), "invalid sensitivity");
        Console.WriteLine("PASS: " + assertions + " assertions; offline USB report tests only.");
    }
}
