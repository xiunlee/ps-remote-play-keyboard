# Windows HID acceptance test

Observed on 2026-10-28 after the user approved installing USBip and testing.
The corrected run passed **20 checks** through the complete Windows input
path:

`KeyboardMapping.cs -> pskb-keyboard-merge.dll -> ViiperTransport.cs ->
VIIPER 0.9.0 -> usbip-win2 0.9.8.1 -> Windows HID ReadFile`

This is stronger than the earlier userspace USB/IP readback. Windows actually
created the virtual DS4 and returned its input reports. It is still **not a
Sony Remote Play/game acceptance test**, and does not capture real keyboard
events or verify application foreground handling.

## Run

Requires the approved, installed USBip driver and a VIIPER v0.9.0 server
listening only on loopback API 33242 / USB-IP 33241 with auto-attach disabled.
The official Sony client was closed for the observed test.

```powershell
& .\research\windows-hid-test\Build.ps1
& "$env:USERPROFILE\.pskeyboard-tools\windows-hid-test\WindowsHidTest.exe" `
    --create-temporary-windows-device
```

Unlike the earlier driverless test, this command deliberately creates a
temporary Windows device. It creates its own VIIPER bus, attaches only that
device with `--once`, confirms the returned USBip port's exact loopback URL,
and removes its own port and bus on completion.

## Scope checks

- Records existing `054C:09CC` HID interface paths before attachment and
  excludes all of them from input reads.
- Accepts exactly one new matching collection. It requires a USB ancestor
  with the matching VID/PID and a controller ancestor with hardware ID
  `ROOT\USBIP_WIN2\UDE`.
- Checks actual HID attributes and 64-byte gamepad capabilities before
  reading input. Physical DS4 `054C:05C4` is never opened.
- Confirms port ownership again immediately before detach. It never calls
  detach-all or changes existing devices.
- Sends neutral before cleanup. The actual application's mapping and
  transport sources are compiled directly into this test.
- Uses explicit unmanaged OVERLAPPED/buffer storage. Timeout requests
  `CancelIoEx`, then waits for completion before freeing it. If completion
  cannot be confirmed, storage is retained until the test process exits;
  there is no reuse of potentially active memory.

## Results and correction

`observed-corrected-2026-10-28.txt` records the successful run. Tests cover
K/Cross press, hold and release; W/A/S/D; W+D normalized diagonal; W+S
neutral; K+I simultaneous face buttons; D-pad up; U/L2 press and release;
disabled held keys returning neutral; right stick remaining centered; and
scoped cleanup.

The first two logs show a test-harness identity error: the guard looked for
`ROOT\USBIP_WIN2` in a PnP **instance ID**. This machine's instance ID is
`ROOT\USB\0000`; the expected identifier is its **hardware ID**. Diagnostics
showed that the virtual HID device had appeared. The guard was corrected to
read `CM_DRP_HARDWAREID` without weakening the VID/PID or new-path checks.
Those attempts sent no test button states and cleaned up their own devices.
They are not evidence of USBip failure.

Primary API references:

- [Microsoft: Obtaining HID reports](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/obtaining-hid-reports)
- [Microsoft: ReadFile](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-readfile)
- [Microsoft: CancelIoEx and completion lifetime](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-cancelioex)
- [usbip-win2 controller hardware ID](https://github.com/vadimgrn/usbip-win2/blob/master/drivers/ude/usbip2_ude.inf)
