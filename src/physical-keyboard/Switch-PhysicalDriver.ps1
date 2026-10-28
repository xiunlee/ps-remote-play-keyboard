param(
    [Parameter(Mandatory=$true)][ValidateSet('Bind','Restore')][string]$Action,
    [Parameter(Mandatory=$true)][string]$BaselineFile,
    [Parameter(Mandatory=$true)][string]$ResultFile,
    [Guid]$ExpectedPersistedGuid=[Guid]::Empty
)
# Exactly one verified physical DS4. No wildcard binding, device removal or reboot.
$ErrorActionPreference='Stop'
$result=[ordered]@{status='starting';action=$Action;busId=$null;persistedGuidBefore=$null;persistedGuidAfter=$null;exitCode=$null;rebootRequested=$false;service=$null;inf=$null;isForced=$null;message=$null;error=$null}
$tool=Join-Path $env:ProgramFiles 'usbipd-win\usbipd.exe'
function Run-Exporter([string]$arguments) {
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$tool;$info.Arguments=$arguments;$info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process;$process.StartInfo=$info
    try {
        if(-not $process.Start()){throw 'Cannot start exporter command.'}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{Code=$process.ExitCode;Out=$stdout.Result;Err=$stderr.Result}
    } finally {$process.Dispose()}
}
try {
    $principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator permission is required.'}
    if((Get-AuthenticodeSignature -LiteralPath $tool).Status -ne 'Valid'){throw 'Exporter signature invalid.'}
    $baseline=Get-Content -LiteralPath $BaselineFile -Raw | ConvertFrom-Json
    $instance=[string]$baseline.InstanceId
    if($instance -notmatch '^USB\\VID_054C&PID_05C4\\[^\\]+$'){throw 'Baseline is not the selected physical DS4.'}
    $expectedParent=($baseline.Properties | Where-Object {$_.KeyName -eq 'DEVPKEY_Device_Parent'}).Data
    $present=@(Get-PnpDevice -PresentOnly | Where-Object {$_.InstanceId -eq $instance})
    if($present.Count -ne 1){throw 'The exact original physical device is not present.'}
    $parent=(Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_Parent).Data
    if(-not $expectedParent -or $parent -ne $expectedParent){throw 'Original physical parent changed.'}
    $state=Run-Exporter 'state'
    if($state.Code -ne 0){throw 'Cannot read exporter state.'}
    $devices=@(($state.Out | ConvertFrom-Json).Devices | Where-Object {$_.InstanceId -eq $instance})
    if($devices.Count -ne 1){throw 'Cannot uniquely match physical exporter device.'}
    $device=$devices[0];$bus=[string]$device.BusId
    $result.persistedGuidBefore=$device.PersistedGuid
    if($bus -notmatch '^\d+-\d+(\.\d+)*$'){throw 'Physical bus id is unavailable or unsupported.'}
    $result.busId=$bus
    if($device.ClientIPAddress){throw 'Detach this exact imported device before changing its physical driver.'}
    if($Action -eq 'Bind') {
        if($device.PersistedGuid -or $device.IsForced){throw 'Physical device already shared or forced; refusing to overwrite ownership.'}
        foreach($serviceName in @('usbipd','VBoxUSBMon')) {
            if((Get-Service -Name $serviceName).Status -ne 'Running'){throw 'Exporter service or monitor is not running.'}
        }
        $originalService=(Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_Service).Data
        $originalInf=(Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_DriverInfPath).Data
        if($originalService -ne 'HidUsb' -or $originalInf -ne 'input.inf'){throw 'Original driver baseline changed.'}
        $operation=Run-Exporter ('bind --busid '+$bus+' --force')
    } else {
        if(-not $device.PersistedGuid){throw 'Exact physical device has no sharing registration to restore.'}
        if($ExpectedPersistedGuid -eq [Guid]::Empty -or [Guid]$device.PersistedGuid -ne $ExpectedPersistedGuid){throw 'Sharing ownership does not match this approved test session.'}
        $operation=Run-Exporter ('unbind --busid '+$bus)
    }
    $result.exitCode=$operation.Code
    $result.message=($operation.Out+$operation.Err).Trim()
    $result.rebootRequested=$result.message -match '(?i)reboot|restart'
    if($operation.Code -ne 0){throw ('Driver operation failed with exit code '+$operation.Code)}
    Start-Sleep -Milliseconds 500
    $after=Run-Exporter 'state'
    if($after.Code -ne 0){throw 'Cannot verify exporter state after driver operation.'}
    $match=@(($after.Out | ConvertFrom-Json).Devices | Where-Object {$_.InstanceId -eq $instance})
    if($match.Count -ne 1){throw 'Physical device missing from post-operation state.'}
    $result.persistedGuidAfter=$match[0].PersistedGuid
    $result.isForced=[bool]$match[0].IsForced
    $result.service=(Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_Service).Data
    $result.inf=(Get-PnpDeviceProperty -InstanceId $instance -KeyName DEVPKEY_Device_DriverInfPath).Data
    if($result.rebootRequested){$result.status='restart-requested'}
    elseif($Action -eq 'Bind') {
        if(-not $result.isForced -or $result.service -ne 'VBoxUSB' -or -not $match[0].PersistedGuid){throw 'Forced physical driver was not confirmed.'}
        $result.status='bound-exact-physical-device'
    } else {
        if($result.isForced -or $match[0].PersistedGuid -or $result.service -ne 'HidUsb' -or $result.inf -ne 'input.inf'){throw 'Original physical HID driver was not restored.'}
        $result.status='original-physical-driver-restored'
    }
} catch {$result.status='failed';$result.error=$_.Exception.Message}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ResultFile -Encoding UTF8
if($result.status -eq 'failed'){exit 1}
