param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# This runner compiles and executes only ephemeral loopback fake-server tests.
# It cannot start the real candidate server, keyboard frontend, or USBip driver.
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) {
    throw 'OutputDirectory must be new; previous test artifacts are never overwritten.'
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'The x64 .NET Framework C# compiler is required.' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$exe = Join-Path $outputRoot 'CandidateTransportTests.exe'
$result = Join-Path $outputRoot 'transport-tests.json'
$sources = @(
    (Join-Path $PSScriptRoot 'CandidateTransport.cs'),
    (Join-Path $PSScriptRoot 'CandidateTransportTests.cs'),
    (Join-Path $repoRoot 'research\dualsense-v5-test\V5Protocol.cs')
)
& $compiler /nologo /warnaserror /langversion:5 /optimize+ /platform:x64 /r:System.Web.Extensions.dll "/out:$exe" @sources
if ($LASTEXITCODE -ne 0) { throw "Candidate transport test compilation failed ($LASTEXITCODE)." }
& $exe $result | Tee-Object -FilePath (Join-Path $outputRoot 'transport-tests.log')
$testExit = $LASTEXITCODE
if ($testExit -ne 0) { throw "Candidate transport fake-server tests failed ($testExit)." }
$receipt = Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
if (-not $receipt.success -or $receipt.failures -ne 0 -or -not $receipt.fake_loopback_only -or $receipt.driver_operations -or $receipt.sony_tested) {
    throw 'Candidate transport test result is incomplete or failed.'
}
Write-Output "Candidate transport tests passed: $($receipt.checks) checks. Result: $result"
