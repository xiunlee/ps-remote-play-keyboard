# Remote Play DS4 handle metadata probe

This x64 .NET Framework helper locates existing DUALSHOCK 4 handles in the
installed Sony Remote Play process. It does not inject code, inspect process
memory, query file names, consume input reports, or submit controller output.

The probe uses `NtQueryInformationProcess(ProcessHandleInformation = 51)` to
snapshot handles. It derives the current File object type index from a local
file handle, restricts candidates to ordinary file access masks, and checks
each candidate in a separate worker with a 1.5-second timeout. Common socket
handles with broader access are excluded before duplication; disk files and
pipes are skipped. Duplicates request zero access and never close the source
handle. `HidD_GetAttributes` identifies VID `054C`, PID `05C4` or `09CC`.
The latter covers the V2 identity used by the VIIPER keyboard-only candidate;
the output prints the actual PID so physical and virtual observations are not
conflated. Seeing either handle does not prove the client uses its input.

Pass `--dualsense` after the PID to select only `054C:0CE6` instead. The
same read-only, access-mask and timeout restrictions apply. Zero matches
means none were observed among eligible handles at that time; it does not
prove the process never opened the device or establish why input is ignored.

Only a matching DS4 handle receives the additional `FileModeInformation = 16`
metadata query. If zero-access querying is denied, the probe retries with
`FILE_READ_ATTRIBUTES` only. No data-read/write permission is requested.

Build and run in PowerShell, substituting the current official Remote Play PID:

```powershell
$probeDir = Join-Path $env:LOCALAPPDATA 'PSKeyboard\native-probe'
New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /platform:x64 /optimize+ ('/out:' + (Join-Path $probeDir 'RemotePlayHandleProbe.exe')) 'research\native-probe\RemotePlayHandleProbe.cs'
& (Join-Path $probeDir 'RemotePlayHandleProbe.exe') <RemotePlay-PID>
& (Join-Path $probeDir 'RemotePlayHandleProbe.exe') <RemotePlay-PID> --dualsense
```

## Observed on 2026-10-28

The existing Remote Play 9.5 process had one matching DS4 handle among 59
eligible candidates. All workers completed without a timeout. The handle's
granted access was `0x0012019F`; `FileModeInformation` returned success and mode
`0x00000000`. Neither `FILE_SYNCHRONOUS_IO_ALERT` (`0x10`) nor
`FILE_SYNCHRONOUS_IO_NONALERT` (`0x20`) was set. Remote Play remained responsive.

This establishes an actual DS4 file handle in the live official client and
shows that it is opened for asynchronous I/O. It does **not** identify which
read API or completion API the client uses. In particular, a future hook must
not infer that an immediate successful read return makes buffer rewriting
safe when a completion consumer might run concurrently.

Process IDs and handle values are transient; rerun the probe after a process
restart or controller reconnection. Probe success does not demonstrate input
merging or PS5 control.

References:

- [System Informer handle snapshot structure](https://ntdoc.m417z.com/process_handle_snapshot_information)
- [System Informer handle table entry layout](https://ntdoc.m417z.com/process_handle_table_entry_info)
- [Microsoft DuplicateHandle](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-duplicatehandle)
- [Microsoft HidD_GetAttributes](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidsdi/nf-hidsdi-hidd_getattributes)
