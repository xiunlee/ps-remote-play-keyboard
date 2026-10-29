param(
    [Parameter(Mandatory=$true)][string]$BackendDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][string]$Zig,
    [switch]$RunTests,
    [switch]$RequireClean
)
# Build only: no candidate GUI/server execution, Windows import, or Sony access.
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $out) { throw 'Candidate output must be a new directory; old builds are immutable.' }
$version=[IO.File]::ReadAllText((Join-Path $repo 'VERSION')).Trim()
if ($version -cmatch '\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.([0-9])\z' -and [int]$Matches[1] -le 65534 -and [int]$Matches[2] -le 65534) {
    $fileVersion=$Matches[1]+'.'+$Matches[2]+'.'+$Matches[3]+'.0';$assemblyVersion=$fileVersion;$channel='public-release'
} elseif ($version -cmatch '\A0\.1\.0-alpha\.([0-9]+)\z' -and [int]$Matches[1] -ge 11 -and [int]$Matches[1] -le 65534) {
    $fileVersion='0.1.0.'+$Matches[1];$assemblyVersion='0.1.0.0';$channel='public-alpha'
} else { throw 'Expected major.minor.patch (major/minor 0-65534, patch 0-9), or historical 0.1.0-alpha.11 through alpha.65534.' }
$commit=(& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $commit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Cannot identify source commit.' }
$dirty=@(& git -C $repo status --porcelain --untracked-files=normal).Count -ne 0
if ($LASTEXITCODE -ne 0 -or ($RequireClean -and $dirty)) { throw 'Clean candidate build requires committed source.' }
& git -C $repo show-ref --verify --quiet ('refs/tags/v'+$version)
if ($LASTEXITCODE -eq 0) {
    $tagCommit=(& git -C $repo rev-parse ('refs/tags/v'+$version+'^{commit}')).Trim()
    if ($tagCommit -ne $commit) { throw 'Version tag belongs to another revision. Increment VERSION; never move the tag.' }
} elseif ($LASTEXITCODE -ne 1) { throw 'Cannot check the version tag.' }
$backendRoot=(Resolve-Path -LiteralPath $BackendDirectory).Path
$backend=Join-Path $backendRoot 'DualSenseCandidateBackend.exe'
$backendRecord=Get-Content -LiteralPath (Join-Path $backendRoot 'BUILD.json') -Raw | ConvertFrom-Json
$backendHash=(Get-FileHash -LiteralPath $backend -Algorithm SHA256).Hash
if ($backendRecord.executableSha256 -cne $backendHash -or $backendRecord.upstreamCommit -cne 'b78e31e93e84b4cb7c4b4c15b8aace10749106d2' -or
    $backendRecord.backendSourceSha256 -cne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'backend/main.go')).Hash -or
    $backendRecord.pureTestSourceSha256 -cne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'backend/main_test.go')).Hash -or
    $backendRecord.buildScriptSha256 -cne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'backend/Build-Backend.ps1')).Hash -or
    $backendRecord.pureUnitTestsPassed -ne $true -or $backendRecord.materialsManifestSha256 -cne (Get-FileHash -LiteralPath (Join-Path $backendRoot 'MATERIALS.json')).Hash) {
    throw 'Candidate backend provenance does not match the reviewed source.'
}
$sourceNames=@(
    'research/dualsense-preview/DualSensePreview.cs','research/dualsense-preview/CandidateTransport.cs',
    'research/dualsense-preview/SupportLinks.cs','research/dualsense-preview/PreviewText.cs',
    'research/dualsense-v5-test/V5Protocol.cs','src/keyboard-pad/ViiperTransport.cs',
    'src/keyboard-pad/KeyboardMapping.cs','src/keyboard-pad/KeyboardInputState.cs',
    'src/physical-keyboard/BindingProfile.cs','src/physical-keyboard/RapidFireEngine.cs',
    'src/physical-keyboard/KeyboardControlPipeline.cs','src/physical-keyboard/PhysicalReportMerge.cs','src/physical-keyboard/BindingEditor.cs',
    'src/physical-keyboard/KeyboardUi.cs','src/physical-keyboard/UiText.cs'
)
$trackedInputs=$sourceNames+@('VERSION','LICENSE','COPYRIGHT.md','research/dualsense-preview/Build-Candidate.ps1',
    'packaging/Build-PublicRelease.ps1','docs/REBUILD.md','packaging/README.zh-TW.md','packaging/README.en.md',
    'research/dualsense-preview/Build-Native.ps1','research/native-merge/keyboard_merge.c',
    'research/native-merge/keyboard_merge.h','research/native-merge/keyboard_merge_test.c','research/native-merge/KeyboardMergeInteropTest.cs',
    'research/dualsense-preview/Verify-Candidate.ps1','research/dualsense-preview/README.md',
    'research/dualsense-preview/PreviewGuardTests.cs','research/dualsense-preview/PreviewTextTests.cs','research/dualsense-preview/CandidateTransportTests.cs',
    'research/dualsense-preview/RunTransportTests.ps1','research/dualsense-preview/backend/main.go',
    'research/dualsense-preview/backend/Build-Backend.ps1','research/dualsense-preview/backend/main_test.go',
    'research/dualsense-preview/backend/README.md','assets/branding/app.ico','assets/branding/ps-mark.png',
    'src/physical-keyboard/BindingProfileTests.cs','src/physical-keyboard/RapidFireTests.cs',
    'src/physical-keyboard/KeyboardControlPipelineTests.cs','src/physical-keyboard/UiInteractionTests.cs','src/physical-keyboard/UiTextTests.cs')
