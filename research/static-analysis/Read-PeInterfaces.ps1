param([Parameter(Mandatory=$true)][string[]]$Path)
$ErrorActionPreference = 'Stop'
# Read PE metadata from disk only. No assemblies are loaded and no processes
# or account/configuration files are opened.
foreach ($file in $Path) {
    $resolved = (Resolve-Path -LiteralPath $file).Path
    $bytes = [IO.File]::ReadAllBytes($resolved)
    function U16([int]$offset) { [BitConverter]::ToUInt16($bytes,$offset) }
    function U32([int]$offset) { [BitConverter]::ToUInt32($bytes,$offset) }
    function U64([int]$offset) { [BitConverter]::ToUInt64($bytes,$offset) }
    function TextAt([int]$offset) {
        if ($offset -lt 0 -or $offset -ge $bytes.Length) { throw 'String outside file' }
        $end=$offset
        while ($end -lt $bytes.Length -and $end-$offset -lt 4096 -and $bytes[$end] -ne 0) { $end++ }
        if ($end-$offset -ge 4096) { throw 'Oversized PE name' }
        [Text.Encoding]::ASCII.GetString($bytes,$offset,$end-$offset)
    }
    if ($bytes.Length -lt 64 -or (U16 0) -ne 0x5A4D) { throw 'Not a DOS/PE file' }
    $pe=U32 60
    if ((U32 $pe) -ne 0x4550) { throw 'Invalid PE signature' }
    $count=U16 ($pe+6)
    $optionalSize=U16 ($pe+20)
    $optional=$pe+24
    $magic=U16 $optional
    if ($magic -eq 0x20b) { $width=8; $directory=$optional+112 }
    elseif ($magic -eq 0x10b) { $width=4; $directory=$optional+96 }
    else { throw 'Unsupported PE optional header' }
    $headers=U32 ($optional+60)
    $sections=@()
    for ($i=0;$i -lt $count;$i++) {
        $off=$optional+$optionalSize+$i*40
        $sections += [pscustomobject]@{
            Name=[Text.Encoding]::ASCII.GetString($bytes,$off,8).Trim([char]0)
            Rva=U32 ($off+12); VirtualSize=U32 ($off+8)
            Raw=U32 ($off+20); RawSize=U32 ($off+16)
        }
    }
    function OffsetOf([uint64]$rva) {
        if ($rva -lt $headers) { return [int]$rva }
        foreach ($section in $sections) {
            if ($rva -ge $section.Rva -and $rva-$section.Rva -lt $section.RawSize) {
                return [int]($section.Raw+$rva-$section.Rva)
            }
        }
        throw ('RVA has no disk data: 0x{0:X}' -f $rva)
    }
    $imports=@()
    $importRva=U32 ($directory+8)
    if ($importRva) {
        $entry=OffsetOf $importRva
        for ($j=0;$j -lt 1024;$j++) {
            $d=$entry+$j*20
            $names=U32 $d; $nameRva=U32 ($d+12); $firstThunk=U32 ($d+16)
            if (!$names -and !$nameRva -and !$firstThunk) { break }
            $dll=TextAt (OffsetOf $nameRva)
            if (!$names) { $names=$firstThunk }
            $thunk=OffsetOf $names
            $functions=@()
            for ($k=0;$k -lt 65536;$k++) {
                if ($width -eq 8) { $value=U64 ($thunk+$k*8); $isOrdinal=($value -shr 63) -ne 0 }
                else { $value=U32 ($thunk+$k*4); $isOrdinal=($value -shr 31) -ne 0 }
                if (!$value) { break }
                if ($isOrdinal) { $functions += ('#'+($value -band 65535)) }
                else { $functions += TextAt ((OffsetOf $value)+2) }
            }
            $imports += [pscustomobject]@{Dll=$dll;Functions=$functions}
        }
    }
    $delayImports=@()
    $delayParseError=$null
    $delayRva=U32 ($directory+13*8)
    if ($delayRva) {
        $entry=OffsetOf $delayRva
        try { for ($j=0;$j -lt 1024;$j++) {
            $d=$entry+$j*32
            $attributes=U32 $d; $nameRva=U32 ($d+4); $names=U32 ($d+16)
            if (!$attributes -and !$nameRva -and !$names) { break }
            if (($attributes -band 1) -eq 0) {
                $delayImports += [pscustomobject]@{Dll='[VA-based descriptor not parsed]';Functions=@()}
                break
            }
            $dll=TextAt (OffsetOf $nameRva)
            $thunk=OffsetOf $names
            $functions=@()
            for ($k=0;$k -lt 65536;$k++) {
                if ($width -eq 8) { $value=U64 ($thunk+$k*8); $isOrdinal=($value -shr 63) -ne 0 }
                else { $value=U32 ($thunk+$k*4); $isOrdinal=($value -shr 31) -ne 0 }
                if (!$value) { break }
                if ($isOrdinal) { $functions += ('#'+($value -band 65535)) }
                else { $functions += TextAt ((OffsetOf $value)+2) }
            }
            $delayImports += [pscustomobject]@{Dll=$dll;Functions=$functions}
        } } catch { $delayParseError=$_.Exception.Message }
    }
    $exports=@()
    $exportRva=U32 $directory
    if ($exportRva) {
        $off=OffsetOf $exportRva
        $nameCount=U32 ($off+24)
        if ($nameCount -gt 65536) { throw 'Too many exports' }
        $nameTable=OffsetOf (U32 ($off+32))
        for ($i=0;$i -lt $nameCount;$i++) { $exports += TextAt (OffsetOf (U32 ($nameTable+$i*4))) }
    }
    [pscustomobject]@{
        File=[IO.Path]::GetFileName($resolved)
        SHA256=(Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
        Machine=('0x{0:X}' -f (U16 ($pe+4)))
        EntryPoint=('0x{0:X}' -f (U32 ($optional+16)))
        Sections=@($sections | ForEach-Object {$_.Name})
        Imports=$imports; DelayImports=$delayImports; Exports=$exports
        DelayParseError=$delayParseError
        DelayImportRva=('0x{0:X}' -f (U32 ($directory+13*8)))
        ClrRva=('0x{0:X}' -f (U32 ($directory+14*8)))
    }
}
