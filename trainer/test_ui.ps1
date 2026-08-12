# test_ui.ps1 — automated UI test for Trainer.exe (重构版：指令拼接器) via UIAutomation.
# Usage: test_ui.ps1 [-Root <repo-root>] [-GameDir <game-dir>]
param(
    [string]$Root = "C:\Users\1\Desktop\CR\DXCXN",
    [string]$GameDir = "C:\Users\1\Desktop\CR\ダンジョン＆ブライド"
)
$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$trainer = "$Root\trainer\Trainer.exe"
$crashLog = "$Root\trainer\crash.log"
$results = @()

# ---- 预置测试配置（防首次启动向导弹窗 + 验证配置系统） ----
$cfgDir = Join-Path $env:APPDATA "DzbTrainer"
New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
[System.IO.File]::WriteAllText((Join-Path $cfgDir "config.json"),
    "{`"gameDir`":`"$GameDir`",`"debug`":false,`"autoLaunchGame`":true}",
    [System.Text.Encoding]::UTF8)
Write-Output "pre-seeded config: $cfgDir\config.json"

function Write-Test($name, $ok, $detail) {
    $script:results += [pscustomobject]@{ Name = $name; OK = $ok; Detail = $detail }
    $mark = if ($ok) { "PASS" } else { "FAIL" }
    Write-Output ("[{0}] {1} {2}" -f $mark, $name, $detail)
}

$crashBefore = 0
if (Test-Path $crashLog) { $crashBefore = (Get-Content $crashLog).Count }

# ---- 预启动游戏（冷启动慢，先就绪再测 Trainer；以 game.items.count 可返回为就绪标志，轮询最长 120s） ----
$gameDir2 = $GameDir
if (-not (Get-Process game64 -ErrorAction SilentlyContinue)) {
    Start-Process (Join-Path $gameDir2 "game64.exe")
    Write-Output "game launched, waiting for ready..."
    $tbc = "C:\Users\1\Desktop\CR\DXCXN\tools\TbcCli.exe"
    $ready = $false
    for ($i = 0; $i -lt 240 -and -not $ready; $i++) {
        Start-Sleep -Milliseconds 500
        $out = & $tbc "game.items.count" 2>$null | Select-Object -Last 1
        if ($out -match "= \d+") { $ready = $true }
    }
    Write-Output "game ready: $ready (waited $([math]::Round($i * 0.5))s)"
    if (-not $ready) { Write-Output "game NOT ready - connection may fail" }
} else {
    Write-Output "game already running"
}

# start trainer
Get-Process Trainer -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }
Start-Sleep -Milliseconds 800
$p = Start-Process $trainer -PassThru
Start-Sleep -Seconds 6
if ($p.HasExited) {
    Write-Test "启动" $false "异常退出 code=$($p.ExitCode)"
    exit 1
}
Write-Test "启动" $true "PID=$($p.Id)"

$cond = New-Object System.Windows.Automation.PropertyCondition(
    [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $null
for ($i = 0; $i -lt 20; $i++) {
    $win = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
        [System.Windows.Automation.TreeScope]::Children, $cond)
    if ($win) { break }
    Start-Sleep -Milliseconds 500
}
Write-Test "窗口" ($null -ne $win) ""

if (-not $win) { exit 1 }

function Get-All($el) {
    return $el.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
}
function Get-Lists($el) {
    return $el.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::List)))
}
function Get-Buttons($el) {
    return $el.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))
}
function Select-Item($listEl, $match) {
    $items = $listEl.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($it in $items) {
        if ($it.Current.Name -match $match) {
            $it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            return $true
        }
    }
    return $false
}
function Get-LogLines($el) {
    $lines = @()
    foreach ($x in (Get-All $el)) {
        if ($x.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem -and $x.Current.Name -match "^\[\d{2}:\d{2}") {
            $lines += $x.Current.Name
        }
    }
    return $lines
}

# ---- 顶栏 ----
$btns = Get-Buttons $win
$navNames = @()
foreach ($b in $btns) { $navNames += $b.Current.Name }
Write-Test "顶栏导航(指令/控制台/维护/启动游戏)" (($navNames -contains "指令") -and ($navNames -contains "控制台") -and ($navNames -contains "维护") -and ($navNames -contains "启动游戏")) "btns=$($navNames -join ',')"

# ---- 指令页：分组 ----
$lists = Get-Lists $win
Write-Test "列表控件>=2" ($lists.Count -ge 2) "count=$($lists.Count)"
$groupItems = $lists[0].FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
Write-Test "分组13项" ($groupItems.Count -eq 13) "count=$($groupItems.Count)"
$groupNames = @(); foreach ($g in $groupItems) { $groupNames += $g.Current.Name }
$expGroups = @("等级经验","六维属性","魔法上限","物品","时间","状态异常","结婚","队伍","地图","技能","男主数据","后宫数值","性记录")
$missing = $expGroups | Where-Object { $groupNames -notcontains $_ }
Write-Test "分组名称完整" ($missing.Count -eq 0) "missing=$($missing -join ',')"

# 默认选中的指令组（等级经验）应有指令
$cmdItems = $lists[1].FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
Write-Test "默认组指令已填充" ($cmdItems.Count -ge 4) "count=$($cmdItems.Count)"

# 注册表数据加载（等待日志；游戏已预启动，Trainer 连接后秒级完成，等待放宽到 40s）
$loaded = $false
for ($i = 0; $i -lt 80; $i++) {
    Start-Sleep -Milliseconds 500
    foreach ($l in (Get-LogLines $win)) { if ($l -match "已加载注册表") { $loaded = $true } }
    if ($loaded) { break }
}
Write-Test "注册表加载(物品/角色/魔法)" $loaded ""

# ---- TDD 新增断言：配置系统 / 初始化流程（先 RED 后 GREEN） ----
# 1. 配置持久化：预置 config.json 应被正确读取（gameDir 中文路径往返）
$cfgText = Get-Content (Join-Path $cfgDir "config.json") -Raw -Encoding UTF8
Write-Test "配置持久化(gameDir中文路径)" ($cfgText -match [regex]::Escape($GameDir)) ""

# 2. 初始化流程：日志应出现 插件部署/连接 记录（自动部署+自动连接）
$initOk = $false
foreach ($l in (Get-LogLines $win)) { if ($l -match "插件已部署|连接成功") { $initOk = $true } }
Write-Test "初始化流程(插件部署/连接日志)" $initOk ""

# ---- 切到物品组 ----
if (Select-Item $lists[0] "物品") { Start-Sleep -Milliseconds 500 }
$lists2 = Get-Lists $win
$itemCmds = $lists2[1].FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
Write-Test "物品组5指令(含金钱)" ($itemCmds.Count -eq 5) "count=$($itemCmds.Count)"

# 选中"发放物品(目标角色)" → 应生成表单
$selected = Select-Item $lists2[1] "发放物品\(目标角色\)"
Start-Sleep -Milliseconds 500
$all = Get-All $win
$cbs = @(); $edits = @()
foreach ($el in $all) {
    if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox) { $cbs += $el }
    elseif ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit) { $edits += $el }
}
Write-Test "物品发放表单(2下拉+数量+预览)" (($cbs.Count -ge 2) -and ($edits.Count -ge 2)) "combos=$($cbs.Count) edits=$($edits.Count)"

# 角色下拉展开含角色
if ($cbs.Count -ge 1) {
    try {
        $cbs[0].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep -Milliseconds 400
        $items = $cbs[0].FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        Write-Test "角色下拉双语(含テオ)" (($items.Count -ge 14) -and ($items[0].Current.Name -match "\[0\]")) "items=$($items.Count)"
        $cbs[0].GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
    } catch { Write-Test "角色下拉双语(含テオ)" $false "err" }
}

# ---- 控制台页 ----
$btns = Get-Buttons $win
foreach ($b in $btns) { if ($b.Current.Name -eq "控制台") { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break } }
Start-Sleep -Milliseconds 500
$all = Get-All $win
$hasInput = $false; $hasExec = $false
foreach ($el in $all) {
    if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit) { $hasInput = $true }
    if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $el.Current.Name -eq "执行") { $hasExec = $true }
}
Write-Test "控制台页(输入+执行)" ($hasInput -and $hasExec) ""

# ---- 维护页 ----
$btns = Get-Buttons $win
foreach ($b in $btns) { if ($b.Current.Name -eq "维护") { $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); break } }
Start-Sleep -Milliseconds 500
$all = Get-All $win
$hasClear = $false
foreach ($el in $all) {
    if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $el.Current.Name -match "清空全员背包") { $hasClear = $true }
}
Write-Test "维护页(清空背包/重载)" $hasClear ""

# TDD 新增断言：维护页 修改游戏目录/调试模式/查看插件日志 三按钮（未实现→RED）
$hasDirBtn = $false; $hasDebugBtn = $false; $hasLogBtn = $false
foreach ($el in $all) {
    if ($el.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button) {
        if ($el.Current.Name -match "修改游戏目录") { $hasDirBtn = $true }
        elseif ($el.Current.Name -match "调试模式") { $hasDebugBtn = $true }
        elseif ($el.Current.Name -match "查看插件日志") { $hasLogBtn = $true }
    }
}
Write-Test "维护页(修改目录/调试/插件日志按钮)" ($hasDirBtn -and $hasDebugBtn -and $hasLogBtn) "dir=$hasDirBtn debug=$hasDebugBtn log=$hasLogBtn"

# ---- crash check ----
$crashAfter = 0
if (Test-Path $crashLog) { $crashAfter = (Get-Content $crashLog).Count }
Write-Test "无新crash" ($crashAfter -le $crashBefore) "lines=$crashAfter/$crashBefore"

# ---- 汇总 ----
$fail = @($results | Where-Object { -not $_.OK })
Write-Output ("=== 汇总: {0}/{1} 通过 ===" -f ($results.Count - $fail.Count), $results.Count)
if ($fail.Count -gt 0) { exit 1 }
