// Read-only existing imported-controller watcher. No keyboard capture or input writes.
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

// All scope policy is independent of Windows APIs so rejection paths can be tested
// without opening a HID interface, querying USBip, or touching a running session.
internal sealed class ObserverScope
{
    public ushort Product;
    public int Port,Seconds;
    public string Url,Kind;
    public string TargetId { get { return "vid_054c&pid_"+Product.ToString("x4"); } }
    internal static bool TryParse(string[] args,out ObserverScope scope)
    {
        scope=null;
        if(args.Length!=4)return false;
        ushort product;int service;
        if(args[0]=="--observe-existing-virtual"){product=0x09cc;service=33241;}
        else if(args[0]=="--observe-existing-physical-relay"){product=0x05c4;service=33251;}
        else return false;
        int port,seconds;
        if(!Int32.TryParse(args[1],out port)||port<1||port>255 ||
            !Regex.IsMatch(args[2],@"\Ausbip://127\.0\.0\.1:"+service+@"/[1-9]\d*-[1-9]\d*\z") ||
            !Int32.TryParse(args[3],out seconds)||seconds<1||seconds>120)return false;
        scope=new ObserverScope{Product=product,Port=port,Seconds=seconds,Url=args[2],
            Kind=product==0x05c4?"physical-relay":"virtual"};
        return true;
    }
    internal bool MatchesUsbInstance(string id)
    {
        string prefix=@"USB\"+TargetId;
        return id.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) &&
            id.Length>prefix.Length && (id[prefix.Length]=='&'||id[prefix.Length]=='\\');
    }
    internal bool MatchesImportedAncestry(IList<string> ids,IList<string[]> hardware)
    {
        if(ids.Count!=hardware.Count)return false;
        bool usbIdentity=false;
        for(int i=0;i<ids.Count;i++) {
            if(MatchesUsbInstance(ids[i]))usbIdentity=true;
            foreach(string hardwareId in hardware[i])
                if(String.Equals(hardwareId,@"ROOT\USBIP_WIN2\UDE",StringComparison.OrdinalIgnoreCase))return usbIdentity;
        }
        return false;
    }
    internal bool MatchesPortOutput(string output)
    {
        int count=0,current=0,urls=0;bool exact=false;
        foreach(string line in output.Split('\n')) {
            Match m=Regex.Match(line,@"^Port\s+(\d+):");
            if(m.Success) {
                count++;
                if(!Int32.TryParse(m.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out current))return false;
            }
            string trimmed=line.Trim();
            if(trimmed.StartsWith("->",StringComparison.Ordinal)) {
                string value=trimmed.Substring(2).TrimStart();
                if(value.StartsWith("usbip://",StringComparison.Ordinal)) {
                    urls++;
                    if(current==Port && String.Equals(trimmed,"-> "+Url,StringComparison.Ordinal))exact=true;
                } else if(!value.StartsWith("remote bus/dev: ",StringComparison.Ordinal) &&
                    !value.StartsWith("serial:",StringComparison.Ordinal) &&
                    !value.StartsWith("mode: ",StringComparison.Ordinal))return false;
            }
        }
        return count==1 && urls==1 && exact;
    }
}

internal static class WindowsHidObserver
{
    const string UsbIpExe = @"C:\Program Files\USBip\usbip.exe";
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

