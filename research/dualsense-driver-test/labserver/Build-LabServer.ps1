param(
    [Parameter(Mandatory=$true)][string]$SourceArchive,
    [Parameter(Mandatory=$true)][string]$GoExecutable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$expectedArchiveHash = '8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5'
$upstreamCommit = 'b78e31e93e84b4cb7c4b4c15b8aace10749106d2'
$archive = (Resolve-Path -LiteralPath $SourceArchive).Path
$go = (Resolve-Path -LiteralPath $GoExecutable).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
    throw 'Pinned upstream source ZIP hash mismatch.'
}
if (Test-Path -LiteralPath $out) { throw 'Choose a new, nonexistent output directory; prior builds are immutable.' }
New-Item -ItemType Directory -Path $out | Out-Null
$sourceRoot = Join-Path $out 'upstream'
Expand-Archive -LiteralPath $archive -DestinationPath $sourceRoot
$upstream = Join-Path $sourceRoot ('VIIPER-' + $upstreamCommit)
if (-not (Test-Path -LiteralPath (Join-Path $upstream 'go.mod') -PathType Leaf)) { throw 'Expected pinned archive layout is missing.' }
$module = Join-Path $out 'module'
New-Item -ItemType Directory -Path $module | Out-Null
$mainSource = Join-Path $PSScriptRoot 'main.go'
$mainHash = (Get-FileHash -LiteralPath $mainSource -Algorithm SHA256).Hash
$buildScriptHash = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
Copy-Item -LiteralPath $mainSource -Destination (Join-Path $module 'main.go')
if ((Get-FileHash -LiteralPath (Join-Path $module 'main.go') -Algorithm SHA256).Hash -ne $mainHash) { throw 'Lab source changed during copy.' }
$upstreamSum = Join-Path $upstream 'go.sum'
$upstreamSumHash = (Get-FileHash -LiteralPath $upstreamSum -Algorithm SHA256).Hash
$moduleSum = Join-Path $module 'go.sum'
Copy-Item -LiteralPath $upstreamSum -Destination $moduleSum
if ((Get-FileHash -LiteralPath $moduleSum -Algorithm SHA256).Hash -ne $upstreamSumHash) { throw 'Pinned dependency checksums changed during copy.' }
$replacePath = ($upstream.Replace('\','/') | ConvertTo-Json -Compress)
$moduleText = "module github.com/Alia5/VIIPER/pskeyboarddriverlab`n`ngo 1.26.2`n`nrequire github.com/Alia5/VIIPER v0.0.0`n`nreplace github.com/Alia5/VIIPER => $replacePath`n"
[IO.File]::WriteAllText((Join-Path $module 'go.mod'), $moduleText, [Text.UTF8Encoding]::new($false))

# All Go writes stay in this isolated output. No go env -w, global PATH edits,
# installation, driver probe, executable launch, or upstream source patch.
$environment = @{
    GOENV='off'; GOWORK='off'; GOTOOLCHAIN='local'; CGO_ENABLED='0';
    GOOS='windows'; GOARCH='amd64'; GOFLAGS=''; GOPROXY='https://proxy.golang.org';
    GOSUMDB='sum.golang.org'; GOPRIVATE=''; GONOSUMDB=''; GONOPROXY='';
    GOPATH=(Join-Path $out 'gopath'); GOMODCACHE=(Join-Path $out 'gomodcache');
    GOCACHE=(Join-Path $out 'gocache'); GOTMPDIR=(Join-Path $out 'gotmp');
    TEST_TELEMETRY_DIR=(Join-Path $out 'telemetry')
}
$prior = @{}
$executable = Join-Path $out 'DualSenseV5DriverLabServer.exe'
try {
    foreach ($name in $environment.Keys) {
        $prior[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
    }
    foreach ($name in @('GOPATH','GOMODCACHE','GOCACHE','GOTMPDIR','TEST_TELEMETRY_DIR')) { New-Item -ItemType Directory -Path $environment[$name] | Out-Null }
    # Go 1.27 uses TEST_TELEMETRY_DIR for both counters and its telemetry child.
    # GOTELEMETRY is a read-only go env value, not a supported process override.
    # Set the isolated mode before invoking Go; never change the user's mode file.
    [IO.File]::WriteAllText((Join-Path $environment['TEST_TELEMETRY_DIR'] 'mode'), 'off', [Text.UTF8Encoding]::new($false))
    $goVersion = (& $go version | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to query Go toolchain.' }
    Push-Location -LiteralPath $module
    try {
        & $go build -mod=mod -trimpath -buildvcs=false -o $executable .
        if ($LASTEXITCODE -ne 0) { throw 'Lab server build failed.' }
        $dependencies = & $go list -m -json all
        if ($LASTEXITCODE -ne 0) { throw 'Unable to record module dependency identities.' }
        [IO.File]::WriteAllLines((Join-Path $out 'MODULES.jsonl'), [string[]]$dependencies, [Text.UTF8Encoding]::new($false))
        if ((Get-FileHash -LiteralPath $moduleSum -Algorithm SHA256).Hash -ne $upstreamSumHash) {
            throw 'Build changed the pinned upstream go.sum; review dependency resolution before accepting this candidate.'
        }
    } finally { Pop-Location }
    if ((Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash -ne $buildScriptHash) { throw 'Build script changed during execution.' }
    $record = [ordered]@{
        artifact='PSKeyboard DualSense V5 hosted driver lab server'; researchVersion='driver.1';
        upstreamRepository='https://github.com/hbashton/VIIPER'; upstreamCommit=$upstreamCommit;
        upstreamTag='v0.1.9-rc4.6.6'; upstreamArchiveSha256=$expectedArchiveHash;
        labSourceSha256=$mainHash; buildScriptSha256=$buildScriptHash;
        upstreamGoSumSha256=$upstreamSumHash; dependencyChecksumsUnchanged=$true;
        executableSha256=(Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash;
        goVersion=$goVersion; cgoEnabled=$false; builtOnly=$true;
        telemetryMode='off'; telemetryDirectoryIsolated=$true;
        driverRuntimeTested=$false; hostedRuntimeGuard=$true; sonyTested=$false; productVersionChanged=$false
    }
    [IO.File]::WriteAllText((Join-Path $out 'BUILD.json'), ($record | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
    [pscustomobject]@{ executable=$executable; buildRecord=(Join-Path $out 'BUILD.json'); builtOnly=$true } | ConvertTo-Json -Compress
} finally {
    foreach ($name in $prior.Keys) { [Environment]::SetEnvironmentVariable($name, $prior[$name], 'Process') }
}
