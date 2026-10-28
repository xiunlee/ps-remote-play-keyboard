param([string]$OutputDirectory=(Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-usb-relay'))
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /platform:x64 /optimize+ /warnaserror /target:library `
    ('/out:'+(Join-Path $out 'PhysicalUsbRelay.dll')) `
    (Join-Path $PSScriptRoot 'UsbIpRelay.cs')
if($LASTEXITCODE -ne 0){throw 'Physical relay library build failed'}
& $csc /nologo /platform:x64 /optimize+ /warnaserror `
    ('/out:'+(Join-Path $out 'RelayProtocolTests.exe')) `
    (Join-Path $PSScriptRoot 'UsbIpRelay.cs') (Join-Path $PSScriptRoot 'RelayProtocolTests.cs') (Join-Path $PSScriptRoot 'RelaySocketTests.cs')
if($LASTEXITCODE -ne 0){throw 'Relay protocol test build failed'}
& (Join-Path $out 'RelayProtocolTests.exe')
if($LASTEXITCODE -ne 0){throw 'Relay protocol tests failed'}