$sourceHashes=@($trackedInputs|Sort-Object -Unique|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash}})
New-Item -ItemType Directory -Path $out | Out-Null
$nativeRoot=Join-Path $out 'native-build'
& (Join-Path $PSScriptRoot 'Build-Native.ps1') -Zig $Zig -OutputDirectory $nativeRoot -RunTests:$RunTests
if(-not $?) { throw 'Native source build failed.' }
$native=Join-Path $nativeRoot 'pskb-keyboard-merge.dll'
$nativeRecord=Get-Content -LiteralPath (Join-Path $nativeRoot 'NATIVE-BUILD.json') -Raw | ConvertFrom-Json
$nativeHash=(Get-FileHash -LiteralPath $native).Hash
if($nativeRecord.executableSha256 -cne $nativeHash -or $nativeRecord.testsRun -ne [bool]$RunTests) { throw 'Native build record does not match the freshly compiled library.' }
$utf8=[Text.UTF8Encoding]::new($false)
$builtUtc=[DateTime]::UtcNow.ToString('o')
$infoVersion=$version+'+g'+$commit.Substring(0,12)
if ($dirty) { $infoVersion+='.dirty' }
$dirtyLiteral=if($dirty){'true'}else{'false'}
$generated=Join-Path $out 'BuildInfo.g.cs'
[IO.File]::WriteAllText($generated,@"
using System.Reflection;
[assembly: AssemblyTitle("PS Remote Play Keyboard")]
[assembly: AssemblyProduct("PS Remote Play Keyboard")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$fileVersion")]
[assembly: AssemblyInformationalVersion("$infoVersion")]
namespace PSKeyboard { internal static class BuildInfo {
internal const string Version="$version", SourceCommit="$commit", BuiltUtc="$builtUtc";
internal const string InformationalVersion="$infoVersion", FileVersion="$fileVersion", BackendSha256="$backendHash";
internal const bool WorkingTreeDirty=$dirtyLiteral;
} }
"@,$utf8)
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$icon=Join-Path $repo 'assets/branding/app.ico'
$mark=Join-Path $repo 'assets/branding/ps-mark.png'
$common=@('/nologo','/platform:x64','/optimize+','/warnaserror','/langversion:5','/codepage:65001',
    '/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll',
    ('/win32icon:'+$icon),('/resource:'+$icon+',PSKeyboard.BrandIcon'),('/resource:'+$mark+',PSKeyboard.BrandMark'))
$sources=@($sourceNames|ForEach-Object{Join-Path $repo $_})+@($generated)
& $csc @common /target:winexe ('/out:'+(Join-Path $out 'PSKeyboard-DualSensePreview.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Candidate GUI compilation failed.' }
Copy-Item -LiteralPath $native -Destination (Join-Path $out 'pskb-keyboard-merge.dll')
Copy-Item -LiteralPath $backend -Destination (Join-Path $out 'DualSenseCandidateServer.exe')
Copy-Item -LiteralPath (Join-Path $backendRoot 'BUILD.json') -Destination (Join-Path $out 'BACKEND-BUILD.json')
Copy-Item -LiteralPath (Join-Path $backendRoot 'MATERIALS.json') -Destination (Join-Path $out 'MATERIALS.json')
Copy-Item -LiteralPath (Join-Path $nativeRoot 'NATIVE-BUILD.json') -Destination (Join-Path $out 'NATIVE-BUILD.json')
Copy-Item -LiteralPath (Join-Path $nativeRoot 'NATIVE-MATERIALS.json') -Destination (Join-Path $out 'NATIVE-MATERIALS.json')
$materials=Get-Content -LiteralPath (Join-Path $backendRoot 'MATERIALS.json') -Raw | ConvertFrom-Json
$nativeMaterials=Get-Content -LiteralPath (Join-Path $nativeRoot 'NATIVE-MATERIALS.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path (Join-Path $out 'notices') | Out-Null
foreach($material in $materials) {
    if($material.license -cnotmatch '\Anotices/[A-Za-z0-9_.-]+\.txt\z') { throw 'Invalid notice path.' }
    $license=Join-Path $backendRoot $material.license
    if((Get-FileHash -LiteralPath $license).Hash -cne $material.licenseSha256) { throw 'Notice content hash mismatch.' }
    Copy-Item -LiteralPath $license -Destination (Join-Path $out $material.license)
}
foreach($material in $nativeMaterials) {
    if($material.license -cnotmatch '\Anotices/Native-[A-Za-z0-9_.-]+\.txt\z') { throw 'Invalid native notice path.' }
    $license=Join-Path $nativeRoot $material.license
    $destination=Join-Path $out $material.license
    if((Test-Path -LiteralPath $destination) -or (Get-FileHash -LiteralPath $license).Hash -cne $material.licenseSha256) { throw 'Duplicate native notice or content hash mismatch.' }
    Copy-Item -LiteralPath $license -Destination $destination
}
Copy-Item -LiteralPath (Join-Path $repo 'packaging/README.zh-TW.md') -Destination (Join-Path $out 'README.md')
Copy-Item -LiteralPath (Join-Path $repo 'packaging/README.en.md') -Destination (Join-Path $out 'README.en.md')
foreach($name in @('LICENSE','COPYRIGHT.md')) { Copy-Item -LiteralPath (Join-Path $repo $name) -Destination (Join-Path $out $name) }
$testResults=@($nativeRecord.testResults)
if ($RunTests) {
    $guard=Join-Path $out 'PreviewGuardTests.exe'
    & $csc @common /target:exe /main:PSKeyboard.PreviewGuardTests ('/out:'+$guard) @sources (Join-Path $PSScriptRoot 'PreviewGuardTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Candidate guard compilation failed.' }
    $lines=@(& $guard); $lines|Write-Output
    if ($LASTEXITCODE -ne 0) { throw 'Candidate guards failed.' }
    $testResults+=@([ordered]@{kind='candidate-guards';passed=$true;output=$lines})
    $textTest=Join-Path $out 'PreviewTextTests.exe'
    & $csc @common /target:exe /main:PSKeyboard.PreviewTextTests ('/out:'+$textTest) @sources (Join-Path $PSScriptRoot 'PreviewTextTests.cs')
    if($LASTEXITCODE -ne 0){throw 'Preview translation tests did not compile.'}
    $lines=@(& $textTest); $lines|Write-Output
    if($LASTEXITCODE -ne 0){throw 'Preview translation tests failed.'}
    $testResults+=@([ordered]@{kind='preview-localization';passed=$true;output=$lines})
    & (Join-Path $PSScriptRoot 'RunTransportTests.ps1') -OutputDirectory (Join-Path $out 'transport-tests')
    if (-not $?) { throw 'Candidate fake transport tests failed.' }
    $testResults+=@([ordered]@{kind='candidate-fake-transport';passed=$true})
    foreach($testName in @('BindingProfileTests','RapidFireTests','KeyboardControlPipelineTests','UiInteractionTests','UiTextTests')) {
        $testExe=Join-Path $out ($testName+'.exe')
        $testMain=if($testName -eq 'UiInteractionTests'){'PSKeyboard.UiInteractionTests'}else{$testName}
        & $csc @common /target:exe ('/main:'+$testMain) ('/out:'+$testExe) @sources (Join-Path $repo ('src/physical-keyboard/'+$testName+'.cs'))
        if ($LASTEXITCODE -ne 0) { throw ('Shared input tests did not compile: '+$testName) }
        $lines=@(& $testExe); $lines|Write-Output
        if ($LASTEXITCODE -ne 0) { throw ('Shared input tests failed: '+$testName) }
        $testResults+=@([ordered]@{kind=$testName;passed=$true;output=$lines})
    }
}
foreach($source in $sourceHashes) {
    if ((Get-FileHash -LiteralPath (Join-Path $repo $source.path)).Hash -cne $source.sha256) { throw ('Source changed during build: '+$source.path) }
}
if ((& git -C $repo rev-parse HEAD).Trim() -cne $commit) { throw 'Commit changed during build.' }
if ($RequireClean -and @(& git -C $repo status --porcelain --untracked-files=normal).Count -ne 0) { throw 'Working tree changed during build.' }
$runtimeNames=@('PSKeyboard-DualSensePreview.exe','DualSenseCandidateServer.exe','pskb-keyboard-merge.dll',
    'BACKEND-BUILD.json','MATERIALS.json','NATIVE-BUILD.json','NATIVE-MATERIALS.json','README.md','README.en.md','LICENSE','COPYRIGHT.md')+
    @($materials|ForEach-Object{$_.license})+@($nativeMaterials|ForEach-Object{$_.license})
$manifest=[ordered]@{
    schemaVersion=1;product='PS Remote Play Keyboard';variant='dualsense-candidate';version=$version;fileVersion=$fileVersion;
    informationalVersion=$infoVersion;sourceCommit=$commit;workingTreeDirty=$dirty;builtUtc=$builtUtc;
    entryPoint='PSKeyboard-DualSensePreview.exe';channel=$channel;priceModel='free';license='GPL-3.0-only';driversBundled=$false;
    sonyOperationVerified=$false;commercialReady=$false;profileReadOnly=$false;profileSaveRequiresUserAction=$true;defaultConnected=$false;defaultEnabled=$false;
    sonyBaselineEvidence=[ordered]@{version='0.1.0-alpha.9';date='2026-10-29';source='user-report';persona='dualsensegamepadv5';operationPassed=$true};
    sessionTimeLimitSeconds=0;
    driverVersion='0.9.8.1';apiPort=33642;usbPort=33641;backendSha256=$backendHash;nativeSha256=$nativeHash;
    controllerModes=@('dualsensegamepadv5');defaultControllerMode='dualsensegamepadv5';
    uiLanguages=@('en','zh-Hant','zh-Hans','ja','ko','de','fr','es','pt-BR');uiLanguageSelection='windows-ui-culture';uiLanguageFallback='en';
    driverValidationRun='https://github.com/xiunlee/ps-remote-play-keyboard/actions/runs/40838199781';
    testsRun=[bool]$RunTests;testResults=$testResults;sources=$sourceHashes;
    outputs=@($runtimeNames|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $out $_)).Hash}})
}
[IO.File]::WriteAllText((Join-Path $out 'BUILD.json'),($manifest|ConvertTo-Json -Depth 8),$utf8)
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),[string[]]@(($runtimeNames+@('BUILD.json'))|Sort-Object|ForEach-Object{(Get-FileHash -LiteralPath (Join-Path $out $_)).Hash+'  '+$_}),$utf8)
& (Join-Path $PSScriptRoot 'Verify-Candidate.ps1') -BuildDirectory $out -RequireClean:$RequireClean
Write-Output ('Built candidate without launching it: '+(Join-Path $out 'PSKeyboard-DualSensePreview.exe'))
