# VIIPER local transport verification

`src/keyboard-pad/ViiperTransport.cs` implements a narrowly scoped C# 5 / .NET
Framework transport for an existing local VIIPER v0.9.0 server. Reference
`System.Web.Extensions.dll` when compiling it. It only connects to `127.0.0.1`;
the caller supplies the port, normally `33242` for this project.

The class verifies the server identity, creates its own bus and DualShock 4,
opens the binary stream, sends a neutral state, and drains fixed-size feedback
on a background thread. It validates management responses only after EOF,
rejects API/authentication failures, enforces response/timeout bounds, and
serializes writers. Disconnect sends a neutral state and removes only its own
allocated bus. A cleanup failure remains visible through `LastFailure` and the
retained bus ID rather than being reported as success.

The upstream stream protocol has **no success acknowledgement**. The client
checks for immediate error/EOF for 200 ms, then starts sending and continues
watching feedback. `IsConnected` means that this local API stream is open; it
does not confirm a Windows USB attachment, hand controller recognition, or PS5
game operation. The transport performs no USBIP attachment, driver installation,
Remote Play process modification, credential reads, or remote-host connections.

The 200 ms interval also separates the text handshake from binary state: the
v0.9 server reads the handshake through a buffered reader and then passes the
raw connection to the device handler. There is no protocol-level guarantee
that this delay proves the server has consumed the handshake.

## Run the mock tests

```powershell
.\research\viiper-client-tests\Run.ps1
```

The tests use only temporary loopback listeners. They cover concurrent writers,
fragmented feedback, quiet feedback periods, rejected API/authentication calls,
stream rejection, complete JSON without EOF, malformed JSON, truncated/stalled
feedback, disconnects, neutral release, owned-bus cleanup, and cleanup failure.

## Optional real local API test

Start a separately verified VIIPER server with `auto-attach` disabled, then run:

```powershell
& "$env:USERPROFILE\.pskeyboard-tools\viiper-client-tests\TransportTests.exe" --integration 33242
```

On 2026-10-28, the real test connected to the official v0.9.0 server, created
its own `1-1` device, sent neutral and sample input, and removed the bus. Both
integration assertions passed. No OS device was attached. The local output is
`%USERPROFILE%\.pskeyboard-tools\viiper-client-tests\integration.log`.

Protocol references pinned to v0.9.0:

- [Management and stream API](https://github.com/Alia5/VIIPER/blob/v0.9.0/docs/api/overview.md)
- [DS4 state byte layout](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualshock4/state.go)
- [Server handshake routing](https://github.com/Alia5/VIIPER/blob/v0.9.0/internal/server/api/server.go)
- [DS4 binary stream handler](https://github.com/Alia5/VIIPER/blob/v0.9.0/device/dualshock4/handler.go)
