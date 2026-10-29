param([Parameter(Mandatory=$true)][string]$BuildDirectory,[switch]$RequireClean)
# Artifact inspection only. Does not run application/backend code.
$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath $BuildDirectory).Path
$manifest=Get-Content -LiteralPath (Join-Path $root 'BUILD.json') -Raw | ConvertFrom-Json
if($manifest.schemaVersion -ne 1 -or $manifest.variant -cne 'dualsense-candidate' -or
    $manifest.entryPoint -cne 'PSKeyboard-DualSensePreview.exe' -or $manifest.driverVersion -cne '0.9.8.1' -or
    $manifest.sourceCommit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Unexpected candidate identity.' }
if($RequireClean -and ($manifest.workingTreeDirty -ne $false -or $manifest.testsRun -ne $true)) { throw 'Delivery requires clean sources and completed offline tests.' }
foreach($name in @('sonyOperationVerified','commercialReady','driversBundled','defaultConnected','defaultEnabled')) {
    if($manifest.$name -isnot [bool] -or $manifest.$name -ne $false) { throw ('Invalid candidate claim: '+$name) }
}
if($manifest.profileReadOnly -ne $true -or $manifest.apiPort -ne 33642 -or $manifest.usbPort -ne 33641) { throw 'Candidate constraints differ from the reviewed design.' }
$materials=Get-Content -LiteralPath (Join-Path $root 'MATERIALS.json') -Raw | ConvertFrom-Json
foreach($material in $materials) {
    if($material.license -cnotmatch '\Anotices/[A-Za-z0-9_.-]+\.txt\z' -or
        (Get-FileHash -LiteralPath (Join-Path $root $material.license)).Hash -cne $material.licenseSha256) { throw 'Invalid notice path or content.' }
}
$allowed=@('PSKeyboard-DualSensePreview.exe','DualSenseCandidateServer.exe','pskb-keyboard-merge.dll','BACKEND-BUILD.json','MATERIALS.json','README.md')+@($materials|ForEach-Object{$_.license})
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
    $backend.materialsManifestSha256 -cne (Get-FileHash -LiteralPath (Join-Path $root 'MATERIALS.json')).Hash -or
    (Get-FileHash -LiteralPath (Join-Path $root 'DualSenseCandidateServer.exe')).Hash -cne $manifest.backendSha256 -or
    $backend.executableSha256 -cne $manifest.backendSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $root 'pskb-keyboard-merge.dll')).Hash -cne 'DE1263A6FD34E98D5DAB8DAC5C9EF1B23FC9134ED89CF306A382C6ED83B159AE') { throw 'Backend or preserved native dependency mismatch.' }
if(@($manifest.sources).Count -eq 0) { throw 'Missing source snapshot identity.' }
Write-Output ('CANDIDATE_ARTIFACT_VERIFIED version='+$manifest.version+' source='+$manifest.sourceCommit+' sony_tested=false')
