param(
    [Parameter(Mandatory=$true)][string]$Installer,
    [Parameter(Mandatory=$true)][string]$ResultFile
)
# Invoke only after the user approves USB hub interruption. Never reboot.
$ErrorActionPreference='Stop'
$result=[ordered]@{status='starting';installerExitCode=$null;restorePoint=$null;error=$null}
$log=[IO.Path]::ChangeExtension([IO.Path]::GetFullPath($ResultFile),'.installer.log')
try {
    $principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator permission is required.'}
    $path=(Resolve-Path -LiteralPath $Installer).Path
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne '38CAD6D4432B52D5BB9409D9AD03B72FDFFC4ADA4CD3A48FBECA1A2752A8518A'){throw 'Unexpected USBip installer hash.'}
    if((Get-AuthenticodeSignature -LiteralPath $path).Status -ne 'Valid'){throw 'USBip signature verification failed.'}
    try {
        $point=Invoke-CimMethod -Namespace 'root/default' -ClassName SystemRestore -MethodName CreateRestorePoint `
            -Arguments @{Description='Before PSKeyboard USBip 0.9.8.1';RestorePointType=[uint32]12;EventType=[uint32]100} -OperationTimeoutSec 60
        $result.restorePoint='SystemRestore return code: '+$point.ReturnValue
    } catch { $result.restorePoint='Unavailable: '+$_.Exception.Message }
    $result.status='installing';$result|ConvertTo-Json|Set-Content -LiteralPath $ResultFile -Encoding UTF8
    $arguments=@('/SP-','/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/RESTARTEXITCODE=3010',
        '/NOCLOSEAPPLICATIONS','/NORESTARTAPPLICATIONS','/TYPE=compact','/TASKS=vcredist',('/LOG="'+$log+'"'))
    $setup=Start-Process -FilePath $path -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    $result.installerExitCode=$setup.ExitCode
    if($setup.ExitCode -ne 0 -and $setup.ExitCode -ne 3010){throw ('USBip installer failed: '+$setup.ExitCode)}
    $result.status=if($setup.ExitCode -eq 3010){'installed-restart-requested'}else{'installed'}
} catch {$result.status='failed';$result.error=$_.Exception.Message}
$result|ConvertTo-Json|Set-Content -LiteralPath $ResultFile -Encoding UTF8
