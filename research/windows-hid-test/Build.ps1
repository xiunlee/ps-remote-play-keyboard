param([string]$OutputDirectory=(Join-Path $env:USERPROFILE '.pskeyboard-tools\windows-hid-test'))
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$source=Join-Path $PSScriptRoot '..\..\src\keyboard-pad'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /platform:x64 /optimize+ /r:System.Web.Extensions.dll ('/out:'+(Join-Path $out 'WindowsHidTest.exe')) (Join-Path $PSScriptRoot 'WindowsHidTest.cs') (Join-Path $source 'KeyboardMapping.cs') (Join-Path $source 'ViiperTransport.cs')
if($LASTEXITCODE -ne 0){throw 'Windows HID test build failed'}
Copy-Item -LiteralPath (Join-Path $env:USERPROFILE '.pskeyboard-tools\native-merge\pskb-keyboard-merge.dll') -Destination (Join-Path $out 'pskb-keyboard-merge.dll') -Force
Write-Output (Join-Path $out 'WindowsHidTest.exe')
