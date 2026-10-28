// Read-only handle metadata probe for the installed Sony Remote Play process.
// No process-memory reads, HID input reports, output reports, or file names are read.
// Each candidate is checked in a bounded child process using a zero-access duplicate.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class RemotePlayHandleProbe
{
    const uint QueryAndDuplicate = 0x440;
    const int ProcessHandleInformation = 51;
    const uint StatusLengthMismatch = 0xC0000004;
    const uint AllowedFileAccess = 0x0012019F;

    [StructLayout(LayoutKind.Sequential)]
    struct Entry
    {
        public IntPtr Handle;
        public UIntPtr HandleCount, PointerCount;
        public uint GrantedAccess, ObjectTypeIndex, Attributes, Reserved;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct Attributes { public uint Size; public ushort Vendor, Product, Version; }
    [StructLayout(LayoutKind.Sequential)]
    struct IoStatus { public IntPtr Status; public UIntPtr Information; }
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint rights, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr sourceHandle, IntPtr targetProcess, out IntPtr targetHandle, uint access, bool inherit, uint options);
    [DllImport("kernel32.dll", SetLastError=true)] static extern uint GetFileType(IntPtr handle);
    [DllImport("ntdll.dll")] static extern uint NtQueryInformationProcess(IntPtr process, int infoClass, IntPtr info, uint size, out uint needed);
    [DllImport("ntdll.dll")] static extern uint NtQueryInformationFile(IntPtr file, out IoStatus status, out uint mode, uint size, int infoClass);
    [DllImport("hid.dll", SetLastError=true)]
    [return: MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetAttributes(IntPtr handle, ref Attributes attributes);

    static List<Entry> Snapshot(IntPtr process)
    {
        uint size = 65536;
        for (int attempt=0; attempt<8; attempt++)
        {
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                uint needed;
                uint status = NtQueryInformationProcess(process, ProcessHandleInformation, buffer, size, out needed);
                if (status == StatusLengthMismatch) { size = Math.Max(size*2, needed+4096); continue; }
                if (status != 0) throw new InvalidOperationException("Handle snapshot NTSTATUS 0x" + status.ToString("X8"));
                ulong count = unchecked((ulong)Marshal.ReadInt64(buffer));
                int stride = Marshal.SizeOf(typeof(Entry));
                if (count > (size-16)/(uint)stride) throw new InvalidDataException("Invalid snapshot length");
                List<Entry> entries = new List<Entry>();
                for (ulong i=0; i<count; i++)
                    entries.Add((Entry)Marshal.PtrToStructure(IntPtr.Add(buffer, checked(16+(int)i*stride)), typeof(Entry)));
                return entries;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new InvalidOperationException("Handle table changed too quickly");
    }

    static Process ValidateTarget(int pid, long expectedStart)
    {
        Process process = Process.GetProcessById(pid);
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Sony\PS Remote Play\RemotePlay.exe");
        if (!string.Equals(Path.GetFullPath(process.MainModule.FileName), Path.GetFullPath(expected), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Target is not the installed Sony Remote Play executable");
        if (expectedStart != 0 && process.StartTime.ToUniversalTime().Ticks != expectedStart)
            throw new InvalidOperationException("Target process changed");
        return process;
    }

    static int CheckHandle(string[] args)
    {
        int pid = int.Parse(args[1], CultureInfo.InvariantCulture);
        long start = long.Parse(args[2], CultureInfo.InvariantCulture);
        long value = long.Parse(args[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        uint fileType = uint.Parse(args[4], CultureInfo.InvariantCulture);
        using (Process target = ValidateTarget(pid, start))
        {
            IntPtr process = OpenProcess(QueryAndDuplicate, false, pid);
            if (process == IntPtr.Zero) return 3;
            try
            {
                Entry selected = new Entry(); bool found=false;
                foreach (Entry entry in Snapshot(process))
                    if (entry.Handle.ToInt64() == value && entry.ObjectTypeIndex == fileType && (entry.GrantedAccess & ~AllowedFileAccess) == 0)
                    { selected=entry; found=true; break; }
                if (!found) return 4;
                IntPtr duplicate;
                // No DUPLICATE_CLOSE_SOURCE and no DUPLICATE_SAME_ACCESS. The
                // local duplicate receives zero data-read/write access rights.
                if (!DuplicateHandle(process, selected.Handle, GetCurrentProcess(), out duplicate, 0, false, 0)) return 5;
                try
                {
                    uint kind = GetFileType(duplicate);
                    if (kind == 1 || kind == 3) return 6; // Skip disk files and pipes.
                    Attributes attr = new Attributes(); attr.Size=(uint)Marshal.SizeOf(typeof(Attributes));
                    if (HidD_GetAttributes(duplicate, ref attr) && attr.Vendor==0x054C && (attr.Product==0x05C4 || attr.Product==0x09CC))
                    {
                        IoStatus ioStatus; uint mode;
                        uint modeStatus=NtQueryInformationFile(duplicate, out ioStatus, out mode, 4, 16);
                        if (modeStatus==0xC0000022)
                        {
                            IntPtr metadata;
                            // On access denial, request only FILE_READ_ATTRIBUTES
                            // after VID/PID has identified this as the target DS4.
                            if (DuplicateHandle(process, selected.Handle, GetCurrentProcess(), out metadata, 0x80, false, 0))
                                try { modeStatus=NtQueryInformationFile(metadata, out ioStatus, out mode, 4, 16); }
                                finally { CloseHandle(metadata); }
                        }
                        Console.WriteLine("DS4 handle=0x{0:X} access=0x{1:X8} VID=054C PID={6:X4} modeStatus=0x{2:X8} mode=0x{3:X8} syncAlert={4} syncNonAlert={5}",
                            value, selected.GrantedAccess, modeStatus, mode, modeStatus==0 && (mode&0x10)!=0, modeStatus==0 && (mode&0x20)!=0, attr.Product);
                        return 0;
                    }
                    return 7;
                }
                finally { CloseHandle(duplicate); }
            }
            finally { CloseHandle(process); }
        }
    }

    static int Main(string[] args)
    {
        try
        {
            if (IntPtr.Size != 8) throw new InvalidOperationException("Build this probe for x64");
            if (args.Length==5 && args[0]=="--check-handle") return CheckHandle(args);
            if (args.Length!=1) { Console.Error.WriteLine("Usage: RemotePlayHandleProbe.exe <RemotePlay PID>"); return 2; }
            int pid=int.Parse(args[0], CultureInfo.InvariantCulture);
            using (Process target=ValidateTarget(pid, 0))
            {
                long start=target.StartTime.ToUniversalTime().Ticks;
                string self=Process.GetCurrentProcess().MainModule.FileName;
                uint fileType=0;
                // Determine the OS's current File object type index without
                // assuming a version-dependent numeric constant.
                using (FileStream file=File.OpenRead(self))
                    foreach (Entry entry in Snapshot(GetCurrentProcess()))
                        if (entry.Handle==file.SafeFileHandle.DangerousGetHandle()) { fileType=entry.ObjectTypeIndex; break; }
                if (fileType==0) throw new InvalidOperationException("File object type not found");
                IntPtr process=OpenProcess(QueryAndDuplicate, false, pid);
                if (process==IntPtr.Zero) throw new InvalidOperationException("OpenProcess failed: " + Marshal.GetLastWin32Error());
                List<Entry> entries;
                try { entries=Snapshot(process); } finally { CloseHandle(process); }
                int candidates=0, matches=0, timeouts=0;
                foreach (Entry entry in entries)
                {
                    // Common sockets have broader access. Exclude such handles
                    // before duplication, as DuplicateHandle is not a socket API.
                    if (entry.ObjectTypeIndex!=fileType || (entry.GrantedAccess & ~AllowedFileAccess)!=0) continue;
                    if (++candidates > 100) throw new InvalidOperationException("Candidate bound exceeded");
                    ProcessStartInfo info=new ProcessStartInfo(self,
                        "--check-handle " + pid + " " + start + " " + entry.Handle.ToInt64().ToString("X") + " " + fileType);
                    info.UseShellExecute=false; info.CreateNoWindow=true; info.RedirectStandardOutput=true; info.RedirectStandardError=true;
                    using (Process worker=Process.Start(info))
                    {
                        if (!worker.WaitForExit(1500)) { worker.Kill(); worker.WaitForExit(1000); timeouts++; continue; }
                        string output=worker.StandardOutput.ReadToEnd().Trim();
                        if (worker.ExitCode==0 && output.StartsWith("DS4 handle=", StringComparison.Ordinal)) { matches++; Console.WriteLine(output); }
                    }
                }
                Console.WriteLine("Probe: PID={0}, candidates={1}, DS4 matches={2}, timeouts={3}; no reports read", pid, candidates, matches, timeouts);
                return matches>0 ? 0 : 1;
            }
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
