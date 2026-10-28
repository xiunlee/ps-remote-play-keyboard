param([switch]$CheckOnly)
# Desktop entry point. No installation, binding, unbinding, Sony control or autostart.
$ErrorActionPreference = 'Stop'
$cache = Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-relay-research'
$appDir = Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-keyboard-preview'
$app = Join-Path $appDir 'PSKeyboard-Physical.exe'
$statusFile = Join-Path $appDir 'status.json'
$resultFile = Join-Path $cache 'launcher-result.json'
$exporter = Join-Path $env:ProgramFiles 'usbipd-win\usbipd.exe'
$client = Join-Path $env:ProgramFiles 'USBip\usbip.exe'
$serviceHelper = Join-Path $PSScriptRoot 'Start-ExporterService.ps1'
$created = $null
$phase = 'preflight'
$result = [ordered]@{status='starting';errorCode=$null;pid=$null;port=$null}

function Invoke-Tool([string]$file, [string]$arguments) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$file; $info.Arguments=$arguments; $info.UseShellExecute=$false; $info.CreateNoWindow=$true
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    $process = New-Object Diagnostics.Process; $process.StartInfo=$info
    try {
        if(-not $process.Start()){throw 'Cannot start required command.'}
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(10000)){
            # This is only the exact CLI child created immediately above.
            try {$process.Kill(); [void]$process.WaitForExit(2000)} catch {}
            throw 'Required command timed out.'
        }
        if($process.ExitCode -ne 0){throw 'Required command failed.'}
        return $stdout.Result
    } finally {$process.Dispose()}
}
function Read-PhysicalDevice {
    $state = Invoke-Tool $exporter 'state' | ConvertFrom-Json
    $matchedDevices = @($state.Devices | Where-Object {$_.InstanceId -eq $baseline.InstanceId})
    if($matchedDevices.Count -ne 1 -or -not $matchedDevices[0].IsForced -or
        [Guid]$matchedDevices[0].PersistedGuid -ne [Guid]$binding.persistedGuidAfter){throw 'Physical sharing ownership changed.'}
    if([string]$matchedDevices[0].BusId -notmatch '^\d+-\d+(\.\d+)*$'){throw 'Current physical location unavailable.'}
    return $matchedDevices[0]
}
function Get-ImportUrls {
    $listing = Invoke-Tool $client 'port'
    return @([regex]::Matches($listing, 'usbip://[^\s]+') | ForEach-Object {$_.Value})
}
function Show-LauncherMessage([string]$message) {
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show($message,'PS 鍵盤手把')
}

