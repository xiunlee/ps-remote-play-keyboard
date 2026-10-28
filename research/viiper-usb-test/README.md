# VIIPER USB/IP readback test

This test exercises the real VIIPER v0.9.0 emulator over loopback without
installing or attaching a Windows USB device. It creates its own temporary
bus, opens a DS4 feeder stream, imports that emulated device through a
userspace USB/IP connection, and reads its actual 64-byte input reports.
Cleanup removes only the test-owned bus.

It does not capture the keyboard, touch the physical PS4 controller, inject
into Sony, access PSN, or prove Remote Play compatibility. The default mode
tests pre-mapped states. The `--keyboard-mapping` mode calls the actual app's
`KeyboardMapping.cs` and native `pskb-keyboard-merge.dll` before sending the
resulting state to VIIPER.

## Prerequisite and build

Run against a dedicated VIIPER v0.9.0 server whose API and USB/IP listeners
are bound to `127.0.0.1` with **auto-attach disabled**. This is a required
precondition: the API does not expose a setting the test can use to verify
auto-attach state. The explicit command-line switch acknowledges this
isolated server configuration.

The observed run used API port 33242 and USB/IP port 33241. The parent
controller confirmed auto-attach was disabled before running the test.

```powershell
& .\research\viiper-usb-test\Build.ps1
& "$env:USERPROFILE\.pskeyboard-tools\viiper-usb-test\UsbReadbackTest.exe" `
    --isolated-loopback-server 33242 33241
& "$env:USERPROFILE\.pskeyboard-tools\viiper-usb-test\UsbReadbackTest.exe" `
    --isolated-loopback-server 33242 33241 --keyboard-mapping
```

The executable requires only the built-in .NET Framework compiler/runtime.
It rejects a server version other than 0.9.0, limits all network connections
to loopback, uses bounded reads and socket timeouts, and checks USB/IP reply
codes, sequence numbers, lengths and DS4 report ID.

The build includes `src/keyboard-pad/KeyboardMapping.cs` directly and copies
the native engine from `.pskeyboard-tools/native-merge` beside the test
executable. Rebuild that engine first if its source changes. The mapping
mode supplies explicit held virtual-key codes instead of reading keyboard
events; it therefore tests app mapping, not foreground filtering or capture.

## Observed result: 2026-10-28

`observed-2026-10-28.txt` records the successful run: 14 checks passed.
The checks include USB identity, neutral, Cross press/hold/release, all four
movement axes, diagonal state, D-pad, L2 and release back to neutral. Right
stick bytes remain centered throughout; mouse mapping is excluded.

`observed-mapping-2026-10-28.txt` records a second run: 16 checks passed
through the actual application's mapping and native engine. These include
K/Cross press, hold and release, W+D diagonal normalization, W+S neutral,
K+I simultaneous face buttons, U/L2 press and release, and disabled input
returning neutral even with W+K+U held. Expected USB report fields are
checked independently of the application's mapping functions.

The initial run rejected the imported identity because this test initially
expected the physical controller's `054C:05C4`. Upstream v0.9.0's actual
default is `054C:09CC`; the source and imported descriptor agreed. The
expectation was corrected before the successful run. No VID/PID override was
applied to the emulator.

The matching USB/IP report is stronger evidence than merely accepting the
API command, but it is still **not** a Windows HID acceptance test or a Sony
Remote Play/game test.

## References

- [Linux kernel USB/IP protocol](https://www.kernel.org/doc/html/latest/usb/usbip_protocol.html)
- [VIIPER v0.9.0 DS4 device](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualshock4/device.go)
- [VIIPER v0.9.0 DS4 constants](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualshock4/const.go)
- [VIIPER v0.9.0 USB/IP implementation](https://github.com/Alia5/VIIPER/blob/v0.9.0/usbip/usbip.go)
- [VIIPER v0.9.0 upstream userspace test client](https://github.com/Alia5/VIIPER/blob/v0.9.0/_testing/usbip_client.go)

The implementation follows the protocol, rather than copying the upstream
Go test client. It does not contain the VIIPER implementation or binaries.

## Remaining gap

The inspected DS4 emulator has selected metadata feature-report handlers,
not a physical-controller authentication forwarder. Report IDs F0/F1/F2
were not implemented in the inspected source. Whether Sony 9.5 needs any
particular omitted report is unverified; see
[candidate assessment](../static-analysis/VIIPER-ASSESSMENT.md).

The parent agent reports that the usbip-win2 0.9.8.1 installer was downloaded
and its Authenticode signature verified, but **not installed** during this
test. A later Windows attachment test remains separate. Upstream's installer
restarts USB 3.0 hubs and can briefly interrupt attached USB hardware.
