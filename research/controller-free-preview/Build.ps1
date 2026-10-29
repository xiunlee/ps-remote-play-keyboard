param(
    [string]$Zig = (Join-Path $env:USERPROFILE '.pskeyboard-tools\zig017\zig-x86_64-windows-0.17.0\zig.exe'),
    [string]$BackendDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\viiper-v0.9.0'),
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\controller-free-preview-20261029'),
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
$backend = Join-Path (Resolve-Path -LiteralPath $BackendDirectory).Path 'viiper.exe'
$expected = '142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6'
if ((Get-FileHash -LiteralPath $backend -Algorithm SHA256).Hash -ne $expected) { throw 'Expected the pinned official VIIPER v0.9.0 x64 executable.' }
if ((Get-AuthenticodeSignature -LiteralPath $backend).Status -ne 'Valid') { throw 'VIIPER Authenticode validation failed.' }
if (Test-Path -LiteralPath (Join-Path $out 'PSKeyboard-ControllerFree.exe')) {
    $inUse = Get-CimInstance Win32_Process -Filter "Name = 'PSKeyboard-ControllerFree.exe' OR Name = 'viiper.exe'" |
        Where-Object { $_.ExecutablePath -and ([IO.Path]::GetDirectoryName($_.ExecutablePath).TrimEnd('\') -ieq $out.TrimEnd('\')) }
    if ($inUse) { throw 'The experimental output directory is in use; choose a new output directory.' }
}
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = (Resolve-Path -LiteralPath $Zig).Path
if (-not $env:ZIG_GLOBAL_CACHE_DIR) { $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache' }
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 -shared -DPSKB_MERGE_EXPORTS `
    (Join-Path $repo 'research\native-merge\keyboard_merge.c') -o (Join-Path $out 'pskb-keyboard-merge.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native mapping build failed.' }
$sources = @(
    (Join-Path $PSScriptRoot 'ControllerFreeKeyboard.cs'),
    (Join-Path $repo 'src\keyboard-pad\ViiperTransport.cs'),
    (Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs'),
    (Join-Path $repo 'src\keyboard-pad\KeyboardInputState.cs'),
    (Join-Path $repo 'src\physical-keyboard\BindingProfile.cs'),
    (Join-Path $repo 'src\physical-keyboard\RapidFireEngine.cs'),
    (Join-Path $repo 'src\physical-keyboard\KeyboardControlPipeline.cs'),
    (Join-Path $repo 'src\physical-keyboard\BindingEditor.cs'),
    (Join-Path $repo 'src\physical-keyboard\KeyboardUi.cs'),
    (Join-Path $repo 'src\physical-keyboard\UiText.cs')
)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    ('/out:' + (Join-Path $out 'PSKeyboard-ControllerFree.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Controller-free experimental GUI build failed.' }
if ($RunTests) {
    $guardOutput = Join-Path $out 'ControllerFreeGuardTests.exe'
    & $csc /nologo /target:exe /main:PSKeyboard.ControllerFreeGuardTests /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
        /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
        ('/out:' + $guardOutput) @sources (Join-Path $PSScriptRoot 'ControllerFreeGuardTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Controller-free guard tests failed to compile.' }
    & $guardOutput
    if ($LASTEXITCODE -ne 0) { throw 'Controller-free guard tests failed.' }
}
Copy-Item -LiteralPath $backend -Destination (Join-Path $out 'viiper.exe') -Force
Write-Output ('Built isolated experimental GUI: ' + (Join-Path $out 'PSKeyboard-ControllerFree.exe'))
Write-Output 'Build only: no GUI/server launched, no driver installed, no USB device attached, Sony unchanged.'
