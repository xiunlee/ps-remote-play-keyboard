param(
    [Parameter(Mandatory=$true)][string]$Zig,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$RunTests
)
# Builds and optionally tests synthetic reports only. No keyboard, device, driver,
# transport, candidate application, backend, or Sony process is opened.
$ErrorActionPreference='Stop'
$repo=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$compiler=(Resolve-Path -LiteralPath $Zig).Path
$toolchain=Split-Path -Parent $compiler
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out) { throw 'Native output must be a new directory; old builds are immutable.' }
$expectedCompilerHash='F54B14F02A4D7D8AC62A94A0663486EB166BBA3745D85B466238C1B93E723D46'
if((Get-FileHash -LiteralPath $compiler).Hash -cne $expectedCompilerHash) { throw 'Expected the verified Zig 0.17.0 Windows x86_64 compiler.' }
$utf8=[Text.UTF8Encoding]::new($false)
$sourceNames=@('research/native-merge/keyboard_merge.c','research/native-merge/keyboard_merge.h',
    'research/native-merge/keyboard_merge_test.c','research/native-merge/KeyboardMergeInteropTest.cs',
    'research/dualsense-preview/Build-Native.ps1')
$sourceHashes=@($sourceNames|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash}})
New-Item -ItemType Directory -Path $out | Out-Null
$environment=@{
    ZIG_GLOBAL_CACHE_DIR=(Join-Path $out 'zig-global-cache'); ZIG_LOCAL_CACHE_DIR=(Join-Path $out 'zig-local-cache');
    ZIG_LIB_DIR=(Join-Path $toolchain 'lib'); TEMP=(Join-Path $out 'tmp'); TMP=(Join-Path $out 'tmp');
    CPATH=''; C_INCLUDE_PATH=''; CPLUS_INCLUDE_PATH=''; LIBRARY_PATH=''; COMPILER_PATH=''; SDKROOT=''
}
$prior=@{}
try {
    foreach($name in $environment.Keys) {
        $prior[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
        [Environment]::SetEnvironmentVariable($name,$environment[$name],'Process')
    }
    foreach($name in @('ZIG_GLOBAL_CACHE_DIR','ZIG_LOCAL_CACHE_DIR','TEMP')) { New-Item -ItemType Directory -Path $environment[$name] | Out-Null }
    $compilerVersion=(& $compiler version | Out-String).Trim()
    if($LASTEXITCODE -ne 0 -or $compilerVersion -cne '0.17.0') { throw 'Unexpected Zig compiler version.' }
    $sourceRoot=Join-Path $out 'source'
    New-Item -ItemType Directory -Path $sourceRoot | Out-Null
    foreach($name in $sourceNames | Where-Object { $_ -like 'research/native-merge/*' }) {
        Copy-Item -LiteralPath (Join-Path $repo $name) -Destination (Join-Path $sourceRoot (Split-Path -Leaf $name))
    }
    $common=@('cc','-target','x86_64-windows-gnu','-std=c11','-Wall','-Wextra','-Werror','-O2')
    $library=Join-Path $out 'pskb-keyboard-merge.dll'
    Push-Location -LiteralPath $sourceRoot
    try {
        & $compiler @common -shared -DPSKB_MERGE_EXPORTS keyboard_merge.c -o $library
        if($LASTEXITCODE -ne 0) { throw 'Native keyboard mapping DLL compilation failed.' }
        $testResults=@()
        if($RunTests) {
            $testExe=Join-Path $out 'keyboard-merge-test.exe'
            & $compiler @common keyboard_merge.c keyboard_merge_test.c -o $testExe
            if($LASTEXITCODE -ne 0) { throw 'Native synthetic-report tests did not compile.' }
            $lines=@(& $testExe); $lines|Write-Output
            if($LASTEXITCODE -ne 0) { throw 'Native synthetic-report tests failed.' }
            $result=@($lines|Where-Object{$_ -cmatch '\AKEYBOARD_MERGE_TEST checks=([0-9]+) failures=0;'})
            if($result.Count -ne 1 -or $result[0] -cnotmatch '\AKEYBOARD_MERGE_TEST checks=([0-9]+) failures=0;' -or [int]$Matches[1] -le 0) { throw 'Missing native test result.' }
            $checks=[int]$Matches[1]
            $testResults+=@([ordered]@{kind='native-synthetic-reports';passed=$true;checks=$checks;output=$lines})
            $csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
            $interop=Join-Path $out 'keyboard-merge-interop-test.exe'
            & $csc /nologo /target:exe /platform:x64 /optimize+ /warnaserror /langversion:5 ('/out:'+$interop) KeyboardMergeInteropTest.cs
            if($LASTEXITCODE -ne 0) { throw 'Native C# ABI tests did not compile.' }
            $lines=@(& $interop); $lines|Write-Output
            if($LASTEXITCODE -ne 0 -or @($lines|Where-Object{$_ -clike 'KEYBOARD_MERGE_INTEROP_PASS *'}).Count -ne 1) { throw 'Native C# ABI tests failed.' }
            $testResults+=@([ordered]@{kind='native-csharp-interop';passed=$true;output=$lines})
        }
    } finally { Pop-Location }
    $notices=Join-Path $out 'notices'
    New-Item -ItemType Directory -Path $notices | Out-Null
    $noticeSpecs=@(
        @{component='Zig toolchain';source='LICENSE';name='Native-Zig-LICENSE.txt';scope='Toolchain notice; not a claim that the compiler executable is bundled.'},
        @{component='MinGW-w64 runtime';source='lib/libc/mingw/COPYING';name='Native-MinGW-COPYING.txt';scope='Default ZPL-2.1 and the exceptions stated in the original notice; individual files retain their own licenses.'},
        @{component='LLVM toolchain notices';source='lib/libcxx/LICENSE.TXT';name='Native-LLVM-LICENSE.txt';scope='Conservative toolchain notice including LLVM exceptions and legacy notices; not a claim that libc++ is linked.'}
    )
    $materials=@()
    foreach($spec in $noticeSpecs) {
        $original=Join-Path $toolchain $spec.source
        $relative='notices/'+$spec.name
        Copy-Item -LiteralPath $original -Destination (Join-Path $out $relative)
        $hash=(Get-FileHash -LiteralPath $original).Hash
        if((Get-FileHash -LiteralPath (Join-Path $out $relative)).Hash -cne $hash) { throw 'Toolchain notice copy changed.' }
        $materials+=@([ordered]@{component=$spec.component;toolchainVersion=$compilerVersion;source=$spec.source;sourceSha256=$hash;license=$relative;licenseSha256=$hash;scope=$spec.scope})
    }
    $pseudoReloc=Join-Path $toolchain 'lib/libc/mingw/crt/pseudo-reloc.c'
    $pseudoSource=[IO.File]::ReadAllText($pseudoReloc)
    if($pseudoSource -cnotmatch '\A(/\* pseudo-reloc\.c[\s\S]*?\*/)') { throw 'Missing MinGW pseudo-relocation notice.' }
    $pseudoNotice=$Matches[1]+"`n"
    $relative='notices/Native-MinGW-pseudo-reloc-NOTICE.txt'
    [IO.File]::WriteAllText((Join-Path $out $relative),$pseudoNotice,$utf8)
    $materials+=@([ordered]@{component='MinGW-w64 pseudo-relocation runtime';toolchainVersion=$compilerVersion;
        source='lib/libc/mingw/crt/pseudo-reloc.c';sourceSha256=(Get-FileHash -LiteralPath $pseudoReloc).Hash;
        license=$relative;licenseSha256=(Get-FileHash -LiteralPath (Join-Path $out $relative)).Hash;
        scope='Unmodified leading public-domain notice from the runtime source.'})
    [IO.File]::WriteAllText((Join-Path $out 'NATIVE-MATERIALS.json'),($materials|ConvertTo-Json -Depth 5),$utf8)
    foreach($source in $sourceHashes) {
        if((Get-FileHash -LiteralPath (Join-Path $repo $source.path)).Hash -cne $source.sha256) { throw ('Native source changed during build: '+$source.path) }
    }
    if((Get-FileHash -LiteralPath $compiler).Hash -cne $expectedCompilerHash) { throw 'Compiler changed during build.' }
    $record=[ordered]@{
        schemaVersion=1;artifact='pskb-keyboard-merge.dll';target='x86_64-windows-gnu';
        compiler='Zig';compilerVersion=$compilerVersion;compilerExecutableSha256=$expectedCompilerHash;
        compilerArchiveUrl='https://ziglang.org/download/0.17.0/zig-x86_64-windows-0.17.0.zip';
        compilerArchiveExpectedSha256='B5663F69581DCF391293FBF16C06CB80D81D806545CE618B4D0BAB7F0EB8C428';
        compileArguments=@($common)+@('-shared','-DPSKB_MERGE_EXPORTS','keyboard_merge.c','-o','pskb-keyboard-merge.dll');
        sources=$sourceHashes;executableSha256=(Get-FileHash -LiteralPath $library).Hash;
        materialsManifestSha256=(Get-FileHash -LiteralPath (Join-Path $out 'NATIVE-MATERIALS.json')).Hash;
        testsRun=[bool]$RunTests;testResults=$testResults;cacheDirectoriesIsolated=$true;
        syntheticReportsOnly=$true;keyboardCaptured=$false;devicesOpened=$false;sonyTested=$false
    }
    [IO.File]::WriteAllText((Join-Path $out 'NATIVE-BUILD.json'),($record|ConvertTo-Json -Depth 8),$utf8)
    Write-Output ('NATIVE_ARTIFACT_BUILT sha256='+$record.executableSha256+' tests='+[bool]$RunTests)
} finally {
    foreach($name in $prior.Keys) { [Environment]::SetEnvironmentVariable($name,$prior[$name],'Process') }
}