    static HashSet<string> Paths(ObserverScope scope)
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
                if(path.IndexOf(scope.TargetId,StringComparison.OrdinalIgnoreCase)>=0) result.Add(path);
            return result;
        }
        throw new IOException("HID interface list changed repeatedly");
    }
    static bool IsUsbIpDescendant(string path,ObserverScope scope)
    {
        var key=new PropertyKey{Format=new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"),Id=256};
        byte[] buffer=new byte[4096];uint size=(uint)buffer.Length,type;
        int propertyResult=CM_Get_Device_Interface_PropertyW(path,ref key,out type,buffer,ref size,0);
        if(propertyResult!=0 || type!=0x12 || size<2 || size>buffer.Length || size%2!=0)return false;
        uint node;
        if(CM_Locate_DevNodeW(out node,Encoding.Unicode.GetString(buffer,0,(int)size).TrimEnd('\0'),0)!=0) return false;
        var ids=new List<string>();var hardwareIds=new List<string[]>();
        for(int depth=0;depth<16;depth++) {
            var id=new StringBuilder(1024);
            if(CM_Get_Device_IDW(node,id,id.Capacity,0)!=0) return false;
            ids.Add(id.ToString());
            // PNP instance id is ROOT\USB\0000 on this system. Match the
            // controller's HardwareID, never infer it from the instance name.
            byte[] hardware=new byte[4096];uint hardwareSize=(uint)hardware.Length,hardwareType;
            string[] currentHardware=new string[0];
            if(CM_Get_DevNode_Registry_PropertyW(node,2,out hardwareType,hardware,ref hardwareSize,0)==0 &&
                hardwareType==7 && hardwareSize<=hardware.Length && hardwareSize%2==0) {
                currentHardware=Encoding.Unicode.GetString(hardware,0,(int)hardwareSize).Split('\0');
            }
            hardwareIds.Add(currentHardware);
            if(scope.MatchesImportedAncestry(ids,hardwareIds))return true;
            uint parent;if(CM_Get_Parent(out parent,node,0)!=0) return false;node=parent;
        }
        return false;
    }
    static void ValidateHid(SafeFileHandle handle,ObserverScope scope)
    {
        var attributes=new Attributes{Size=(uint)Marshal.SizeOf(typeof(Attributes))};
        if(!HidD_GetAttributes(handle,ref attributes)||attributes.Vendor!=0x054c||attributes.Product!=scope.Product)
            throw new IOException("Existing imported HID identity mismatch");
        IntPtr data;
        if(!HidD_GetPreparsedData(handle,out data))throw new Win32Exception(Marshal.GetLastWin32Error(),"HID metadata");
        try {
            Caps caps;
            if(HidP_GetCaps(data,out caps)!=0x00110000 || caps.InputLength!=64 || caps.UsagePage!=1 || caps.Usage!=5)
                throw new IOException("Expected 64-byte gamepad input collection");
        }finally{HidD_FreePreparsedData(data);}
        Console.WriteLine("VALIDATED "+scope.Kind+" DS4 054C:"+scope.Product.ToString("X4")+"; input=64; usage=gamepad");
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
    static void CheckPort(ObserverScope scope)
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
        if(!scope.MatchesPortOutput(output.ToString()))throw new IOException("Expected exactly the one specified existing USBip port; observation refused");
    }
    static int Main(string[] args)
    {
        ObserverScope scope;
        if(!ObserverScope.TryParse(args,out scope)) {
            Console.Error.WriteLine("Usage: WindowsHidObserver.exe --observe-existing-virtual PORT usbip://127.0.0.1:33241/BUS-DEVICE SECONDS(1..120)");
            Console.Error.WriteLine("   or: WindowsHidObserver.exe --observe-existing-physical-relay PORT usbip://127.0.0.1:33251/BUS-DEVICE SECONDS(1..120)");return 2;
        }
        int reports=0,changes=0,nonneutral=0,idle=0;var watch=Stopwatch.StartNew();
        try {
            if(IntPtr.Size!=8)throw new IOException("Requires x64");
            CheckPort(scope);string selected=null;
            foreach(string path in Paths(scope)) {
                if(!IsUsbIpDescendant(path,scope))continue;
                if(selected!=null)throw new IOException("More than one matching imported DS4 HID collection; refusing ambiguous read");
                selected=path;
            }
            if(selected==null)throw new IOException("No matching existing imported DS4 HID collection");
            using(SafeFileHandle hid=CreateFile(selected,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)) {
                if(hid.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Read-only imported HID open");
                ValidateHid(hid,scope);CheckPort(scope);watch.Restart();
                Console.WriteLine("READY seconds="+scope.Seconds+" mode="+scope.Kind+" existing_only=true input_writes=false keyboard_capture=false");Console.Out.Flush();
                string previous=null;long nextCheck=5000;
                while(watch.ElapsedMilliseconds<scope.Seconds*1000L) {
                    if(watch.ElapsedMilliseconds>=nextCheck){CheckPort(scope);nextCheck=watch.ElapsedMilliseconds+5000;}
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
