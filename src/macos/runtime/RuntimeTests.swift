// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
// Offline only: fake loopback API, no VM, HID, vendor process, keyboard or Sony.
import Foundation
import Darwin

private final class FakeGuestAPI: @unchecked Sendable {
    let listener:Int32
    let port:UInt16
    private let stop = RuntimeStopFlag()
    private let lock = NSLock()
    private var _removed = false
    private var _frames = 0
    var removed:Bool { lock.lock(); defer {lock.unlock()}; return _removed }
    var frames:Int { lock.lock(); defer {lock.unlock()}; return _frames }
    init() throws {
        let fd = try RuntimeSocket.listen(0)
        listener = fd
        var value = sockaddr_in(); var size = socklen_t(MemoryLayout<sockaddr_in>.size)
        guard withUnsafeMutablePointer(to:&value,{p in p.withMemoryRebound(to:sockaddr.self,capacity:1) {getsockname(fd,$0,&size)}}) == 0 else {throw RuntimeFailure("test_port")}
        port = UInt16(bigEndian:value.sin_port)
        Thread {self.run()}.start()
    }
    func finish() {stop.request()}
    private func run() {
        defer {close(listener)}
        while !stop.requested {
            var polled = pollfd(fd:listener,events:Int16(POLLIN),revents:0)
            if poll(&polled,1,20) <= 0 {continue}
            let fd = accept(listener,nil,nil)
            if fd < 0 {continue}
            do {
                try RuntimeSocket.configure(fd)
                var command:[UInt8] = []
                while !command.contains(0) {
                    try RuntimeSocket.wait(fd,events:Int16(POLLIN),deadline:RuntimeSocket.now+2)
                    var byte:UInt8 = 0
                    guard read(fd,&byte,1) == 1, command.count < 512 else {throw RuntimeFailure("test_command")}
                    command.append(byte)
                }
                let text = String(decoding:command.dropLast(),as:UTF8.self)
                if text == "bus/7/3" {Thread {self.stream(fd)}.start(); continue}
                let reply:[String:Any]
                switch text {
                case "ping": reply = ["server":"PSKeyboard-DualSenseV5-GuestCandidate","version":"research.guest.1","labMarker":"pskeyboard-dualsense-v5-guest","sourceCommit":RuntimeWire.commit,"apiAddress":"127.0.0.1:33642","usbAddress":"127.0.0.1:33641","autoAttach":false,"hostAutoAttach":false,"loopbackOnly":true,"stdinEofStops":true,"sessionLifetimeLimit":false]
                case "bus/create":reply = ["busId":7]
                case "bus/7/add {\"type\":\"dualsensegamepadv5\"}":reply = ["busId":7,"devId":"3","type":"dualsensegamepadv5"]
                case "bus/remove 7": lock.lock();_removed = true;lock.unlock();reply = ["busId":7]
                default:reply = ["status":404]
                }
                try RuntimeSocket.sendComplete(fd,Array(JSONSerialization.data(withJSONObject:reply)))
            } catch {}
            close(fd)
        }
    }
    private func stream(_ fd:Int32) {
        defer {close(fd)}
        var parser = RuntimeFrameParser()
        do {
            while !stop.requested {
                var polled = pollfd(fd:fd,events:Int16(POLLIN),revents:0)
                if poll(&polled,1,20) <= 0 {continue}
                var bytes = [UInt8](repeating:0,count:4096)
                let count = read(fd,&bytes,bytes.count)
                if count <= 0 {return}
                let decoded = try parser.feed(Array(bytes.prefix(count)),at:RuntimeSocket.now)
                lock.lock();_frames += decoded.count;lock.unlock()
            }
        } catch {}
    }
}

