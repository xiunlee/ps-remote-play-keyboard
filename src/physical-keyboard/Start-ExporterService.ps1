# Elevated helper for an already installed, project-owned local exporter.
# Never installs, binds, unbinds, changes firewall rules, or reboots.
$ErrorActionPreference='Stop'
$cache=Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-relay-research'
$resultPath=Join-Path $cache 'exporter-restart-result.json'
$result=[ordered]@{status='starting';error=$null}
try {
    $principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator permission is required.'}
    $exe=Join-Path $env:ProgramFiles 'usbipd-win\usbipd.exe'
    if((Get-AuthenticodeSignature -LiteralPath $exe).Status -ne 'Valid'){throw 'Exporter signature invalid.'}
    $install=Get-Content -LiteralPath (Join-Path $cache 'exporter-install-result.json') -Raw | ConvertFrom-Json
    if($install.status -ne 'installed-local-only-no-device-bound'){throw 'Project installation record is not ready.'}
    $block=Get-NetFirewallRule -PolicyStore ActiveStore -Name $install.blockRule
    if($block.Enabled -ne 'True' -or $block.Action -ne 'Block' -or $block.Direction -ne 'Inbound' -or $block.Profile.ToString() -ne 'Any'){throw 'Exporter local-only rule changed.'}
    if(($block | Get-NetFirewallApplicationFilter).Program -ne $exe){throw 'Exporter firewall program changed.'}
    $ports=$block | Get-NetFirewallPortFilter
    if($ports.Protocol -ne 'TCP' -or $ports.LocalPort -ne '3240'){throw 'Exporter firewall port changed.'}
    $expected=@('0.0.0.0-126.255.255.255','128.0.0.0-255.255.255.255','::2-ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff')
    $actual=@(($block | Get-NetFirewallAddressFilter).RemoteAddress)
    if(@(Compare-Object $expected $actual).Count){throw 'Exporter firewall address scope changed.'}
    $vendor=Get-NetFirewallRule -PolicyStore ActiveStore -Name $install.disabledRule
    if($vendor.Enabled -ne 'False'){throw 'Broad exporter firewall rule is enabled.'}
    if(@(Get-NetFirewallProfile -PolicyStore ActiveStore | Where-Object {$_.Enabled -ne 1}).Count){throw 'Windows firewall profile is disabled.'}
    Start-Service -Name usbipd
    if((Get-Service -Name usbipd).Status -ne 'Running' -or (Get-Service -Name VBoxUSBMon).Status -ne 'Running'){throw 'Exporter services did not start.'}
    $result.status='running'
} catch {$result.status='failed';$result.error=$_.Exception.Message}
$result | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
if($result.status -eq 'failed'){exit 1}
