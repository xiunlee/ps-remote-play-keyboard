param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-keyboard-preview'),
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = (Resolve-Path -LiteralPath $Zig).Path
if (-not $env:ZIG_GLOBAL_CACHE_DIR) { $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache' }
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 `
    -shared -DPSKB_MERGE_EXPORTS (Join-Path $repo 'research\native-merge\keyboard_merge.c') `
    -o (Join-Path $out 'pskb-keyboard-merge.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native physical report merge build failed.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$inputSource = Join-Path $repo 'src\keyboard-pad\KeyboardInputState.cs'
$bindingSource = Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs'
$mergeSource = Join-Path $PSScriptRoot 'PhysicalReportMerge.cs'
$optionsSource = Join-Path $PSScriptRoot 'PhysicalKeyboardOptions.cs'
$relaySource = Join-Path $repo 'research\physical-usb-relay\UsbIpRelay.cs'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warnaserror `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll `
    ('/out:'+(Join-Path $out 'PSKeyboard-Physical.exe')) `
    (Join-Path $PSScriptRoot 'PhysicalKeyboard.cs') $mergeSource $optionsSource $inputSource $bindingSource $relaySource
if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard app build failed.' }
if ($RunTests) {
    $test = Join-Path $out 'PhysicalMergeTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror ('/out:'+$test) `
        (Join-Path $PSScriptRoot 'PhysicalMergeTests.cs') $mergeSource $optionsSource $inputSource $bindingSource
    if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard test build failed.' }
    & $test
    if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard merge tests failed.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $out 'README.md') -Force
Write-Output ('Built physical relay preview: '+(Join-Path $out 'PSKeyboard-Physical.exe'))
Write-Output 'No driver installed, no app launched, no device bound or imported.'
