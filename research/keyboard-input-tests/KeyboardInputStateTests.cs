using System;
using System.Threading;
using PSKeyboard;

static class KeyboardInputStateTests
{
    const int K=0x4b, W=0x57, D=0x44, Ctrl=0x11;
    const uint Cross=1u<<8, Forward=1u<<0, Right=1u<<3;
    static int checks;

    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    static bool Down(KeyboardInputState state, int key, bool capture)
    { return state.Process(key,true,false,false,capture); }
    static bool Up(KeyboardInputState state, int key, bool capture)
    { return state.Process(key,false,true,false,capture); }

    static void EnableDuringPress()
    {
        KeyboardInputState s = new KeyboardInputState();
        Check(!Down(s,K,false),"disabled key-down passes to Windows");
        s.Reset(); // toggling enable does not claim a pre-existing press
        Check(!Down(s,K,true) && s.Held==0,"repeat after enabling remains passed");
        Check(!Up(s,K,true),"key-up paired with passed down remains passed");
        Check(Down(s,K,true) && s.Held==Cross,"fresh press after release captures Cross");
        Check(Down(s,K,true) && s.Held==Cross,"captured repeat stays captured");
        Check(Up(s,K,true) && s.Held==0,"captured up releases Cross and is swallowed");
        Check(!Up(s,K,true),"orphan up is not swallowed");
    }

    static void FocusAndPause()
    {
        KeyboardInputState s = new KeyboardInputState();
        Check(Down(s,W,true) && Down(s,D,true),"two fresh movement keys captured");
        Check(s.Held==(Forward|Right),"diagonal held mask includes both actions");
        s.Reset();
        Check(s.Held==0,"focus loss or pause immediately clears all game actions");
        Check(Down(s,W,false) && s.Held==0,"captured repeat outside allowed focus is suppressed without game input");
        Check(Down(s,D,true) && s.Held==0,"returning focus does not re-arm captured held key");
        Check(Up(s,W,false) && Up(s,D,true),"captured releases remain suppressed across focus changes");
        Check(Down(s,W,true) && s.Held==Forward,"fresh movement after focus return re-arms");
        Check(Up(s,W,true) && s.Held==0,"movement ends on physical up");
    }

    static void ModifiersAndSyntheticEvents()
    {
        KeyboardInputState s = new KeyboardInputState();
        Check(Down(s,K,true),"capture Cross before modifier");
        Check(!Down(s,Ctrl,false) && s.Held==0,"unmapped disallowed modifier clears game actions");
        Check(Down(s,K,true) && s.Held==0,"modifier release cannot re-arm existing Cross press");
        Check(!s.Process(K,false,true,true,true),"injected key-up passes without touching physical state");
        Check(Down(s,K,true) && s.Held==0,"injected up does not permit repeat to re-arm");
        Check(Up(s,K,true),"real captured up still consumed after injected up");
        Check(!s.Process(K,true,false,true,true) && s.Held==0,"injected down never captures");
        Check(Down(s,K,true) && s.Held==Cross,"physical down after injected down is fresh");
        Check(!s.Process(K,false,true,true,false) && s.Held==Cross,"injected disallowed event does not reset physical held action");
        Check(Up(s,K,true) && s.Held==0,"real release ends held action");
        Check(!Down(s,K,false),"shortcut-related new key press passes");
        Check(!Down(s,K,true) && s.Held==0,"passed shortcut repeat stays passed after modifier release");
        Check(!Up(s,K,true),"passed shortcut up stays passed");
    }

    static void IndependentKeysAndBadEvents()
    {
        KeyboardInputState s = new KeyboardInputState();
        Check(Down(s,W,true) && Down(s,D,true) && Down(s,K,true),"mixed stick and button inputs capture");
        Check(Up(s,D,true) && s.Held==(Forward|Cross),"one release preserves other held actions");
        Check(!s.Process(K,true,true,false,true) && s.Held==(Forward|Cross),"invalid simultaneous down/up is ignored");
        Check(!s.Process(K,false,false,false,false) && s.Held==(Forward|Cross),"non-key event is ignored");
        Check(!Down(s,-1,true) && !Up(s,300,true),"out-of-range virtual keys ignored");
        Check(!Down(s,0x20,true) && s.Held==(Forward|Cross),"unmapped allowed event leaves mapped actions intact");
        Check(Up(s,W,true) && Up(s,K,true) && s.Held==0,"remaining actions release independently");
        s.Reset(); s.Reset();
        Check(s.Held==0,"repeated reset is idempotent");
    }

    static void ConcurrentResetAndEvents()
    {
        KeyboardInputState s = new KeyboardInputState();
        Exception failure = null;
        Thread events = new Thread(delegate() {
            try { for (int i=0;i<3000;i++) { Down(s,K,true); Up(s,K,true); } }
            catch(Exception ex) { failure=ex; }
        });
        Thread reset = new Thread(delegate() {
            try { for (int i=0;i<3000;i++) { s.Reset(); uint current=s.Held;
                if ((current&~Cross)!=0) throw new InvalidOperationException("Unexpected action bit."); } }
            catch(Exception ex) { failure=ex; }
        });
        events.Start();reset.Start();events.Join();reset.Join();
        s.Reset();
        Check(failure==null && s.Held==0,"concurrent reset/getter/event updates complete without stale actions");
        Check(Down(s,K,true) && s.Held==Cross && Up(s,K,true) && s.Held==0,
            "fresh key lifecycle still works after concurrent operations");
    }

    static int Main()
    {
        EnableDuringPress(); FocusAndPause(); ModifiersAndSyntheticEvents();
        IndependentKeysAndBadEvents(); ConcurrentResetAndEvents();
        Console.WriteLine("KEYBOARD_INPUT_STATE_PASS checks="+checks+"; pure event sequences, no Windows input APIs");
        return 0;
    }
}
