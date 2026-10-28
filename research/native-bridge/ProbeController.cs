using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// The native DLL initially observes only the explicitly identified DS4 handle.
// No general-purpose memory dump, driver installation or PSN access is used.
static class ProbeController {
    const int Size = 304;
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, UIntPtr size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool VirtualFreeEx(IntPtr process, IntPtr address, UIntPtr size, uint type);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] data, UIntPtr length, out UIntPtr written);
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr CreateRemoteThread(IntPtr process, IntPtr attributes, UIntPtr stackSize, IntPtr start, IntPtr argument, uint flags, out uint tid);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetExitCodeThread(IntPtr thread, out uint result);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi, SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetProcessTimes(IntPtr process, out long created, out long exited, out long kernel, out long user);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint GetProcessId(IntPtr process);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetProcessMitigationPolicy(IntPtr process, int policy, out uint flags, UIntPtr size);

    static Exception Error(string operation) { return new Win32Exception(Marshal.GetLastWin32Error(), operation); }
    static IntPtr Module(Process process, string filename) {
        process.Refresh();
        foreach (ProcessModule m in process.Modules)
            if (String.Equals(Path.GetFileName(m.FileName), filename, StringComparison.OrdinalIgnoreCase)) return m.BaseAddress;
        throw new InvalidOperationException("Target module not loaded: " + filename);
    }
    static void EnsureProbeNotLoaded(Process process, string path) {
        process.Refresh();
        foreach (ProcessModule module in process.Modules)
            if (String.Equals(Path.GetFileName(module.FileName), Path.GetFileName(path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A probe DLL with this filename is already loaded. It stays pinned until target exit; restart the target or use a distinct build filename before another probe.");
    }
    static IntPtr ProbeModule(Process process, string path) {
        process.Refresh();
        foreach (ProcessModule module in process.Modules)
            if (String.Equals(Path.GetFullPath(module.FileName), path, StringComparison.OrdinalIgnoreCase)) return module.BaseAddress;
        throw new InvalidOperationException("The exact probe DLL path was not loaded in the target.");
    }
    static void ValidateOpenedProcess(IntPtr process, int expectedPid, long expectedStart, string expectedPath) {
        long created, exited, kernel, user;
        if (!GetProcessTimes(process,out created,out exited,out kernel,out user)) throw Error("GetProcessTimes");
        StringBuilder path = new StringBuilder(32768); uint length=(uint)path.Capacity;
        if (!QueryFullProcessImageName(process,0,path,ref length)) throw Error("QueryFullProcessImageName");
        if (GetProcessId(process)!=(uint)expectedPid || created!=expectedStart ||
            !String.Equals(Path.GetFullPath(path.ToString()),Path.GetFullPath(expectedPath),StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Opened process no longer matches the validated target PID, start time and path.");
    }
    static void CheckMitigations(IntPtr process) {
        uint dynamicCode, signature;
        if (!GetProcessMitigationPolicy(process,2,out dynamicCode,(UIntPtr)4)) throw Error("Read DynamicCodePolicy");
        if (!GetProcessMitigationPolicy(process,8,out signature,(UIntPtr)4)) throw Error("Read SignaturePolicy");
        Console.WriteLine("Mitigations: DynamicCodePolicy=0x"+dynamicCode.ToString("X8")+" SignaturePolicy=0x"+signature.ToString("X8"));
        if ((dynamicCode&1)!=0)
            throw new InvalidOperationException("Target prohibits dynamic code modification. Probe not injected; policy is unchanged.");
        // Microsoft-only, Store-only and Microsoft/Store/WHQL opt-in all
        // exclude this unsigned self-built DLL. Audit flags alone do not.
        if ((signature&7)!=0)
            throw new InvalidOperationException("Target enforces a binary-signature policy incompatible with this probe. Probe not injected; policy is unchanged.");
    }
    static IntPtr RemoteSystemFunction(Process target, string name) {
        IntPtr address = GetProcAddress(GetModuleHandle("kernel32.dll"), name);
        if (address == IntPtr.Zero) throw Error(name);
        using (Process current = Process.GetCurrentProcess()) {
            foreach (ProcessModule m in current.Modules) {
                long delta = address.ToInt64() - m.BaseAddress.ToInt64();
                if (delta >= 0 && delta < m.ModuleMemorySize)
                    return new IntPtr(Module(target, Path.GetFileName(m.FileName)).ToInt64() + delta);
            }
        }
        throw new InvalidOperationException("Cannot resolve system function owner.");
    }
    static uint RemoteCall(IntPtr process, IntPtr entry, string argument) {
        byte[] bytes = Encoding.Unicode.GetBytes(argument + "\0");
        IntPtr memory = VirtualAllocEx(process, IntPtr.Zero, (UIntPtr)bytes.Length, 0x3000, 0x04);
        if (memory == IntPtr.Zero) throw Error("VirtualAllocEx");
        IntPtr thread = IntPtr.Zero;
        bool completed = true;
        try {
            UIntPtr written;
            if (!WriteProcessMemory(process, memory, bytes, (UIntPtr)bytes.Length, out written) || written.ToUInt64() != (ulong)bytes.Length)
                throw Error("WriteProcessMemory argument");
            uint tid;
            thread = CreateRemoteThread(process, IntPtr.Zero, UIntPtr.Zero, entry, memory, 0, out tid);
            if (thread == IntPtr.Zero) throw Error("CreateRemoteThread");
            completed = false;
            uint wait = WaitForSingleObject(thread, 10000);
            if (wait != 0) throw new InvalidOperationException("Target call did not finish (wait=" + wait + "). Its argument is retained to avoid freeing live memory.");
            completed = true;
            uint result;
            if (!GetExitCodeThread(thread, out result)) throw Error("GetExitCodeThread");
            return result;
        } finally {
            if (thread != IntPtr.Zero) CloseHandle(thread);
            if (completed) VirtualFreeEx(process, memory, UIntPtr.Zero, 0x8000);
        }
    }
    static string Location(Process p, long address) {
        if (address == 0) return "none";
        foreach (ProcessModule m in p.Modules) {
            long offset = address - m.BaseAddress.ToInt64();
            if (offset >= 0 && offset < m.ModuleMemorySize) return Path.GetFileName(m.FileName) + "+0x" + offset.ToString("X");
        }
        return "unresolved module";
    }
    static void Snapshot(MemoryMappedViewAccessor view, Process target) {
        string[] labels = {"ntRead","ntSuccess","ntPending","ntFailure","readFile","overlapped","iocp","hidInput","hidParse","validReport","merged","otherCompletion","readFileEx","wait","hooks","reserved"};
        StringBuilder line = new StringBuilder();
        line.Append("state=" + view.ReadInt32(20) + " error=" + view.ReadInt32(24));
        for (int i = 0; i < labels.Length; i++) line.Append(" " + labels[i] + "=" + view.ReadInt32(144+i*4));
        line.Append(" ntStatus=0x" + view.ReadUInt32(272).ToString("X8") + " reportLength=" + view.ReadUInt32(276));
        Console.WriteLine(line);
        for (int i=0;i<8;i++) {
            long address = view.ReadInt64(208+i*8);
            if (address != 0) Console.WriteLine("caller["+i+"]="+Location(target,address));
        }
    }
    static int Main(string[] args) {
        if (args.Length < 4) { Console.Error.WriteLine("Usage: ProbeController.exe PID DLL DS4_HANDLE_HEX MODE_HEX [SECONDS] [test]"); return 2; }
        try {
            if (IntPtr.Size != 8) throw new InvalidOperationException("Build x64.");
            int pid = Int32.Parse(args[0]);
            string dll = Path.GetFullPath(args[1]);
            ulong handle = Convert.ToUInt64(args[2].Replace("0x",""),16);
            uint mode = Convert.ToUInt32(args[3].Replace("0x",""),16);
            int seconds = args.Length > 4 ? Int32.Parse(args[4]) : 30;
            bool test = args.Length > 5 && args[5] == "test";
            if (seconds < 1 || seconds > 120) throw new ArgumentOutOfRangeException("SECONDS");
            if (!File.Exists(dll)) throw new FileNotFoundException("Probe DLL",dll);
            using (Process target = Process.GetProcessById(pid)) {
                string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Sony", "PS Remote Play", "RemotePlay.exe");
                string targetPath = target.MainModule.FileName;
                long targetStart = target.StartTime.ToUniversalTime().ToFileTimeUtc();
                if (test ? !String.Equals(Path.GetFileName(targetPath),"pskb-probe-test.exe",StringComparison.OrdinalIgnoreCase) : !String.Equals(targetPath,expected,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Only the installed Sony RemotePlay.exe or the explicit test host is allowed.");
                EnsureProbeNotLoaded(target,dll);
                string mapName = "Local\\PSKeyboardProbe-"+pid+"-"+Guid.NewGuid().ToString("N");
                using (MemoryMappedFile map = MemoryMappedFile.CreateNew(mapName,Size))
                using (MemoryMappedViewAccessor view = map.CreateViewAccessor(0,Size)) {
                    view.Write(0,0x504B4231u); view.Write(4,1u); view.Write(8,(uint)Size); view.Write(12,(uint)pid);
                    view.Write(32,(long)GetTickCount64()); view.Write(40,handle); view.Write(104,mode); view.Write(136,1u); view.Write(140,test?1u:0u);
                    IntPtr process = OpenProcess(0x0002|0x0008|0x0010|0x0020|0x0400,false,pid);
                    if (process == IntPtr.Zero) throw Error("OpenProcess");
                    Timer heartbeat = null;
                    bool startRequested = false;
                    int finalState = 0;
                    try {
                        ValidateOpenedProcess(process,pid,targetStart,targetPath);
                        CheckMitigations(process);
                        heartbeat = new Timer(delegate { view.Write(32,(long)GetTickCount64()); },null,0,100);
                        IntPtr local = LoadLibraryEx(dll,IntPtr.Zero,1); // DONT_RESOLVE_DLL_REFERENCES: do not execute DllMain locally.
                        if (local == IntPtr.Zero) throw Error("Inspect probe export");
                        long rva;
                        try {
                            IntPtr export = GetProcAddress(local,"StartProbe");
                            if (export == IntPtr.Zero) throw Error("StartProbe export");
                            rva = export.ToInt64()-local.ToInt64();
                        } finally { FreeLibrary(local); }
                        Console.WriteLine("Target="+Path.GetFileName(targetPath)+" pid="+pid+" handle=0x"+handle.ToString("X")+" mode=0x"+mode.ToString("X"));
                        if (target.HasExited) throw new InvalidOperationException("Target exited before loading probe.");
                        ValidateOpenedProcess(process,pid,targetStart,targetPath);
                        uint loadResult=RemoteCall(process,RemoteSystemFunction(target,"LoadLibraryW"),dll);
                        Console.WriteLine("LoadLibraryW thread return=0x"+loadResult.ToString("X8"));
                        IntPtr loaded = ProbeModule(target,dll);
                        startRequested = true;
                        uint start = RemoteCall(process,new IntPtr(loaded.ToInt64()+rva),mapName);
                        Console.WriteLine("StartProbe="+start);
                        if (start != 0) throw new InvalidOperationException("Probe initialization failed: "+start);
                        for (int i=0;i<seconds*10;i++) {
                            if (target.HasExited) throw new InvalidOperationException("Target exited during observation.");
                            if(i%50==0) Snapshot(view,target);
                            Thread.Sleep(100);
                        }
                        Snapshot(view,target);
                    } finally {
                        try {
                            view.Write(16,2);
                            if (startRequested) {
                                Stopwatch stopping = Stopwatch.StartNew();
                                do {
                                    finalState=view.ReadInt32(20);
                                    if (finalState==4 || finalState==3 || target.HasExited) break;
                                    Thread.Sleep(50);
                                } while (stopping.ElapsedMilliseconds<5000);
                                finalState=view.ReadInt32(20);
                            }
                        } finally {
                            // Timer.Dispose alone does not drain queued callbacks.
                            // Keep this view alive until every heartbeat writer ends.
                            if (heartbeat!=null) using (ManualResetEvent drained=new ManualResetEvent(false)) {
                                if (heartbeat.Dispose(drained)) drained.WaitOne();
                            }
                            CloseHandle(process);
                        }
                    }
                    if (finalState!=4)
                        throw new InvalidOperationException("Stop requested, but hook removal was not confirmed; final state="+finalState+" error="+view.ReadInt32(24)+". DLL remains loaded; no inert-state guarantee is made.");
                    Console.WriteLine("Stop confirmed (state=4). Native DLL remains loaded, with hooks disabled, until target exits.");
                }
            }
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex.GetType().Name+": "+ex.Message); return 1; }
    }
}
