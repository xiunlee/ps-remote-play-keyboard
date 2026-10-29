param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)]
    [ValidateSet('dualsensecombinedaudioduplexv5','dualsensegamepadv5','dualsenseedgecombinedaudioduplexv5','dualsenseedgegamepadv5')]
    [string]$Persona,
    [switch]$IsolatedDriverTestOnly
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Environment checks prevent accidental local execution, not malicious spoofing.
# This entry point must only be invoked by the repository's disposable hosted job.
function Assert-HostedGuest {
    if (-not $IsolatedDriverTestOnly) { throw 'The explicit -IsolatedDriverTestOnly switch is required.' }
    foreach ($entry in @{
        GITHUB_ACTIONS='true'; RUNNER_ENVIRONMENT='github-hosted';
        GITHUB_REPOSITORY='xiunlee/ps-remote-play-keyboard'; RUNNER_OS='Windows'
    }.GetEnumerator()) {
        if (-not [string]::Equals([Environment]::GetEnvironmentVariable($entry.Key), $entry.Value, [StringComparison]::Ordinal)) {
            throw ('Hosted guest guard rejected ' + $entry.Key)
        }
    }
    if ($PSVersionTable.PSVersion.Major -lt 7 -or -not [Environment]::Is64BitProcess) {
        throw 'This fixture requires x64 PowerShell 7 on the hosted Windows runner.'
    }
    $protected = @(Get-Process | Where-Object { $_.ProcessName -match '(?i)(RemotePlay|PSKeyboard|viiper|KeyboardPad|PhysicalKeyboard|上班不要玩)' })
    if ($protected.Count -ne 0) { throw 'A protected Remote Play/controller process is already present; refusing the driver experiment.' }
}
Assert-HostedGuest

