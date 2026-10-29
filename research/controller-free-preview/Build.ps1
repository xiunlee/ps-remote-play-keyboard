param(
    [string]$Zig = (Join-Path $env:USERPROFILE '.pskeyboard-tools\zig017\zig-x86_64-windows-0.17.0\zig.exe'),
    [string]$BackendDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\viiper-v0.9.0'),
    [string]$OutputDirectory,
    [switch]$RunTests,
    [switch]$RequireClean
)
# Build only. Never launch the GUI/backend, install drivers or touch USB/Sony.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$versionFile = Join-Path $repo 'VERSION'
$version = [IO.File]::ReadAllText($versionFile).Trim()
if ($version -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-alpha\.(0|[1-9][0-9]*)\z') {
    throw 'VERSION must contain one canonical major.minor.patch-alpha.number value.'
}
$parts = @($Matches[1], $Matches[2], $Matches[3], $Matches[4])
foreach ($part in $parts) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65534) { throw 'Version components must be between 0 and 65534.' }
}
$assemblyVersion = $parts[0] + '.' + $parts[1] + '.' + $parts[2] + '.0'
$fileVersion = $parts -join '.'
$commit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Cannot identify source revision.' }
$gitStatus = @(& git -C $repo status --porcelain --untracked-files=normal)
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify working-tree state.' }
$dirty = $gitStatus.Count -ne 0
if ($RequireClean -and $dirty) { throw 'A release-candidate build requires a clean working tree.' }
if ($RequireClean) {
    $versionTag = 'refs/tags/v' + $version
    & git -C $repo show-ref --verify --quiet $versionTag
    $tagLookupResult = $LASTEXITCODE
    if ($tagLookupResult -eq 0) {
        $tagCommit = (& git -C $repo rev-parse ($versionTag + '^{commit}')).Trim()
        if ($LASTEXITCODE -ne 0 -or $tagCommit -ne $commit) { throw 'This version tag already identifies another source revision. Increment VERSION; do not move the tag.' }
    } elseif ($tagLookupResult -ne 1) { throw 'Cannot verify the version tag.' }
}
$informationalVersion = $version + '+g' + $commit.Substring(0, 12)
if ($dirty) { $informationalVersion += '.dirty' }
$builtUtc = [DateTime]::UtcNow.ToString('o')

$backend = Join-Path (Resolve-Path -LiteralPath $BackendDirectory).Path 'viiper.exe'
$expected = '142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6'
if ((Get-FileHash -LiteralPath $backend -Algorithm SHA256).Hash -ne $expected) { throw 'Expected the pinned official VIIPER v0.9.0 x64 executable.' }
if ((Get-AuthenticodeSignature -LiteralPath $backend).Status -ne 'Valid') { throw 'VIIPER Authenticode validation failed.' }
$compiler = (Resolve-Path -LiteralPath $Zig).Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) { throw 'The .NET Framework C# compiler was not found.' }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path (Join-Path $env:USERPROFILE '.pskeyboard-tools\controller-free-builds') ($version + '-' + $commit.Substring(0, 12) + '-' + [Guid]::NewGuid().ToString('N'))
}
$out = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $out) {
    if (-not (Test-Path -LiteralPath $out -PathType Container) -or @(Get-ChildItem -LiteralPath $out -Force).Count -ne 0) {
        throw 'The output directory must be new or empty; existing builds are never overwritten.'
    }
}

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
$guardSource = Join-Path $PSScriptRoot 'ControllerFreeGuardTests.cs'
$verifyScript = Join-Path $repo 'packaging\Verify-ControllerFreeBuild.ps1'
$versioningTestsScript = Join-Path $repo 'packaging\Test-ControllerFreeVersioning.ps1'
$transportTestsScript = Join-Path $repo 'research\viiper-client-tests\Run.ps1'
$inputPaths = @($sources) + @($guardSource, $versionFile, $PSCommandPath, $verifyScript, $versioningTestsScript,
    $transportTestsScript, (Join-Path $repo 'research\viiper-client-tests\TransportTests.cs'),
    (Join-Path $repo 'research\native-merge\keyboard_merge.c'), (Join-Path $repo 'research\native-merge\keyboard_merge.h'))
