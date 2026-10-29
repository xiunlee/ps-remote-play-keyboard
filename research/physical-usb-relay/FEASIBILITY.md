# Physical DS4 relay feasibility — 2026-10-28

Research only. No exporter installation, device binding, attachment, driver replacement, or Sony process changes were performed for this assessment.

## Conclusion

Implementable candidate: real USB DS4 → usbipd-win → local USB/IP packet proxy → the installed usbip-win2 client → normal Windows HID → Sony Remote Play. The proxy changes the physical controller's completed input reports; control/feature requests continue to the actual controller. This is materially different from the prior independent VIIPER controller.

Protocol compatibility and same-Windows-machine physical-device transport have primary-source support. There is no verified result for this DS4 and Sony Remote Play 9.5. Preserve that distinction: this is a concrete candidate, not a delivered working keyboard controller.

## Pinned upstream

- usbipd-win latest release found: **v5.3.0**, commit `aa3db8b82c4cb5071fd31bc54211606c70886912`.
- Existing client: usbip-win2 **0.9.8.1**; source tag `v.0.9.8.1` resolves to `55e1fa7f0c2157017b02dc1f3236e98169a535e4`.
- [usbipd-win release](https://github.com/dorssel/usbipd-win/releases/tag/v5.3.0) supplies x64 and ARM64 MSI installers. Its bundled VBox drivers are version 7.2.2.

## Evidence

1. The exporter supports USB/IP 1.1.1 and clients other than WSL. Its maintainer confirmed compatibility with usbip-win2 in [discussion 1365](https://github.com/dorssel/usbipd-win/discussions/1365); the requester subsequently reported successful physical J2534 use inside a Windows VM. This is not a same-host DS4 test.
2. [Server.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/Server.cs) accepts TCP clients generally and explicitly handles loopback at lines 110–115. There is no rejection of same-host clients. The listener defaults to all interfaces, not loopback only.
3. A separate upstream implementation author reports actual same-machine physical-device testing with the exact **usbipd-win 5.3.0 + usbip-win2 0.9.8.1** pair: a spare USB drive passed a 16 MiB write/read/hash check, and a YubiKey attached and cleaned up. [remoteusb README lines 535–550](https://github.com/coconutbird/remoteusb/blob/master/README.md#L535). That author explicitly did not qualify authentication functions. This corroborates the architecture, not DS4/Sony compatibility.
4. [ExportedDevice.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/ExportedDevice.cs) reads identity and configuration from the real device's parent hub (lines 133–204). The proxy need not invent VID, PID, descriptors, serial, calibration, or authentication responses.
5. [AttachedEndpoint.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/AttachedEndpoint.cs) copies the setup packet and outgoing payload, sends ordinary transfers through `SUPUSB_IOCTL.SEND_URB`, and returns the actual incoming payload (lines 219–256, 312–337, 392–409). HID class GET_REPORT/SET_REPORT feature traffic therefore reaches the real device rather than a fabricated feature table.
6. Exporting is exclusive. [ConnectedClient.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/ConnectedClient.cs) restarts and captures the physical device before replying to IMPORT. While exported, the original physical Windows HID interface is unavailable; the re-imported interface represents the same hardware through a different host controller.

## Important limits in transparency

This is not literally the original Windows device path. USB topology and PnP identity can change. The exporter also masks the configuration descriptor's remote-wakeup bit (`AttachedEndpoint.cs` lines 350–367), and translates SET_CONFIGURATION, SET_INTERFACE, and endpoint-clear requests into corresponding driver operations. Therefore, do not claim every USB byte or every Windows property remains identical.

Forwarding real feature requests is a sound way to retain hardware responses, but there is no evidence yet that authentication is the reason the earlier synthetic controller was ignored, or that Sony will accept this topology.

## Same-host capture-filter lifecycle

Normal binding uses a **persistent-for-the-session CAPTURE filter**, not a one-shot filter:

- `ConnectedClient.cs` line 182 adds it.
- Line 195 claims the original hardware.
- Lines 205–206 send the successful IMPORT response, allowing client enumeration.
- Line 256 awaits the entire attached session.
- Only line 274, in detach cleanup, removes the filter.

[VBoxUsbMon.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/VBoxUsbMon.cs#L49) matches VID, PID, revision, class, subclass, protocol, and **port number**. It does **not** match hub/bus or serial. If the re-imported copy is seen by VBoxUSBMon on the same numbered port, it could match the still-active capture filter. This is an inferred collision risk from source, not an observed failure on this PC. Do not assert that the filter disappears before clone enumeration.

Two bounded options (the second is now the selected candidate for this PC; it is not an automatic fallback):

- First, use ordinary bind only when the real USB port and actual imported hub port are known to differ; inspect actual PnP location, not merely the CLI attachment slot. Keep the selected physical instance and BUSID fixed. Never add automatic VID/PID-wide binding rules.
- If capture collision or a Windows open-handle veto prevents ordinary binding, `bind --force` is an alternate **per-device driver replacement**, not a harmless retry. [DriverTools.cs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/DriverTools.cs#L95) installs VBoxUSB on the explicitly selected instance; `ConnectedClient.cs` skips adding a monitor capture filter when that device already has VBoxUSB. This can require reboot, persists until unbind, and still needs a controlled identity/collision test. Do not silently choose it.

The parent agent confirmed that the physical DS4 occupies port 1 and the first imported controller would also occupy port 1. Therefore, do not conduct the ordinary-bind same-port test on this PC. The planned experiment should explicitly use one-instance force binding and its exact unbind restoration after user approval.

The current [official VirtualBox monitor source](https://github.com/VirtualBox/virtualbox/blob/main/src/VBox/HostDrivers/VBoxUSB/win/mon/VBoxUsbMon.cpp#L324) enumerates all `GUID_DEVINTERFACE_USB_HUB` interfaces and hooks their driver objects (lines 845–901). No USBip-root exclusion was found in these paths. This current upstream source is not a byte-for-byte proof of the shipped 7.2.2 binary. It does not establish that imported roots are excluded, and should not be used to dismiss the collision risk.

## Exact force-bind and restoration scope

Read-only physical baseline verified on 2026-10-28. The full instance path, parent path, and location are stored only in the machine-local research cache file `physical-ds4-original-driver.json`, outside the public repository. Public-safe properties:

| Property | Value |
| --- | --- |
| Hardware identity | Sony `054C:05C4` |
| Class | `HIDClass` |
| Class GUID | `{745A17A0-74D3-11D0-B6FE-00A0C90F57DA}` |
| Service | `HidUsb` |
| Driver INF | `input.inf` |
| MatchingDeviceId | `USB\Class_03` |
| Driver version | `10.0.26100.9444` |
| Physical hub port | `1` |

`CommandHandlersCli.Bind` selects the one BUSID, persists its exact InstanceId, and passes that instance to `DriverTools.ForceVBoxDriver` ([source lines 153–196](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/CommandHandlersCli.cs#L153)). `SetVBoxDriver` uses `SetupDiOpenDeviceInfo` for this one instance followed by `DiInstallDevice`; it does not replace every `054C:05C4` device or the whole HID class.

**For a successfully forced device, this attachment creates no generic capture filter.** [ConnectedClient lines 174–183](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/ConnectedClient.cs#L174) put both `new VBoxUsbMon()` and `AddFilter` wholly inside `if (!device.HasVBoxDriver)`. The import then opens the already assigned VBoxUSB device. This avoids the normal session's VID/PID/port filter.

`unbind --busid <exact physical BUSID>` removes that device's sharing entry and calls `UnforceVBoxDriver` ([CLI lines 211–237](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/CommandHandlersCli.cs#L211)). [DriverTools lines 117–167](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/DriverTools.cs#L117) first clears the forced driver, then asks Windows to select its default PnP driver for that exact instance. Both operations can indicate reboot required. This is default-driver reselection, not restoration from a saved custom driver backup. On this device the baseline is the normal in-box `HidUsb` / `input.inf` class-03 match, so that is the expected restoration target. Verify these properties and physical input afterward; exit code 0 alone does not prove restoration. No actual force/unforce test has yet occurred.

## Minimal reversible test sequence

The next hardware test should establish raw relay compatibility before enabling keyboard merging:

1. Reconnect the physical `054C:05C4`. Record its full physical instance path, parent chain, BUSID, device and HID descriptors, input size, and original sharing state. Reject a source whose parent chain contains USBip's emulated host controller.
2. Stop the earlier independent synthetic attachment. Close Sony cleanly before claiming the DS4, because open handles can veto device restart. This requires no injection and no PSN credential changes.
3. Install the official x64 usbipd-win only when the parent task proceeds with the installation. Limit its default network exposure to this local use before binding: stock server listens on all interfaces and MSI adds a local-subnet firewall rule.
4. For this PC's confirmed port-1 collision, perform only the separately approved explicit `bind --force` for the recorded physical BUSID. Do not try ordinary bind first or create auto-bind rules/use wildcard hardware IDs. If Windows requests a reboot or binding fails, stop and handle that explicit result; do not repeatedly attach or broaden the binding.
5. Start a loopback-only proxy on a distinct port, forwarding exclusively to `127.0.0.1:3240` and the selected original BUSID. Begin with merging disabled. Refuse a second import and any other BUSID. Attach using the existing client's `--once` behavior so a failure does not become a retry loop.
6. Confirm the imported HID still reports `054C:05C4`, actual physical input moves it, and feature GET/SET transactions pass both ways. Then start Sony and verify physical controls through the relay. Failure here isolates transport/selection compatibility and makes keyboard testing unnecessary.
7. Only after the physical relay works in Sony, enable the existing keyboard merge function on completed DS4 input report 01. Observe real Windows input first, then Sony input.
8. Cleanup order: disable merge and clear key state; detach only the verified proxy-owned attachment; close its sockets; allow exporter to restore the original driver; undo only the sharing registration this test created. Verify the normal physical HID returns. Keep an emergency exact-BUSID detach/unbind path available. Never detach all or unbind all.

## Proxy implementation requirements

- Parse USB/IP framing, not TCP read boundaries. Preserve partial frames, request order per endpoint, multiple pending submissions, cancellation/UNLINK, and short/error replies.
- Track each SUBMIT by sequence number, including direction, endpoint, requested length, setup packet, and ISO count. usbipd's RET_SUBMIT header sets command and sequence only; direction/endpoint are zero, so those reply fields cannot identify the DS4 input stream.
- Discover the HID interrupt-IN endpoint from actual descriptors. Merge only its successful 64-byte report `01` completion; never alter EP0 feature traffic, output, other interfaces, ISO/audio data, errors, descriptors, or authentication exchanges.
- Preserve report bytes outside the existing keyboard mapping fields. Physical source values, counters, battery, sensor, touch, and unknown bytes remain real hardware data.
- On input disable or focus loss, clear the keyboard action mask; subsequent physical reports pass through unmodified. Do not replace the hardware stream with synthesized neutral reports.
- Use one writer per direction and bounded buffers. Reject malformed sizes before allocation. Unexpected traffic should remain opaque when framing is valid, rather than guessed into a DS4 format.

## Signature verification performed without installation

Downloaded official v5.3.0 x64 driver files only, into:
`%USERPROFILE%\.pskeyboard-tools\physical-relay-research`

| File | Authenticode result | Publisher | SHA-256 |
| --- | --- | --- | --- |
| VBoxUSB.sys | Valid | Oracle America, Inc. | `06CEE818949D6DCE18E6EC291B8AC521A5F0C168C792BE8EACD7A8AE99D8FDF9` |
| VBoxUSBMon.sys | Valid | Oracle America, Inc. | `FFCAD560AC7714460248631AA17238F4D2AD9FD5AED87531B93DAB410D23FD87` |
| VBoxUSB.cat | Valid | Microsoft Windows Hardware Compatibility Publisher | `021ADDB8C696FB291A93B3E398CF1F7D3F9D9A4E7CF669360D3D7E1F00C08299` |

The official x64 MSI was subsequently downloaded, inspected read-only, and verified without execution:

- Path: `%USERPROFILE%\.pskeyboard-tools\physical-relay-research\usbipd-win_5.3.0_x64.msi`
- Length: `4501504` bytes.
- Authenticode: **Valid**, signer `Open Source Developer, Frans van Dorsselaer`.
- SHA-256: `1C984914AEC944DE19B64EFF232421439629699F8138E3DDC29301175BC6D938`.

Signed drivers establish publisher/integrity, not DS4 or Remote Play compatibility. The [installer design](https://github.com/dorssel/usbipd-win/wiki/Installer-design) installs a VBoxUSB PnP package, VBoxUSBMon kernel service, and usbipd service. Driver replacement/updates can require a reboot. No test-signing or signature-enforcement change is needed for the verified official driver files.

## Prepared installation/network isolation facts

Stock v5.3.0 has **no listener-address configuration**: `Server.cs` reads `usbipd:Port`, then calls `TcpListener.Create(port)`. A made-up `Server:Address` or `usbipd:Address` switch will not restrict it to loopback.

[Installer/Server.wxs](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Installer/Server.wxs#L18) installs the firewall rule and service as part of the required Server component. There is no separate firewall feature or service-start-disable property in this source. The service is automatic, and custom action `TryStartService` attempts to start it. Omitting WSL/PowerShell features does not omit the firewall rule.

Read-only inspection of the downloaded MSI's `Wix5FirewallException` table confirmed:

- Row key `usbipd`; rule name/display text `usbipd`.
- TCP (protocol 6), local port `3240`, remote scope `LocalSubnet`, all profiles.
- Program reference `[#usbipd.exe]`, normally resolved to `C:\Program Files\usbipd-win\usbipd.exe`.
- Description: `Allow computers on local subnets to access the USBIP Device Host service.`

The helper should find the rule by `DisplayName = usbipd` **and** exact application filter path. Record its actual runtime `Name` before changing it; do not assume no other tool has created a similarly named rule.

Recommended fresh-install transaction, after approval, with **no devices shared during installation**:

1. Verify no existing usbipd service/sharing/policies would be overwritten. Reverify MSI signature/hash, then run `msiexec /i <verified MSI> /qn /norestart /L*v <installation log>`. Do not force a reboot.
2. Stop service `usbipd` and set its startup type to Manual immediately after successful installation. No physical device has been bound at this point.
3. Disable only the confirmed installer firewall allow rule. Add an application-and-port scoped inbound block for non-loopback addresses, all profiles, with an application-owned unique rule name. IPv4 ranges: `0.0.0.0-126.255.255.255` and `128.0.0.0-255.255.255.255`; IPv6 range: `::2-ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff`. This leaves `127.0.0.0/8` and `::1` available for local traffic. Verify the effective firewall is enabled; if isolation cannot be confirmed, do not bind/start exporting.
4. Start the service, verify loopback connectivity, query `usbipd state`, and select the recorded exact physical instance. Do not trust BUSID from an old run if it no longer maps to that instance.
5. Approved bind command: `usbipd bind --busid <verified physical BUSID> --force`. Verify `IsForced` and the exact device driver before attaching the client.
6. Client attaches only to the own loopback proxy: `usbip --tcp-port <proxy port> attach --remote 127.0.0.1 --bus-id <physical BUSID> --once --terse`.

Scoped rollback commands, only after revalidating ownership:

```powershell
# Detach only the imported port whose URL matches this proxy and BUSID.
& 'C:\Program Files\USBip\usbip.exe' detach --port <verified imported port>
# Release/export-restore only the physical BUSID that still maps to the recorded instance.
& 'C:\Program Files\usbipd-win\usbipd.exe' detach --busid <verified physical BUSID>
& 'C:\Program Files\usbipd-win\usbipd.exe' unbind --busid <verified physical BUSID>
```

Then verify the original physical device is back on `HidUsb` / `input.inf`; honor any reboot-required result. Stop the exporter when not needed. Keep the application-owned non-loopback block while the exporter remains installed; do not restore its broad LAN allow rule merely because one test ended. Full removal can use the same verified MSI with `/x`, but only if this task installed it and no unrelated bindings/dependents appeared. Its uninstaller intentionally unbinds all exporter devices, so it must not be used casually as per-device cleanup.
