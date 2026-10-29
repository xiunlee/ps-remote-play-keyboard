param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$RequireClean)
# Artifact inspection only. Does not run application/backend code.
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $BuildDirectory).Path
$manifest=Get-Content -LiteralPath (Join-Path $root 'BUILD.json') -Raw | ConvertFrom-Json
if($manifest.schemaVersion -ne 1 -or $manifest.variant -cne 'dualsense-candidate' -or
    $manifest.entryPoint -cne 'PSKeyboard-DualSensePreview.exe' -or $manifest.driverVersion -cne '0.9.8.1' -or
    $manifest.sourceCommit -cnotmatch '\A[0-9a-f]{40}\z' -or $manifest.channel -cne 'public-alpha' -or
    $manifest.priceModel -cne 'free' -or $manifest.license -cne 'GPL-3.0-only' -or
    $manifest.version -cnotmatch '\A0\.1\.0-alpha\.([0-9]+)\z' -or [int]$Matches[1] -lt 11) { throw 'Unexpected public candidate identity.' }
if($RequireClean -and ($manifest.workingTreeDirty -ne $false -or $manifest.testsRun -ne $true)) { throw 'Delivery requires clean sources and completed offline tests.' }
foreach($name in @('sonyOperationVerified','commercialReady','driversBundled','defaultConnected','defaultEnabled')) {
    if($manifest.$name -isnot [bool] -or $manifest.$name -ne $false) { throw ('Invalid candidate claim: '+$name) }
}
if($manifest.profileReadOnly -ne $false -or $manifest.profileSaveRequiresUserAction -ne $true -or
    $manifest.apiPort -ne 33642 -or $manifest.usbPort -ne 33641 -or $manifest.sessionTimeLimitSeconds -ne 0 -or
    (@($manifest.controllerModes) -join ',') -cne 'dualsensegamepadv5' -or $manifest.defaultControllerMode -cne 'dualsensegamepadv5') { throw 'Candidate constraints differ from the reviewed design.' }
$materials=Get-Content -LiteralPath (Join-Path $root 'MATERIALS.json') -Raw | ConvertFrom-Json
$nativeMaterials=Get-Content -LiteralPath (Join-Path $root 'NATIVE-MATERIALS.json') -Raw | ConvertFrom-Json
$nativeNotices=@('notices/Native-Zig-LICENSE.txt','notices/Native-MinGW-COPYING.txt',
    'notices/Native-LLVM-LICENSE.txt','notices/Native-MinGW-pseudo-reloc-NOTICE.txt')
if(@($nativeMaterials).Count -ne $nativeNotices.Count -or
    @($nativeMaterials|Where-Object{$nativeNotices -cnotcontains $_.license}).Count -ne 0) { throw 'Incomplete native runtime/toolchain notices.' }
foreach($material in @($materials)+@($nativeMaterials)) {
    if($material.license -cnotmatch '\Anotices/[A-Za-z0-9_.-]+\.txt\z' -or
        (Get-FileHash -LiteralPath (Join-Path $root $material.license)).Hash -cne $material.licenseSha256) { throw 'Invalid notice path or content.' }
}
$allowed=@('PSKeyboard-DualSensePreview.exe','DualSenseCandidateServer.exe','pskb-keyboard-merge.dll',
    'BACKEND-BUILD.json','MATERIALS.json','NATIVE-BUILD.json','NATIVE-MATERIALS.json','README.md','README.en.md','LICENSE','COPYRIGHT.md')+
    @($materials|ForEach-Object{$_.license})+@($nativeMaterials|ForEach-Object{$_.license})
if(@($allowed|Sort-Object -Unique).Count -ne $allowed.Count) { throw 'Duplicate runtime notice paths.' }
if(@($manifest.outputs).Count -ne $allowed.Count) { throw 'Runtime manifest is incomplete.' }
$seen=@{}
foreach($entry in $manifest.outputs) {
    if($allowed -cnotcontains $entry.path -or $seen.ContainsKey($entry.path)) { throw 'Unexpected or duplicate runtime entry.' }
    if((Get-FileHash -LiteralPath (Join-Path $root $entry.path)).Hash -cne $entry.sha256) { throw ('Runtime hash mismatch: '+$entry.path) }
    $seen[$entry.path]=$true
}
$seen=@{}
foreach($line in Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt')) {
    if($line -cnotmatch '\A([0-9A-F]{64})  ((?:notices/)?[A-Za-z0-9_.-]+)\z') { throw 'Invalid checksum syntax.' }
    $hash=$Matches[1];$name=$Matches[2]
    if(($allowed+@('BUILD.json')) -cnotcontains $name -or $seen.ContainsKey($name)) { throw 'Unexpected checksum entry.' }
    if((Get-FileHash -LiteralPath (Join-Path $root $name)).Hash -cne $hash) { throw ('Checksum mismatch: '+$name) }
    $seen[$name]=$true
}
if($seen.Count -ne $allowed.Count+1) { throw 'Incomplete checksums.' }
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $manifest.entryPoint))
if($info.FileVersion -cne $manifest.fileVersion -or $info.ProductVersion -cne $manifest.informationalVersion) { throw 'Embedded application version mismatch.' }
$backend=Get-Content -LiteralPath (Join-Path $root 'BACKEND-BUILD.json') -Raw | ConvertFrom-Json
if($backend.upstreamCommit -cne 'b78e31e93e84b4cb7c4b4c15b8aace10749106d2' -or
    $backend.privateBuildPathsEmbedded -ne $false -or
    $backend.persona -cne 'dualsensegamepadv5' -or $backend.sessionLifetimeLimit -ne $false -or
    $backend.shutdownTimeoutSeconds -ne 15 -or $backend.pureUnitTestsPassed -ne $true -or
    $backend.materialsManifestSha256 -cne (Get-FileHash -LiteralPath (Join-Path $root 'MATERIALS.json')).Hash -or
    (Get-FileHash -LiteralPath (Join-Path $root 'DualSenseCandidateServer.exe')).Hash -cne $manifest.backendSha256 -or
    $backend.executableSha256 -cne $manifest.backendSha256) { throw 'Backend dependency mismatch.' }
