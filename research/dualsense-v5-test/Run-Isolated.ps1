param(
    [Parameter(Mandatory=$true)][string]$LabDirectory,
    [Parameter(Mandatory=$true)][string]$TestDirectory,
    [Parameter(Mandatory=$true)][string]$ResultDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$labRoot = (Resolve-Path -LiteralPath $LabDirectory).Path
$testRoot = (Resolve-Path -LiteralPath $TestDirectory).Path
$resultRoot = [IO.Path]::GetFullPath($ResultDirectory)
if (-not $resultRoot.StartsWith((Join-Path $repo '.local') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Private trial results must stay under the ignored repository .local directory.'
}
if (Test-Path -LiteralPath $resultRoot) { throw 'Choose a new result directory.' }
$labExe = Join-Path $labRoot 'DualSenseV5LabServer.exe'
$labBuild = Get-Content -Raw -LiteralPath (Join-Path $labRoot 'BUILD.json') | ConvertFrom-Json
$testBuild = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'BUILD.json') | ConvertFrom-Json
if ($labBuild.upstreamCommit -ne 'b78e31e93e84b4cb7c4b4c15b8aace10749106d2' -or
    $labBuild.upstreamArchiveSha256 -ne '8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5' -or
    (Get-FileHash -LiteralPath $labExe).Hash -ne $labBuild.executableSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'labserver/main.go')).Hash -ne $labBuild.labSourceSha256) {
    throw 'Lab source/build identity mismatch.'
}
foreach ($file in $testBuild.outputs) {
    if ([IO.Path]::GetFileName($file.name) -ne $file.name -or
        (Get-FileHash -LiteralPath (Join-Path $testRoot $file.name)).Hash -ne $file.sha256) { throw 'Test output hash mismatch.' }
}
foreach ($source in $testBuild.sources) {
    $path = [IO.Path]::GetFullPath((Join-Path $repo $source.path))
    if (-not $path.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        (Get-FileHash -LiteralPath $path).Hash -ne $source.sha256) { throw 'Research sources changed since build.' }
}
if (@(Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in @(33441,33442) }).Count -ne 0) {
    throw 'Research ports are already occupied; nothing has been started.'
}
function ProtectedSnapshot {
    $profile = Join-Path $env:LOCALAPPDATA 'PSKeyboard\bindings.json'
    [ordered]@{
        processes=@(Get-Process RemotePlay,PSKeyboard-ControllerFree,viiper -ErrorAction SilentlyContinue |
            Sort-Object Id | ForEach-Object { [ordered]@{name=$_.ProcessName;id=$_.Id;started=$_.StartTime.ToUniversalTime().ToString('o');path=$_.Path} });
        profileExists=(Test-Path -LiteralPath $profile -PathType Leaf);
        profileHash=$(if (Test-Path -LiteralPath $profile -PathType Leaf) { (Get-FileHash -LiteralPath $profile).Hash } else { $null })
    }
}
$before = ProtectedSnapshot
New-Item -ItemType Directory -Path $resultRoot | Out-Null
# Private receipts remain outside tracked source. No profile contents or raw input.
$before | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultRoot 'protected-before.private.json')
$lab = $null; $test = $null; $testExit = $null; $stopped = $false
$labExit = $null; $labForced = $false; $cleanupErrors = @()
try {
    $lab = Start-Process -FilePath $labExe -ArgumentList '--isolated-userspace-only' -WorkingDirectory $labRoot `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $resultRoot 'lab.stdout.txt') `
        -RedirectStandardError (Join-Path $resultRoot 'lab.stderr.txt')
    $ready = $false
    for ($attempt=0; $attempt -lt 50; $attempt++) {
        if ($lab.HasExited) { throw 'Lab exited before readiness.' }
        if ((Get-Content -Raw -LiteralPath (Join-Path $resultRoot 'lab.stdout.txt') -ErrorAction SilentlyContinue) -match '^READY ') { $ready=$true; break }
        Start-Sleep -Milliseconds 100
    }
    if (-not $ready) { throw 'Lab readiness timeout.' }
    $ports = @(Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in @(33441,33442) })
    if ($ports.Count -ne 2 -or @($ports | Where-Object { $_.LocalAddress -ne '127.0.0.1' -or $_.OwningProcess -ne $lab.Id }).Count -ne 0) {
        throw 'Research listeners are not exclusively owned loopback sockets.'
    }
    $test = Start-Process -FilePath (Join-Path $testRoot 'V5UsbReadbackTest.exe') `
        -ArgumentList '--isolated-loopback-server 33442 33441 --auto-attach-disabled' -WorkingDirectory $testRoot `
        -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $resultRoot 'test.stdout.txt') `
        -RedirectStandardError (Join-Path $resultRoot 'test.stderr.txt')
    if (-not $test.WaitForExit(100000)) { throw 'Readback test exceeded its hard process deadline.' }
    $test.WaitForExit(); $testExit = $test.ExitCode
} finally {
    try {
        if ($null -ne $test -and -not $test.HasExited) { $test.Kill(); $test.WaitForExit(3000) | Out-Null }
    } catch { $cleanupErrors += 'Readback helper cleanup failed.' }
    try {
      if ($null -ne $lab -and -not $lab.HasExited) {
        try {
            # Target only a currently verified socket owned by this attempt.
            $owner = @(Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -eq 33442 -and $_.LocalAddress -eq '127.0.0.1' -and $_.OwningProcess -eq $lab.Id })
            if ($owner.Count -eq 1) {
                $tcp = [Net.Sockets.TcpClient]::new()
                try {
                    $pending = $tcp.BeginConnect('127.0.0.1',33442,$null,$null)
                    try { if (-not $pending.AsyncWaitHandle.WaitOne(2000)) { throw 'Stop connect timeout.' }; $tcp.EndConnect($pending) }
                    finally { $pending.AsyncWaitHandle.Close() }
                    $stream=$tcp.GetStream(); $stream.WriteTimeout=2000
                    $bytes=[Text.Encoding]::ASCII.GetBytes("lab/stop`0"); $stream.Write($bytes,0,$bytes.Length)
                } finally { $tcp.Dispose() }
            }
        } catch { Write-Warning 'Graceful lab stop failed; only the attempt-owned helper may be terminated.' }
        if (-not $lab.WaitForExit(5000)) { $labForced=$true; $lab.Kill(); $lab.WaitForExit(3000) | Out-Null }
        $stopped=$lab.HasExited
      } elseif ($null -ne $lab) { $stopped=$true }
      if ($stopped) { $lab.WaitForExit(); $labExit=$lab.ExitCode }
    } catch { $cleanupErrors += 'Lab helper cleanup failed.' }
    $after=ProtectedSnapshot
    $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultRoot 'protected-after.private.json')
    $unchanged=($before | ConvertTo-Json -Depth 5 -Compress) -eq ($after | ConvertTo-Json -Depth 5 -Compress)
    $receipt=[ordered]@{researchVersion='dualsense-v5-research.1';testExitCode=$testExit;labStopped=$stopped;
        labExitCode=$labExit;labForcedTermination=$labForced;cleanupErrors=$cleanupErrors;
        protectedProcessesAndProfileUnchanged=$unchanged;windowsAttached=$false;sonyTested=$false;
        labSha256=$labBuild.executableSha256;testSha256=(Get-FileHash -LiteralPath (Join-Path $testRoot 'V5UsbReadbackTest.exe')).Hash}
    $receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultRoot 'RESULT.json')
    Get-Content -LiteralPath (Join-Path $resultRoot 'test.stdout.txt') -ErrorAction SilentlyContinue
    Get-Content -LiteralPath (Join-Path $resultRoot 'test.stderr.txt') -ErrorAction SilentlyContinue
    $receipt | ConvertTo-Json -Compress
}
if ($testExit -ne 0 -or -not $stopped -or $labExit -ne 0 -or $labForced -or $cleanupErrors.Count -ne 0 -or -not $unchanged) {
    throw 'Isolated trial did not pass all checks; inspect its private result directory.'
}
