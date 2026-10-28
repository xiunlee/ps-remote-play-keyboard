param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$ResultFile
)
# Run elevated only after explicit approval for installing the physical exporter.
# This phase never binds a USB device and never reboots Windows.
$ErrorActionPreference='Stop'
$result=[ordered]@{status='starting';installerExitCode=$null;restorePoint=$null;disabledRule=$null;blockRule=$null;service=$null;monitor=$null;error=$null}
$resultPath=[IO.Path]::GetFullPath($ResultFile)
$ownedInstallation=$false
$exporter=Join-Path $env:ProgramFiles 'usbipd-win\usbipd.exe'
$blockName='PSKeyboard-Usbipd-LocalOnly-20261028'
function Save-Result { $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding UTF8 }
try {
    $principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator permission is required.'}
    if((Get-Service -Name usbipd -ErrorAction SilentlyContinue) -or (Test-Path -LiteralPath $exporter) -or (Test-Path -LiteralPath 'HKLM:\SOFTWARE\usbipd-win')){throw 'Existing exporter state found; refusing to replace it.'}
    if(Get-Service -Name VBoxUSBMon,VBoxUSB -ErrorAction SilentlyContinue){throw 'Existing VirtualBox USB driver state found; refusing to replace it.'}
    if(Get-NetFirewallRule -Name $blockName -ErrorAction SilentlyContinue){throw 'Existing application firewall rule found; inspect it before continuing.'}
    $path=(Resolve-Path -LiteralPath $Installer).Path
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne '1C984914AEC944DE19B64EFF232421439629699F8138E3DDC29301175BC6D938'){throw 'Unexpected exporter installer hash.'}
    if((Get-AuthenticodeSignature -LiteralPath $path).Status -ne 'Valid'){throw 'Exporter signature verification failed.'}
    try {
        $point=Invoke-CimMethod -Namespace 'root/default' -ClassName SystemRestore -MethodName CreateRestorePoint -Arguments @{Description='Before PSKeyboard physical USB exporter';RestorePointType=[uint32]12;EventType=[uint32]100} -OperationTimeoutSec 45
        $result.restorePoint='SystemRestore return code: '+$point.ReturnValue
    } catch { $result.restorePoint='Unavailable: '+$_.Exception.Message }
    $result.status='installing';Save-Result
    $log=[IO.Path]::ChangeExtension($resultPath,'.installer.log')
    $ownedInstallation=$true
    $setup=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList @('/i',('"'+$path+'"'),'/qn','/norestart','/L*v',('"'+$log+'"')) -WindowStyle Hidden -Wait -PassThru
    $result.installerExitCode=$setup.ExitCode
    if($setup.ExitCode -ne 0 -and $setup.ExitCode -ne 3010){throw ('Exporter installer failed: '+$setup.ExitCode)}
    Stop-Service -Name usbipd -ErrorAction Stop
    Set-Service -Name usbipd -StartupType Manual
    $result.status='restricting-to-this-computer';Save-Result
    if(-not (Test-Path -LiteralPath $exporter)){throw 'Installed exporter executable missing.'}
    if((Get-AuthenticodeSignature -LiteralPath $exporter).Status -ne 'Valid'){throw 'Installed exporter signature invalid.'}
    $rules=@(Get-NetFirewallRule -DisplayName usbipd -ErrorAction Stop | Where-Object {
        $app=$_ | Get-NetFirewallApplicationFilter
        $app.Program -eq $exporter
    })
    if($rules.Count -ne 1){throw 'Cannot uniquely identify the installer firewall rule.'}
    $result.disabledRule=$rules[0].Name
    $rules[0] | Disable-NetFirewallRule | Out-Null
    $ranges=@('0.0.0.0-126.255.255.255','128.0.0.0-255.255.255.255','::2-ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff')
    New-NetFirewallRule -Name $blockName -DisplayName 'PSKeyboard USB exporter - block non-local access' -Direction Inbound -Action Block -Enabled True -Profile Any -Program $exporter -Protocol TCP -LocalPort 3240 -RemoteAddress $ranges | Out-Null
    $result.blockRule=$blockName
    $profiles=@(Get-NetFirewallProfile -PolicyStore ActiveStore)
    if($profiles.Count -ne 3 -or @($profiles | Where-Object {$_.Enabled -ne 1}).Count -gt 0){throw 'Effective Windows firewall is not enabled for all profiles.'}
    $effective=Get-NetFirewallRule -PolicyStore ActiveStore -Name $blockName -ErrorAction Stop
    if($effective.Enabled -ne 'True' -or $effective.Action -ne 'Block'){throw 'Effective local-only block not confirmed.'}
    $vendor=Get-NetFirewallRule -PolicyStore ActiveStore -Name $rules[0].Name -ErrorAction Stop
    if($vendor.Enabled -ne 'False'){throw 'Broad installer firewall rule still enabled.'}
    if($setup.ExitCode -eq 3010){$result.status='installed-restart-requested';Save-Result;exit 0}
    Start-Service -Name usbipd
    $result.service=(Get-Service -Name usbipd).Status.ToString()
    $result.monitor=(Get-Service -Name VBoxUSBMon).Status.ToString()
    if($result.service -ne 'Running' -or $result.monitor -ne 'Running'){throw 'Exporter service or monitor unavailable; restart may be required.'}
    $result.status='installed-local-only-no-device-bound'
} catch {
    $result.status='failed';$result.error=$_.Exception.Message
    if($ownedInstallation){Stop-Service -Name usbipd -ErrorAction SilentlyContinue}
}
Save-Result
if($result.status -eq 'failed'){exit 1}
