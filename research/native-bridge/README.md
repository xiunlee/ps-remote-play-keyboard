# Earlier native observation experiment — not a keyboard bridge

This experiment only observes selected HID I/O calls in an explicitly
identified process. It never merges keyboard data. The `merge` command is
reserved, and tests assert that report payloads remain unchanged.

On 2026-10-28 the probe passed synthetic I/O tests and an attachment test in
our own host. Attempting to load it into the installed Sony Remote Play 9.5
did not produce a matching loaded module; `StartProbe` was not invoked.
The user subsequently reported that Remote Play exited by itself. The cause
was not established. No working Sony interception or keyboard input resulted.

The later [isolated loader checks](../loader-check/README.md) show that the
same DLL can load and unload in our own test process, including a remotely
created thread. They do not reproduce Sony's process or establish that a
retry in Sony is safe. Do not use these test passes as Sony compatibility
evidence. The current keyboard-only candidate is under
[src/keyboard-pad](../../src/keyboard-pad/README.md) and uses an external USB
device backend instead of this experiment.

`Build.ps1` builds the observer, synthetic host, and scoped controller using
an explicitly supplied Zig compiler plus the Windows .NET Framework compiler.
MinHook's original license and source notices are retained under
`third-party/minhook`. The controller executable is a research tool, not the
application launcher. It is not automatically run by the keyboard app.
