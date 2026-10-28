param([string]$OutputDirectory=(Join-Path $env:USERPROFILE '.pskeyboard-tools\windows-hid-observer'))
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testExe=Join-Path $out 'ObserverGuardTests.exe'
& $csc /nologo /platform:x64 /optimize+ /warnaserror /main:ObserverGuardTests `
    ('/out:'+$testExe) `
    (Join-Path $PSScriptRoot 'WindowsHidObserver.cs') `
    (Join-Path $PSScriptRoot 'ObserverGuardTests.cs')
if($LASTEXITCODE -ne 0){throw 'HID observer guard-test build failed'}
& $testExe
if($LASTEXITCODE -ne 0){throw 'HID observer guard tests failed'}
