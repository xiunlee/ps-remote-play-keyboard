// Copyright (C) 2026 xiunlee. SPDX-License-Identifier: GPL-3.0-only
import Foundation
import Darwin

final class RuntimeOwner {
    let resources: URL
    let helpers: URL
    let support: URL
    let stop: RuntimeStopFlag
    private var guest: OwnedProcess?
    private var client: OwnedProcess?
    private var relay: NativeRelay?
    private var ownedHID: UInt64?
    private var ownedAddress: String?
    private var ownedGuestPort:Int?
    private var baseline: Set<UInt64> = []
    private var lockFD: Int32 = -1
    private var cleanupRan = false
    private var cleanupResult = false
    private var ownsJournal = false
    private var startupDiagnostic:[String:Any] = [:]
    private var clientExecutable: URL { support.appendingPathComponent("Dependencies/VirtualHereUniversal.app/Contents/MacOS/VirtualHereUniversal") }
    private var state: URL { support.appendingPathComponent("RuntimeState") }
    private var shared: URL { state.appendingPathComponent("shared") }
    private var clientConfig: URL { state.appendingPathComponent("client.ini") }
    private var cleanupJournal: URL { state.appendingPathComponent("cleanup-required.json") }
    init(resources: URL, helpers: URL, support: URL, stop: RuntimeStopFlag) {
        self.resources = resources; self.helpers = helpers; self.support = support; self.stop = stop
    }
    static func status(_ event: String, _ value: String) {
        let data = try! JSONSerialization.data(withJSONObject:["event":event,"value":value],options:[.sortedKeys])
        let line = data + Data([10])
        _ = line.withUnsafeBytes { Darwin.write(STDOUT_FILENO,$0.baseAddress,$0.count) }
    }
    private func progress(_ value: String) { Self.status("progress",value) }
    func run() throws {
        do {
            progress("Checking components…")
            try prepare()
            try cancelled()
            baseline = try RuntimeHID.dualSenseEntries()
            // Reserve every external endpoint before launching any guest resource.
            for port: UInt16 in [33742,33743,7575] { let fd = try RuntimeSocket.listen(port); close(fd) }
            progress("Starting controller services…")
            guest = try OwnedProcess(executable:helpers.appendingPathComponent("PSKeyboardGuest"),
                arguments:[resources.appendingPathComponent("Image").path,resources.appendingPathComponent("initrd.gz").path,shared.path],inputEnabled:true)
            try recordOwnership()
            guard let guest else { throw RuntimeFailure("guest_start") }
            _ = try guest.waitFor("GUEST_READY",seconds:35,stop:stop)
            try guest.write("stty -echo\n")
            try stage("bridges") { _ = try guest.command("umask 077; start-loopback-bridges; mkdir -p /run/pskb",stop:stop) }
            try stage("syslog") {
                _ = try guest.command("umask 077; touch /run/pskb/system.log; busybox syslogd -n -O /run/pskb/system.log -s 64 -b 0 </dev/null >/run/pskb/syslog-console.log 2>&1 & echo $! > /run/pskb/syslog.pid",stop:stop)
                try until(seconds:2) { (try? guest.command("test -S /dev/log; kill -0 \"$(cat /run/pskb/syslog.pid)\"",seconds:1,stop:stop)) != nil }
            }
            try stage("backend") { _ = try guest.command("""
            mkfifo -m 600 /run/pskb/control
            sleep 2147483647 > /run/pskb/control &
            echo $! > /run/pskb/keeper.pid
            /mnt/host/GuestDualSenseBackend-linux-arm64 --guest-candidate-preview < /run/pskb/control > /run/pskb/backend.log 2>&1 &
            echo $! > /run/pskb/backend.pid
            """,stop:stop) }
            try stage("modules") { _ = try guest.command("""
            test -d /sys/module/dummy || insmod /mnt/host/dummy.ko numdummies=0
            test -d /sys/module/af_packet || insmod /mnt/host/af_packet.ko
            """,stop:stop) }
            try stage("identity") { _ = try guest.command("""
            ip link add psusb0 type dummy
            pskb_mac=$(cat /mnt/host/guest-mac.txt)
            ip link set psusb0 address "$pskb_mac"
            ip link set psusb0 up
            test "$(cat /sys/class/net/psusb0/address)" = "$pskb_mac"
            """,stop:stop) }
            try stage("vendor_start") { _ = try guest.command("""
            cp /mnt/host/vh-config.ini /run/pskb/vh-config.ini
            /mnt/host/vhusbdarm64 -c /run/pskb/vh-config.ini </dev/null > /run/pskb/vh.log 2>&1 &
            echo $! > /run/pskb/vh.pid
            """,stop:stop) }
            try stage("backend_ready") { try until(seconds:10) { (try? RuntimeSocket.validatePing(RuntimeSocket.request("ping"))) != nil } }
            // Readiness is the owned process plus its listening socket, not a
            // vendor log line whose destination/buffering can change.
            try stage("vendor_ready") { try until(seconds:8) {
                (try? guest.command("kill -0 \"$(cat /run/pskb/vh.pid)\"; cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | awk '$2 ~ /:1D97$/ && $4 == \"0A\" { found=1 } END { exit !found }'",seconds:2,stop:stop)) != nil
            } }
            // Store the generated vendor configuration before exposing a device.
            try stage("vendor_config") { try persistGuestConfiguration() }
            progress("Connecting the controller…")
            try stage("client_start",prefix:"virtualhere") {
                client = try OwnedProcess(executable:clientExecutable,
                    arguments:["-c",clientConfig.path,"--autofind-off","--start-minimized"])
                try recordOwnership()
            }
            var initialListing = ""
            try stage("client_ipc",prefix:"virtualhere") { try until(seconds:15) {
                guard let listing = try? self.clientCommand("LIST") else { return false }
                initialListing = listing
                return Self.listFlag("Auto-Find",in:listing) != nil
            } }
            try stage("client_config",prefix:"virtualhere") {
                // This is our dedicated client, started without any manual hubs.
                // Use its public API, rather than guessing INI serialization.
                guard Self.listFlag("Auto-Find",in:initialListing) == false,
                      Self.listFlag("Reverse Lookup",in:initialListing) == false,
                      Self.listFlag("Reverse SSL Lookup",in:initialListing) != true else {
                    throw RuntimeFailure("discovery_not_disabled")
                }
                try clientOK("AUTO USE CLEAR ALL")
                try clientOK("MANUAL HUB REMOVE ALL")
                try clientOK("MANUAL HUB ADD,127.0.0.1:7575")
                let hubs = try clientCommand("MANUAL HUB LIST")
                guard hubs.contains("127.0.0.1:7575") else { throw RuntimeFailure("manual_hub_unconfirmed") }
            }
            // Wait for the vendor's own USB event monitor after client connect.
            // A listening TCP socket alone does not mean hotplug is observed.
            captureGuestDiagnostics(key:"vendorBeforeMonitor")
            try stage("vendor_monitor") { try until(seconds:8) {
                (try? guest.command(Self.vendorMonitorReadyScript,seconds:2,stop:stop)) != nil
            } }
            captureGuestDiagnostics(key:"vendorBeforeAttach")
            progress("Creating the DualSense controller…")
            let candidate = NativeRelay(); relay = candidate; try candidate.start()
            guard let bus = candidate.busID, let device = candidate.deviceID else { throw RuntimeFailure("device_identity") }
            let listing = try guest.command("usbip --tcp-port 33641 list -r 127.0.0.1",stop:stop)
            let busID = "\(bus)-\(device)"
            guard listing.range(of:"(?m)^\\s*" + NSRegularExpression.escapedPattern(for:busID) + ":",options:.regularExpression) != nil else { throw RuntimeFailure("usbip_device_not_listed") }
            try stage("usbip_attach") {
                _ = try guest.command("mkdir -p /var/run/vhci_hcd; usbip --tcp-port 33641 attach -r 127.0.0.1 -b \(busID)",stop:stop)
            }
            var verifyReason="status_unavailable"
            try stage("usbip_verify") { do {try until(seconds:6) {
                let status = try guest.command("cat /sys/devices/platform/vhci_hcd.0/status",seconds:2,stop:stop)
                let rows=RuntimeVHCIImport.safeRows(status)
                self.startupDiagnostic.merge(["stage":"usbip_verify","expectedRemoteID":String(format:"%08x",(bus << 16)|device),"vhciRows":rows,"matched":false]) { _,new in new }
                guard let imported = try RuntimeVHCIImport.matching(status,bus:bus,device:device) else {
                    verifyReason="vhci_no_match_used_\(rows.filter{$0["state"] == "6"}.count)"
                    return false
                }
                let local = imported.localBusID
                self.startupDiagnostic["matched"] = true
                let metadata = try guest.command("printf 'PSKBUSB|%s|%s|%s\\n' \"$(cat /sys/bus/usb/devices/\(local)/idVendor 2>/dev/null)\" \"$(cat /sys/bus/usb/devices/\(local)/idProduct 2>/dev/null)\" \"$(readlink -f /sys/bus/usb/devices/\(local))\"",seconds:2,stop:stop)
                guard let line=metadata.split(whereSeparator:{$0.isNewline}).last(where:{$0.hasPrefix("PSKBUSB|")}) else {verifyReason="sysfs_probe_marker";return false}
                let fields=line.trimmingCharacters(in:.whitespacesAndNewlines).components(separatedBy:"|")
                guard fields.count == 4 else {verifyReason="sysfs_probe_format";return false}
                let vendor=fields[1].range(of:"^[0-9a-f]{4}$",options:.regularExpression) != nil ? fields[1] : "absent"
                let product=fields[2].range(of:"^[0-9a-f]{4}$",options:.regularExpression) != nil ? fields[2] : "absent"
                let ancestry=fields[3].hasPrefix("/sys/devices/platform/vhci_hcd.0/")
                self.startupDiagnostic["vendor"] = vendor;self.startupDiagnostic["product"] = product
                self.startupDiagnostic["vhciAncestry"] = ancestry
                self.startupDiagnostic["sysfsPath"] = fields[3].hasPrefix("/sys/devices/") && fields[3].range(of:"^[A-Za-z0-9_./:-]+$",options:.regularExpression) != nil ? fields[3] : "unrecognized"
                guard vendor == "054c",product == "0ce6",ancestry else {verifyReason="sysfs_vendor_\(vendor)_product_\(product)_ancestry_\(ancestry ? 1 : 0)";return false}
                self.ownedGuestPort = imported.port
                return true
            }} catch let error as RuntimeFailure {
                if error.code == "startup_deadline" {throw RuntimeFailure(verifyReason)}
                throw error
            } }
            try cancelled()
            progress("Connecting the controller…")
            var address: String?
            try stage("device_list",prefix:"virtualhere") { try until(seconds:20) {
                let listing = try self.clientCommand("LIST")
                self.startupDiagnostic["listSucceeded"] = true
                self.startupDiagnostic["listLines"] = listing.split(whereSeparator:{$0.isNewline}).count
                self.startupDiagnostic["listDeviceRows"] = Self.deviceRows(in:listing).count
                self.startupDiagnostic["listHubRows"] = Self.hubRows(in:listing).count
                self.startupDiagnostic["listShapes"] = Self.listShapes(listing)
                self.startupDiagnostic["listContainsDualSense"] = listing.contains("DualSense")
                self.startupDiagnostic["listContainsConnecting"] = listing.localizedCaseInsensitiveContains("connecting")
                guard Self.listFlag("Auto-Find",in:listing) == false,
                      Self.listFlag("Auto-Use All",in:listing) == false,
                      Self.listFlag("Reverse Lookup",in:listing) == false,
                      Self.listFlag("Reverse SSL Lookup",in:listing) != true else {
                    throw RuntimeFailure("discovery_not_disabled")
                }
                address = Self.deviceAddress(in:listing)
                return address != nil
            } }
            guard let address else { throw RuntimeFailure("virtualhere_device_missing") }
            ownedAddress = address
            try stage("device_use",prefix:"virtualhere") { try clientOK("USE," + address) }
            try stage("device_hid",prefix:"virtualhere") { try until(seconds:12) {
                let added = try RuntimeHID.dualSenseEntries().subtracting(self.baseline)
                self.startupDiagnostic["newHIDCount"] = added.count
                if added.count > 1 { throw RuntimeFailure("ambiguous_usb_device") }
                if let entry = added.first { self.ownedHID = entry; return true }
                return false
            } }
            try recordOwnership()
            try cancelled(); Self.status("ready","Controller ready")
            var nextHIDCheck = RuntimeSocket.now + 1
            while !stop.requested {
                try checkChildren()
                if RuntimeSocket.now >= nextHIDCheck,let ownedHID {
                    guard try RuntimeHID.dualSenseEntries().contains(ownedHID) else { throw RuntimeFailure("controller_removed") }
                    nextHIDCheck = RuntimeSocket.now + 1
                }
                _ = try guest.readAvailable(); _ = try client?.readAvailable()
                Thread.sleep(forTimeInterval:0.05)
            }
            let clean = cleanup()
            guard clean else { throw RuntimeFailure("cleanup_unconfirmed") }
            Self.status("stopped","Stopped")
        } catch {
            if ["vendor_monitor","device_list"].contains(startupDiagnostic["stage"] as? String ?? "") { captureGuestDiagnostics(key:"vendorAtFailure") }
            startupDiagnostic["failure"] = (error as? RuntimeFailure)?.code ?? "runtime_failure"
            if !startupDiagnostic.isEmpty, let data=try? JSONSerialization.data(withJSONObject:startupDiagnostic,options:[.sortedKeys,.prettyPrinted]) {
                try? writePrivate(data,state.appendingPathComponent("startup-diagnostic.json"))
            }
            let clean = cleanup()
            if stop.requested && clean { Self.status("stopped","Stopped"); return }
            throw clean ? error : RuntimeFailure("cleanup_unconfirmed")
        }
    }
    private func cancelled() throws { if stop.requested { throw RuntimeFailure("cancelled") }; try checkChildren() }
    private func stage(_ name:String,prefix:String="guest",_ action:() throws -> Void) throws {
        startupDiagnostic["stage"] = name
        do { try action() }
        catch let error as RuntimeFailure {
            if stop.requested { throw RuntimeFailure("cancelled") }
            throw RuntimeFailure(prefix + "_" + name + "_" + error.code)
        }
    }
    private func checkChildren() throws {
        if let guest, !guest.running { throw RuntimeFailure("guest_ended") }
        if let client, !client.running { throw RuntimeFailure("virtualhere_ended") }
        if let outcome = relay?.outcome { throw RuntimeFailure("relay_" + outcome) }
    }
    private func until(seconds:Double, _ predicate:() throws -> Bool) throws {
        let deadline = RuntimeSocket.now + seconds
        repeat {
            try cancelled()
            if try predicate() { return }
            _ = try guest?.readAvailable(); _ = try client?.readAvailable()
            Thread.sleep(forTimeInterval:0.10)
        } while RuntimeSocket.now < deadline
        throw RuntimeFailure("startup_deadline")
    }
    private func prepare() throws {
        let fm = FileManager.default
        try fm.createDirectory(at:state,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
        guard (try state.resourceValues(forKeys:[.isSymbolicLinkKey])).isSymbolicLink != true else { throw RuntimeFailure("state_symlink") }
        lockFD = open(state.appendingPathComponent("runtime.lock").path,O_CREAT|O_RDWR|O_CLOEXEC|O_NOFOLLOW,0o600)
        guard lockFD >= 0, flock(lockFD,LOCK_EX|LOCK_NB) == 0 else { throw RuntimeFailure("already_running") }
        try verifyPreviousCleanup()
        for file in ["Image","initrd.gz","GuestDualSenseBackend-linux-arm64","dummy.ko","af_packet.ko"] {
            guard fm.fileExists(atPath:resources.appendingPathComponent(file).path) else { throw RuntimeFailure("runtime_components_missing") }
        }
        guard fm.isExecutableFile(atPath:clientExecutable.path), fm.isExecutableFile(atPath:helpers.appendingPathComponent("PSKeyboardGuest").path),
              fm.fileExists(atPath:support.appendingPathComponent("Dependencies/vhusbdarm64").path) else { throw RuntimeFailure("dependencies_missing") }
        let processes = try OwnedProcess(executable:URL(fileURLWithPath:"/bin/ps"),arguments:["-axo","comm="]).collect(seconds:3)
        guard !processes.split(whereSeparator:{$0.isNewline}).contains(where:{URL(fileURLWithPath:String($0).trimmingCharacters(in:.whitespaces)).lastPathComponent == "VirtualHereUniversal"}) else { throw RuntimeFailure("virtualhere_already_running") }
        let vendorApp = clientExecutable.deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
        _ = try OwnedProcess(executable:URL(fileURLWithPath:"/usr/bin/codesign"),arguments:["--verify","--deep","--strict",vendorApp.path]).collect(seconds:5)
        let signature = try OwnedProcess(executable:URL(fileURLWithPath:"/usr/bin/codesign"),arguments:["-dvv",vendorApp.path]).collect(seconds:5)
        guard signature.contains("TeamIdentifier=N8C8ZTN347") else { throw RuntimeFailure("vendor_signature") }
        try fm.createDirectory(at:shared,withIntermediateDirectories:true,attributes:[.posixPermissions:0o700])
        guard (try shared.resourceValues(forKeys:[.isSymbolicLinkKey])).isSymbolicLink != true else { throw RuntimeFailure("state_symlink") }
        for file in ["GuestDualSenseBackend-linux-arm64","dummy.ko","af_packet.ko"] { try copyPrivate(resources.appendingPathComponent(file),shared.appendingPathComponent(file)) }
        try copyPrivate(support.appendingPathComponent("Dependencies/vhusbdarm64"),shared.appendingPathComponent("vhusbdarm64"))
        for file in ["GuestDualSenseBackend-linux-arm64","vhusbdarm64"] { try fm.setAttributes([.posixPermissions:0o700],ofItemAtPath:shared.appendingPathComponent(file).path) }
        let identity = state.appendingPathComponent("guest-mac.txt")
        if !fm.fileExists(atPath:identity.path) {
            var bytes = (0..<6).map {_ in UInt8.random(in:0...255)}; bytes[0] = (bytes[0] | 2) & 0xfe
            try writePrivate(Data((bytes.map{String(format:"%02x",$0)}.joined(separator:":") + "\n").utf8),identity)
        }
        let mac = try String(contentsOf:identity,encoding:.utf8).trimmingCharacters(in:.whitespacesAndNewlines)
        guard mac.range(of:"^[0-9a-f]{2}(:[0-9a-f]{2}){5}$",options:.regularExpression) != nil,
              let first = UInt8(mac.prefix(2),radix:16), first & 3 == 2 else { throw RuntimeFailure("guest_identity_invalid") }
        try copyPrivate(identity,shared.appendingPathComponent("guest-mac.txt"))
        let config = state.appendingPathComponent("vh-config.ini")
        if !fm.fileExists(atPath:config.path) {
            try writePrivate(Data("ServerName=PSKeyboard\nUseAVAHI=0\nAllowedDevices=54c/ce6\n".utf8),config)
        }
        try copyPrivate(config,shared.appendingPathComponent("vh-config.ini"))
        let previous = fm.fileExists(atPath:clientConfig.path) ? try String(contentsOf:clientConfig,encoding:.utf8) : ""
        try writePrivate(Data(Self.preparedClientConfiguration(previous).utf8),clientConfig)
        ownsJournal = true
        try recordOwnership()
    }
    private func copyPrivate(_ source:URL,_ destination:URL) throws { try writePrivate(Data(contentsOf:source),destination) }
    private func writePrivate(_ data:Data,_ destination:URL) throws {
        if FileManager.default.fileExists(atPath:destination.path), (try destination.resourceValues(forKeys:[.isSymbolicLinkKey])).isSymbolicLink == true { throw RuntimeFailure("state_symlink") }
        try data.write(to:destination,options:.atomic)
        try FileManager.default.setAttributes([.posixPermissions:0o600],ofItemAtPath:destination.path)
    }
    private func clientCommand(_ command:String,seconds:Double=6.5) throws -> String {
        guard client?.running == true else { throw RuntimeFailure("virtualhere_ended") }
        return try OwnedProcess(executable:clientExecutable,arguments:["-t",command]).collect(seconds:seconds,stop:stop.requested ? nil : stop)
    }
    private func clientOK(_ command:String) throws {
        guard try clientCommand(command).trimmingCharacters(in:.whitespacesAndNewlines) == "OK" else {
            throw RuntimeFailure("api_command_rejected")
        }
    }
    static func preparedClientConfiguration(_ previous:String) -> String {
        // Preserve identity, saved credentials and all other unknown values.
        // Remove misplaced keys written by earlier preview builds, then put
        // managed startup values into the vendor's actual INI sections.
        let managed:Set<String> = ["AutoFind","ReverseLookup","ManualHubs","AutoUseAll","Language"]
        var lines:[String] = []
        var general = false, settings = false
        for raw in previous.split(whereSeparator:{$0.isNewline}) {
            let line = String(raw), trimmed = line.trimmingCharacters(in:.whitespaces)
            if managed.contains(String(trimmed.prefix(while:{$0 != "="}))) { continue }
            lines.append(line)
            if trimmed == "[General]",!general {
                lines += ["AutoFind=0","ReverseLookup=0","Language=EN-US"];general = true
            } else if trimmed == "[Settings]",!settings {
                lines.append("ManualHubs=");settings = true
            }
        }
        if !general { lines += ["[General]","AutoFind=0","ReverseLookup=0","Language=EN-US"] }
        if !settings { lines += ["[Settings]","ManualHubs="] }
        return lines.joined(separator:"\n") + "\n"
    }
    static func listFlag(_ name:String,in listing:String) -> Bool? {
        let lines = listing.split(whereSeparator:{$0.isNewline}).map{$0.trimmingCharacters(in:.whitespaces)}
        let values = lines.filter{$0.hasPrefix(name + " currently ")}
        guard values.count == 1 else { return nil }
        if values[0] == name + " currently on" { return true }
        if values[0] == name + " currently off" { return false }
        return nil
    }
    static func listShapes(_ listing:String) -> [String] {
        // Retain delimiters for format debugging, never names, addresses or IDs.
        let punctuation = CharacterSet(charactersIn:" ()[]<>:.,*/=+-|\t")
        return listing.split(whereSeparator:{$0.isNewline}).prefix(32).map { line in
            let shape = line.prefix(160).unicodeScalars.map { scalar -> String in
                if CharacterSet.alphanumerics.contains(scalar) || scalar == "_" { return "x" }
                return punctuation.contains(scalar) ? String(scalar) : "?"
            }.joined()
            return shape.replacingOccurrences(of:"x+",with:"x",options:.regularExpression)
        }
    }
    static func guestDiagnosticCounts(_ output:String) -> [String:Int]? {
        guard let line = output.split(whereSeparator:{$0.isNewline}).last(where:{$0.hasPrefix("PSKBDIAG|")}) else { return nil }
        let fields = line.components(separatedBy:"|")
        let names = ["listening","connections","netlinkUSBMonitors","singleDeviceModeLines","foundDeviceLines","failureLines","serialFailureLines","syslogBytes","ownedNetlinkUSBMonitors"]
        guard fields.count == names.count + 1 else { return nil }
        var result:[String:Int] = [:]
        for (name,field) in zip(names,fields.dropFirst()) {
            guard let value = Int(field),value >= 0,value <= 1_048_576 else { return nil }
            result[name] = value
        }
        return result
    }
    static let vendorMonitorAwk = "$2 == 15 && $4 != \"00000000\" && index(fds,\"socket:[\" $10 \"]\") {ready=1} END {exit !ready}"
    static let vendorMonitorReadyScript = """
    pskb_vh_pid=$(cat /run/pskb/vh.pid)
    kill -0 "$pskb_vh_pid"
    pskb_vh_fds=$(for pskb_fd in /proc/"$pskb_vh_pid"/fd/*; do readlink "$pskb_fd" || :; done | tr '\\n' ' ')
    awk -v fds="$pskb_vh_fds" '\(vendorMonitorAwk)' /proc/net/netlink
    cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | awk '$2 ~ /:1D97$/ && $4 == "01" {ready=1} END {exit !ready}'
    """
    private func captureGuestDiagnostics(key:String) {
        guard let guest,guest.running,!stop.requested else { return }
        // Vendor messages go to syslog, not stdout. Only fixed numeric counters
        // leave the guest; the bounded log remains in disposable guest RAM.
        let script = """
        pskb_tcp=$(cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | awk '$2 ~ /:1D97$/ {if ($4 == "0A") l++; if ($4 == "01") c++} END {printf "%d|%d",l+0,c+0}')
        pskb_mon=$(awk '$2 == 15 {n++} END {print n+0}' /proc/net/netlink)
        pskb_log=$(awk '/max_devices=1/ {m++} /Found.*(speed|device)/ {f++} /[Ee]rror|[Ff]ail/ {e++} /Failed to load serial number/ {s++} END {printf "%d|%d|%d|%d",m+0,f+0,e+0,s+0}' /run/pskb/system.log)
        pskb_bytes=$(wc -c < /run/pskb/system.log)
        pskb_vh_pid=$(cat /run/pskb/vh.pid)
        pskb_vh_fds=$(for pskb_fd in /proc/"$pskb_vh_pid"/fd/*; do readlink "$pskb_fd" || :; done | tr '\\n' ' ')
        pskb_owned_mon=$(awk -v fds="$pskb_vh_fds" '$2 == 15 && $4 != "00000000" && index(fds,"socket:[" $10 "]") {n++} END {print n+0}' /proc/net/netlink)
        printf 'PSKBDIAG|%s|%s|%s|%s|%s\\n' "$pskb_tcp" "$pskb_mon" "$pskb_log" "$pskb_bytes" "$pskb_owned_mon"
        """
        if let output = try? guest.command(script,seconds:2),let counts = Self.guestDiagnosticCounts(output) {
            startupDiagnostic[key] = counts
        }
    }
    static func deviceRows(in listing:String) -> [String] {
        listing.split(whereSeparator:{$0.isNewline}).map{String($0)}.filter{$0.contains("-->")}
    }
    static func hubRows(in listing:String) -> [String] {
        listing.split(whereSeparator:{$0.isNewline}).map{String($0)}.filter {
            !$0.contains("-->") && $0.range(of:"\\([^\\r\\n]*:[0-9]+\\)\\s*$",options:.regularExpression) != nil
        }
    }
    static func deviceAddress(in listing:String) -> String? {
        // The only allowed hub is the owned diskless guest. Its sole imported
        // USB device has already passed remote ID, VID/PID and VHCI ancestry
        // verification. Product names and cached nicknames are not identities.
        let rows = deviceRows(in:listing), hubs = hubRows(in:listing)
        // LIST displays the server's advertised hostname, which can be `(none)`;
        // the single transport endpoint was set explicitly through MANUAL HUB.
        guard rows.count == 1,hubs.count == 1 else { return nil }
        let matches = rows.compactMap { line -> String? in
            let text = String(line)
            let regex = try! NSRegularExpression(pattern:"\\(([A-Za-z0-9_.():%\\-]+\\.[0-9]+)\\)\\s*$")
            guard let match = regex.firstMatch(in:text,range:NSRange(text.startIndex...,in:text)),
                  let range = Range(match.range(at:1),in:text) else { return nil }
            return String(text[range])
        }
        return matches.count == 1 ? matches[0] : nil
    }
    private func persistGuestConfiguration() throws {
        guard let guest,guest.running else { return }
        let encoded = try guest.command("base64 /run/pskb/vh-config.ini",seconds:5)
        guard let bytes = Data(base64Encoded:encoded,options:.ignoreUnknownCharacters), !bytes.isEmpty, bytes.count <= 65536,
              String(data:bytes,encoding:.utf8) != nil else { throw RuntimeFailure("guest_configuration_read") }
        try writePrivate(bytes,state.appendingPathComponent("vh-config.ini"))
    }
    private func verifyPreviousCleanup() throws {
        guard FileManager.default.fileExists(atPath:cleanupJournal.path) else { return }
        guard let object = try JSONSerialization.jsonObject(with:Data(contentsOf:cleanupJournal)) as? [String:Any],
              let entries = object["hidEntries"] as? [NSNumber], let children = object["children"] as? [[String:Any]] else { throw RuntimeFailure("cleanup_unconfirmed") }
        guard Set(entries.map{$0.uint64Value}).isDisjoint(with:try RuntimeHID.dualSenseEntries()) else { throw RuntimeFailure("cleanup_unconfirmed") }
        for child in children {
            guard let pid = child["pid"] as? Int, let expected = child["executable"] as? String, pid > 0 else { throw RuntimeFailure("cleanup_unconfirmed") }
            let current = try? OwnedProcess(executable:URL(fileURLWithPath:"/bin/ps"),arguments:["-p",String(pid),"-o","comm="]).collect(seconds:2)
            guard current?.trimmingCharacters(in:.whitespacesAndNewlines) != expected else { throw RuntimeFailure("cleanup_unconfirmed") }
        }
        try FileManager.default.removeItem(at:cleanupJournal)
    }
    private func recordOwnership() throws {
        guard ownsJournal else { return }
        let children:[[String:Any]] = [guest,client].compactMap({$0}).map {
            ["pid":Int($0.process.processIdentifier),"executable":$0.process.executableURL?.path ?? ""]
        }
        let entries:[UInt64] = ownedHID.map{[$0]} ?? []
        try writePrivate(JSONSerialization.data(withJSONObject:["hidEntries":entries,"children":children],options:[.sortedKeys]),cleanupJournal)
    }
    private func cleanup() -> Bool {
        if cleanupRan { return cleanupResult }; cleanupRan = true
        var children: [[String:Any]] = []
        for child in [guest,client].compactMap({$0}) {
            children.append(["pid":Int(child.process.processIdentifier),"executable":child.process.executableURL?.path ?? ""])
        }
        relay?.requestStop()
        let relayClean = relay?.waitForStop() ?? true
        if client?.running == true,let ownedAddress { _ = try? clientCommand("STOP USING," + ownedAddress) }
        try? persistGuestConfiguration()
        if client?.running == true { _ = try? clientCommand("EXIT") }
        let clientStopped = client?.terminate() ?? true
        client = nil
        var guestStopped = true
        if let guest,guest.running {
            if let ownedGuestPort { _ = try? guest.command("usbip detach -p \(ownedGuestPort)",seconds:2) }
            _ = try? guest.command("test ! -p /run/pskb/control || printf 'stop\\n' > /run/pskb/control",seconds:2)
            try? guest.write("poweroff -f\n")
            let deadline = RuntimeSocket.now + 3
            while guest.running && RuntimeSocket.now < deadline { _ = try? guest.readAvailable(); Thread.sleep(forTimeInterval:0.05) }
            guestStopped = guest.terminate()
        }
        guest = nil
        let deadline = RuntimeSocket.now + 5
        while let ownedHID,(try? RuntimeHID.dualSenseEntries().contains(ownedHID)) == true,RuntimeSocket.now < deadline { Thread.sleep(forTimeInterval:0.05) }
        let remainingSnapshot = try? RuntimeHID.dualSenseEntries()
        let remaining = remainingSnapshot ?? Set(ownedHID.map{[$0]} ?? [])
        let removed = remainingSnapshot != nil && (ownedHID.map{!remaining.contains($0)} ?? (ownedAddress == nil || remaining.subtracting(baseline).isEmpty))
        cleanupResult = removed && clientStopped && guestStopped && (relayClean || guestStopped)
        if !cleanupResult && ownsJournal {
            let entries = ownedHID.map { remaining.contains($0) ? [$0] : [] } ?? Array(remaining.subtracting(baseline))
            if let data = try? JSONSerialization.data(withJSONObject:["hidEntries":entries,"children":children],options:[.sortedKeys]) {
                try? writePrivate(data,cleanupJournal)
            }
        } else if cleanupResult && ownsJournal {
            do {try FileManager.default.removeItem(at:cleanupJournal)} catch {cleanupResult = false}
        }
        if lockFD >= 0 { _ = flock(lockFD,LOCK_UN); close(lockFD); lockFD = -1 }
        // Destroying the owned diskless VM also removes an uncertain guest bus.
        Self.status("cleanup",cleanupResult ? "confirmed" : "unconfirmed")
        return cleanupResult
    }
}
