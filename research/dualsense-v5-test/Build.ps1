param(
    [Parameter(Mandatory=$true)][string]$MappingDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$native = Join-Path (Resolve-Path -LiteralPath $MappingDirectory).Path 'pskb-keyboard-merge.dll'
if (-not (Test-Path -LiteralPath $native -PathType Leaf)) { throw 'MappingDirectory must contain the existing native mapping DLL.' }
$nativeHash = (Get-FileHash -LiteralPath $native -Algorithm SHA256).Hash
$baseline = Get-Content -Raw -LiteralPath (Join-Path $repo 'packaging/build-records/0.1.0-alpha.8.json') | ConvertFrom-Json
$expectedNative = @($baseline.outputs | Where-Object { $_.path -eq 'pskb-keyboard-merge.dll' })
if ($expectedNative.Count -ne 1 -or $nativeHash -ne $expectedNative[0].sha256) { throw 'Research requires the preserved alpha.8 native mapping DLL.' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $out) { throw 'Choose a new output directory; research builds never overwrite old outputs.' }
New-Item -ItemType Directory -Path $out | Out-Null
$sources = @('research/dualsense-v5-test/Build.ps1','research/dualsense-v5-test/V5Protocol.cs',
    'research/dualsense-v5-test/V5ProtocolTests.cs','research/dualsense-v5-test/V5UsbReadbackTest.cs',
    'src/keyboard-pad/KeyboardMapping.cs','src/keyboard-pad/ViiperTransport.cs')
$snapshot = Join-Path $out 'source-snapshot'
New-Item -ItemType Directory -Path $snapshot | Out-Null
$sourceRecords = @($sources | ForEach-Object {
    $source = Join-Path $repo $_
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $copy = Join-Path $snapshot ([IO.Path]::GetFileName($_))
    Copy-Item -LiteralPath $source -Destination $copy
    if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $hash) { throw 'Source changed while taking build snapshot.' }
    [ordered]@{path=$_;sha256=$hash}
})
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
    ('/out:' + (Join-Path $out 'V5ProtocolTests.exe')) `
    (Join-Path $snapshot 'V5Protocol.cs') (Join-Path $snapshot 'V5ProtocolTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'V5 codec test build failed.' }
& $csc /nologo /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 /r:System.Web.Extensions.dll `
    ('/out:' + (Join-Path $out 'V5UsbReadbackTest.exe')) `
    (Join-Path $snapshot 'V5Protocol.cs') (Join-Path $snapshot 'V5UsbReadbackTest.cs') `
    (Join-Path $snapshot 'KeyboardMapping.cs') (Join-Path $snapshot 'ViiperTransport.cs')
if ($LASTEXITCODE -ne 0) { throw 'V5 userspace readback build failed.' }
Copy-Item -LiteralPath $native -Destination (Join-Path $out 'pskb-keyboard-merge.dll')
if ((Get-FileHash -LiteralPath (Join-Path $out 'pskb-keyboard-merge.dll') -Algorithm SHA256).Hash -ne $nativeHash) { throw 'Native DLL changed while copying.' }
if ($RunTests) {
    & (Join-Path $out 'V5ProtocolTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'V5 codec tests failed.' }
}
foreach ($source in $sourceRecords) {
    if ((Get-FileHash -LiteralPath (Join-Path $repo $source.path) -Algorithm SHA256).Hash -ne $source.sha256) {
        throw 'Working sources changed during build; outputs remain for inspection but no successful manifest will be written.'
    }
}
$manifest = [ordered]@{
    schemaVersion=1; researchVersion='dualsense-v5-research.1';
    upstreamCommit='b78e31e93e84b4cb7c4b4c15b8aace10749106d2';
    nativeSha256=$nativeHash; codecTestsRun=[bool]$RunTests;
    windowsAttached=$false; sonyTested=$false;
    sources=$sourceRecords;
    outputs=@(Get-ChildItem -LiteralPath $out -File | ForEach-Object { [ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $out 'BUILD.json') -Encoding UTF8
Write-Output ('Research tools built: ' + $out)
