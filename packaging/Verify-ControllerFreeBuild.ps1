param(
    [Parameter(Mandatory = $true)][string]$BuildDirectory,
    [switch]$RequireClean,
    [switch]$ReadBrandResources
)
# Read-only artifact verification. Never executes application or backend code.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $BuildDirectory).Path
if ($ReadBrandResources) {
    # Run only in a separate Windows PowerShell/.NET Framework host. Reflection-
    # only loading cannot execute the application's entry point or initializers.
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $root 'PSKeyboard-ControllerFree.exe'))
    $resourceNames = @($assembly.GetManifestResourceNames())
    $resourceHashes = [ordered]@{}
    foreach ($resourceName in @('PSKeyboard.BrandIcon', 'PSKeyboard.BrandMark')) {
        if ($resourceNames -cnotcontains $resourceName) { throw ('Missing embedded brand resource: ' + $resourceName) }
        $stream = $assembly.GetManifestResourceStream($resourceName)
        $hash = [Security.Cryptography.SHA256]::Create()
        try { $resourceHashes[$resourceName] = [BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-', '') }
        finally { if ($stream) { $stream.Dispose() }; $hash.Dispose() }
    }
    # Mapping as data/image resource prevents DllMain or entry-point execution.
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class BrandNativeIconProbe
{
    private delegate bool EnumNames(IntPtr module, IntPtr type, IntPtr name, IntPtr data);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumNames callback, IntPtr data);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    public static int CountIconGroups(string path)
    {
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, 0x22);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            int count = 0;
            EnumNames callback = delegate(IntPtr m, IntPtr t, IntPtr n, IntPtr d) { count++; return true; };
            EnumResourceNames(module, new IntPtr(14), callback, IntPtr.Zero);
            GC.KeepAlive(callback); return count;
        } finally { FreeLibrary(module); }
    }
}
'@
    $resourceHashes['nativeIconGroups'] = [BrandNativeIconProbe]::CountIconGroups((Join-Path $root 'PSKeyboard-ControllerFree.exe'))
    $resourceHashes | ConvertTo-Json -Compress
    exit 0
}
$manifest = Get-Content -LiteralPath (Join-Path $root 'BUILD.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.variant -cne 'controller-free' -or $manifest.channel -cne 'internal-preview' -or
    $manifest.entryPoint -cne 'PSKeyboard-ControllerFree.exe' -or $manifest.commercialReady -ne $false) { throw 'Unsupported build manifest.' }
if ($manifest.workingTreeDirty -isnot [bool] -or $manifest.sourceCommit -cnotmatch '\A[0-9a-f]{40}\z') { throw 'Invalid source identity.' }
if ($RequireClean -and $manifest.workingTreeDirty) { throw 'This artifact was built from an uncommitted working tree.' }
if ($manifest.defaultControllerMode -cne 'dualshock4' -or (@($manifest.controllerModes) -join ',') -cne 'dualshock4,dualsense') { throw 'Unexpected controller mode metadata.' }
if ($manifest.version -cnotmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)-alpha\.(0|[1-9][0-9]*)\z') { throw 'Invalid preview version.' }
$parts = @($Matches[1], $Matches[2], $Matches[3], $Matches[4])
foreach ($part in $parts) {
    $number = 0
    if (-not [int]::TryParse($part, [ref]$number) -or $number -gt 65534) { throw 'Invalid PE version component.' }
}
$expectedFileVersion = $parts -join '.'
$expectedAssemblyVersion = $parts[0] + '.' + $parts[1] + '.' + $parts[2] + '.0'
$expectedInfoVersion = $manifest.version + '+g' + $manifest.sourceCommit.Substring(0, 12)
if ($manifest.workingTreeDirty) { $expectedInfoVersion += '.dirty' }
if ($manifest.fileVersion -cne $expectedFileVersion -or $manifest.assemblyVersion -cne $expectedAssemblyVersion -or $manifest.informationalVersion -cne $expectedInfoVersion) { throw 'Version metadata is inconsistent.' }

