param([string]$OutputDirectory=(Join-Path $env:USERPROFILE '.pskeyboard-tools\windows-hid-observer'))
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /platform:x64 /optimize+ /warnaserror `
    ('/out:'+(Join-Path $out 'WindowsHidObserver.exe')) `
    (Join-Path $PSScriptRoot 'WindowsHidObserver.cs')
if($LASTEXITCODE -ne 0){throw 'Read-only HID observer build failed'}
Write-Output (Join-Path $out 'WindowsHidObserver.exe')
