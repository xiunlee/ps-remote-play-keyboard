param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'build')
)
$ErrorActionPreference = 'Stop'
$compiler = (Resolve-Path -LiteralPath $Zig).Path
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
if (-not $env:ZIG_GLOBAL_CACHE_DIR) {
    $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache'
}
$minhook = Join-Path $repo 'third-party\minhook'
$nativeArgs = @('cc','-target','x86_64-windows-gnu','-D_WIN32_WINNT=0x0A00','-O2')
$sources = @(
    (Join-Path $PSScriptRoot 'probe.c'),
    (Join-Path $minhook 'src\buffer.c'),
    (Join-Path $minhook 'src\hook.c'),
    (Join-Path $minhook 'src\trampoline.c'),
    (Join-Path $minhook 'src\hde\hde64.c')
)
& $compiler @nativeArgs -shared ('-I'+(Join-Path $minhook 'include')) @sources -lhid -o (Join-Path $out 'pskb-probe.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native probe build failed.' }
& $compiler @nativeArgs (Join-Path $PSScriptRoot 'probe_test.c') -o (Join-Path $out 'pskb-probe-test.exe')
if ($LASTEXITCODE -ne 0) { throw 'Native test host build failed.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $out 'ProbeController.exe')) (Join-Path $PSScriptRoot 'ProbeController.cs')
if ($LASTEXITCODE -ne 0) { throw 'Controller build failed.' }
& $csc /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $out 'RemotePlayHandleProbe.exe')) (Join-Path $repo 'research\native-probe\RemotePlayHandleProbe.cs')
if ($LASTEXITCODE -ne 0) { throw 'Handle probe build failed.' }
Write-Output ('Built native observation tools in '+$out)
