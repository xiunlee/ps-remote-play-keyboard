param(
    [Parameter(Mandatory = $true)][string]$BuildDirectory,
    [switch]$RequireClean
)
# Read-only artifact verification. Does not load/run any application or backend.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $root 'BUILD.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.variant -cne 'controller-free' -or $manifest.channel -cne 'internal-preview' -or
    $manifest.entryPoint -cne 'PSKeyboard-ControllerFree.exe' -or $manifest.commercialReady -ne $false) { throw 'Unsupported build manifest.' }
if ($manifest.workingTreeDirty -isnot [bool] -or $manifest.sourceCommit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid source identity.' }
if ($RequireClean -and $manifest.workingTreeDirty) { throw 'This artifact was built from an uncommitted working tree.' }
if ($manifest.defaultControllerMode -cne 'dualshock4' -or (@($manifest.controllerModes) -join ',') -cne 'dualshock4,dualsense') { throw 'Unexpected controller mode metadata.' }
if ($manifest.version -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-alpha\.(0|[1-9][0-9]*)\z') { throw 'Invalid preview version.' }
$parts = @($Matches[1], $Matches[2], $Matches[3], $Matches[4])
foreach ($part in $parts) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65534) { throw 'Invalid PE version component.' }
}
$expectedFileVersion = $parts -join '.'
$expectedAssemblyVersion = $parts[0] + '.' + $parts[1] + '.' + $parts[2] + '.0'
$expectedInfoVersion = $manifest.version + '+g' + $manifest.sourceCommit.Substring(0, 12)
if ($manifest.workingTreeDirty) { $expectedInfoVersion += '.dirty' }
if ($manifest.fileVersion -cne $expectedFileVersion -or $manifest.assemblyVersion -cne $expectedAssemblyVersion -or $manifest.informationalVersion -cne $expectedInfoVersion) { throw 'Version metadata is inconsistent.' }

$runtimeNames = @('PSKeyboard-ControllerFree.exe', 'pskb-keyboard-merge.dll', 'viiper.exe')
$allowedNames = @($runtimeNames) + @('BUILD.json')
$seen = @{}
foreach ($line in (Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Encoding UTF8)) {
    if ($line -cnotmatch '\A([0-9A-F]{64})  ([A-Za-z0-9_.-]+)\z') { throw 'Invalid checksum entry.' }
    $expectedHash = $Matches[1]; $name = $Matches[2]
    if ($allowedNames -cnotcontains $name -or $seen.ContainsKey($name)) { throw 'Unexpected or duplicate checksum entry.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash -cne $expectedHash) { throw ('Checksum mismatch: ' + $name) }
    $seen[$name] = $true
}
if ($seen.Count -ne $allowedNames.Count) { throw 'Checksum list does not cover the complete runtime.' }
$seenOutputs = @{}
foreach ($output in $manifest.outputs) {
    if ($runtimeNames -cnotcontains $output.path -or $seenOutputs.ContainsKey([string]$output.path) -or $output.sha256 -cnotmatch '\A[0-9A-F]{64}\z') { throw 'Invalid output record.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $output.path) -Algorithm SHA256).Hash -cne $output.sha256) { throw ('Output hash mismatch: ' + $output.path) }
    $seenOutputs[[string]$output.path] = $true
}
if ($seenOutputs.Count -ne $runtimeNames.Count) { throw 'Manifest does not cover the complete runtime.' }
$fileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $manifest.entryPoint))
if ($fileInfo.FileVersion -cne $expectedFileVersion -or $fileInfo.ProductVersion -cne $expectedInfoVersion) { throw 'Executable version does not match BUILD.json.' }
if (@($manifest.sources).Count -eq 0) { throw 'Source hashes are missing.' }
$seenSources = @{}
foreach ($source in $manifest.sources) {
    if ($source.path -cnotmatch '\A[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\z' -or ([string]$source.path).Split('/') -contains '..' -or
        $source.sha256 -cnotmatch '\A[0-9A-F]{64}\z' -or $seenSources.ContainsKey([string]$source.path)) { throw 'Invalid source hash record.' }
    $seenSources[[string]$source.path] = $true
}
if (-not $seenSources.ContainsKey('VERSION')) { throw 'VERSION input hash is missing.' }
$external = @($manifest.externalBinaries)
if ($external.Count -ne 1 -or $external[0].name -cne 'VIIPER' -or $external[0].version -cne '0.9.0' -or
    $external[0].path -cne 'viiper.exe' -or $external[0].sha256 -cne '142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6' -or
    $external[0].redistributionApproved -ne $false -or $external[0].localUseOnly -ne $true) { throw 'Unexpected external binary metadata.' }
if ((Get-FileHash -LiteralPath (Join-Path $root 'viiper.exe') -Algorithm SHA256).Hash -cne $external[0].sha256) { throw 'Pinned backend hash mismatch.' }
[pscustomobject]@{ verified = $true; version = $manifest.version; sourceCommit = $manifest.sourceCommit; workingTreeDirty = $manifest.workingTreeDirty; verifiedFiles = $seen.Count; commercialReady = $false }
