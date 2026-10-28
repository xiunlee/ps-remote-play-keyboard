param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [Parameter(Mandatory=$true)][string]$BackendDirectory,
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\keyboard-pad-preview')
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
$backend = Join-Path (Resolve-Path -LiteralPath $BackendDirectory).Path 'viiper.exe'
$expected = '142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6'
if ((Get-FileHash -LiteralPath $backend -Algorithm SHA256).Hash -ne $expected) {
    throw 'Expected the pinned official VIIPER v0.9.0 x64 executable.'
}
if ((Get-AuthenticodeSignature -LiteralPath $backend).Status -ne 'Valid') {
    throw 'VIIPER Authenticode validation failed.'
}
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = (Resolve-Path -LiteralPath $Zig).Path
if (-not $env:ZIG_GLOBAL_CACHE_DIR) { $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache' }
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 `
    -shared -DPSKB_MERGE_EXPORTS (Join-Path $repo 'research\native-merge\keyboard_merge.c') `
    -o (Join-Path $out 'pskb-keyboard-merge.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native keyboard mapping build failed.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object FullName
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warnaserror `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    ('/out:'+(Join-Path $out 'PSKeyboard-USB.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Keyboard app build failed.' }
Copy-Item -LiteralPath $backend -Destination (Join-Path $out 'viiper.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $out 'README.md') -Force
Write-Output ('Built keyboard-only preview: '+(Join-Path $out 'PSKeyboard-USB.exe'))
Write-Output 'No driver installed, no app launched, and no Windows virtual controller created.'
