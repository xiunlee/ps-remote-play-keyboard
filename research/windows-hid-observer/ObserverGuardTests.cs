// Pure scope-policy tests: no HID or process APIs are invoked.
using System;
using System.Collections.Generic;

internal static class ObserverGuardTests
{
    static int checks;
    static void Check(bool condition,string name)
    {
        checks++;
        if(!condition)throw new Exception("Guard failed: "+name);
    }
    static ObserverScope Parse(string mode,string url)
    {
        ObserverScope scope;
        Check(ObserverScope.TryParse(new[]{mode,"1",url,"120"},out scope),"valid "+mode);
        return scope;
    }
    static void Reject(string mode,string port,string url,string duration,string name)
    {
        ObserverScope scope;
        Check(!ObserverScope.TryParse(new[]{mode,port,url,duration},out scope),name);
    }
    static bool Ancestry(ObserverScope scope,string usbId,string rootHardware)
    {
        return scope.MatchesImportedAncestry(
            new[]{@"HID\VID_054C&PID_05C4\COLLECTION",usbId,@"ROOT\USB\0000"},
            new[]{new string[0],new[]{@"USB\Class_03"},new[]{rootHardware}});
    }
    static int Main()
    {
        try {
            const string relayMode="--observe-existing-physical-relay";
            const string virtualMode="--observe-existing-virtual";
            const string relayUrl="usbip://127.0.0.1:33251/6-1";
            const string virtualUrl="usbip://127.0.0.1:33241/1-1";
            ObserverScope relay=Parse(relayMode,relayUrl),virtualScope=Parse(virtualMode,virtualUrl);
            Check(relay.Product==0x05c4 && virtualScope.Product==0x09cc,"mode pins product identity");
            Reject(relayMode,"1",virtualUrl,"120","relay rejects virtual service");
            Reject(virtualMode,"1",relayUrl,"120","virtual rejects relay service");
            Reject(relayMode,"1","usbip://192.0.2.1:33251/6-1","120","reject nonloopback host");
            Reject(relayMode,"1","usbip://localhost:33251/6-1","120","require exact loopback spelling");
            Reject(relayMode,"1",relayUrl+"\n","120","reject trailing newline");
            Reject(relayMode,"1",relayUrl+"/other","120","reject extra URL path");
            Reject(relayMode,"1","usbip://127.0.0.1:33251/0-1","120","reject zero bus");
            Reject(relayMode,"1",relayUrl,"121","duration ceiling");
            Reject(relayMode,"1",relayUrl,"0","duration floor");
            Reject(relayMode,"0",relayUrl,"120","port floor");
            Reject(relayMode,"256",relayUrl,"120","port ceiling");
            Reject("--observe-local-physical","1",relayUrl,"120","no local physical mode");

            // usbip-win2 prints several arrow-prefixed metadata fields as well
            // as the source URL. Their presence must not count as extra sources.
            string listing="Imported USB devices\r\n====================\r\nPort 01: device in use at Full Speed(12Mbps)\r\n"+
                "         Sony Corp. : DualShock 4 [CUH-ZCT1x] (054c:05c4)\r\n           -> "+relayUrl+"\r\n"+
                "           -> remote bus/dev: 006/001\r\n           -> serial: \r\n           -> mode: zero-copy\r\n";
            Check(relay.MatchesPortOutput(listing),"one exact owned port and URL");
            Check(relay.MatchesPortOutput("Port 01: device in use\n -> "+relayUrl+"\n"),"older listing without optional metadata");
            Check(!relay.MatchesPortOutput(listing+" -> unknown source: usbip://127.0.0.1:33251/6-2\n"),"unknown arrow field rejected");
            Check(!relay.MatchesPortOutput(listing+" -> http://127.0.0.1:33251/6-1\n"),"other source scheme rejected");
            Check(!virtualScope.MatchesPortOutput(listing),"port output cannot cross modes");
            Check(!relay.MatchesPortOutput(listing.Replace("Port 01:","Port 02:")),"wrong port");
            Check(!relay.MatchesPortOutput(listing.Replace("6-1","6-2")),"wrong bus");
            Check(!relay.MatchesPortOutput(listing+"Port 02: <Port in Use>\n  -> usbip://127.0.0.1:33241/1-1\n"),"multiple imported devices");
            Check(!relay.MatchesPortOutput(listing+"  -> "+relayUrl+"\n"),"duplicate source URL");
            Check(!relay.MatchesPortOutput("  -> "+relayUrl+"\n"),"URL without port");
            Check(!relay.MatchesPortOutput(listing.Replace("Port 01:","Port 999999999999999999999:")),"overflow port rejected safely");

            Check(Ancestry(relay,@"USB\VID_054C&PID_05C4\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"physical relay imported chain");
            Check(Ancestry(relay,@"USB\VID_054C&PID_05C4&MI_03\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"composite USB interface");
            Check(!Ancestry(relay,@"USB\VID_054C&PID_05C4\INSTANCE",@"PCI\VEN_8086&DEV_1234"),"local physical DS4 rejected before open");
            Check(!Ancestry(relay,@"USB\VID_054C&PID_09CC\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"wrong imported product");
            Check(!Ancestry(relay,@"USB\VID_054C&PID_05C40\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"product prefix cannot match");
            Check(!Ancestry(relay,@"USB\VID_054C&PID_05C4\INSTANCE",@"ROOT\USBIP_WIN2\UDE_SPOOF"),"root hardware ID exact match");
            Check(!Ancestry(relay,@"USB\VID_054C&PID_05C4\INSTANCE",@"ROOT\USB\0000"),"instance name alone is insufficient");
            Check(!relay.MatchesImportedAncestry(new[]{@"ROOT\USB\0000",@"USB\VID_054C&PID_05C4\INSTANCE"},
                new[]{new[]{@"ROOT\USBIP_WIN2\UDE"},new string[0]}),"USB identity must precede controller ancestor");
            Check(Ancestry(virtualScope,@"USB\VID_054C&PID_09CC\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"existing virtual mode preserved");
            Check(!Ancestry(virtualScope,@"USB\VID_054C&PID_05C4\INSTANCE",@"ROOT\USBIP_WIN2\UDE"),"virtual mode rejects physical relay");
            Console.WriteLine("PASS scope_guards="+checks+" hid_opens=0 external_processes=0 device_writes=0");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}
    }
}
