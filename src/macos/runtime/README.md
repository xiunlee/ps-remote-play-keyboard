# Native Mac runtime integration

The App launches `Contents/Helpers/PSKeyboardRuntime` and keeps its stdin pipe
open. Closing that pipe requests cleanup. Status stdout contains only JSON
`event` / `value` records; serial output, vendor configuration and input frames
are never copied to status or persisted in a diagnostic log.

The helper owns a diskless VM, one guest bus / DualSense stream, one official
VirtualHere client process and the resulting Mac HID entry. It does not open
physical HID devices or change Sony Remote Play. A runtime lock and fixed
loopback ports reject concurrent sessions. A pre-existing VirtualHere client is
not adopted or stopped.

## Bundle contract

- `Contents/Helpers/PSKeyboardRuntime`: `RuntimeProtocol.swift`,
  `NativeRelay.swift`, `RuntimeProcess.swift`, `RuntimeOwner.swift`,
  `RuntimeMain.swift`, compiled together in Swift 6.
- `Contents/Helpers/PSKeyboardGuest`: `GuestRunner.swift`, compiled separately
  with Swift 5 language compatibility and warnings as errors, and signed with
  `com.apple.security.virtualization`. Its independent stdin EOF lease stops
  the VM if the runtime supervisor exits unexpectedly.
- `Contents/Resources/Runtime`: `Image`, `initrd.gz`,
  `GuestDualSenseBackend-linux-arm64`, `dummy.ko`, `af_packet.ko`.
- The App target includes `MacRuntimeController.swift`; no host Python runtime
  or Swift / Go compiler is needed by these executables.

`~/Library/Application Support/PSKeyboard/Dependencies` must contain the
unaltered official `VirtualHereUniversal.app` and `vhusbdarm64`. Acquisition,
license presentation, hash verification and public-package notices belong to
the installer. The runtime independently verifies the client signature and
expected vendor team before launch.

`RuntimeState/guest-mac.txt` and `RuntimeState/vh-config.ini` are private, stable
per-installation state. A migration must preserve the existing tested identity
and vendor configuration. A new install generates one local unicast MAC once;
an invalid saved MAC is an error, never a reason to rotate it. The read-only
guest share is assembled under `RuntimeState/shared`. `client.ini` disables
discovery and auto-use, and points only to `127.0.0.1:7575`.

The tested guest initrd must expose `GUEST_READY`, a shell on hvc0, and
`start-loopback-bridges`. If rebuilding it, load `dummy` with `numdummies=0` so
that an unintended random dummy interface does not precede the saved identity.
The native supervisor loads the supplied modules only if absent, starts the
vendor server before USB/IP attach, observes the USB/IP bus ID and VirtualHere
address, and confirms one new matching USB HID entry before reporting ready.

## Safety and lifecycle

The native relay validates the pinned backend marker, creates only one owned
bus, connects its stream before the backend's deadline, and sends neutral
frames until the App joins. The stream runs at an 8 ms latest-state interval;
there is no catch-up backlog. Healthy waiting and connected sessions have no
600 / 7200 second research cap. Once the App joins, its missing 250 ms heartbeat,
EOF, bad frame, CRC / sequence error or partial write closes ownership and
removes the bus. The App also retains its separate socket watchdog.

Every startup stage has a deadline and checks cancellation. The App's `stop()`
shares one non-cancellable cleanup task, keeping the main actor responsive
even when its original connection task was cancelled. The total fallback
budget is 45 seconds; normal shutdown should finish much earlier. Cleanup
records actual child termination and HID disappearance. A private ownership
journal is written during startup and removed only after confirmed cleanup.
A subsequent start refuses unresolved owned children / HID entries rather
than guessing or terminating unrelated processes.

Run `python3 src/macos/runtime/run-tests.py` for offline protocol, timing,
fake-API ownership / watchdog and compile checks. Python is used only by this
developer verification command. These checks do not establish VM, vendor
attachment, clean-install or Sony gameplay success; those require the final
integrated App on the target Mac.