@main
struct RuntimeTests {
    static func main() throws {
        var checks = 0
        func check(_ condition:Bool,_ label:String) {
            checks += 1
            if !condition {fatalError(label)}
        }
        func rejects(_ label:String,_ body:() throws -> Void) {
            do {try body();check(false,label)} catch {check(true,label)}
        }
        let neutral = RuntimeWire.frame(payload:RuntimeWire.neutral,sequence:0)
        check(neutral == (try DualSenseV5Protocol.encode(actionMask:0,sequence:0)),"neutral matches deployed encoder")
        for action in MacAction.allCases {
            let frame = try DualSenseV5Protocol.encode(actionMask:action.mask,sequence:0)
            var parser = RuntimeFrameParser()
            check(try parser.feed(frame,at:0).count == 1,"accept canonical action")
        }
        for split in 1..<neutral.count {
            var parser = RuntimeFrameParser()
            check(try parser.feed(Array(neutral.prefix(split)),at:0).isEmpty,"fragment waits")
            check(try parser.feed(Array(neutral.dropFirst(split)),at:0.01) == [RuntimeWire.neutral],"fragment completes")
        }
        for offset in 0..<neutral.count {
            var damaged = neutral;damaged[offset] ^= 1
            rejects("corruption rejected") {var parser=RuntimeFrameParser();_ = try parser.feed(damaged,at:0)}
        }
        rejects("future sequence") {var parser=RuntimeFrameParser();_ = try parser.feed(RuntimeWire.frame(payload:RuntimeWire.neutral,sequence:1),at:0)}
        rejects("partial timeout") {var parser=RuntimeFrameParser();_ = try parser.feed([86],at:0);try parser.checkDeadline(1.5)}
        for offset in [2,3,11,15,16,20,21,32] {
            var value=RuntimeWire.neutral;value[offset] ^= 1
            rejects("unsupported controls") {var parser=RuntimeFrameParser();_ = try parser.feed(RuntimeWire.frame(payload:value,sequence:0),at:0)}
        }
        var feedback = RuntimeFrameParser(feedback:true)
        check(try feedback.feed(RuntimeWire.frame(payload:[UInt8](repeating:0,count:474),sequence:0,kind:0x81),at:0).count == 1,"feedback envelope")
        rejects("atomic boundary") {var parser=RuntimeFrameParser(feedback:true);_ = try parser.feed(RuntimeWire.frame(payload:[UInt8](repeating:0,count:2396),sequence:0,kind:0x83),at:0)}
        var waiting = RuntimeFeedClock(now:0)
        check(try waiting.tick(100_000) != nil,"unlimited neutral wait")
        try waiting.connect(at:100_000)
        rejects("stale input") {_ = try waiting.tick(100_000.251)}
        rejects("late input cannot revive") {try waiting.input(RuntimeWire.neutral,at:100_000.252)}
        var live = RuntimeFeedClock(now:0);try live.connect(at:0)
        for tick in 1...80_000 {try live.input(RuntimeWire.neutral,at:Double(tick)/10)}
        check(try live.tick(8000) != nil,"no 7200 second session cap")
        check(try live.tick(8000) == nil,"missed slots produce no backlog")
        let hub = "PSKeyboard ((none):7575)\n"
        check(RuntimeOwner.deviceAddress(in:hub + " --> DualSense Wireless Controller ((none).11)\n") == "(none).11","preserve parenthesized hub name")
        check(RuntimeOwner.deviceAddress(in:hub + " --> DualSense (Guest.11)\n --> DualSense (Guest.12)") == nil,"ambiguous device rejected")
        check(RuntimeOwner.deviceAddress(in:hub + " --> Wireless Controller ((none).11)\n") == "(none).11","device identity does not depend on product label")
        check(RuntimeOwner.deviceAddress(in:hub + " --> My Pad (work) ((none).11)\n") == "(none).11","nickname parentheses do not become the address")
        check(RuntimeOwner.deviceAddress(in:hub + "Other (example:7575)\n --> Pad ((none).11)\n") == nil,"extra hub rejected")
        check(RuntimeOwner.deviceAddress(in:" --> Pad ((none).11)\n") == nil,"missing hub rejected")
        check(RuntimeOwner.deviceAddress(in:hub + " --> Pad ((none).11) (In-use by you)\n") == nil,"already-used device is not adopted")
        check(RuntimeOwner.deviceAddress(in:hub + " --> Pad (bad,argument.11)\n") == nil,"API argument injection rejected")
        let oldConfig = "[General]\r\nAutoFind=1\r\nReverseLookup=1\r\nWindowWidth=400\r\n[Settings]\r\nManualHubs=example:7575\r\n[AutoShare]\r\nAll=1\r\n[Transport]\r\nEasyFindId=fixture-id\r\nEasyFindPin=fixture-pin\r\nAutoFind=0\r\nReverseLookup=0\r\nManualHubs=127.0.0.1:7575\r\nAutoUseAll=0\r\n"
        let preparedConfig = RuntimeOwner.preparedClientConfiguration(oldConfig)
        check(preparedConfig.contains("[General]\nAutoFind=0\nReverseLookup=0\nLanguage=EN-US\nWindowWidth=400\n"),"managed discovery keys are in General")
        check(preparedConfig.contains("[Settings]\nManualHubs=\n"),"client starts with no manual hubs before API setup")
        check(preparedConfig.contains("[Transport]\nEasyFindId=fixture-id\nEasyFindPin=fixture-pin\n"),"unknown identity values are preserved exactly")
        check(!preparedConfig.contains("AutoUseAll="),"invalid legacy managed key removed")
        check(preparedConfig.components(separatedBy:"AutoFind=").count == 2,"misplaced duplicate key removed")
        check(RuntimeOwner.preparedClientConfiguration(preparedConfig) == preparedConfig,"configuration repair is idempotent")
        check(RuntimeOwner.preparedClientConfiguration("").contains("[Settings]\nManualHubs=\n"),"fresh configuration has vendor sections")
        check(RuntimeOwner.listFlag("Auto-Find",in:"Auto-Find currently off\r\n") == false,"CRLF discovery flag parsed")
        check(RuntimeOwner.listFlag("Auto-Find",in:"Auto-Find currently on\n") == true,"enabled discovery detected")
        check(RuntimeOwner.listFlag("Auto-Find",in:"Auto-Find currently on\nAuto-Find currently off\n") == nil,"ambiguous discovery flags rejected")
        let shapes = RuntimeOwner.listShapes("Alice secret-123 (private.host:7575)\r\n --> My Controller ((none).11)\r\n")
        check(shapes == ["x x-x (x.x:x)"," --> x x ((x).x)"],"LIST diagnostics retain shape without names or identifiers")
        check(RuntimeOwner.listShapes(String(repeating:"a",count:500) + "\n" + String(repeating:"line\n",count:40)).count == 32,"LIST diagnostics are bounded")
        let guestCounts = RuntimeOwner.guestDiagnosticCounts("PSKBDIAG|1|2|1|1|1|0|0|1024|1\r\n")
        check(guestCounts?["connections"] == 2 && guestCounts?["foundDeviceLines"] == 1,"CRLF guest diagnostics retain only defined numeric counters")
        check(RuntimeOwner.guestDiagnosticCounts("PSKBDIAG|1|private|1|1|1|0|0|1024|1\n") == nil,"unexpected diagnostic content rejected")
        func monitorReady(fds:String,row:String) throws -> Bool {
            let process = try OwnedProcess(executable:URL(fileURLWithPath:"/usr/bin/awk"),arguments:["-v","fds=" + fds,RuntimeOwner.vendorMonitorAwk],inputEnabled:true)
            try process.write("sk Eth Pid Groups Rmem Wmem Dump Locks Drops Inode\n" + row + "\n")
            try process.input.fileHandleForWriting.close()
            do { _ = try process.collect(seconds:2);return true } catch let error as RuntimeFailure {
                if error.code == "command_failed" {return false};throw error
            }
        }
        let monitorRow="0000000000000000 15 123 00000001 0 0 0 2 0 42"
        check(try monitorReady(fds:"socket:[42]",row:monitorRow),"owned subscribed USB event monitor is ready")
        check(try !monitorReady(fds:"socket:[142]",row:monitorRow),"socket inode prefix collision rejected")
        check(try !monitorReady(fds:"socket:[7]",row:monitorRow),"another process monitor is not readiness")
        check(try !monitorReady(fds:"socket:[42]",row:monitorRow.replacingOccurrences(of:"00000001",with:"00000000")),"unsubscribed socket is not readiness")
        check(try !monitorReady(fds:"socket:[42]",row:monitorRow.replacingOccurrences(of:" 15 ",with:" 16 ")),"other netlink protocol is not readiness")
        check(try monitorReady(fds:"/dev/null socket:[7] socket:[42]",row:monitorRow),"space-separated process FD listing can contain other descriptors")
        let vhci="hub port sta spd dev sockfd local_busid\nhs 0000 006 003 00070003 123 1-1\nss 0008 004 000 00000000 0 0-0\n"
        check(try RuntimeVHCIImport.matching(vhci,bus:7,device:3) == RuntimeVHCIImport(port:0,localBusID:"1-1"),"kernel remote identity matches owned candidate")
        check(try RuntimeVHCIImport.matching(vhci,bus:7,device:4) == nil,"different remote identity is not adopted")
        check(try RuntimeVHCIImport.matching(vhci.replacingOccurrences(of:"006",with:"005"),bus:7,device:3) == nil,"not-yet-enumerated port waits")
        rejects("duplicate remote import") {_ = try RuntimeVHCIImport.matching(vhci+"hs 0001 006 003 00070003 124 1-2\n",bus:7,device:3)}
        rejects("invalid local sysfs path") {_ = try RuntimeVHCIImport.matching(vhci.replacingOccurrences(of:" 1-1",with:" ../../private"),bus:7,device:3)}
        let safeRows=RuntimeVHCIImport.safeRows(vhci)
        check(safeRows.count == 2 && safeRows[0]["remoteID"] == "00070003","bounded VHCI diagnostics retain numeric identity")
        check(!String(describing:safeRows).contains("sockfd") && !String(describing:safeRows).contains("123"),"diagnostics omit socket pointers")
        // hvc0 serial output uses CRLF, one Swift Character rather than two.
        let serialVHCI=vhci.replacingOccurrences(of:"\n",with:"\r\n")
        check(serialVHCI.split(separator:"\n").count == 1,"regression fixture reproduces Swift CRLF grapheme behavior")
        check(try RuntimeVHCIImport.matching(serialVHCI,bus:7,device:3) == RuntimeVHCIImport(port:0,localBusID:"1-1"),"actual serial CRLF matches kernel remote identity")
        check(RuntimeVHCIImport.safeRows(serialVHCI) == safeRows,"CRLF diagnostics preserve the same rows")
        check(RuntimeOwner.deviceAddress(in:"VirtualHere IPC\r\nPSKeyboard ((none):7575)\r\n --> DualSense Wireless Controller ((none).11)\r\nAuto-Find off\r\n") == "(none).11","CRLF vendor output is parsed line by line")

        let api = try FakeGuestAPI();defer {api.finish()}
        let temporaryListener = try RuntimeSocket.listen(0)
        var addr = sockaddr_in();var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        _ = withUnsafeMutablePointer(to:&addr) {p in p.withMemoryRebound(to:sockaddr.self,capacity:1) {getsockname(temporaryListener,$0,&length)}}
        let frontPort=UInt16(bigEndian:addr.sin_port);close(temporaryListener)
        let relay=NativeRelay(apiPort:api.port,frontendPort:frontPort)
        try relay.start()
        check(relay.busID == 7 && relay.deviceID == 3,"verified ownership")
        let frontend=try RuntimeSocket.connect(frontPort)
        var received:[UInt8]=[]
        while received.count < RuntimeWire.greeting.count {
            try RuntimeSocket.wait(frontend,events:Int16(POLLIN),deadline:RuntimeSocket.now+2)
            var bytes=[UInt8](repeating:0,count:64)
            let count=read(frontend,&bytes,bytes.count)
            guard count > 0 else {throw RuntimeFailure("test_greeting")}
            received += bytes.prefix(count)
        }
        check(received == RuntimeWire.greeting,"frontend greeting")
        try RuntimeSocket.sendComplete(frontend,neutral)
        // No next heartbeat: independent runtime must remove its own bus.
        let deadline=RuntimeSocket.now+3
        while relay.outcome == nil && RuntimeSocket.now < deadline {Thread.sleep(forTimeInterval:0.01)}
        close(frontend)
        check(relay.outcome == "frontend_stale","watchdog triggers without App cooperation")
        check(relay.cleanupConfirmed && api.removed,"owned bus removed after watchdog")
        check(api.frames > 1,"native neutral scheduler ran")
        let retry = NativeRelay(apiPort:api.port,frontendPort:frontPort)
        try retry.start()
        check(retry.busID == 7,"immediate reconnect reuses the released frontend port")
        retry.requestStop()
        check(retry.waitForStop(),"explicit stop removes the new owned bus")
        print("Runtime offline checks passed: \(checks); no VM, HID, vendor client, keyboard or Sony used.")
    }
}
