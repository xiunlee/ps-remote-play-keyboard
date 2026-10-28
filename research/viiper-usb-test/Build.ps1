param(
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\viiper-usb-test'),
    [string]$NativeLibrary = (Join-Path $env:USERPROFILE '.pskeyboard-tools\native-merge\pskb-keyboard-merge.dll')
)
$ErrorActionPreference = 'Stop'
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$mapping = Join-Path $PSScriptRoot '..\..\src\keyboard-pad\KeyboardMapping.cs'
& $csc /nologo /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:'+(Join-Path $out 'UsbReadbackTest.exe')) (Join-Path $PSScriptRoot 'UsbReadbackTest.cs') $mapping
if ($LASTEXITCODE -ne 0) { throw 'USB/IP readback test build failed' }
Copy-Item -LiteralPath (Resolve-Path -LiteralPath $NativeLibrary).Path -Destination (Join-Path $out 'pskb-keyboard-merge.dll') -Force
Write-Output (Join-Path $out 'UsbReadbackTest.exe')
