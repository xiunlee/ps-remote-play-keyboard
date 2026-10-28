# Isolated x64 DLL loader check

This diagnostic separates an ordinary DLL-loading failure from the unconfirmed
failure seen inside Sony Remote Play. It does not provide controller input.

It loads the existing observation DLL by four routes, then unloads it:

1. A direct call in this test executable.
2. A local worker thread that captures `HMODULE` and `GetLastError` together.
3. An externally created thread in a child of this same executable, using the
   same wrapper.
4. An externally created thread whose entry is `LoadLibraryW`, matching the
   original controller's loading style. The child separately reports the full
   module handle and unloads the module.

Each route also runs a missing-file control. The wrapper controls expect error
126. The raw-thread control can establish a failed load, but does not claim to
recover that thread's loader error.

`GetProcAddress` checks that `StartProbe` exists; **it never calls it**. No hooks
are installed. There is no PID argument, `OpenProcess`, process-memory access,
or copied executable code. Remote threads only target the child process created
by this executable. Timeouts are bounded; cleanup may terminate only that child.

## Run

```powershell
$pskbTools = Join-Path $env:USERPROFILE '.pskeyboard-tools'
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $pskbTools 'zig-cache'
.\research\loader-check\Build.ps1 `
  -Zig (Join-Path $pskbTools 'zig017\zig-x86_64-windows-0.17.0\zig.exe') `
  -OutputDirectory (Join-Path $pskbTools 'loader-check')
& (Join-Path $pskbTools 'loader-check\pskb-loader-check.exe') `
  (Join-Path $pskbTools 'native-bridge\pskb-probe-v2.dll')
```

## Observed on 2026-10-28

Eight checks passed, with zero failures. The probe DLL SHA-256 was
`AB55FEC1C858B5071DD927738E77A781D4447628194BED09801B697BC94654D9`.
All four actual-DLL cases loaded, found the export, and unloaded successfully;
all missing-file controls failed as expected. No observation hooks or keyboard
mapping were activated.

One run reported the full module handle `0x7FFD07CF0000`, while the raw loader
thread exit value was `0x07CF0000`. This illustrates the loss of upper bits when
a pointer-sized result is observed through a DWORD thread exit code. The
original controller already used full-path module enumeration afterward, so
this fact alone does **not** explain its Sony failure.

Local raw output is stored in
`%USERPROFILE%\.pskeyboard-tools\loader-check\result-v2.log`.

This rules out a universally unloadable DLL or an ordinary missing dependency
in these test-host contexts. It does not reproduce Sony's process environment,
diagnose Sony's exit, or establish that another Sony injection would be safe.
Remote Play was left running throughout.

References: Microsoft documents a module handle or NULL plus loader error for
[LoadLibraryW](https://learn.microsoft.com/en-us/windows/win32/api/libloaderapi/nf-libloaderapi-loadlibraryw),
whereas [GetExitCodeThread](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getexitcodethread)
returns a DWORD termination status.