$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$out = [IO.Path]::GetFullPath($OutputDirectory)
$allowedRoots = @((Join-Path $repo '.local'), $env:RUNNER_TEMP) | Where-Object { $_ }
if (@($allowedRoots | Where-Object { $out.StartsWith([IO.Path]::GetFullPath($_).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) {
    throw 'Output must be a new child directory of repository .local or RUNNER_TEMP.'
}
if (Test-Path -LiteralPath $out) { throw 'Choose a new output directory; previous research outputs are immutable.' }
if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The disposable hosted job must already have administrative rights; no elevation is requested.'
}
New-Item -ItemType Directory -Path $out | Out-Null
$logs = Join-Path $out 'logs'
$downloads = Join-Path $out 'downloads'
$testRoot = Join-Path $out 'test'
New-Item -ItemType Directory -Path $logs,$downloads,$testRoot | Out-Null
$encoding = [Text.UTF8Encoding]::new($false)

function Write-JsonFile([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 12), $encoding)
}
function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Expected) {
        throw ('Pinned file hash mismatch: ' + [IO.Path]::GetFileName($Path))
    }
}
function Save-Download([string]$Uri, [string]$Name, [string]$Hash) {
    $destination = Join-Path $downloads $Name
    if (Test-Path -LiteralPath $destination) { throw 'Download destination already exists.' }
    Invoke-WebRequest -Uri $Uri -OutFile $destination -TimeoutSec 120
    Assert-Hash $destination $Hash
    return $destination
}
function Process-Snapshot {
    @(Get-Process | Sort-Object Id | ForEach-Object { [ordered]@{name=$_.ProcessName;pid=$_.Id} })
}
function Research-Listeners {
    @(Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in @(33541,33542) } |
        Select-Object LocalAddress,LocalPort,OwningProcess)
}
function Start-Owned([string]$File, [string[]]$Arguments, [string]$Name, [string]$WorkingDirectory) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $File
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    if (-not $process.Start()) { throw ('Could not start ' + $Name) }
    $record = [ordered]@{name=$Name;pid=$process.Id;file=$File;arguments=$Arguments;startedUtc=$process.StartTime.ToUniversalTime().ToString('o')}
    [IO.File]::AppendAllText((Join-Path $logs 'owned-processes.jsonl'), (($record | ConvertTo-Json -Compress) + "`n"), $encoding)
    return [pscustomobject]@{
        Process=$process; Name=$Name; Stdout=$process.StandardOutput.ReadToEndAsync(); Stderr=$process.StandardError.ReadToEndAsync();
        Captured=$false; ExitCode=$null; Forced=$false; Output=''; ErrorOutput=''
    }
}
function Capture-Owned($Child) {
    if ($Child.Captured) { return }
    if (-not $Child.Process.HasExited) { throw ('Cannot capture a running child: ' + $Child.Name) }
    if (-not $Child.Stdout.Wait(3000) -or -not $Child.Stderr.Wait(3000)) { throw ('Child output pipe did not close: ' + $Child.Name) }
    $Child.Output = $Child.Stdout.Result
    $Child.ErrorOutput = $Child.Stderr.Result
    $Child.ExitCode = $Child.Process.ExitCode
    [IO.File]::WriteAllText((Join-Path $logs ($Child.Name + '.stdout.txt')), $Child.Output, $encoding)
    [IO.File]::WriteAllText((Join-Path $logs ($Child.Name + '.stderr.txt')), $Child.ErrorOutput, $encoding)
    $Child.Captured = $true
}
function Stop-Owned($Child) {
    if ($null -eq $Child) { return }
    if (-not $Child.Process.HasExited) {
        $Child.Forced = $true
        # .NET kills only the process object we created and its descendants.
        $Child.Process.Kill($true)
        if (-not $Child.Process.WaitForExit(5000)) { throw ('Owned child did not terminate: ' + $Child.Name) }
    }
    Capture-Owned $Child
}
function Invoke-Owned([string]$File, [string[]]$Arguments, [string]$Name, [int]$Seconds=120, [int[]]$AllowedExitCodes=@(0), [switch]$RecordOnly) {
    $child = Start-Owned $File $Arguments $Name $testRoot
    try {
        $child.Process.StandardInput.Close()
        if (-not $child.Process.WaitForExit($Seconds * 1000)) { throw ($Name + ' exceeded its process deadline.') }
        Capture-Owned $child
        if (-not $RecordOnly -and $child.ExitCode -notin $AllowedExitCodes) {
            Write-Output $child.Output
            Write-Output $child.ErrorOutput
            throw ($Name + ' failed with exit code ' + $child.ExitCode)
        }
        return $child
    } finally { if (-not $child.Captured) { Stop-Owned $child } }
}
function Assert-DriverResult($Result) {
    $requiredTrue=@('success','guard_passed','lab_marker_verified','usbip_cli_0981_verified',
        'attach_command_succeeded','owned_port_identity_verified','windows_attached',
        'windows_hid_opened','windows_hid_capability_verified','windows_input_readback_passed',
        'neutral_cleanup_sent','owned_port_detached','owned_bus_removed','owned_hid_disappeared','cleanup_complete')
    $requiredFalse=@('watchdog_expired','usbip_child_exit_unconfirmed','sony_tested','keyboard_captured','foreground_gate_tested','audio_streaming_tested','feature_report_tested')
    foreach ($name in $requiredTrue+$requiredFalse) {
        $property=$Result.PSObject.Properties[$name]
        if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne ($name -in $requiredTrue)) {
            throw ('Driver receipt failed required boolean: ' + $name)
        }
    }
    foreach ($name in @('actions_pressed_verified','actions_released_verified')) {
        $property=$Result.PSObject.Properties[$name]
        if ($null -eq $property -or ($property.Value -isnot [long] -and $property.Value -isnot [int]) -or $property.Value -ne 22) {
            throw ('Driver receipt did not verify all 22 actions: ' + $name)
        }
    }
    if ($null -eq $Result.PSObject.Properties['errors'] -or @($Result.errors).Count -ne 0 -or
        $null -eq $Result.PSObject.Properties['source_commit_marker'] -or
        $Result.source_commit_marker -cne 'b78e31e93e84b4cb7c4b4c15b8aace10749106d2' -or
        $null -eq $Result.PSObject.Properties['persona'] -or $Result.persona -cne $Persona) {
        throw 'Driver receipt has errors or does not identify the selected fixed-source persona.'
    }
}
function Assert-MicrosoftSignature([string]$Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(?i)(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw ('A running driver does not have a valid Microsoft signature: ' + [IO.Path]::GetFileName($Path))
    }
    return [ordered]@{status=[string]$signature.Status;subject=$signature.SignerCertificate.Subject;thumbprint=$signature.SignerCertificate.Thumbprint}
}
function Driver-Records {
    $expected = @{
        usbip2_filter='B93A5FC3C23D56F9B49634742374E32F269E53F764CAA2D5FCB43E3259F7ECED'
        usbip2_ude='ABD4FC43DCE40E027E4ACC884CBFE92433E026206F6565C5EE5284A39441169A'
    }
    $records = @()
    foreach ($name in $expected.Keys) {
        $drivers = @(Get-CimInstance Win32_SystemDriver -Filter ("Name='" + $name + "'"))
        if ($drivers.Count -ne 1 -or $drivers[0].State -ne 'Running') { throw ('Pinned driver is not running: ' + $name) }
        $driverPath = [Environment]::ExpandEnvironmentVariables(([string]$drivers[0].PathName).Trim('"'))
        if ($driverPath.StartsWith('\??\')) { $driverPath=$driverPath.Substring(4) }
        if ($driverPath.StartsWith('\SystemRoot\', [StringComparison]::OrdinalIgnoreCase)) { $driverPath=Join-Path $env:WINDIR $driverPath.Substring(12) }
        if (-not [IO.Path]::IsPathFullyQualified($driverPath)) { $driverPath=Join-Path $env:WINDIR $driverPath }
        $driverPath=[IO.Path]::GetFullPath($driverPath)
        if ([IO.Path]::GetFileName($driverPath) -ne ($name + '.sys')) { throw 'Unexpected running driver image name.' }
        Assert-Hash $driverPath $expected[$name]
        $signature=Assert-MicrosoftSignature $driverPath
        $records += [ordered]@{name=$name;state=$drivers[0].State;path=$driverPath;sha256=$expected[$name];signature=$signature}
    }
    return $records
}

$receipt = [ordered]@{
    schemaVersion=1; researchVersion='dualsense-driver-research.1';
    sourceCommit='b78e31e93e84b4cb7c4b4c15b8aace10749106d2';
    repository=$env:GITHUB_REPOSITORY; workflowCommit=$env:GITHUB_SHA; runId=$env:GITHUB_RUN_ID;
    hostedRunner=$true; persona=$Persona; sonyTested=$false; productVersionChanged=$false;
    status='incomplete'; installerExitCode=$null; installerRequestsReboot=$null;
    nativeTestsPassed=$false; loadedDrivers=@(); testExitCode=$null; driverResult=$null; driverResultValidated=$false;
    initialImportedPortsEmpty=$false; cleanupImportedPortsEmpty=$false;
    labExitCode=$null; labForcedTermination=$false; testForcedTermination=$false;
    cleanupErrors=@(); failure=$null
}
$lab=$null; $test=$null; $usbip=$null
try {
    Write-JsonFile (Join-Path $logs 'initial-processes.json') (Process-Snapshot)
    $initialListeners=@(Research-Listeners)
    Write-JsonFile (Join-Path $logs 'initial-listeners.json') $initialListeners
    if ($initialListeners.Count -ne 0) { throw 'Fixture ports are already occupied.' }
    if (@(Get-CimInstance Win32_SystemDriver -Filter "Name='usbip2_filter' OR Name='usbip2_ude'").Count -ne 0 -or
        (Test-Path -LiteralPath 'C:\Program Files\USBip')) {
        throw 'USBip already exists on this guest; refusing to replace an existing installation.'
    }

    $installer=Save-Download 'https://github.com/vadimgrn/usbip-win2/releases/download/v.0.9.8.1/USBip-0.9.8.1-x64.exe' 'USBip-0.9.8.1-x64.exe' '38CAD6D4432B52D5BB9409D9AD03B72FDFFC4ADA4CD3A48FBECA1A2752A8518A'
    $archive=Save-Download 'https://codeload.github.com/hbashton/VIIPER/zip/b78e31e93e84b4cb7c4b4c15b8aace10749106d2' 'candidate.zip' '8305750E881E7E955DCF26DC5FEDC2B43D55902C9F1FA5B4ECA31FE51901B2A5'
    $goArchive=Save-Download 'https://go.dev/dl/go1.27.3.windows-amd64.zip' 'go1.27.3.windows-amd64.zip' 'F26081F438319757F8A1B46D7D0B2C5EC7583D4038E485E0AA725CFDE7885FFF'
    $goRoot=Join-Path $out 'go-toolchain'
    Expand-Archive -LiteralPath $goArchive -DestinationPath $goRoot
    $labRoot=Join-Path $out 'lab'
    & (Join-Path $PSScriptRoot 'labserver/Build-LabServer.ps1') -SourceArchive $archive -GoExecutable (Join-Path $goRoot 'go/bin/go.exe') -OutputDirectory $labRoot

    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $vsResult=Invoke-Owned $vswhere @('-latest','-products','*','-requires','Microsoft.VisualStudio.Component.VC.Tools.x86.x64','-property','installationPath') 'vswhere' 30
    $vsPath=$vsResult.Output.Trim()
    if (-not $vsPath -or -not (Test-Path -LiteralPath $vsPath -PathType Container)) { throw 'Hosted MSVC installation was not found.' }
    Import-Module (Join-Path $vsPath 'Common7/Tools/Microsoft.VisualStudio.DevShell.dll')
    Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null
    $cl=(Get-Command cl.exe -CommandType Application).Source
    $nativeSource=Join-Path $repo 'research/native-merge/keyboard_merge.c'
    $nativeTestSource=Join-Path $repo 'research/native-merge/keyboard_merge_test.c'
    $nativeDll=Join-Path $testRoot 'pskb-keyboard-merge.dll'
    $null=Invoke-Owned $cl @('/nologo','/LD','/O2','/W4','/WX','/DPSKB_MERGE_EXPORTS',('/Fo'+(Join-Path $testRoot 'keyboard_merge_dll.obj')),('/Fe'+$nativeDll),$nativeSource) 'build-native-dll'
    $nativeTest=Join-Path $testRoot 'KeyboardMergeNativeTest.exe'
    $null=Invoke-Owned $cl @('/nologo','/O2','/W4','/WX',('/Fe'+$nativeTest),$nativeSource,$nativeTestSource) 'build-native-tests'
    $csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    $interopTest=Join-Path $testRoot 'KeyboardMergeInteropTest.exe'
    $common=@('/nologo','/platform:x64','/optimize+','/warnaserror','/langversion:5','/codepage:65001')
    $null=Invoke-Owned $csc ($common+@('/target:exe',('/out:'+$interopTest),(Join-Path $repo 'research/native-merge/KeyboardMergeInteropTest.cs'))) 'build-interop'
    $testExe=Join-Path $testRoot 'WindowsDualSenseDriverTest.exe'
    $testSources=@('research/dualsense-driver-test/WindowsDualSenseDriverTest.cs','src/keyboard-pad/KeyboardMapping.cs','src/keyboard-pad/ViiperTransport.cs','research/dualsense-v5-test/V5Protocol.cs')
    $null=Invoke-Owned $csc ($common+@('/target:exe','/r:System.Web.Extensions.dll',('/out:'+$testExe))+@($testSources | ForEach-Object { Join-Path $repo $_ })) 'build-driver-test'
    $nativeResult=Invoke-Owned -File $nativeTest -Arguments @() -Name 'native-tests' -Seconds 30
    $interopResult=Invoke-Owned -File $interopTest -Arguments @() -Name 'interop-tests' -Seconds 30
    Write-Output $nativeResult.Output
    Write-Output $interopResult.Output
    $receipt.nativeTestsPassed=$true
    Write-JsonFile (Join-Path $out 'test-build.json') ([ordered]@{
        sources=@(($testSources+@('research/native-merge/keyboard_merge.c','research/native-merge/keyboard_merge.h','research/native-merge/keyboard_merge_test.c','research/native-merge/KeyboardMergeInteropTest.cs','research/dualsense-driver-test/Run-HostedDriverTest.ps1')) | ForEach-Object { [ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $repo $_)).Hash} });
        outputs=@($testExe,$nativeDll,$nativeTest,$interopTest | ForEach-Object { [ordered]@{name=[IO.Path]::GetFileName($_);sha256=(Get-FileHash -LiteralPath $_).Hash} });
        nativeCompiler=$cl; nativeTestsPassed=$true; sonyTested=$false
    })

    Assert-HostedGuest
    $installLog=Join-Path $logs 'installer.log'
    $installArgs=@('/SP-','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/RESTARTEXITCODE=3010','/NOCLOSEAPPLICATIONS','/NOFORCECLOSEAPPLICATIONS','/NORESTARTAPPLICATIONS','/COMPONENTS=main,client','/TASKS=vcredist',('/LOG='+$installLog))
    $install=Invoke-Owned -File $installer -Arguments $installArgs -Name 'installer' -Seconds 120 -RecordOnly
    $receipt.installerExitCode=$install.ExitCode
    $receipt.installerRequestsReboot=($install.ExitCode -eq 3010)
    if ($install.ExitCode -notin @(0,3010)) { throw ('Installer failed with exit code ' + $install.ExitCode) }
    $installedUsbip='C:\Program Files\USBip\usbip.exe'
    Assert-Hash $installedUsbip '817DA219FA153B4B37C3BC1AA8FCC63F33E046188C57CC3DD2C60A6EF0883C56'
    Assert-Hash 'C:\Program Files\USBip\libusbip.dll' 'F123EF5D288B817A890CBE39A3C2D212B3D8B1765C691C8703CCB76C7337986C'
    $usbip=$installedUsbip
    $version=Invoke-Owned $usbip @('--version') 'usbip-version' 15
    if ($version.Output -notmatch '(?<![0-9.])0\.9\.8\.1(?![0-9.])') { throw 'Installed CLI did not identify as 0.9.8.1.' }
    $driverDeadline=[DateTime]::UtcNow.AddSeconds(30)
    do {
        try { $receipt.loadedDrivers=@(Driver-Records); break }
        catch { if ([DateTime]::UtcNow -ge $driverDeadline) { throw }; Start-Sleep -Milliseconds 500 }
    } while ($true)
    $initialPorts=Invoke-Owned $usbip @('port') 'initial-usbip-ports' 15
    Write-Output $initialPorts.Output
    if ($initialPorts.Output -match '(?m)^Port\s+\d+:') { throw 'The new guest has an unexpected existing imported USBip port.' }
    $receipt.initialImportedPortsEmpty=$true
    Assert-HostedGuest

    $labExe=Join-Path $labRoot 'DualSenseV5DriverLabServer.exe'
    $lab=Start-Owned $labExe @('--isolated-driver-test-only') 'lab' $labRoot
    $listenerDeadline=[DateTime]::UtcNow.AddSeconds(10)
    do {
        if ($lab.Process.HasExited) { Capture-Owned $lab; throw 'Driver fixture exited before readiness.' }
        $listeners=@(Research-Listeners)
        if ($listeners.Count -eq 2 -and @($listeners | Where-Object { $_.LocalAddress -ne '127.0.0.1' -or $_.OwningProcess -ne $lab.Process.Id }).Count -eq 0) { break }
        if ($listeners.Count -ne 0 -and @($listeners | Where-Object { $_.OwningProcess -ne $lab.Process.Id }).Count -ne 0) { throw 'Another process acquired a fixture port.' }
        if ([DateTime]::UtcNow -ge $listenerDeadline) { throw 'Driver fixture listeners did not become ready.' }
        Start-Sleep -Milliseconds 100
    } while ($true)
    Write-JsonFile (Join-Path $logs 'fixture-listeners.json') $listeners
    $test=Start-Owned $testExe @('--isolated-driver-test-only','--persona',$Persona) 'driver-test' $testRoot
    $test.Process.StandardInput.Close()
    if (-not $test.Process.WaitForExit(120000)) { throw 'Windows driver test exceeded its hard process deadline.' }
    Capture-Owned $test
    $receipt.testExitCode=$test.ExitCode
} catch {
    $receipt.failure=$_.Exception.Message
} finally {
    try { Stop-Owned $test } catch { $receipt.cleanupErrors += ('Test cleanup: ' + $_.Exception.Message) }
    if ($null -ne $test) {
        $receipt.testForcedTermination=$test.Forced
        $receipt.testExitCode=$test.ExitCode
        if ($test.Captured) {
            Write-Output $test.Output
            Write-Output $test.ErrorOutput
            try {
                $jsonLines=@($test.Output -split '\r?\n' | Where-Object { $_.StartsWith('RESULT_JSON ') })
                if ($jsonLines.Count -ne 1) { throw 'Expected one driver RESULT_JSON line.' }
                $receipt.driverResult=$jsonLines[0].Substring(12) | ConvertFrom-Json
                Write-JsonFile (Join-Path $out 'driver-result.json') $receipt.driverResult
                Assert-DriverResult $receipt.driverResult
                $receipt.driverResultValidated=$true
            } catch { $receipt.cleanupErrors += ('Driver receipt: ' + $_.Exception.Message) }
        }
    }
    try {
        if ($null -ne $lab) {
            if (-not $lab.Process.HasExited) {
                try {
                    $lab.Process.StandardInput.WriteLine('stop')
                    $lab.Process.StandardInput.Flush()
                    $lab.Process.StandardInput.Close()
                    $null=$lab.Process.WaitForExit(5000)
                } catch { $receipt.cleanupErrors += ('Graceful lab stop: ' + $_.Exception.Message) }
            }
            Stop-Owned $lab
            $receipt.labExitCode=$lab.ExitCode
            $receipt.labForcedTermination=$lab.Forced
        }
    } catch { $receipt.cleanupErrors += ('Lab cleanup: ' + $_.Exception.Message) }
    try {
        if ($null -ne $usbip -and (Test-Path -LiteralPath $usbip -PathType Leaf)) {
            $finalPorts=Invoke-Owned $usbip @('port') 'cleanup-usbip-ports' 15
            Write-Output $finalPorts.Output
            if ($finalPorts.Output -match '(?m)^Port\s+\d+:' -or $finalPorts.Output -match '127\.0\.0\.1:33541') {
                throw 'An imported USBip port remained after the driver test; no blanket detach is attempted.'
            }
            $receipt.cleanupImportedPortsEmpty=$true
        }
        Write-JsonFile (Join-Path $logs 'cleanup-processes.json') (Process-Snapshot)
        $remaining=@(Research-Listeners)
        Write-JsonFile (Join-Path $logs 'cleanup-listeners.json') $remaining
        if ($remaining.Count -ne 0) { throw 'Research listeners remained after fixture cleanup.' }
    } catch { $receipt.cleanupErrors += ('Guest inspection: ' + $_.Exception.Message) }

    # C# owns device/port cleanup. Its detailed receipt must also pass; process
    # exit alone cannot establish successful Windows HID operation or cleanup.
    if ($null -eq $receipt.failure -and $receipt.testExitCode -eq 0 -and $receipt.labExitCode -eq 0 -and
        -not $receipt.testForcedTermination -and -not $receipt.labForcedTermination -and $receipt.cleanupErrors.Count -eq 0 -and
        $receipt.driverResultValidated -and $receipt.initialImportedPortsEmpty -and $receipt.cleanupImportedPortsEmpty) {
        $receipt.status='passed'
    }
    Write-JsonFile (Join-Path $out 'RESULT.json') $receipt
    Write-Output ('HOSTED_RESULT_JSON ' + ($receipt | ConvertTo-Json -Depth 12 -Compress))
    if ($env:GITHUB_STEP_SUMMARY) {
        $summary=@(
            '### Isolated DualSense driver research', '',
            ('Persona: `' + $Persona + '`.'),
            ('Result: **' + $receipt.status + '**. Sony Remote Play tested: **false**.'),
            ('Installer exit: ' + $receipt.installerExitCode + '; installer requested reboot: ' + $receipt.installerRequestsReboot + '. No reboot was performed.'),
            ('Native tests passed: ' + $receipt.nativeTestsPassed + '; driver test exit: ' + $receipt.testExitCode + '; cleanup errors: ' + $receipt.cleanupErrors.Count + '.'),
            'The guest used a separate research fixture and the matched USBip 0.9.8.1 package. This does not prove PS5 operation.', ''
        ) -join "`n"
        [IO.File]::AppendAllText($env:GITHUB_STEP_SUMMARY, $summary, $encoding)
    }
}
if ($receipt.status -ne 'passed') { throw 'Hosted driver experiment did not pass; inspect the job log and JSON receipt.' }
