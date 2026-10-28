param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$compiler = (Resolve-Path -LiteralPath $Zig).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
& $compiler cc -target x86_64-windows-gnu -D_WIN32_WINNT=0x0a00 -municode -O2 -Wall -Wextra (Join-Path $PSScriptRoot 'loader_check.c') -o (Join-Path $out 'pskb-loader-check.exe')
if ($LASTEXITCODE -ne 0) { throw 'Isolated loader diagnostic build failed.' }
