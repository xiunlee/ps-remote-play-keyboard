// Windows acceptance of our keyboard mapping through VIIPER/USBIP.
// Opens only a NEW virtual DS4 (054C:09CC) with a USBIP_WIN2 ancestor.
// No Sony process access, keyboard capture, or physical-controller reads.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using PSKeyboard;

internal static class WindowsHidTest
{
    const string UsbIpExe = @"C:\Program Files\USBip\usbip.exe";
    const string TargetId = "vid_054c&pid_09cc";
    static int passed;
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
    sealed class CommandResult { public int Code; public string Output,Error; }
    static CommandResult Run(string arguments)
    {
        var info=new ProcessStartInfo(UsbIpExe,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var process=new Process{StartInfo=info}) {
            var output=new StringBuilder();var error=new StringBuilder();
            process.OutputDataReceived+=(sender,e)=>{if(e.Data!=null)lock(output)output.AppendLine(e.Data);};
            process.ErrorDataReceived+=(sender,e)=>{if(e.Data!=null)lock(error)error.AppendLine(e.Data);};
            if(!process.Start())throw new IOException("Could not start USBip command");
            process.BeginOutputReadLine();process.BeginErrorReadLine();
            if(!process.WaitForExit(10000)){process.Kill();process.WaitForExit();throw new TimeoutException("USBip command timed out");}
            process.WaitForExit();
            return new CommandResult{Code=process.ExitCode,Output=output.ToString(),Error=error.ToString()};
        }
    }
    static void RequireCommand(CommandResult result,string operation)
    {
        if(result.Code!=0)throw new IOException(operation+" failed: "+result.Error.Trim());
    }
    static bool PortMatches(int port,string expectedUrl)
    {
        var result=Run("port "+port.ToString(CultureInfo.InvariantCulture));
        if(result.Code!=0)return false;
        foreach(string line in result.Output.Split('\n'))
            if(String.Equals(line.Trim(),"-> "+expectedUrl,StringComparison.Ordinal))return true;
        return false;
    }
    static int FindOwnedPort(string expectedUrl)
    {
        var result=Run("port");RequireCommand(result,"Read USBip ports");
        int current=0,found=0;
        foreach(string line in result.Output.Split('\n')) {
            Match m=Regex.Match(line,@"^Port\s+(\d+):");
            if(m.Success)current=Int32.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);
            if(String.Equals(line.Trim(),"-> "+expectedUrl,StringComparison.Ordinal)) {
                if(found!=0)throw new IOException("More than one port matches the test device");found=current;
            }
        }
        return found;
    }
    static string AwaitNewPath(HashSet<string> before)
    {
        var until=Stopwatch.StartNew();
        HashSet<string> diagnosed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int latestNewCount=0;
        while(until.ElapsedMilliseconds<15000) {
            string found=null;
            latestNewCount=0;
            foreach(string path in Paths()) {
                if(before.Contains(path))continue;
                latestNewCount++;
                if(!IsUsbIpDescendant(path,diagnosed.Add(path)))continue;
                if(found!=null)throw new IOException("More than one new USBIP DS4 HID collection; refusing ambiguous read");
                found=path;
            }
            if(found!=null)return found;
            Thread.Sleep(100);
        }
        throw new TimeoutException("No validated NEW USBIP DS4 HID collection in 15 seconds; new VID/PID paths="+latestNewCount);
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
        Pass("Windows HID identity + 64-byte gamepad capability");
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
                throw new TimeoutException("HID read timed out; cancellation drained="+completed);
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
    static void Pass(string label){Console.WriteLine("PASS "+label);passed++;}
    static void Expect(ViiperTransport transport,SafeFileHandle handle,string label,int[] keys,bool enabled,
        int lx,int ly,ushort buttons,int hat,int l2,int r2)
    {
        uint held=0;foreach(int key in keys)held|=KeyboardMapping.ActionForKey(key);
        byte[] state=KeyboardMapping.Frame(held,enabled);
        transport.Send(state);
        var limit=Stopwatch.StartNew();int reads=0;
        while(limit.ElapsedMilliseconds<3000 && reads++<512) {
            byte[] report=ReadReport(handle,500);
            ushort got=(ushort)((report[5]&0xf0)|(report[6]<<8)|(report[7]&3));
            if(report[1]==lx && report[2]==ly && report[3]==128 && report[4]==128 && got==buttons &&
                (report[5]&15)==hat && report[8]==l2 && report[9]==r2){Pass(label);return;}
        }
        throw new IOException("Windows HID state transition not observed: "+label);
    }
    static int Main(string[] args)
    {
        if(args.Length!=1||args[0]!="--create-temporary-windows-device") {
            Console.Error.WriteLine("Usage: WindowsHidTest.exe --create-temporary-windows-device");return 2;
        }
        ViiperTransport transport=null;int port=0;string expectedUrl=null;bool success=false,cleanup=true;
        try {
            if(IntPtr.Size!=8||!File.Exists(UsbIpExe))throw new IOException("Requires x64 and installed USBip");
            HashSet<string> before=Paths();
            Console.WriteLine("Existing DS4 V2 HID collection count="+before.Count+"; excluded from reads");
            transport=new ViiperTransport(33242);transport.Connect();
            if(transport.ServerVersion.TrimStart('v')!="0.9.0")throw new IOException("Requires VIIPER 0.9.0");
            string bus=transport.UsbBusId;
            if(!Regex.IsMatch(bus,@"^\d+-\d+$"))throw new IOException("Invalid allocated bus id");
            expectedUrl="usbip://127.0.0.1:33241/"+bus;
            var attach=Run("--tcp-port 33241 attach --remote 127.0.0.1 --bus-id "+bus+" --once --terse");
            RequireCommand(attach,"USBip attach");
            if(!Int32.TryParse(attach.Output.Trim(),out port)||port<1||port>255)throw new IOException("Attach did not return one valid port");
            if(!PortMatches(port,expectedUrl))throw new IOException("Attached port URL does not match the test-owned device");
            Pass("test-owned USBip attachment and port identity");
            string path=AwaitNewPath(before);
            using(SafeFileHandle hid=CreateFile(path,0x80000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero)) {
                if(hid.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"Open new virtual HID");
                ValidateHid(hid);
                Expect(transport,hid,"initial neutral",new int[0],true,128,128,0,8,0,0);
                Expect(transport,hid,"K -> Cross pressed",new[]{0x4b},true,128,128,0x20,8,0,0);
                Expect(transport,hid,"K -> Cross held",new[]{0x4b},true,128,128,0x20,8,0,0);
                Expect(transport,hid,"K released -> Cross released",new int[0],true,128,128,0,8,0,0);
                Expect(transport,hid,"W -> up",new[]{0x57},true,128,0,0,8,0,0);
                Expect(transport,hid,"A -> left",new[]{0x41},true,0,128,0,8,0,0);
                Expect(transport,hid,"S -> down",new[]{0x53},true,128,255,0,8,0,0);
                Expect(transport,hid,"D -> right",new[]{0x44},true,255,128,0,8,0,0);
                Expect(transport,hid,"W+D -> diagonal",new[]{0x57,0x44},true,218,38,0,8,0,0);
                Expect(transport,hid,"W+S -> neutral",new[]{0x57,0x53},true,128,128,0,8,0,0);
                Expect(transport,hid,"K+I -> Cross+Triangle",new[]{0x4b,0x49},true,128,128,0xa0,8,0,0);
                Expect(transport,hid,"ArrowUp -> D-pad up",new[]{0x26},true,128,128,0,0,0,0);
                Expect(transport,hid,"U -> L2 pressed",new[]{0x55},true,128,128,0x400,8,255,0);
                Expect(transport,hid,"U released -> L2 released",new int[0],true,128,128,0,8,0,0);
                Expect(transport,hid,"disabled held W+K+U -> neutral",new[]{0x57,0x4b,0x55},false,128,128,0,8,0,0);
                Expect(transport,hid,"final all released/right stick centered",new int[0],true,128,128,0,8,0,0);
            }
            success=true;
        }catch(Exception ex){Console.Error.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message);}
        finally {
            if(transport!=null) {
                try{if(transport.IsConnected)transport.Send(KeyboardMapping.Frame(0,false));}
                catch(Exception ex){cleanup=false;Console.Error.WriteLine("FAIL neutral cleanup: "+ex.Message);}
                if(expectedUrl!=null) {
                    try {
                        if(port==0)port=FindOwnedPort(expectedUrl);
                        if(port!=0) {
                            if(!PortMatches(port,expectedUrl))throw new IOException("Port identity changed; refusing detach");
                            RequireCommand(Run("detach --port "+port),"Detach owned port");
                            if(FindOwnedPort(expectedUrl)!=0)throw new IOException("Owned port remains attached");
                            Pass("detached only test-owned Windows device");
                        }
                    }catch(Exception ex){cleanup=false;Console.Error.WriteLine("FAIL attachment cleanup: "+ex.Message);}
                }
                transport.Dispose();
                if(transport.BusId!=0){cleanup=false;Console.Error.WriteLine("FAIL own bus cleanup: "+transport.LastFailure);}
                else Pass("removed only test-owned VIIPER bus");
            }
        }
        Console.WriteLine("RESULT passed="+passed+" success="+(success&&cleanup)+" windows_hid_tested=true sony_tested=false keyboard_captured=false");
        return success&&cleanup?0:1;
    }
}