if(@($manifest.sources).Count -eq 0) { throw 'Missing source snapshot identity.' }
$native=Get-Content -LiteralPath (Join-Path $root 'NATIVE-BUILD.json') -Raw | ConvertFrom-Json
$expectedArguments=@('cc','-target','x86_64-windows-gnu','-std=c11','-Wall','-Wextra','-Werror','-O2',
    '-shared','-DPSKB_MERGE_EXPORTS','keyboard_merge.c','-o','pskb-keyboard-merge.dll')
if($native.schemaVersion -ne 1 -or $native.artifact -cne 'pskb-keyboard-merge.dll' -or
    $native.target -cne 'x86_64-windows-gnu' -or $native.compiler -cne 'Zig' -or $native.compilerVersion -cne '0.17.0' -or
    $native.compilerExecutableSha256 -cne 'F54B14F02A4D7D8AC62A94A0663486EB166BBA3745D85B466238C1B93E723D46' -or
    $native.compilerArchiveExpectedSha256 -cne 'B5663F69581DCF391293FBF16C06CB80D81D806545CE618B4D0BAB7F0EB8C428' -or
    $native.compilerArchiveUrl -cne 'https://ziglang.org/download/0.17.0/zig-x86_64-windows-0.17.0.zip' -or
    (@($native.compileArguments) -join '|') -cne ($expectedArguments -join '|') -or
    $native.materialsManifestSha256 -cne (Get-FileHash -LiteralPath (Join-Path $root 'NATIVE-MATERIALS.json')).Hash -or
    $native.executableSha256 -cne $manifest.nativeSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $root 'pskb-keyboard-merge.dll')).Hash -cne $manifest.nativeSha256 -or
    $native.cacheDirectoriesIsolated -ne $true -or $native.syntheticReportsOnly -ne $true -or
    $native.testsRun -ne $manifest.testsRun) { throw 'Native source build identity or output mismatch.' }
foreach($name in @('keyboardCaptured','devicesOpened','sonyTested')) {
    if($native.$name -isnot [bool] -or $native.$name -ne $false) { throw ('Invalid native build claim: '+$name) }
}
$nativeSources=@('research/native-merge/keyboard_merge.c','research/native-merge/keyboard_merge.h',
    'research/native-merge/keyboard_merge_test.c','research/native-merge/KeyboardMergeInteropTest.cs',
    'research/dualsense-preview/Build-Native.ps1')
if(@($native.sources).Count -ne $nativeSources.Count) { throw 'Native source identity is incomplete.' }
foreach($name in $nativeSources) {
    $entry=@($native.sources|Where-Object{$_.path -ceq $name})
    $parentEntry=@($manifest.sources|Where-Object{$_.path -ceq $name})
    if($entry.Count -ne 1 -or $parentEntry.Count -ne 1 -or $entry[0].sha256 -cnotmatch '\A[0-9A-F]{64}\z' -or
        $entry[0].sha256 -cne $parentEntry[0].sha256) { throw ('Native source identity differs from the application snapshot: '+$name) }
}
foreach($name in @('LICENSE','COPYRIGHT.md')) {
    $entry=@($manifest.sources|Where-Object{$_.path -ceq $name})
    if($entry.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $root $name)).Hash -cne $entry[0].sha256) { throw ('Project license identity mismatch: '+$name) }
}
if($manifest.testsRun) {
    $nativeTest=@($native.testResults|Where-Object{$_.kind -ceq 'native-synthetic-reports'})
    $interopTest=@($native.testResults|Where-Object{$_.kind -ceq 'native-csharp-interop'})
    if(@($native.testResults).Count -ne 2 -or $nativeTest.Count -ne 1 -or $interopTest.Count -ne 1 -or
        $nativeTest[0].passed -ne $true -or $nativeTest[0].checks -le 0 -or $interopTest[0].passed -ne $true) { throw 'Native offline tests are incomplete.' }
}
Write-Output ('CANDIDATE_ARTIFACT_VERIFIED version='+$manifest.version+' source='+$manifest.sourceCommit+' sony_tested=false')
