using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PSKeyboard {
    public static class KeyboardMapping {
        [StructLayout(LayoutKind.Sequential)]
        private struct HeldKeys { public uint Held; public int Enabled; }

        [DllImport("pskb-keyboard-merge.dll", CallingConvention=CallingConvention.Cdecl, ExactSpelling=true)]
        private static extern int pskb_merge_keyboard([In] byte[] source, UIntPtr sourceLength,
            ref HeldKeys keys, [Out] byte[] output, UIntPtr outputLength);

        // Virtual-key codes, not characters typed into a text field.
        private static readonly Dictionary<int,uint> Bindings = new Dictionary<int,uint> {
            {0x57,1u<<0},{0x53,1u<<1},{0x41,1u<<2},{0x44,1u<<3},
            {0x26,1u<<4},{0x28,1u<<5},{0x25,1u<<6},{0x27,1u<<7},
            {0x4B,1u<<8},{0x4C,1u<<9},{0x4A,1u<<10},{0x49,1u<<11},
            {0x51,1u<<12},{0x45,1u<<13},{0x55,1u<<14},{0x4F,1u<<15},
            {0x08,1u<<16},{0x0D,1u<<17},{0x31,1u<<18},{0x32,1u<<19},
            {0x50,1u<<20},{0x54,1u<<21}
        };

        public static uint ActionForKey(int key) {
            uint action; return Bindings.TryGetValue(key,out action) ? action : 0;
        }

        public static byte[] Frame(uint held, bool enabled) {
            byte[] physical = new byte[64], report = new byte[64];
            // This backend creates a separate virtual DS4. No physical input is
            // read here; the template is fresh and neutral for every frame.
            physical[0]=1; physical[1]=physical[2]=physical[3]=physical[4]=128; physical[5]=8;
            HeldKeys keys = new HeldKeys {Held=held, Enabled=enabled?1:0};
            int result=pskb_merge_keyboard(physical,(UIntPtr)64,ref keys,report,(UIntPtr)64);
            if(result!=0) throw new InvalidOperationException("Keyboard mapping failed: "+result);
            byte[] wire = new byte[31];
            for(int i=0;i<4;i++) wire[i]=unchecked((byte)(report[i+1]-128));
            ushort buttons=(ushort)((report[5]&0xF0)|(report[6]<<8)|(report[7]&3));
            wire[4]=(byte)buttons; wire[5]=(byte)(buttons>>8);
            byte[] hats={1,9,8,10,2,6,4,5,0};
            wire[6]=hats[report[5]&15]; wire[7]=report[8]; wire[8]=report[9];
            // VIIPER's documented resting gravity is -8192 on AccelZ.
            wire[29]=0; wire[30]=0xE0;
            return wire;
        }
    }
}
