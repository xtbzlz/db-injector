# extract the compressed export-name table from game64.exe (krkrz FuncStubs)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression

$candidate = Get-ChildItem 'C:\Users\1\Desktop\CR' -Filter 'game64.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $candidate) { throw 'game64.exe not found' }
$exePath = $candidate.FullName
$bytes = [System.IO.File]::ReadAllBytes($exePath)
Write-Output "exe size: $($bytes.Length)"

function Try-Deflate([byte[]]$src, [int]$off, [int]$maxOut) {
    # strip zlib 2-byte header, feed raw deflate
    $ms = New-Object System.IO.MemoryStream
    try {
        $ms.Write($src, $off + 2, [Math]::Min($src.Length - $off - 2 - 4, 262144))
        $ms.Position = 0
        $ds = New-Object System.IO.Compression.DeflateStream($ms, [System.IO.Compression.CompressionMode]::Decompress)
        $out = New-Object byte[] $maxOut
        $read = $ds.Read($out, 0, $maxOut)
        $ds.Close()
        if ($read -lt 10) { return $null }
        $result = New-Object byte[] $read
        [Array]::Copy($out, $result, $read)
        return $result
    } catch {
        return $null
    } finally {
        $ms.Dispose()
    }
}

$found = @()
for ($i = 0; $i -lt $bytes.Length - 8; $i++) {
    if ($bytes[$i] -ne 0x78) { continue }
    $cmf = $bytes[$i]
    $flg = $bytes[$i + 1]
    # zlib methods 8 (deflate), check common presets
    if (($cmf -band 0x0F) -ne 8) { continue }
    $raw = Try-Deflate $bytes $i 200000
    if ($null -eq $raw) { continue }
    # validate: mostly printable ASCII or NUL
    $total = $raw.Length
    $print = 0
    $nulCount = 0
    foreach ($b in $raw) {
        if ($b -eq 0) { $nulCount++ }
        elseif ($b -ge 0x20 -and $b -le 0x7E) { $print++ }
    }
    if ($nulCount -lt 10) { continue }
    $ratio = ($print + $nulCount) / $total
    if ($ratio -lt 0.95) { continue }
    $text = [System.Text.Encoding]::ASCII.GetString($raw)
    $names = $text -split "`0" | Where-Object { $_ -ne '' }
    # heuristics: many names contain :: (signature style) or TVP/TJS
    $sig = ($names | Where-Object { $_ -match '::' -or $_ -match 'TVP' -or $_ -match 'TJS' }).Count
    if ($total -lt 10000) { continue }
    if ($sig -lt 30) { continue }
    $found += [pscustomobject]@{ Offset = $i; Decompressed = $total; Count = $names.Count; Names = $names }
    Write-Output "CANDIDATE at 0x$('{0:X}' -f $i): decompressed=$total names=$($names.Count) ratio=$([Math]::Round($ratio,3))"
}

foreach ($f in $found) {
    Write-Output "`n===== table at 0x$('{0:X}' -f $f.Offset) — $($f.Count) names ====="
    $wanted = 'TVPGetScriptDispatch','TVPGetScriptEngine','TVPExecuteScript','TVPExecuteExpression','TVPExecuteStorage','TVPPostEvent','TVPCreateStream','TVPRegisterGlobalObject','TVPGetGlobals','TVPExecScript','TVPEvalScript','TVPRegisterPlugin','TVPGetTJS2ConsoleOutputGateway'
    $wanted | ForEach-Object {
        $hits = ($f.Names | Select-String -SimpleMatch $_) 
        $cnt = ($f.Names | Where-Object { $_ -eq $_ }).Count
        $exact = ($f.Names | Where-Object { $_ -eq $_ }).Count
        $exact = 0
        foreach ($n in $f.Names) { if ($n -eq $_) { $exact++ } }
        $idx = @(); for ($k = 0; $k -lt $f.Names.Count; $k++) { if ($f.Names[$k] -eq $_) { $idx += $k } }
        Write-Output ("{0,-32} count={1,-3} indices=[{2}]" -f $_, $exact, ($idx -join ','))
    }
}
