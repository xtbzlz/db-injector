# Build Trainer.exe (WPF, .NET Framework 4, code-only UI)
# 单文件交付：tb_bridge.dll 插件内嵌为资源，运行时释放为游戏目录 plugin\tb_bridge.tpm
$csc = "C:\WINDOWS\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$src = "C:\Users\1\Desktop\CR\DXCXN\trainer\src"
$out = "C:\Users\1\Desktop\CR\DXCXN\trainer\Trainer.exe"
$gac = "C:\WINDOWS\Microsoft.NET\assembly"
$pluginRes = "C:\Users\1\Desktop\CR\DXCXN\bridge\bin\tb_bridge.dll"

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
$argList = @()
foreach ($r in $refs) {
    $p = Find-Gac $r
    if (-not $p) { throw "GAC not found: $r" }
    $argList += "/r:$p"
}
# System.Windows.Forms：用与 csc 同目录的 Framework 程序集（避免 GAC 重复引用冲突 CS1703）
$wf = Join-Path (Split-Path $csc) "System.Windows.Forms.dll"
if (-not (Test-Path $wf)) { throw "System.Windows.Forms.dll not found" }
$argList += "/r:$wf"

if (-not (Test-Path $pluginRes)) { throw "plugin resource not found: $pluginRes (run bridge/build.ps1 first)" }
$argList += "/resource:$pluginRes"

$files = @("$src\PipeClient.cs", "$src\WikiItems.cs", "$src\CnItemMap.cs", "$src\CharaMap.cs", "$src\MagicMap.cs", "$src\AppConfig.cs", "$src\TrainerApp.cs")
& $csc /nologo /target:winexe /optimize /out:$out @argList $files
if ($LASTEXITCODE -ne 0) { throw "build failed" }
Write-Output "OK: Trainer.exe $((Get-Item $out).Length) bytes (plugin embedded)"
