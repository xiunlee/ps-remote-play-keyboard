param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [string]$Version='0.1.0-alpha.4',
    [string]$OutputRoot=(Join-Path $env:USERPROFILE '.pskeyboard-tools\release-candidates')
)
# Build/package only: never executes setup, launcher, HID observation or Sony.
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+-alpha\.\d+$'){throw 'Only internal alpha packages are supported.'}
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$root=[IO.Path]::GetFullPath($OutputRoot)
$build=Join-Path $root ($Version+'-'+[Guid]::NewGuid().ToString('N'))
$work=Join-Path $build 'build'
$stage=Join-Path $build 'PSKeyboard-Preview'
New-Item -ItemType Directory -Path $work,$stage -Force | Out-Null
& (Join-Path $repo 'src\physical-keyboard\Build.ps1') -Zig $Zig -OutputDirectory $work -RunTests
& (Join-Path $repo 'research\physical-usb-relay\BuildAndTest.ps1') -OutputDirectory (Join-Path $build 'protocol-tests')
& (Join-Path $repo 'research\windows-hid-observer\Test-Guards.ps1') -OutputDirectory (Join-Path $build 'guard-tests')

# Explicit allowlist excludes personal cache records, installers and obsolete binaries.
foreach($file in @('PSKeyboard-Physical.exe','pskb-keyboard-merge.dll')) {
    Copy-Item -LiteralPath (Join-Path $work $file) -Destination (Join-Path $stage $file)
}
New-Item -ItemType Directory -Path (Join-Path $stage 'operator-tools') | Out-Null
foreach($file in @('Install-Exporter.ps1','Switch-PhysicalDriver.ps1','Start-ExporterService.ps1')) {
    $text=[IO.File]::ReadAllText((Join-Path $repo ('src\physical-keyboard\'+$file)),[Text.Encoding]::UTF8)
    [IO.File]::WriteAllText((Join-Path $stage ('operator-tools\'+$file)),$text,(New-Object Text.UTF8Encoding($true)))
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PREVIEW-README.txt') -Destination (Join-Path $stage '請先閱讀.txt')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DEPENDENCIES.json') -Destination (Join-Path $stage 'DEPENDENCIES.json')
Copy-Item -LiteralPath (Join-Path $repo 'docs\PRODUCT-SCOPE.md') -Destination (Join-Path $stage 'PRODUCT-SCOPE.md')
Copy-Item -LiteralPath (Join-Path $repo 'docs\AUTOMATION-AND-CONTROLLER-TESTS.md') -Destination (Join-Path $stage 'AUTOMATION-AND-CONTROLLER-TESTS.md')
Copy-Item -LiteralPath (Join-Path $repo 'src\physical-keyboard\Check-Status.ps1') -Destination (Join-Path $stage 'Check-Status.ps1')
$commit=(& git -C $repo rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0){throw 'Cannot identify source revision.'}
$dirty=[bool](& git -C $repo status --porcelain)
$sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $repo 'src\physical-keyboard') -File | Where-Object {$_.Extension -in @('.cs','.ps1')})
$sourceFiles+=Get-Item -LiteralPath (Join-Path $repo 'src\keyboard-pad\KeyboardInputState.cs'),(Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs'),(Join-Path $repo 'research\physical-usb-relay\UsbIpRelay.cs'),(Join-Path $repo 'research\native-merge\keyboard_merge.c'),(Join-Path $repo 'research\native-merge\keyboard_merge.h')
$sourceHashes=@($sourceFiles | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=$_.FullName.Substring($repo.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$manifest=[ordered]@{version=$Version;channel='internal-preview';commercialReady=$false;sourceCommit=$commit;workingTreeDirty=$dirty;builtUtc=[DateTime]::UtcNow.ToString('o');driversBundled=$false;setupSupported=$false;sources=$sourceHashes}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $stage 'BUILD.json') -Encoding UTF8
$checksums=@(Get-ChildItem -LiteralPath $stage -File -Recurse | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash+'  '+$_.FullName.Substring($stage.Length+1).Replace('\','/')
})
$checksums | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding UTF8
$zip=Join-Path $build ('PSKeyboard-'+$Version+'-internal-preview.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $zip
$verify=Join-Path $build 'verification'
Expand-Archive -LiteralPath $zip -DestinationPath $verify
$unpacked=Join-Path $verify 'PSKeyboard-Preview'
foreach($line in (Get-Content -LiteralPath (Join-Path $unpacked 'SHA256SUMS.txt') -Encoding UTF8)) {
    $hash,$relative=$line -split '  ',2
    if((Get-FileHash -LiteralPath (Join-Path $unpacked $relative) -Algorithm SHA256).Hash -ne $hash){throw 'Archive integrity verification failed.'}
}
[pscustomobject]@{archive=$zip;sha256=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash;verifiedFiles=$checksums.Count;commercialReady=$false} | ConvertTo-Json
