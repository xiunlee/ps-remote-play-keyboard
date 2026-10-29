param(
    [Parameter(Mandatory=$true)][string]$MappingDirectory,
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE ('.pskeyboard-tools\dualsense-usb-test-' + [Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$mappingRoot = (Resolve-Path -LiteralPath $MappingDirectory).Path
$native = Join-Path $mappingRoot 'pskb-keyboard-merge.dll'
if (-not (Test-Path -LiteralPath $native -PathType Leaf)) { throw 'MappingDirectory must contain the built native mapping DLL.' }
$nativeHash = (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash
$out = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count -gt 0) { throw 'Choose an empty output directory.' }
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 /r:System.Web.Extensions.dll `
    ('/out:' + (Join-Path $out 'DualSenseUsbReadbackTest.exe')) `
    (Join-Path $PSScriptRoot 'DualSenseUsbReadbackTest.cs') `
    (Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs') `
    (Join-Path $repo 'src\keyboard-pad\ViiperTransport.cs')
if ($LASTEXITCODE -ne 0) { throw 'DualSense userspace USB/IP test build failed.' }
Copy-Item -LiteralPath $native -Destination (Join-Path $out 'pskb-keyboard-merge.dll')
if ((Get-FileHash -LiteralPath (Join-Path $out 'pskb-keyboard-merge.dll') -Algorithm SHA256).Hash -ne $nativeHash) { throw 'Native mapping DLL changed while copying.' }
[pscustomobject]@{ executable=(Join-Path $out 'DualSenseUsbReadbackTest.exe'); nativeSha256=$nativeHash; builtOnly=$true; windowsAttached=$false } | ConvertTo-Json -Compress