$sourceHashes = @($inputPaths | Sort-Object -Unique | ForEach-Object {
    [ordered]@{ path = $_.Substring($repo.Length + 1).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})

New-Item -ItemType Directory -Path $out -Force | Out-Null
$utf8 = New-Object Text.UTF8Encoding($false)
$generated = Join-Path $out 'BuildInfo.g.cs'
$dirtyLiteral = if ($dirty) { 'true' } else { 'false' }
$generatedSource = @"
// Generated by Build.ps1 from VERSION and the source snapshot. Do not edit.
using System.Reflection;
[assembly: AssemblyTitle("上班不要玩 — 免手把測試版")]
[assembly: AssemblyProduct("上班不要玩")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$fileVersion")]
[assembly: AssemblyInformationalVersion("$informationalVersion")]
namespace PSKeyboard
{
    internal static class BuildInfo
    {
        internal const string Version = "$version";
        internal const string SourceCommit = "$commit";
        internal const bool WorkingTreeDirty = $dirtyLiteral;
        internal const string BuiltUtc = "$builtUtc";
        internal const string InformationalVersion = "$informationalVersion";
        internal const string FileVersion = "$fileVersion";
    }
}
"@
[IO.File]::WriteAllText($generated, $generatedSource, $utf8)
if (-not $env:ZIG_GLOBAL_CACHE_DIR) { $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache' }
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 -shared -DPSKB_MERGE_EXPORTS `
    (Join-Path $repo 'research\native-merge\keyboard_merge.c') -o (Join-Path $out 'pskb-keyboard-merge.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native mapping build failed.' }
$compileSources = @($sources) + @($generated)
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    ('/out:' + (Join-Path $out 'PSKeyboard-ControllerFree.exe')) @compileSources
if ($LASTEXITCODE -ne 0) { throw 'Controller-free experimental GUI build failed.' }
$guardChecks = $null
$transportTestsPassed = $false
if ($RunTests) {
    $guardOutput = Join-Path $out 'ControllerFreeGuardTests.exe'
    & $csc /nologo /target:exe /main:PSKeyboard.ControllerFreeGuardTests /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
        /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
        ('/out:' + $guardOutput) @compileSources $guardSource
    if ($LASTEXITCODE -ne 0) { throw 'Controller-free guard tests failed to compile.' }
    $guardLines = @(& $guardOutput)
    if ($LASTEXITCODE -ne 0) { throw 'Controller-free guard tests failed.' }
    $guardSummary = @($guardLines | Where-Object { $_ -match '^CONTROLLER_FREE_GUARDS_PASS checks=[0-9]+;' })
    if ($guardSummary.Count -ne 1 -or $guardSummary[0] -notmatch '^CONTROLLER_FREE_GUARDS_PASS checks=([0-9]+);') { throw 'Guard test result was not recognized.' }
    $guardChecks = [int]$Matches[1]
    $guardLines | Write-Output
    & $transportTestsScript -OutputDirectory (Join-Path $out 'transport-tests')
    $transportTestsPassed = $true
}
Copy-Item -LiteralPath $backend -Destination (Join-Path $out 'viiper.exe')
if ((Get-FileHash -LiteralPath (Join-Path $out 'viiper.exe') -Algorithm SHA256).Hash -ne $expected) { throw 'Backend changed while copying.' }

# Refuse a mixed source build if an editor or parallel commit changed an input.
foreach ($source in $sourceHashes) {
    if ((Get-FileHash -LiteralPath (Join-Path $repo $source.path) -Algorithm SHA256).Hash -ne $source.sha256) { throw ('Build input changed: ' + $source.path) }
}
$endCommit = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $endCommit -ne $commit) { throw 'Source revision changed during the build.' }
if ($RequireClean) {
    $endStatus = @(& git -C $repo status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0 -or $endStatus.Count -ne 0) { throw 'The release-candidate working tree changed during the build.' }
}
$runtimeNames = @('PSKeyboard-ControllerFree.exe', 'pskb-keyboard-merge.dll', 'viiper.exe')
$binaryHashes = @($runtimeNames | ForEach-Object {
    [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $out $_) -Algorithm SHA256).Hash }
})
$manifest = [ordered]@{
    schemaVersion = 1; product = '上班不要玩'; variant = 'controller-free'; version = $version
    assemblyVersion = $assemblyVersion; fileVersion = $fileVersion; informationalVersion = $informationalVersion
    channel = 'internal-preview'; sourceCommit = $commit; workingTreeDirty = $dirty; builtUtc = $builtUtc
    entryPoint = 'PSKeyboard-ControllerFree.exe'; commercialReady = $false; driversBundled = $false; setupSupported = $false
    controllerModes = @('dualshock4', 'dualsense'); defaultControllerMode = 'dualshock4'
    verification = [ordered]@{ controllerFreeGuardsRun = [bool]$RunTests; controllerFreeGuardChecks = $guardChecks; fakeTransportTestsPassed = $transportTestsPassed; sonyOperationVerifiedByBuild = $false }
    externalBinaries = @([ordered]@{ name = 'VIIPER'; version = '0.9.0'; path = 'viiper.exe'; sha256 = $expected; signatureVerified = $true; localUseOnly = $true; redistributionApproved = $false })
    sources = $sourceHashes; outputs = $binaryHashes
}
[IO.File]::WriteAllText((Join-Path $out 'BUILD.json'), ($manifest | ConvertTo-Json -Depth 7), $utf8)
$checksumLines = @((@($runtimeNames) + @('BUILD.json')) | Sort-Object | ForEach-Object {
    (Get-FileHash -LiteralPath (Join-Path $out $_) -Algorithm SHA256).Hash + '  ' + $_
})
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'), $checksumLines, $utf8)
& $verifyScript -BuildDirectory $out -RequireClean:$RequireClean
if ($RunTests) { & $versioningTestsScript -BuildDirectory $out }
Write-Output ('Built isolated versioned preview: ' + (Join-Path $out 'PSKeyboard-ControllerFree.exe'))
Write-Output 'Build only: no GUI/backend launched, no driver installed, no USB device attached, Sony unchanged.'
