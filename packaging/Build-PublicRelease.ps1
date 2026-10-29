param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$BackendDirectory,
    [Parameter(Mandatory=$true)][string]$UpstreamArchive,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
# Package reviewed files only. Never copy a live folder wholesale, run a player,
# install drivers, launch payment/email links, or publish anything from this script.
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$build=(Resolve-Path -LiteralPath $BuildDirectory).Path
$backend=(Resolve-Path -LiteralPath $BackendDirectory).Path
$upstreamZip=(Resolve-Path -LiteralPath $UpstreamArchive).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Public package output must be a new directory.'}
& (Join-Path $repo 'research/dualsense-preview/Verify-Candidate.ps1') -BuildDirectory $build -RequireClean
$manifest=Get-Content -LiteralPath (Join-Path $build 'BUILD.json') -Raw | ConvertFrom-Json
$backendRecord=Get-Content -LiteralPath (Join-Path $backend 'BUILD.json') -Raw | ConvertFrom-Json
if($manifest.backendSha256 -cne $backendRecord.executableSha256){throw 'Backend does not belong to this build.'}
if((Get-FileHash -LiteralPath (Join-Path $backend 'BUILD.json')).Hash -cne
   (Get-FileHash -LiteralPath (Join-Path $build 'BACKEND-BUILD.json')).Hash -or
   (Get-FileHash -LiteralPath (Join-Path $backend 'MATERIALS.json')).Hash -cne $backendRecord.materialsManifestSha256 -or
   (Get-FileHash -LiteralPath (Join-Path $backend 'module/main.go')).Hash -cne $backendRecord.backendSourceSha256 -or
   (Get-FileHash -LiteralPath (Join-Path $backend 'module/main_test.go')).Hash -cne $backendRecord.pureTestSourceSha256 -or
   (Get-FileHash -LiteralPath (Join-Path $backend 'module/go.sum')).Hash -cne $backendRecord.upstreamGoSumSha256){
    throw 'Backend source or materials differ from the shipped binary provenance.'
}
if((Get-FileHash -LiteralPath $upstreamZip).Hash -cne $backendRecord.upstreamArchiveSha256){throw 'Wrong upstream source archive.'}
if(@(& git -C $repo status --porcelain --untracked-files=normal).Count -ne 0){throw 'Commit release sources before packaging.'}
if((& git -C $repo rev-parse HEAD).Trim() -cne $manifest.sourceCommit){throw 'Package from the exact clean release checkout.'}
$version=$manifest.version
if($version -cmatch '\A(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.([0-9])\z' -and [int]$Matches[1] -le 65534 -and [int]$Matches[2] -le 65534) {
    $releaseChannel='public-release'
} elseif($version -cmatch '\A0\.1\.0-alpha\.([0-9]+)\z' -and [int]$Matches[1] -ge 11 -and [int]$Matches[1] -le 65534) {
    $releaseChannel='public-alpha'
} else { throw 'Unexpected release version.' }
if($manifest.channel -cne $releaseChannel){throw 'Release channel does not match the version.'}
$prefix='PSKeyboard-'+$version
New-Item -ItemType Directory -Path $out | Out-Null
$utf8=[Text.UTF8Encoding]::new($false)
$runtime=Join-Path $out ($prefix+'-windows-x64')
New-Item -ItemType Directory -Path $runtime | Out-Null
foreach($entry in @($manifest.outputs)+@(@{path='BUILD.json'},@{path='SHA256SUMS.txt'})){
    $name=$entry.path
    if($name -cnotmatch '\A(?:notices/)?[A-Za-z0-9_.-]+\z'){throw 'Unsafe runtime manifest path.'}
    $destination=Join-Path $runtime $name
    [IO.Directory]::CreateDirectory((Split-Path -Parent $destination)) | Out-Null
    Copy-Item -LiteralPath (Join-Path $build $name) -Destination $destination
}
& (Join-Path $repo 'research/dualsense-preview/Verify-Candidate.ps1') -BuildDirectory $runtime -RequireClean
$runtimeZip=Join-Path $out ($prefix+'-windows-x64.zip')
Compress-Archive -LiteralPath $runtime -DestinationPath $runtimeZip

