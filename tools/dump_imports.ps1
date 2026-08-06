$ErrorActionPreference = 'Stop'
$dll = "C:\Users\1\Desktop\CR\DXCXN\tools\krkrzHook32.dll"
$bytes = [System.IO.File]::ReadAllBytes($dll)
$fs = [System.IO.File]::OpenRead($dll)
$br = New-Object System.IO.BinaryReader($fs)
$fs.Seek(0x3C, 'Begin') | Out-Null
$pe = $br.ReadInt32()
$fs.Seek($pe, 'Begin') | Out-Null
$null = $br.ReadUInt32()
$machine = $br.ReadUInt16()
$secNum = $br.ReadUInt16()
$fs.Seek($pe + 0x18, 'Begin') | Out-Null
$optSz = $br.ReadUInt16()
$fs.Seek($pe + 0x18 + $optSz, 'Begin') | Out-Null
$sections = @()
for ($i = 0; $i -lt $secNum; $i++) {
    $sName = [System.Text.Encoding]::ASCII.GetString($br.ReadBytes(8)).TrimEnd([char]0)
    $vsz = $br.ReadUInt32(); $vaddr = $br.ReadUInt32(); $rsz = $br.ReadUInt32(); $raddr = $br.ReadUInt32()
    $sections += [pscustomobject]@{ Name = $sName; VA = $vaddr; VS = $vsz; RA = $raddr; RS = $rsz }
    $br.ReadBytes(16) | Out-Null
}
function RvaToOff($rva) {
    foreach ($s in $sections) { if ($rva -ge $s.VA -and $rva -lt ($s.VA + $s.VS)) { return $s.RA + ($rva - $s.VA) } }
    return -1
}
# import directory: DataDirectory[1] at PE+0x78+8
$fs.Seek($pe + 0x80, 'Begin') | Out-Null
$impRva = $br.ReadUInt32()
$impOff = RvaToOff $impRva
$fs.Seek($impOff, 'Begin') | Out-Null
while ($true) {
    $iltRva = $br.ReadUInt32()
    $ts = $br.ReadUInt32()
    $fwd = $br.ReadUInt32()
    $nameRva = $br.ReadUInt32()
    $iatRva = $br.ReadUInt32()
    if ($nameRva -eq 0) { break }
    try { $nameOff = RvaToOff $nameRva } catch { break }; if ($nameOff -lt 0) { break }
    $pos = $fs.Position
    $fs.Seek($nameOff, 'Begin') | Out-Null
    $sb = New-Object System.Text.StringBuilder
    while (($c = $br.ReadByte()) -ne 0) { [void]$sb.Append([char]$c) }
    $dllName = $sb.ToString()
    $fs.Seek($pos, 'Begin') | Out-Null
    Write-Output "DLL: $dllName"
    # walk the import lookup table (32-bit: RVA entries, ordinal if high bit set)
    if ($iltRva -ne 0) {
        $iltOff = RvaToOff $iltRva
        $fs.Seek($iltOff, 'Begin') | Out-Null
        while ($true) {
            $entry = $br.ReadUInt32()
            if ($entry -eq 0) { break }
            if (($entry -band 0x80000000) -ne 0) { Write-Output "  ordinal $($entry -band 0xFFFF)"; continue }
            $hintOff = RvaToOff $entry; if ($hintOff -lt 0) { continue }
            $p2 = $fs.Position
            $fs.Seek($hintOff, 'Begin') | Out-Null
            $null = $br.ReadUInt16()
            $sb2 = New-Object System.Text.StringBuilder
            while (($c2 = $br.ReadByte()) -ne 0) { [void]$sb2.Append([char]$c2) }
            Write-Output "  $($sb2.ToString())"
            $fs.Seek($p2, 'Begin') | Out-Null
        }
    }
}
$br.Close(); $fs.Close()


