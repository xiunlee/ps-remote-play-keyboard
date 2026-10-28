# Remote Play 9.5 static interface inspection

This is disk-only PE metadata inspection, performed on 2026-10-28. The
running game/client, account data, driver state and installed files were not
modified. No Sony executable or DLL was loaded by the inspection script.

`Read-PeInterfaces.ps1` reads ordinary imports, named exports, section names
and RVA-based delay imports. `interfaces-9.5.json` records file hashes and the
observed metadata. The parser is a bounded research helper, not a general PE
validation library. PE field interpretation follows
[Microsoft's PE specification](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format).

## Findings

- `RemotePlay.exe` is x64 and its ordinary import table contains only
  `preloader.dll` ordinal 1. It has no named exports. It contains `.grfn10`,
  `.grfn20`, `.grfn11` and `.grfn22` sections.
- `runtime.dll` has no named exports. It contains a writable/executable
  `.rwx` section and multiple `packer*` sections.
- `preloader.dll` imports native file/memory/loader functions including
  `NtReadFile`, but exports only a comment-like identifier and
  `preloader_link_func`. This is not a discovered controller interface.
- `RemotePlayUIWrapper.dll` exports UI launch/lifetime helpers only.
- `scenp.dll` exports network/account/session-signaling functions, not an
  identified game-controller input API. Those functions are outside this
  keyboard-mapping investigation and were not invoked.
- `RemotePlay.exe` declares a delay import directory, but the ordinary
  disk parser encountered `0xA1372913`, an RVA outside all file-backed image
  sections. Its delay imports were therefore **not decoded**.

The layout is consistent with a custom loading/packing layer. It does not
prove a particular protection product, an anti-injection policy, or the
cause of the earlier client exit. A missing clear-text import is not proof
that the function is unused at runtime.

This inspection found no public-looking input export that a separate
keyboard mapper could call. It does not establish that mapping is impossible.
It also supplies no evidence that retrying the earlier injection is safe.

## Reproduce

```powershell
$install = 'C:\Program Files (x86)\Sony\PS Remote Play'
$paths = 'RemotePlay.exe','runtime.dll','preloader.dll','RemotePlayUIWrapper.dll','scenp.dll' |
    ForEach-Object { Join-Path $install $_ }
& .\research\static-analysis\Read-PeInterfaces.ps1 -Path $paths |
    ConvertTo-Json -Depth 5
```
