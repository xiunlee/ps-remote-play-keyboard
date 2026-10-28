# Physical USB/IP relay research

This is a bounded transport library, not a working Remote Play claim. It imports
one explicitly selected, physically backed Sony DS4 through a separate USB/IP
server and forwards its traffic. It does not synthesize a controller identity,
implement authentication responses, open HID devices, install drivers, bind USB
devices, attach a USB/IP client, or interact with the Sony process.

## Public API

`PsKeyboard.PhysicalRelay.UsbIpRelay` provides:

```csharp
new UsbIpRelay(listenPort, upstreamPort, busId, productId, inputEndpoint, merge);
relay.Start();
relay.IsAttached;
relay.EndpointVerified;
relay.InputReports;
relay.MergedReports;
relay.Failure;
relay.Dispose();
```

Both network endpoints are fixed to IPv4 loopback. The listener accepts one
connection/session and stops listening after that connection. Create a fresh
instance for a retry. Only an exact, configured import bus ID is accepted; the
upstream import response must agree and identify vendor `054c` and the selected
product `05c4` or `09cc`. Device listing and unrelated protocol operations are
rejected. A bind/attach controller must separately verify the intended physical
Windows instance; VID/PID alone does not uniquely identify a controller.

`IsAttached` means the import handshake succeeded, not that Sony accepts the
controller. `InputReports` counts matching report-shaped responses even before
endpoint proof. `MergedReports` counts reports whose contents actually changed.
Neither counter establishes that Remote Play or PS5 consumed the input.

## Input modification boundaries

The callback receives a private copy of a fresh physical report, never a prior
merged report. It runs only for a successful, non-ISO, 64-byte input response with
report ID `01`, on the configured endpoint, matching its original request.

Before any callback runs, a complete successful configuration-descriptor response
must establish that this endpoint is an IN interrupt endpoint with a 64-byte
maximum packet size under an HID interface. Its configuration and alternate
setting must be active. Initial nine-byte descriptor reads do not establish this.
Successful configuration/interface changes update the gate. No proof means
unchanged passthrough. Endpoint number alone never enables modification.

The callback may change only left-stick bytes 1/2, button bytes 5/6, the low two
bits of byte 7, and trigger bytes 8/9. It must preserve every other byte, including
the right stick and report counter. Invalid results terminate the session. A
null callback performs unchanged passthrough. Callbacks must be bounded and must
not perform blocking device or network operations.

Control, descriptor, feature (including F0/F1/F2), output, other report IDs, short
reports, error responses, and ISO traffic are not modified. This says what this
relay does; the separate server/client drivers may make USB transport adaptations
of their own. This library does not verify cryptographic authentication.

## Framing and shutdown

Request/response tracking uses sequence IDs and original request direction;
server headers normally contain zero direction and endpoint fields. The parser
supports both 0 and -1 for non-ISO packet count, carries ISO descriptors, and
handles unlink winning or losing the completion race. Limits are 1 MiB per
transfer, 1,024 ISO packets per transfer, and 4,096 pending requests. Malformed,
unsupported, truncated, and unmatched frames terminate the session.

Disposal closes both TCP sides and the listener. It cannot itself detach a client
device, unbind a physical export, or prove that the local physical controller has
been restored. Those operations belong to the owning application/installation
workflow. A disconnected relay interrupts controller traffic. There is no
automatic reconnect or fallback that could silently change the selected device.

Only generic failure categories and aggregate counters are exposed; the library
does not log raw USB payloads, authentication data, serials, or button history.

## Verification performed

Run `BuildAndTest.ps1` to compile with the installed .NET Framework C# compiler
and warnings treated as errors. Output defaults outside the repository to
`%USERPROFILE%\.pskeyboard-tools\physical-usb-relay`.

On 2026-10-28, **107 offline protocol checks passed** using fragmented in-memory
streams. They cover out-of-order input completion, descriptor gating, active
alternate/configuration changes, exact control/feature/output passthrough,
ISO framing, unlink races, malformed bounds, truncated streams, pending-request
limits, and guards against modifying unrelated report bytes.

An additional **31 real loopback TCP checks passed** against disposable fake
server/client peers on temporary ports. They cover fragmented import and URB
messages, interleaved input replies, feature/output preservation, peer disconnect,
refused upstream connections, released listener ports, and disposal while accept,
partial import, or both imported-session readers are blocked. Deliberate disposal
does not report a failure. A truly stalled loopback connection attempt was not
tested because it cannot be induced reliably without network configuration changes.

The protocol-only checks opened no sockets. The TCP checks opened loopback sockets
only. Neither group used USB/IP drivers, hardware devices, or the Sony process.
Actual server/client interoperability, physical DS4 capture/release, and official
Remote Play acceptance remain unverified here.

Primary protocol references:

- [Linux USB/IP protocol](https://docs.kernel.org/usb/usbip_protocol.html)
- [usbipd-win v5.3.0 endpoint handling](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/AttachedEndpoint.cs)
- [usbipd-win v5.3.0 unlink handling](https://github.com/dorssel/usbipd-win/blob/v5.3.0/Usbipd/AttachedClient.cs)
