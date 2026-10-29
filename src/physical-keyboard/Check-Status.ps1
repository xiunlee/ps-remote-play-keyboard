# Read-only support snapshot. No USB payloads, device identities or account data.
$ErrorActionPreference='Stop'
$path=Join-Path $env:USERPROFILE '.pskeyboard-tools\physical-keyboard-preview\status.json'
$report=[ordered]@{recordAvailable=$false;recordFresh=$false;appRunning=$false;keyboardEnabled=$false;attached=$false;endpointVerified=$false;inputReports=0;mergedReports=0;failure=$null;exporterService=$null;remotePlayRunning=$false}
if(Test-Path -LiteralPath $path) {
    $file=Get-Item -LiteralPath $path
    $state=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $report.recordAvailable=$true
    $age=([DateTime]::UtcNow-$file.LastWriteTimeUtc).TotalSeconds
    $process=Get-Process -Id $state.pid -ErrorAction SilentlyContinue
    $report.appRunning=$null -ne $process -and $process.ProcessName -eq 'PSKeyboard-Physical'
    $report.recordFresh=$report.appRunning -and $age -ge 0 -and $age -lt 5
    if($report.recordFresh) {
        $report.keyboardEnabled=[bool]$state.enabled;$report.attached=[bool]$state.attached
        $report.endpointVerified=[bool]$state.endpointVerified
        $report.inputReports=[long]$state.inputReports;$report.mergedReports=[long]$state.mergedReports
        if($state.failure){$report.failure='reported-error'}
    }
}
$service=Get-Service -Name usbipd -ErrorAction SilentlyContinue
if($service){$report.exporterService=$service.Status.ToString()}
$report.remotePlayRunning=[bool](Get-Process -Name RemotePlay -ErrorAction SilentlyContinue)
$report | ConvertTo-Json
