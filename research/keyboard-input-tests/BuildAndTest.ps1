param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $out 'keyboard-input-state-tests.exe'
& $csc /nologo /target:exe /platform:x64 ('/out:'+$exe) `
    (Join-Path $repo 'src\keyboard-pad\KeyboardInputState.cs') `
    (Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs') `
    (Join-Path $PSScriptRoot 'KeyboardInputStateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Keyboard input state tests failed to build.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'Keyboard input state tests failed.' }
