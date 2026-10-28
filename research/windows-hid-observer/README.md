# Existing virtual DS4 read-only observer

This helper observes the already-created keyboard app controller without
creating a bus/device, sending input, detaching a device, or accessing Sony.
It does not install a keyboard hook or record typed text. It prints only
changes to controller buttons, D-pad, and left-stick state, plus read counts.

The HID enumeration, capability checks, and cancellation-safe read routine
are adapted from this project's previously verified
`research/windows-hid-test/WindowsHidTest.cs`.

## Scope

- Requires exactly one imported USBip device, at the explicitly supplied
  port and loopback URL. It rechecks that condition every five seconds.
- Accepts exactly one HID interface with VID `054C`, PID `09CC`, a matching
  USB ancestor, and controller hardware ID `ROOT\USBIP_WIN2\UDE`.
- Validates HID attributes and a 64-byte gamepad input collection before
  reading. It never opens the physical `054C:05C4` controller.
- Opens the device with read access only and shared read/write access.
- A read timeout cancels only its own operation, then drains completion.
  Unconfirmed pending storage is retained until this short-lived process
  exits; it is never freed or reused while the operation may still use it.
- Duration is explicit and capped at 120 seconds. Slow metadata queries or
  cancellation cleanup can extend process lifetime beyond the sampling time.
- It never detaches, removes, or alters the controller when it exits.

## Build and coordinated use

```powershell
.\research\windows-hid-observer\Build.ps1
& "$env:USERPROFILE\.pskeyboard-tools\windows-hid-observer\WindowsHidObserver.exe" `
    --observe-existing-virtual 1 usbip://127.0.0.1:33241/1-1 120
```

The example port and bus refer to one observed session and must be checked
again before use. Start the observer only when coordinating a real keyboard
test with the user. The `READY` line means the existing virtual HID collection
was validated and opened; it does not prove keyboard capture or PS5 response.

## Coordinated observation on 2026-10-28

The parent ran the observer against the existing app-owned port, after
compilation without warnings. The user enabled the mapper in its own window,
pressed K three times and held W for about one second. Windows HID reports
showed three Cross press/release pairs and left-stick Y moving to 0 and back
to 128. The user then switched to official Remote Play and reported that
menu selection did not move; further left-stick changes were observed in
that interval. We did not observe the exact requested three D-pad pairs,
so those specific button events are not claimed as verified.

The 120-second observation completed successfully: 56,952 reports,
22 state transitions, 1,073 non-neutral reports, no idle timeout, and no
device change. These counts summarize the tool output, not PS5 acceptance.
The user then temporarily unplugged the physical controller and still
reported no response. No Sony restart with only the virtual device was
performed, because the user redirected the project to physical passthrough.

Afterward the app-owned virtual port was verified by its exact loopback URL
and detached; only the mapper and its owned backend process were stopped.
The physical DS4 was present again and Sony was still responding. The
independent virtual route is not an accepted product implementation.