$runtimeNames = @('PSKeyboard-ControllerFree.exe', 'pskb-keyboard-merge.dll', 'viiper.exe')
$allowedNames = @($runtimeNames) + @('BUILD.json')
$seen = @{}
foreach ($line in (Get-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Encoding UTF8)) {
    if ($line -cnotmatch '\A([0-9A-F]{64})  ([A-Za-z0-9_.-]+)\z') { throw 'Invalid checksum entry.' }
    $expectedHash = $Matches[1]; $name = $Matches[2]
    if ($allowedNames -cnotcontains $name -or $seen.ContainsKey($name)) { throw 'Unexpected or duplicate checksum entry.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $name) -Algorithm SHA256).Hash -cne $expectedHash) { throw ('Checksum mismatch: ' + $name) }
    $seen[$name] = $true
}
if ($seen.Count -ne $allowedNames.Count) { throw 'Checksum list does not cover the complete runtime.' }
$seenOutputs = @{}
foreach ($output in $manifest.outputs) {
    if ($runtimeNames -cnotcontains $output.path -or $seenOutputs.ContainsKey([string]$output.path) -or $output.sha256 -cnotmatch '\A[0-9A-F]{64}\z') { throw 'Invalid output record.' }
    if ((Get-FileHash -LiteralPath (Join-Path $root $output.path) -Algorithm SHA256).Hash -cne $output.sha256) { throw ('Output hash mismatch: ' + $output.path) }
    $seenOutputs[[string]$output.path] = $true
}
if ($seenOutputs.Count -ne $runtimeNames.Count) { throw 'Manifest does not cover the complete runtime.' }
$fileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $manifest.entryPoint))
if ($fileInfo.FileVersion -cne $expectedFileVersion -or $fileInfo.ProductVersion -cne $expectedInfoVersion) { throw 'Executable version does not match BUILD.json.' }
if (@($manifest.sources).Count -eq 0) { throw 'Source hashes are missing.' }
$seenSources = @{}
foreach ($source in $manifest.sources) {
    if ($source.path -cnotmatch '\A[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*\z' -or ([string]$source.path).Split('/') -contains '..' -or
        $source.sha256 -cnotmatch '\A[0-9A-F]{64}\z' -or $seenSources.ContainsKey([string]$source.path)) { throw 'Invalid source hash record.' }
    $seenSources[[string]$source.path] = $true
}
if (-not $seenSources.ContainsKey('VERSION')) { throw 'VERSION input hash is missing.' }
if ($null -ne $manifest.branding) {
    $brand = $manifest.branding
    if ($brand.accent -cne '#D62355' -or $brand.iconSource -cne 'assets/branding/app.ico' -or
        $brand.markSource -cne 'assets/branding/ps-mark.png' -or $brand.iconResource -cne 'PSKeyboard.BrandIcon' -or
        $brand.markResource -cne 'PSKeyboard.BrandMark' -or $brand.nativeExecutableIcon -ne $true -or
        $brand.iconSha256 -cnotmatch '\A[0-9A-F]{64}\z' -or $brand.markSha256 -cnotmatch '\A[0-9A-F]{64}\z') { throw 'Invalid branding metadata.' }
    foreach ($asset in @(@{ path=$brand.iconSource; hash=$brand.iconSha256 }, @{ path=$brand.markSource; hash=$brand.markSha256 })) {
        $sourceRecord = @($manifest.sources | Where-Object { $_.path -ceq $asset.path })
        if ($sourceRecord.Count -ne 1 -or $sourceRecord[0].sha256 -cne $asset.hash) { throw 'Branding source provenance is inconsistent.' }
    }
    $metadataHost = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $resourceResult = @(& $metadataHost -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $PSCommandPath -BuildDirectory $root -ReadBrandResources)
    if ($LASTEXITCODE -ne 0 -or $resourceResult.Count -ne 1) { throw 'Embedded branding could not be inspected.' }
    $embedded = $resourceResult[0] | ConvertFrom-Json
    if ($embedded.'PSKeyboard.BrandIcon' -cne $brand.iconSha256 -or $embedded.'PSKeyboard.BrandMark' -cne $brand.markSha256 -or
        $embedded.nativeIconGroups -lt 1) { throw 'Executable branding does not match BUILD.json.' }
}
$external = @($manifest.externalBinaries)
if ($external.Count -ne 1 -or $external[0].name -cne 'VIIPER' -or $external[0].version -cne '0.9.0' -or
    $external[0].path -cne 'viiper.exe' -or $external[0].sha256 -cne '142C144F6ABEE8B8A5F6A1ABBC7018683444DE71D2E45102CDE6AAAB9B2223E6' -or
    $external[0].redistributionApproved -ne $false -or $external[0].localUseOnly -ne $true) { throw 'Unexpected external binary metadata.' }
if ((Get-FileHash -LiteralPath (Join-Path $root 'viiper.exe') -Algorithm SHA256).Hash -cne $external[0].sha256) { throw 'Pinned backend hash mismatch.' }
[pscustomobject]@{ verified = $true; version = $manifest.version; sourceCommit = $manifest.sourceCommit; workingTreeDirty = $manifest.workingTreeDirty; verifiedFiles = $seen.Count; commercialReady = $false }
