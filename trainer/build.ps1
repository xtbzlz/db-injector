# Build Trainer.exe (WPF, .NET Framework 4, code-only UI)
$csc = "C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = "C:\Users\1\Desktop\CR\DXCXN\trainer\src"
$out = "C:\Users\1\Desktop\CR\DXCXN\trainer\Trainer.exe"
$gac = "C:\WINDOWS\Microsoft.NET\assembly"

function Find-Gac($name) {
    foreach ($root in @("$gac\GAC_MSIL", "$gac\GAC_32", "$gac\GAC_64")) {
        $dirs = Get-ChildItem "$root\$name" -Directory -ErrorAction SilentlyContinue
        foreach ($d in $dirs) {
            $f = Join-Path $d.FullName "$name.dll"
            if (Test-Path $f) { return $f }
        }
    }
    return $null
}

$refs = @("PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml")
$args = @()
foreach ($r in $refs) {
    $p = Find-Gac $r
    if (-not $p) { throw "GAC not found: $r" }
    $args += "/r:$p"
}

& $csc /nologo /target:winexe /optimize /out:$out @args "$src\PipeClient.cs" "$src\WikiItems.cs" "$src\CnItemMap.cs" "$src\CharaMap.cs" "$src\MagicMap.cs" "$src\TrainerApp.cs"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
Write-Output "OK: Trainer.exe $((Get-Item $out).Length) bytes"
