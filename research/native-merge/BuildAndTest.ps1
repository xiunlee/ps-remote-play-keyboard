param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$compiler = (Resolve-Path -LiteralPath $Zig).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
if (-not $env:ZIG_GLOBAL_CACHE_DIR) {
    $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache'
}
$library = Join-Path $out 'pskb-keyboard-merge.dll'
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 `
    -shared -DPSKB_MERGE_EXPORTS (Join-Path $PSScriptRoot 'keyboard_merge.c') -o $library
if ($LASTEXITCODE -ne 0) { throw 'Keyboard merge DLL build failed.' }
$executable = Join-Path $out 'keyboard-merge-test.exe'
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 `
    (Join-Path $PSScriptRoot 'keyboard_merge.c') `
    (Join-Path $PSScriptRoot 'keyboard_merge_test.c') -o $executable
if ($LASTEXITCODE -ne 0) { throw 'Keyboard merge test build failed.' }
& $executable
if ($LASTEXITCODE -ne 0) { throw 'Keyboard merge tests failed.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$interopTest = Join-Path $out 'keyboard-merge-interop-test.exe'
& $csc /nologo /target:exe /platform:x64 ('/out:'+$interopTest) `
    (Join-Path $PSScriptRoot 'KeyboardMergeInteropTest.cs')
if ($LASTEXITCODE -ne 0) { throw 'Keyboard merge interop test build failed.' }
& $interopTest
if ($LASTEXITCODE -ne 0) { throw 'Keyboard merge interop test failed.' }
