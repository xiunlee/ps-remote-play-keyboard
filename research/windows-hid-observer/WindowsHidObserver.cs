// Read-only existing virtual-controller watcher. No keyboard capture or input writes.
// HID enumeration and bounded reads are adapted from our WindowsHidTest.cs.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

internal static class WindowsHidObserver
{
    const string UsbIpExe = @"C:\Program Files\USBip\usbip.exe";
    const string TargetId = "vid_054c&pid_09cc";
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] struct Attributes { public uint Size; public ushort Vendor,Product,Version; }
    [StructLayout(LayoutKind.Sequential)] struct Caps {
        public ushort Usage,UsagePage,InputLength,OutputLength,FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray,SizeConst=17)] public ushort[] Reserved;
        public ushort LinkNodes,InputButtons,InputValues,InputIndices,OutputButtons,OutputValues,OutputIndices,FeatureButtons,FeatureValues,FeatureIndices;
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeOverlapped {
        public UIntPtr Internal,InternalHigh; public uint Offset,OffsetHigh; public IntPtr Event;
    }
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_Interface_List_SizeW(out uint size,ref Guid guid,string id,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_Interface_ListW(ref Guid guid,string id,[Out] char[] buffer,uint size,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_Interface_PropertyW(string path,ref PropertyKey key,out uint type,byte[] buffer,ref uint size,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Locate_DevNodeW(out uint node,string id,uint flags);
    [DllImport("cfgmgr32.dll")] static extern int CM_Get_Parent(out uint parent,uint node,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_Device_IDW(uint node,StringBuilder id,int length,uint flags);
    [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)] static extern int CM_Get_DevNode_Registry_PropertyW(uint node,uint property,out uint type,byte[] buffer,ref uint size,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint sharing,IntPtr security,uint disposition,uint flags,IntPtr template);
    [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetAttributes(SafeFileHandle device,ref Attributes attributes);
    [DllImport("hid.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.U1)] static extern bool HidD_GetPreparsedData(SafeFileHandle device,out IntPtr data);
    [DllImport("hid.dll")] [return:MarshalAs(UnmanagedType.U1)] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data,out Caps caps);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateEvent(IntPtr attributes,bool manual,bool initial,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadFile(SafeFileHandle handle,IntPtr buffer,uint length,IntPtr bytesRead,IntPtr overlapped);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetOverlappedResult(SafeFileHandle handle,IntPtr overlapped,out uint transferred,bool wait);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool CancelIoEx(SafeFileHandle handle,IntPtr overlapped);
    [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint timeout);

    static HashSet<string> Paths()
    {
        Guid guid=new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        for(int retry=0;retry<3;retry++) {
            uint size;
            int status=CM_Get_Device_Interface_List_SizeW(out size,ref guid,null,0);
            if(status!=0 || size>1048576) throw new IOException("HID interface size failed: "+status);
            char[] buffer=new char[size];
            status=CM_Get_Device_Interface_ListW(ref guid,null,buffer,size,0);
            if(status==26) continue; // CR_BUFFER_SMALL: PnP changed between queries.
            if(status!=0) throw new IOException("HID interface enumeration failed: "+status);
            var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string path in new string(buffer).Split('\0'))
                if(path.IndexOf(TargetId,StringComparison.OrdinalIgnoreCase)>=0) result.Add(path);
            return result;
        }
        throw new IOException("HID interface list changed repeatedly");
    }
    static bool IsUsbIpDescendant(string path,bool diagnose)
    {
        var key=new PropertyKey{Format=new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"),Id=256};
        byte[] buffer=new byte[4096];uint size=(uint)buffer.Length,type;
        int propertyResult=CM_Get_Device_Interface_PropertyW(path,ref key,out type,buffer,ref size,0);
        if(propertyResult!=0 || type!=0x12 || size<2 || size>buffer.Length || size%2!=0) {
            if(diagnose)Console.WriteLine("DIAGNOSTIC HID instance property status="+propertyResult+" type="+type+" size="+size);
            return false;
        }
        uint node;
        if(CM_Locate_DevNodeW(out node,Encoding.Unicode.GetString(buffer,0,(int)size).TrimEnd('\0'),0)!=0) return false;
        bool usbIdentity=false;
        for(int depth=0;depth<16;depth++) {
            var id=new StringBuilder(1024);
            if(CM_Get_Device_IDW(node,id,id.Capacity,0)!=0) return false;
            string value=id.ToString();
            if(diagnose){string[] parts=value.Split('\\');Console.WriteLine("DIAGNOSTIC ancestor["+depth+"]="+(parts.Length>1?parts[0]+"\\"+parts[1]:parts[0]));}
            if(value.StartsWith(@"USB\VID_054C&PID_09CC",StringComparison.OrdinalIgnoreCase)) usbIdentity=true;
            // PNP instance id is ROOT\USB\0000 on this system. Match the
            // controller's HardwareID, never infer it from the instance name.
            byte[] hardware=new byte[4096];uint hardwareSize=(uint)hardware.Length,hardwareType;
            if(CM_Get_DevNode_Registry_PropertyW(node,2,out hardwareType,hardware,ref hardwareSize,0)==0 &&
                hardwareType==7 && hardwareSize<=hardware.Length && hardwareSize%2==0) {
                foreach(string hardwareId in Encoding.Unicode.GetString(hardware,0,(int)hardwareSize).Split('\0'))
                    if(String.Equals(hardwareId,@"ROOT\USBIP_WIN2\UDE",StringComparison.OrdinalIgnoreCase)) {
                        if(diagnose)Console.WriteLine("DIAGNOSTIC ancestor hardware identity=ROOT\\USBIP_WIN2\\UDE");
                        return usbIdentity;
                    }
            }
            uint parent;if(CM_Get_Parent(out parent,node,0)!=0) return false;node=parent;
        }
        return false;
    }
    static void ValidateHid(SafeFileHandle handle)
    {
        var attributes=new Attributes{Size=(uint)Marshal.SizeOf(typeof(Attributes))};
        if(!HidD_GetAttributes(handle,ref attributes)||attributes.Vendor!=0x054c||attributes.Product!=0x09cc)
            throw new IOException("New HID identity mismatch");
        IntPtr data;
        if(!HidD_GetPreparsedData(handle,out data))throw new Win32Exception(Marshal.GetLastWin32Error(),"HID metadata");
        try {
            Caps caps;
            if(HidP_GetCaps(data,out caps)!=0x00110000 || caps.InputLength!=64 || caps.UsagePage!=1 || caps.Usage!=5)
                throw new IOException("Expected 64-byte gamepad input collection");
        }finally{HidD_FreePreparsedData(data);}
        Console.WriteLine("VALIDATED virtual DS4 054C:09CC; input=64; usage=gamepad");
    }
    static byte[] ReadReport(SafeFileHandle handle,uint timeout)
    {
        IntPtr buffer=IntPtr.Zero,overlapped=IntPtr.Zero,ev=IntPtr.Zero;
        bool completed=true;
        try {
            buffer=Marshal.AllocHGlobal(64);overlapped=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeOverlapped)));
            ev=CreateEvent(IntPtr.Zero,true,false,null);if(ev==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());
            Marshal.StructureToPtr(new NativeOverlapped{Event=ev},overlapped,false);
            bool immediate=ReadFile(handle,buffer,64,IntPtr.Zero,overlapped);
            int error=immediate?0:Marshal.GetLastWin32Error();
            if(!immediate && error!=997)throw new Win32Exception(error,"HID read submission");
            completed=false;
            if(!immediate && WaitForSingleObject(ev,timeout)!=0) {
                CancelIoEx(handle,overlapped);
                if(WaitForSingleObject(ev,3000)!=0)
                    throw new TimeoutException("HID cancellation did not complete; operation storage retained until process exit");
                uint cancelledBytes;
                bool cancelledResult=GetOverlappedResult(handle,overlapped,out cancelledBytes,false);
                int cancelledError=cancelledResult?0:Marshal.GetLastWin32Error();
                completed=cancelledResult||cancelledError!=996;
                if(!completed) throw new IOException("HID cancellation was not drained; stopping observer");
                return null;
            }
            uint length;
            bool ok=GetOverlappedResult(handle,overlapped,out length,false);
            int terminalError=ok?0:Marshal.GetLastWin32Error();
            completed=ok||terminalError!=996;
            if(!ok)throw new Win32Exception(terminalError,"HID read completion");
            if(length!=64)throw new IOException("HID report length was "+length+", expected 64");
            byte[] report=new byte[64];Marshal.Copy(buffer,report,0,64);
            if(report[0]!=1)throw new IOException("Unexpected HID report id");
            return report;
        }finally{
            // CancelIoEx is NOT completion. A still-pending request retains all
            // three allocations/handles until this short-lived process exits.
            if(completed){if(ev!=IntPtr.Zero)CloseHandle(ev);if(overlapped!=IntPtr.Zero)Marshal.FreeHGlobal(overlapped);if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);}
        }
    }
    static void CheckPort(int expectedPort,string expectedUrl)
    {
        var output=new StringBuilder();var error=new StringBuilder();
        var info=new ProcessStartInfo(UsbIpExe,"port"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var p=new Process{StartInfo=info}) {
            p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)lock(output)output.AppendLine(e.Data);};
            p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(error)error.AppendLine(e.Data);};
            if(!p.Start())throw new IOException("Could not query USBip port metadata");
            p.BeginOutputReadLine();p.BeginErrorReadLine();
            if(!p.WaitForExit(10000)){p.Kill();p.WaitForExit();throw new TimeoutException("USBip port query timed out");}
            p.WaitForExit();
            if(p.ExitCode!=0)throw new IOException("USBip port query failed");
        }
        int count=0,current=0;bool exact=false;
        foreach(string line in output.ToString().Split('\n')) {
            Match m=Regex.Match(line,@"^Port\s+(\d+):");
            if(m.Success){count++;current=Int32.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);}
            if(current==expectedPort && String.Equals(line.Trim(),"-> "+expectedUrl,StringComparison.Ordinal))exact=true;
        }
        if(count!=1 || !exact)throw new IOException("Expected exactly the one specified existing USBip port; no HID read attempted");
    }
    static int Main(string[] args)
    {
        int port,seconds;
        if(args.Length!=4 || args[0]!="--observe-existing-virtual" ||
            !Int32.TryParse(args[1],out port) || port<1 || port>255 ||
            !Regex.IsMatch(args[2],@"^usbip://127\.0\.0\.1:33241/[1-9]\d*-[1-9]\d*$") ||
            !Int32.TryParse(args[3],out seconds) || seconds<1 || seconds>120) {
            Console.Error.WriteLine("Usage: WindowsHidObserver.exe --observe-existing-virtual PORT usbip://127.0.0.1:33241/BUS-DEVICE SECONDS(1..120)");return 2;
        }
        int reports=0,changes=0,nonneutral=0,idle=0;var watch=Stopwatch.StartNew();
        try {
            if(IntPtr.Size!=8)throw new IOException("Requires x64");
            CheckPort(port,args[2]);string selected=null;
            foreach(string path in Paths()) {
                if(!IsUsbIpDescendant(path,false))continue;
                if(selected!=null)throw new IOException("More than one virtual DS4 HID collection; refusing ambiguous read");
                selected=path;
            }
            if(selected==null)throw new IOException("No matching existing virtual DS4 HID collection");
            using(SafeFileHandle hid=CreateFile(selected,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)) {
                if(hid.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Read-only virtual HID open");
                ValidateHid(hid);CheckPort(port,args[2]);watch.Restart();
                Console.WriteLine("READY seconds="+seconds+" existing_only=true input_writes=false keyboard_capture=false");Console.Out.Flush();
                string previous=null;long nextCheck=5000;
                while(watch.ElapsedMilliseconds<seconds*1000L) {
                    if(watch.ElapsedMilliseconds>=nextCheck){CheckPort(port,args[2]);nextCheck=watch.ElapsedMilliseconds+5000;}
                    byte[] report=ReadReport(hid,500);
                    if(report==null){idle++;continue;}
                    reports++;
                    ushort buttons=(ushort)((report[5]&0xf0)|(report[6]<<8)|(report[7]&3));
                    int hat=report[5]&15;
                    string state="buttons=0x"+buttons.ToString("X4")+" hat="+hat+" lx="+report[1]+" ly="+report[2];
                    if(buttons!=0 || hat!=8 || report[1]!=128 || report[2]!=128)nonneutral++;
                    if(state!=previous){changes++;Console.WriteLine("STATE t_ms="+watch.ElapsedMilliseconds+" "+state);Console.Out.Flush();previous=state;}
                }
            }
            Console.WriteLine("RESULT success=true elapsed_ms="+watch.ElapsedMilliseconds+" reports="+reports+" transitions="+changes+" nonneutral="+nonneutral+" idle_timeouts="+idle+" device_changed=false");return 0;
        } catch(Exception ex) {
            Console.Error.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message);
            Console.WriteLine("RESULT success=false elapsed_ms="+watch.ElapsedMilliseconds+" reports="+reports+" transitions="+changes+" nonneutral="+nonneutral+" device_changed=false");return 1;
        }
    }
}