try {
    foreach($required in @($app,$exporter,$client,(Join-Path $cache 'physical-ds4-original-driver.json'),
        (Join-Path $cache 'physical-bind-result.json'))){if(-not (Test-Path -LiteralPath $required -PathType Leaf)){throw 'Required local file missing.'}}
    $existing = @(Get-Process -Name 'PSKeyboard-Physical' -ErrorAction SilentlyContinue | Where-Object {
        try {$_.MainModule.FileName -eq $app -and $_.SessionId -eq (Get-Process -Id $PID).SessionId} catch {$false}
    })
    if($existing.Count -gt 1){throw 'Multiple mapper processes found.'}
    if($existing.Count -eq 1 -and -not $CheckOnly) {
        $result.status='already-open'; $result.pid=$existing[0].Id
        $result | ConvertTo-Json | Set-Content -LiteralPath $resultFile -Encoding UTF8
        Show-LauncherMessage '鍵盤手把程式已經開啟，請從工作列切回程式視窗。'
        exit 0
    }
    foreach($signed in @($exporter,$client)){if((Get-AuthenticodeSignature -LiteralPath $signed).Status -ne 'Valid'){throw 'Installed component signature invalid.'}}
    $baseline = Get-Content -LiteralPath (Join-Path $cache 'physical-ds4-original-driver.json') -Raw | ConvertFrom-Json
    $binding = Get-Content -LiteralPath (Join-Path $cache 'physical-bind-result.json') -Raw | ConvertFrom-Json
    if($baseline.InstanceId -notmatch '^USB\\VID_054C&PID_05C4\\[^\\]+$' -or
        $binding.status -ne 'bound-exact-physical-device' -or -not $binding.isForced -or
        [Guid]$binding.persistedGuidAfter -eq [Guid]::Empty){throw 'Saved physical ownership is invalid.'}
    $device = Read-PhysicalDevice
    $bus = [string]$device.BusId
    $url = 'usbip://127.0.0.1:33251/'+$bus
    $imports = @(Get-ImportUrls)
    if(@($imports | Where-Object {$_ -ne $url}).Count -or $imports.Count -gt 1){throw 'Unrelated imported device exists.'}
    $service = Get-Service -Name usbipd
    if($service.Status -notin @('Running','Stopped')){throw 'Exporter service is changing state.'}
    if($service.Status -eq 'Stopped' -and -not (Test-Path -LiteralPath $serviceHelper -PathType Leaf)){throw 'Service startup helper missing.'}
    if($CheckOnly) {
        [pscustomobject]@{status='preflight-ok';service=$service.Status.ToString();appRunning=($existing.Count -eq 1);ownedImport=($imports.Count -eq 1)} | ConvertTo-Json -Compress
        exit 0
    }
    # A surviving import without its original proxy cannot be resumed by a new
    # listener. Leave it untouched instead of creating a duplicate attachment.
    if($imports.Count){$phase='stale-owned-import';throw 'Previous owned import needs its original application.'}
    if($device.ClientIPAddress){throw 'Physical device is already captured.'}
    $phase='service-start'
    if($service.Status -eq 'Stopped') {
        $shell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        $arguments = '-NoProfile -ExecutionPolicy Bypass -File "'+$serviceHelper+'"'
        $helper = Start-Process -FilePath $shell -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -PassThru
        try {if(-not $helper.WaitForExit(60000) -or $helper.ExitCode -ne 0){throw 'Exporter startup did not complete.'}} finally {$helper.Dispose()}
    }
    if((Get-Service -Name usbipd).Status -ne 'Running' -or (Get-Service -Name VBoxUSBMon).Status -ne 'Running'){throw 'Exporter services unavailable.'}
    # Re-read after a possible elevation prompt; USB locations are not permanent.
    $device=Read-PhysicalDevice; $bus=[string]$device.BusId; $url='usbip://127.0.0.1:33251/'+$bus
    if($device.ClientIPAddress -or @(Get-ImportUrls).Count){throw 'Controller state changed during startup.'}
    if(@(Get-NetTCPConnection -State Listen -LocalPort 33251 -ErrorAction SilentlyContinue).Count){throw 'Relay port is already in use.'}
    $phase='application-start'
    $startedAt = [DateTime]::UtcNow
    $created = Start-Process -FilePath $app -ArgumentList @('--start-relay',$bus,'--status-file',('"'+$statusFile+'"')) -WorkingDirectory $appDir -PassThru
    $result.pid=$created.Id; $ready=$false
    for($attempt=0;$attempt -lt 50;$attempt++) {
        if($created.HasExited){throw 'Mapper exited during startup.'}
        if(Test-Path -LiteralPath $statusFile) {
            try {
                $state=Get-Content -LiteralPath $statusFile -Raw | ConvertFrom-Json
                $listener=@(Get-NetTCPConnection -State Listen -LocalPort 33251 -ErrorAction SilentlyContinue)
                $ready=$state.pid -eq $created.Id -and -not $state.failure -and -not $state.enabled -and
                    (Get-Item -LiteralPath $statusFile).LastWriteTimeUtc -ge $startedAt -and $listener.Count -eq 1 -and
                    $listener[0].LocalAddress -eq '127.0.0.1' -and $listener[0].OwningProcess -eq $created.Id
            } catch {$ready=$false}
        }
        if($ready){break}; Start-Sleep -Milliseconds 200
    }
    if(-not $ready){throw 'Mapper did not establish its own local listener.'}
    $phase='controller-attach'
    if(@(Get-ImportUrls).Count){throw 'Imported device appeared during startup.'}
    $attach=Invoke-Tool $client ('--tcp-port 33251 attach --remote 127.0.0.1 --bus-id '+$bus+' --once --terse')
    $attachedPort=0
    if(-not [int]::TryParse($attach.Trim(),[ref]$attachedPort) -or $attachedPort -le 0){throw 'Client did not return a valid attachment port.'}
    $detail=Invoke-Tool $client ('port '+$attachedPort)
    if($detail -notmatch ('(?m)^\s*->\s*'+[regex]::Escape($url)+'\s*$')){throw 'Attachment ownership could not be verified.'}
    $result.port=$attachedPort; $result.status='started-keyboard-paused'
} catch {
    $result.status='failed'; $result.errorCode=$phase
    if($created){try {if(-not $created.HasExited){[void]$created.CloseMainWindow()}} catch {}}
    if($CheckOnly){[pscustomobject]$result | ConvertTo-Json -Compress; exit 1}
    $result | ConvertTo-Json | Set-Content -LiteralPath $resultFile -Encoding UTF8
    $message = switch($phase) {
        'preflight' {'找不到原本設定的手把或必要元件。請確認 PS4 手把仍插在電腦上；程式未接管其他裝置。'}
        'stale-owned-import' {'先前的手把連線仍未結束。程式已停止啟動，沒有建立重複連線；請稍後檢查原本的鍵盤手把程式。'}
        'service-start' {'無法啟動手把服務。若剛才取消了 Windows 的管理員提示，請下次啟動時允許。'}
        'application-start' {'鍵盤手把視窗未完成啟動。本次啟動已停止，請稍後再試。'}
        default {'實體手把未能接入鍵盤程式。本次啟動已停止，沒有重新綁定手把或操作 PS 遠端。'}
    }
    Show-LauncherMessage ($message+"`r`n啟動結果已保存在本機，方便稍後檢查。")
}
if(-not $CheckOnly){$result | ConvertTo-Json | Set-Content -LiteralPath $resultFile -Encoding UTF8}
if($created){$created.Dispose()}
if($result.status -eq 'failed'){exit 1}
