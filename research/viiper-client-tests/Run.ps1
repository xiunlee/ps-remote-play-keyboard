param([string]$OutputDirectory = (Join-Path $env:USERPROFILE '.pskeyboard-tools\viiper-client-tests'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /platform:x64 /optimize+ /warnaserror /langversion:5 /r:System.Web.Extensions.dll ('/out:' + (Join-Path $OutputDirectory 'TransportTests.exe')) (Join-Path $repo 'src\keyboard-pad\ViiperTransport.cs') (Join-Path $PSScriptRoot 'TransportTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'VIIPER transport test compilation failed.' }
& (Join-Path $OutputDirectory 'TransportTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'VIIPER transport tests failed.' }
