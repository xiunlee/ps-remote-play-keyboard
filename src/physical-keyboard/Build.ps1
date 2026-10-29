param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [string]$OutputDirectory = (Join-Path $env:USERPROFILE ('.pskeyboard-tools\physical-keyboard-builds\'+[Guid]::NewGuid().ToString('N'))),
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$compiler = (Resolve-Path -LiteralPath $Zig).Path
if (-not $env:ZIG_GLOBAL_CACHE_DIR) { $env:ZIG_GLOBAL_CACHE_DIR = Join-Path $out 'zig-cache' }
& $compiler cc -target x86_64-windows-gnu -std=c11 -Wall -Wextra -Werror -O2 `
    -shared -DPSKB_MERGE_EXPORTS (Join-Path $repo 'research\native-merge\keyboard_merge.c') `
    -o (Join-Path $out 'pskb-keyboard-merge.dll')
if ($LASTEXITCODE -ne 0) { throw 'Native physical report merge build failed.' }
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$inputSource = Join-Path $repo 'src\keyboard-pad\KeyboardInputState.cs'
$bindingSource = Join-Path $repo 'src\keyboard-pad\KeyboardMapping.cs'
$mergeSource = Join-Path $PSScriptRoot 'PhysicalReportMerge.cs'
$optionsSource = Join-Path $PSScriptRoot 'PhysicalKeyboardOptions.cs'
$statusSource = Join-Path $PSScriptRoot 'OptionalStatusFile.cs'
$relaySource = Join-Path $repo 'research\physical-usb-relay\UsbIpRelay.cs'
$profileSource = Join-Path $PSScriptRoot 'BindingProfile.cs'
$uiTextSource = Join-Path $PSScriptRoot 'UiText.cs'
$repeatSource = Join-Path $PSScriptRoot 'RapidFireEngine.cs'
$pipelineSource = Join-Path $PSScriptRoot 'KeyboardControlPipeline.cs'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll `
    ('/out:'+(Join-Path $out 'PSKeyboard-Physical.exe')) `
    (Join-Path $PSScriptRoot 'PhysicalKeyboard.cs') (Join-Path $PSScriptRoot 'BindingEditor.cs') (Join-Path $PSScriptRoot 'KeyboardUi.cs') `
    $profileSource $uiTextSource $repeatSource $pipelineSource $mergeSource $optionsSource $statusSource $inputSource $bindingSource $relaySource
if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard app build failed.' }
if ($RunTests) {
    $test = Join-Path $out 'PhysicalMergeTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 /r:System.Web.Extensions.dll ('/out:'+$test) `
        (Join-Path $PSScriptRoot 'PhysicalMergeTests.cs') $mergeSource $optionsSource $statusSource $inputSource $bindingSource
    if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard test build failed.' }
    & $test
    if ($LASTEXITCODE -ne 0) { throw 'Physical keyboard merge tests failed.' }
    $profileTest = Join-Path $out 'BindingProfileTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 /r:System.Web.Extensions.dll ('/out:'+$profileTest) `
        (Join-Path $PSScriptRoot 'BindingProfileTests.cs') $profileSource $inputSource $bindingSource
    if ($LASTEXITCODE -ne 0) { throw 'Custom binding tests failed to build.' }
    & $profileTest
    if ($LASTEXITCODE -ne 0) { throw 'Custom binding tests failed.' }
    $repeatTest = Join-Path $out 'RapidFireTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
        /r:System.Web.Extensions.dll ('/out:'+$repeatTest) (Join-Path $PSScriptRoot 'RapidFireTests.cs') $profileSource $repeatSource $inputSource $bindingSource
    if ($LASTEXITCODE -ne 0) { throw 'Repeat tests failed to build.' }
    & $repeatTest
    if ($LASTEXITCODE -ne 0) { throw 'Repeat tests failed.' }
    $pipelineTest = Join-Path $out 'KeyboardControlPipelineTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
        /r:System.Web.Extensions.dll ('/out:'+$pipelineTest) (Join-Path $PSScriptRoot 'KeyboardControlPipelineTests.cs') `
        $profileSource $repeatSource $pipelineSource $inputSource $bindingSource $mergeSource
    if ($LASTEXITCODE -ne 0) { throw 'Keyboard pipeline tests failed to build.' }
    & $pipelineTest
    if ($LASTEXITCODE -ne 0) { throw 'Keyboard pipeline tests failed.' }
    $languageTest = Join-Path $out 'UiTextTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 ('/out:'+$languageTest) `
        (Join-Path $PSScriptRoot 'UiTextTests.cs') $uiTextSource
    if ($LASTEXITCODE -ne 0) { throw 'Language tests failed to build.' }
    & $languageTest
    if ($LASTEXITCODE -ne 0) { throw 'Language tests failed.' }
    $interactionTest = Join-Path $out 'UiInteractionTests.exe'
    & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 /codepage:65001 `
        /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll ('/out:'+$interactionTest) `
        (Join-Path $PSScriptRoot 'UiInteractionTests.cs') (Join-Path $PSScriptRoot 'BindingEditor.cs') `
        (Join-Path $PSScriptRoot 'KeyboardUi.cs') $profileSource $uiTextSource
    if ($LASTEXITCODE -ne 0) { throw 'Binding editor tests failed to build.' }
    & $interactionTest
    if ($LASTEXITCODE -ne 0) { throw 'Binding editor interaction tests failed.' }
    & (Join-Path $repo 'research\keyboard-input-tests\BuildAndTest.ps1') -OutputDirectory (Join-Path $out 'input-tests')
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $out 'README.md') -Force
# The working desktop launcher targets the deployed installation. Do not copy
# it beside an isolated build: that would silently open the older executable.
Write-Output ('Built physical relay preview: '+(Join-Path $out 'PSKeyboard-Physical.exe'))
Write-Output 'No driver installed, no app launched, no device bound or imported.'
