param([Parameter(Mandatory = $true)][string]$BuildDirectory)
# Mutate private copies of metadata only; never launch an app or touch USB/Sony.
$ErrorActionPreference = 'Stop'
$inputRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
$verifier = Join-Path $PSScriptRoot 'Verify-ControllerFreeBuild.ps1'
$testRoot = Join-Path (Join-Path $env:USERPROFILE '.pskeyboard-tools\versioning-verification-tests') ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
foreach ($file in @('PSKeyboard-ControllerFree.exe', 'pskb-keyboard-merge.dll', 'viiper.exe', 'BUILD.json', 'SHA256SUMS.txt')) {
    Copy-Item -LiteralPath (Join-Path $inputRoot $file) -Destination (Join-Path $testRoot $file)
}
$utf8 = New-Object Text.UTF8Encoding($false)
$manifestPath = Join-Path $testRoot 'BUILD.json'
$checksumsPath = Join-Path $testRoot 'SHA256SUMS.txt'
$originalManifest = [IO.File]::ReadAllBytes($manifestPath)
$originalChecksums = [IO.File]::ReadAllBytes($checksumsPath)
$checks = 0
function Reset-Metadata {
    [IO.File]::WriteAllBytes($manifestPath, $originalManifest)
    [IO.File]::WriteAllBytes($checksumsPath, $originalChecksums)
}
function Write-TestManifest($value) {
    [IO.File]::WriteAllText($manifestPath, ($value | ConvertTo-Json -Depth 8), $utf8)
    $newHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
    $lines = @(Get-Content -LiteralPath $checksumsPath -Encoding UTF8 | ForEach-Object {
        if ($_ -match '  BUILD\.json$') { $newHash + '  BUILD.json' } else { $_ }
    })
    [IO.File]::WriteAllLines($checksumsPath, $lines, $utf8)
}
function Expect-Rejection([string]$label, [switch]$Clean) {
    $rejected = $false
    try { & $verifier -BuildDirectory $testRoot -RequireClean:$Clean | Out-Null }
    catch { $rejected = $true }
    if (-not $rejected) { throw ('Verifier accepted invalid candidate: ' + $label) }
    $script:checks++
    Reset-Metadata
}
& $verifier -BuildDirectory $testRoot | Out-Null
$checks++

[IO.File]::AppendAllText($manifestPath, ' ', $utf8)
Expect-Rejection 'changed manifest without checksum update'

$lines = @(Get-Content -LiteralPath $checksumsPath -Encoding UTF8)
[IO.File]::WriteAllLines($checksumsPath, (@($lines) + @($lines[0])), $utf8)
Expect-Rejection 'duplicate checksum entry'

$lines = @(Get-Content -LiteralPath $checksumsPath -Encoding UTF8)
[IO.File]::WriteAllLines($checksumsPath, $lines[1..($lines.Length - 1)], $utf8)
Expect-Rejection 'missing checksum entry'

$lines = @(Get-Content -LiteralPath $checksumsPath -Encoding UTF8)
$lines[0] = ('0' * 64) + '  ../BUILD.json'
[IO.File]::WriteAllLines($checksumsPath, $lines, $utf8)
Expect-Rejection 'checksum path traversal'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.sourceCommit = 'not-a-commit'
Write-TestManifest $manifest
Expect-Rejection 'invalid commit'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.fileVersion = '99.99.99.99'
Write-TestManifest $manifest
Expect-Rejection 'inconsistent version metadata'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.version = '1.2.3-alpha.4'; $manifest.fileVersion = '1.2.3.4'; $manifest.assemblyVersion = '1.2.3.0'
$manifest.informationalVersion = $manifest.version + '+g' + $manifest.sourceCommit.Substring(0, 12)
if ($manifest.workingTreeDirty) { $manifest.informationalVersion += '.dirty' }
Write-TestManifest $manifest
Expect-Rejection 'metadata does not match the compiled executable'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.outputs = @($manifest.outputs | Select-Object -First 1)
Write-TestManifest $manifest
Expect-Rejection 'incomplete runtime hash list'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.sources = @($manifest.sources | Where-Object { $_.path -ne 'VERSION' })
Write-TestManifest $manifest
Expect-Rejection 'missing VERSION provenance'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.externalBinaries[0].redistributionApproved = $true
Write-TestManifest $manifest
Expect-Rejection 'unsupported redistribution claim'

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$manifest.workingTreeDirty = $true
Write-TestManifest $manifest
Expect-Rejection 'dirty candidate in clean verification mode' -Clean

& $verifier -BuildDirectory $testRoot | Out-Null
$checks++
[pscustomobject]@{ passed = $true; checks = $checks; applicationStarted = $false; usbOperations = $false }
