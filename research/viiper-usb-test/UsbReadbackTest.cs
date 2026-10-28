// Local VIIPER TCP -> USB/IP readback test. No Windows device is attached.
// Run ONLY against a dedicated loopback VIIPER server with auto-attach disabled.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using PSKeyboard;

internal static class UsbReadbackTest
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static int apiPort, usbPort;
    static uint sequence;
    static int passed;
    static TcpClient Connect(int port)
    {
        var c = new TcpClient(AddressFamily.InterNetwork);
        c.NoDelay = true;
        c.ReceiveTimeout = c.SendTimeout = 3000;
        var pending = c.BeginConnect(IPAddress.Loopback, port, null, null);
        using (pending.AsyncWaitHandle)
        {
            if (!pending.AsyncWaitHandle.WaitOne(3000)) { c.Close(); throw new TimeoutException("Loopback connection timed out"); }
            c.EndConnect(pending);
        }
        return c;
    }
    static byte[] ReadExact(Stream stream, int length)
    {
        var b = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int n = stream.Read(b, offset, length - offset);
            if (n == 0) throw new EndOfStreamException("Truncated protocol response");
            offset += n;
        }
        return b;
    }
    static void Write(Stream stream, byte[] b) { stream.Write(b, 0, b.Length); }
    static void Put16(byte[] b, int offset, ushort value) { b[offset]=(byte)(value >> 8); b[offset+1]=(byte)value; }
    static void Put32(byte[] b, int offset, uint value)
    {
        b[offset]=(byte)(value >> 24); b[offset+1]=(byte)(value >> 16);
        b[offset+2]=(byte)(value >> 8); b[offset+3]=(byte)value;
    }
    static ushort Get16(byte[] b, int offset) { return (ushort)((b[offset] << 8) | b[offset+1]); }
    static uint Get32(byte[] b, int offset)
    {
        return ((uint)b[offset] << 24) | ((uint)b[offset+1] << 16) | ((uint)b[offset+2] << 8) | b[offset+3];
    }
    static Dictionary<string, object> Api(string request)
    {
        using (var c = Connect(apiPort))
        using (var stream = c.GetStream())
        using (var response = new MemoryStream())
        {
            Write(stream, Encoding.UTF8.GetBytes(request + "\0"));
            var buffer = new byte[4096];
            int n;
            while ((n=stream.Read(buffer,0,buffer.Length)) != 0)
            {
                if (response.Length+n > 65536) throw new InvalidDataException("API response too large");
                response.Write(buffer,0,n);
            }
            var value = Json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(response.ToArray()));
            if (value == null) throw new InvalidDataException("Missing API response");
            if (value.ContainsKey("status") && Convert.ToInt32(value["status"],CultureInfo.InvariantCulture)>=400)
                throw new InvalidOperationException("VIIPER API error: " + Convert.ToString(value["title"],CultureInfo.InvariantCulture));
            return value;
        }
    }
    static uint Import(NetworkStream usb, string busId)
    {
        var request = new byte[40];
        Put16(request,0,0x0111); Put16(request,2,0x8003);
        var name=Encoding.ASCII.GetBytes(busId);
        if (name.Length>31) throw new InvalidDataException("USB bus id too long");
        Buffer.BlockCopy(name,0,request,8,name.Length);
        Write(usb,request);
        var header=ReadExact(usb,8);
        if (Get16(header,0)!=0x0111 || Get16(header,2)!=0x0003 || Get32(header,4)!=0)
            throw new InvalidDataException("USB/IP import rejected");
        var device=ReadExact(usb,312); // Import does not include interface trailers.
        if (Get16(device,300)!=0x054c || Get16(device,302)!=0x09cc)
            throw new InvalidDataException("Imported device is not the requested DS4 identity");
        uint bus=Get32(device,288), dev=Get32(device,292);
        if (bus>65535 || dev>65535) throw new InvalidDataException("USB/IP device id out of range");
        Console.WriteLine("PASS userspace USB/IP import: VID=054C PID=09CC"); passed++;
        return (bus<<16)|dev;
    }
    static byte[] ReadReport(NetworkStream usb, uint device)
    {
        var request = new byte[48];
        uint current = ++sequence;
        Put32(request,0,1); Put32(request,4,current); Put32(request,8,device);
        Put32(request,12,1); Put32(request,16,4); // IN endpoint 4, from VIIPER DS4 implementation.
        Put32(request,24,64); Put32(request,32,0xffffffff); Put32(request,36,4);
        Write(usb,request);
        var header=ReadExact(usb,48);
        if (Get32(header,0)!=3 || Get32(header,4)!=current || Get32(header,20)!=0)
            throw new InvalidDataException("Invalid USB/IP transfer response");
        uint length=Get32(header,24);
        if (length!=64) throw new InvalidDataException("Expected exactly 64 DS4 input bytes, received "+length);
        uint packets=Get32(header,32);
        if (packets!=0 && packets!=0xffffffff) throw new InvalidDataException("Unexpected isochronous descriptors");
        var report=ReadExact(usb,(int)length);
        if (report[0]!=1) throw new InvalidDataException("Unexpected DS4 report id");
        return report;
    }
    static byte[] State(sbyte lx,sbyte ly,ushort buttons,byte dpad,byte l2,byte r2)
    {
        var state = new byte[31];
        state[0]=unchecked((byte)lx); state[1]=unchecked((byte)ly);
        state[4]=(byte)buttons; state[5]=(byte)(buttons>>8);
        state[6]=dpad; state[7]=l2; state[8]=r2;
        state[29]=0; state[30]=0xe0; // AccelZ=-8192, matching upstream neutral constructor.
        return state;
    }
    static void Expect(NetworkStream feeder,NetworkStream usb,uint device,string label,
        sbyte lx,sbyte ly,ushort buttons,byte dpad,byte expectedHat,byte l2,byte r2)
    {
        ExpectPacket(feeder,usb,device,label,State(lx,ly,buttons,dpad,l2,r2),lx,ly,buttons,expectedHat,l2,r2);
    }
    static void ExpectMapped(NetworkStream feeder,NetworkStream usb,uint device,string label,
        int[] virtualKeys,bool enabled,sbyte lx,sbyte ly,ushort buttons,byte expectedHat,byte l2,byte r2)
    {
        uint held=0;
        foreach (int key in virtualKeys) held|=KeyboardMapping.ActionForKey(key);
        // Calls the application's exact C# mapping and its compiled native engine.
        byte[] state=KeyboardMapping.Frame(held,enabled);
        ExpectPacket(feeder,usb,device,label,state,lx,ly,buttons,expectedHat,l2,r2);
    }
    static void ExpectPacket(NetworkStream feeder,NetworkStream usb,uint device,string label,
        byte[] state,sbyte lx,sbyte ly,ushort buttons,byte expectedHat,byte l2,byte r2)
    {
        if(state.Length!=31) throw new InvalidDataException("Mapped state must contain 31 bytes");
        for (int attempt=0;attempt<12;attempt++)
        {
            Write(feeder,state);
            Thread.Sleep(10); // Separate TCP connections have no mutual delivery ordering.
            var report=ReadReport(usb,device);
            ushort actualButtons=(ushort)((report[5]&0xf0)|(report[6]<<8)|((report[7]&3)));
            bool match=report[1]==lx+128 && report[2]==ly+128 && report[3]==128 && report[4]==128 &&
                actualButtons==buttons && (report[5]&0xf)==expectedHat && report[8]==l2 && report[9]==r2;
            if (match) { Console.WriteLine("PASS "+label); passed++; return; }
        }
        throw new InvalidDataException("State transition not observed: "+label);
    }
    static int Main(string[] args)
    {
        bool mapped=args.Length==4 && args[3]=="--keyboard-mapping";
        if ((args.Length!=3 && !mapped) || args[0]!="--isolated-loopback-server" ||
            !Int32.TryParse(args[1],out apiPort) || !Int32.TryParse(args[2],out usbPort) ||
            apiPort<1024 || apiPort>65535 || usbPort<1024 || usbPort>65535 || apiPort==usbPort)
        {
            Console.Error.WriteLine("Usage: UsbReadbackTest.exe --isolated-loopback-server API_PORT USB_PORT [--keyboard-mapping]");
            Console.Error.WriteLine("Dedicated VIIPER v0.9.0 server must bind loopback with auto-attach DISABLED.");
            return 2;
        }
        string bus=null;
        bool cleanup=true, success=false;
        try
        {
            var ping=Api("ping");
            if (!ping.ContainsKey("server") || Convert.ToString(ping["server"])!="VIIPER")
                throw new InvalidDataException("Loopback endpoint is not VIIPER");
            string version=Convert.ToString(ping["version"]);
            if (version.TrimStart('v')!="0.9.0") throw new InvalidDataException("Expected tested VIIPER v0.9.0, got "+version);
            Console.WriteLine("Server version="+version+"; no Windows USB driver used by this client");
            bus=Convert.ToString(Api("bus/create")["busId"],CultureInfo.InvariantCulture);
            var added=Api("bus/"+bus+"/add {\"type\":\"dualshock4\"}");
            string deviceId=Convert.ToString(added["devId"],CultureInfo.InvariantCulture);
            using (var feedClient=Connect(apiPort))
            using (var feeder=feedClient.GetStream())
            using (var usbClient=Connect(usbPort))
            using (var usb=usbClient.GetStream())
            {
                Write(feeder,Encoding.ASCII.GetBytes("bus/"+bus+"/"+deviceId+"\0"));
                uint device=Import(usb,bus+"-"+deviceId);
                if(mapped)
                {
                    Console.WriteLine("Mapping source=src/keyboard-pad/KeyboardMapping.cs; native=pskb-keyboard-merge.dll");
                    ExpectMapped(feeder,usb,device,"mapped initial neutral",new int[0],true,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"K -> Cross pressed",new[]{0x4b},true,0,0,0x20,8,0,0);
                    ExpectMapped(feeder,usb,device,"K -> Cross held",new[]{0x4b},true,0,0,0x20,8,0,0);
                    ExpectMapped(feeder,usb,device,"K released -> Cross released",new int[0],true,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"W -> left stick up",new[]{0x57},true,0,-128,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"W+D -> normalized diagonal",new[]{0x57,0x44},true,90,-90,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"W+S -> opposite keys neutral",new[]{0x57,0x53},true,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"K+I -> Cross+Triangle",new[]{0x4b,0x49},true,0,0,0xa0,8,0,0);
                    ExpectMapped(feeder,usb,device,"ArrowUp -> D-pad up",new[]{0x26},true,0,0,0,0,0,0);
                    ExpectMapped(feeder,usb,device,"U -> L2 digital+analog pressed",new[]{0x55},true,0,0,0x400,8,255,0);
                    ExpectMapped(feeder,usb,device,"U released -> L2 released",new int[0],true,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"disabled held W+K+U -> neutral",new[]{0x57,0x4b,0x55},false,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"unmapped G -> neutral",new[]{0x47},true,0,0,0,8,0,0);
                    ExpectMapped(feeder,usb,device,"mapped final release/right stick unchanged",new int[0],true,0,0,0,8,0,0);
                }
                else
                {
                Expect(feeder,usb,device,"initial neutral",0,0,0,0,8,0,0);
                Expect(feeder,usb,device,"Cross pressed",0,0,0x20,0,8,0,0);
                Expect(feeder,usb,device,"Cross held",0,0,0x20,0,8,0,0);
                Expect(feeder,usb,device,"Cross released",0,0,0,0,8,0,0);
                Expect(feeder,usb,device,"W/up axis",0,-128,0,0,8,0,0);
                Expect(feeder,usb,device,"A/left axis",-128,0,0,0,8,0,0);
                Expect(feeder,usb,device,"S/down axis",0,127,0,0,8,0,0);
                Expect(feeder,usb,device,"D/right axis",127,0,0,0,8,0,0);
                Expect(feeder,usb,device,"diagonal normalized state",90,-90,0,0,8,0,0);
                Expect(feeder,usb,device,"D-pad up bitfield",0,0,0,1,0,0,0);
                Expect(feeder,usb,device,"L2 full state",0,0,0x400,0,8,255,0);
                Expect(feeder,usb,device,"all released/right stick unchanged",0,0,0,0,8,0,0);
                }
            }
            success=true;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL "+ex.GetType().Name+": "+ex.Message); }
        finally
        {
            if (bus!=null)
            {
                try { Api("bus/remove "+bus); Console.WriteLine("PASS removed test-owned bus"); passed++; }
                catch (Exception ex) { cleanup=false; Console.Error.WriteLine("FAIL test bus cleanup: "+ex.Message); }
            }
        }
        Console.WriteLine("RESULT passed="+passed+" success="+(success&&cleanup)+" mapping_mode="+mapped+" windows_attached=false sony_tested=false keyboard_captured=false");
        return success&&cleanup?0:1;
    }
}
