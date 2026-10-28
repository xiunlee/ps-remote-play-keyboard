# VIIPER candidate: keyboard-only feeder

Assessed 2026-10-28 against upstream `Alia5/VIIPER` main commit
`20aa112c6ea47187526dd88d0fd666a68a5f4416`. No VIIPER/USBIP installation or
device attachment was performed by this assessment.

## What is concrete

VIIPER supplies a receiving interface for controller states. A self-made
keyboard mapper can feed its local TCP API, while `usbip-win2` provides the
Windows virtual USB transport. This is a different transport from the
previously removed ViGEmBus, and does not require inserting code into Sony's
process. It creates a separate virtual controller; it does not merge into
the plugged-in physical DS4 automatically.

Upstream documents both `dualshock4` and `dualsense` device types. Its server
is GPL-3.0-or-later, while generated client libraries are MIT. The standalone
TCP approach keeps our mapper separate from the emulator; distribution still
needs the applicable notices and source-availability obligations.

Primary sources:

- [README](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/README.md)
- [TCP API](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/docs/api/overview.md)
- [DS4 state format](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/docs/devices/dualshock4.md)
- [DualSense state format](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/docs/devices/dualsense.md)

## Smallest keyboard test

1. Start server bound to loopback only; avoid exposing either API or USBIP
   listener to the LAN.
2. Create a bus and add `dualshock4`.
3. Open `bus/{busId}/{devId}` followed by a null byte within the default
   5-second connection deadline.
4. Send 31-byte little-endian **VIIPER state packets**, not 64-byte raw USB
   DS4 reports. Zero axes mean center. Button mask `0x0020` means Cross.
   D-pad zero means neutral; `0x08` means right in this API.
5. Send neutral immediately on key release, pause, focus loss or shutdown.
   A neutral state should also initialize `AccelZ` to `-8192` as upstream's
   constructor does. Drain feedback separately.
6. Verify successful Windows attachment, then official Remote Play reception.
   API add success does not prove attachment: upstream says auto-attach
   failures can be logged without failing the add response.

## Unverified compatibility

No verified Sony Remote Play 9.5 success was found in the inspected README,
device docs, repository issue/PR bodies or targeted web search. Search results
about Steam Remote Play are a different product and do not establish Sony
compatibility. Upstream's statement that it emulates a complete controller
must not be converted into a local PS5 acceptance claim.

In the inspected DS4 source, feature-report support is explicitly limited to
calibration/capability/identity/telemetry/probe handlers; the control switch
does not implement report IDs `0xF0`, `0xF1` or `0xF2`, nor a challenge-response
forwarder to a physical DS4. Unknown feature GET requests return unhandled.
The inspected DualSense source has similarly enumerated metadata handlers,
not an identified authentication forwarder. This does not by itself prove
that Sony 9.5 requires a particular omitted report.

- [DS4 control and feature handlers](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/device/dualshock4/device.go)
- [DS4 feature IDs](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/device/dualshock4/const.go)
- [DualSense control and feature handlers](https://github.com/Alia5/VIIPER/blob/20aa112c6ea47187526dd88d0fd666a68a5f4416/device/dualsense/device.go)

The Windows transport is an additional driver dependency. The
[usbip-win2 README](https://github.com/vadimgrn/usbip-win2#install-usbip)
states that signed release drivers are available and that installation
restarts all USB 3.0 hubs, briefly disconnecting attached USB devices.
That installation can interrupt the currently working physical hand
controller. A release package's actual signature must be verified; no test
signing or trust-store change is implied by this candidate assessment.