$source=Join-Path $out ($prefix+'-source')
New-Item -ItemType Directory -Path $source | Out-Null
$projectArchive=Join-Path $source 'project.zip'
& git -C $repo archive --format=zip ('--output='+$projectArchive) $manifest.sourceCommit
if($LASTEXITCODE -ne 0){throw 'Cannot archive exact release source.'}
Expand-Archive -LiteralPath $projectArchive -DestinationPath (Join-Path $source 'project')
foreach($entry in $manifest.sources){
    if($entry.path -cnotmatch '\A[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\z' -or $entry.path.Split('/') -contains '..'){throw 'Unsafe source path.'}
    $workingFile=Join-Path $repo $entry.path
    $archivedFile=Join-Path (Join-Path $source 'project') $entry.path
    if((Get-FileHash -LiteralPath $workingFile).Hash -cne $entry.sha256){throw ('Build source changed: '+$entry.path)}
    if((Get-FileHash -LiteralPath $archivedFile).Hash -cne $entry.sha256){
        # Git may normalize CRLF in text files. No other content difference is allowed.
        if([IO.Path]::GetExtension($entry.path) -notin @('.cs','.ps1','.md','.go','.c','.h','') -or
           [IO.File]::ReadAllText($workingFile).Replace("`r`n","`n") -cne
           [IO.File]::ReadAllText($archivedFile).Replace("`r`n","`n")){throw ('Archived source differs from build: '+$entry.path)}
    }
}
# The source archive is kept intact as an additional identity record, not deleted.
$deps=Join-Path $source 'dependencies'
New-Item -ItemType Directory -Path $deps | Out-Null
Copy-Item -LiteralPath $upstreamZip -Destination (Join-Path $deps 'VIIPER-source.zip')
$wrapper=Join-Path $deps 'wrapper'
New-Item -ItemType Directory -Path $wrapper | Out-Null
foreach($name in @('main.go','main_test.go','go.sum')){
    Copy-Item -LiteralPath (Join-Path $backend ('module/'+$name)) -Destination (Join-Path $wrapper $name)
}
$goMod=[IO.File]::ReadAllText((Join-Path $backend 'module/go.mod'))
$goMod=[regex]::Replace($goMod,'(?m)^replace github\.com/Alia5/VIIPER => .*$',
    'replace github.com/Alia5/VIIPER => ../VIIPER-'+$backendRecord.upstreamCommit)
[IO.File]::WriteAllText((Join-Path $wrapper 'go.mod'),$goMod,$utf8)
$materials=Get-Content -LiteralPath (Join-Path $backend 'MATERIALS.json') -Raw | ConvertFrom-Json
$moduleRecords=@()
$expectedModules=@('golang.org/x/crypto','golang.org/x/exp','golang.org/x/sys')
foreach($material in $materials){
    if($material.module -in @('github.com/Alia5/VIIPER','Go standard library')){continue}
    if($material.module -cnotin $expectedModules -or $material.version -cnotmatch '\Av[0-9A-Za-z.+-]+\z'){throw 'Review new dependency before packaging.'}
    $modulePath=Join-Path $backend ('gomodcache/'+$material.module+'@'+$material.version)
    $moduleRoot=(Resolve-Path -LiteralPath $modulePath).Path
    if(-not $moduleRoot.StartsWith(($backend.TrimEnd('\')+'\gomodcache\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Dependency source escapes build cache.'}
    $folder=$material.module.Replace('/','_')+'@'+$material.version
    Copy-Item -LiteralPath $moduleRoot -Destination (Join-Path $deps $folder) -Recurse
    $moduleRecords+=@([ordered]@{module=$material.module;version=$material.version;directory=('dependencies/'+$folder)})
}
if($moduleRecords.Count -ne $expectedModules.Count){throw 'Linked dependency source set is incomplete.'}
Copy-Item -LiteralPath (Join-Path $source 'project/docs/REBUILD.md') -Destination (Join-Path $source 'REBUILD.md')
Copy-Item -LiteralPath (Join-Path $build 'LICENSE'),(Join-Path $build 'COPYRIGHT.md') -Destination $source
$sourceInfo=[ordered]@{
    version=$version;sourceCommit=$manifest.sourceCommit;
    upstreamCommit=$backendRecord.upstreamCommit;upstreamArchiveSha256=$backendRecord.upstreamArchiveSha256;
    linkedGoModules=$moduleRecords;offlineBuildClaim=$false;
    note='Windows/.NET and compiler toolchains are prerequisites; see REBUILD.md. No personal settings or diagnostics included.'
}
[IO.File]::WriteAllText((Join-Path $source 'SOURCE.json'),($sourceInfo|ConvertTo-Json -Depth 5),$utf8)
$sourceChecksums=@(Get-ChildItem -LiteralPath $source -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative=$_.FullName.Substring($source.Length+1).Replace('\','/')
    (Get-FileHash -LiteralPath $_.FullName).Hash+'  '+$relative
})
[IO.File]::WriteAllLines((Join-Path $source 'SHA256SUMS.txt'),[string[]]$sourceChecksums,$utf8)
$sourceZip=Join-Path $out ($prefix+'-source.zip')
Compress-Archive -LiteralPath $source -DestinationPath $sourceZip
$release=[ordered]@{
    version=$version;sourceCommit=$manifest.sourceCommit;channel=$releaseChannel;price=0;license='GPL-3.0-only';
    author='xiunlee';driversBundled=$false;sonyOperationVerified=$false;
    files=@($runtimeZip,$sourceZip | ForEach-Object{[ordered]@{name=(Split-Path -Leaf $_);sha256=(Get-FileHash -LiteralPath $_).Hash;bytes=(Get-Item -LiteralPath $_).Length}})
}
[IO.File]::WriteAllText((Join-Path $out 'RELEASE.json'),($release|ConvertTo-Json -Depth 5),$utf8)
$publicFiles=@($runtimeZip,$sourceZip,(Join-Path $out 'RELEASE.json'))
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),[string[]]@($publicFiles|ForEach-Object{(Get-FileHash -LiteralPath $_).Hash+'  '+(Split-Path -Leaf $_)}),$utf8)
Write-Output ('PUBLIC_PACKAGE_READY version='+$version+' source='+$manifest.sourceCommit)
