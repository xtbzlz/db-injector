# test_state.ps1 — give item with stat=-12 via UI, verify bagStat
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

$trainer = "C:\Users\1\Desktop\CR\DXCXN\trainer\Trainer.exe"
$cli = "C:\Users\1\Desktop\CR\DXCXN\tools\TbcCli.exe"

function Find-Elem($root, $name, $type) {
    $nc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    $tc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, $type)
    $and = New-Object System.Windows.Automation.AndCondition($nc, $tc)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $and)
}

function Click($root, $name) {
    $btn = Find-Elem $root $name ([System.Windows.Automation.ControlType]::Button)
    if (-not $btn) { return $false }
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    return $true
}

function Select-ListIndex($root, $index) {
    $lc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $lc)
    if ($index -ge $items.Count) { return $false }
    $items[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    return $true
}

Get-Process Trainer -ErrorAction SilentlyContinue | ForEach-Object { $_.Kill() }
Start-Sleep -Milliseconds 800
$p = Start-Process $trainer -PassThru
Start-Sleep -Seconds 5
$cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $p.Id)
$win = $null
for ($i = 0; $i -lt 20; $i++) {
    $win = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
    if ($win) { break }
    Start-Sleep -Milliseconds 500
}
if (-not $win) { Write-Output "FAIL: no window"; exit 1 }

Click $win "物品发放" | Out-Null
Start-Sleep -Milliseconds 400
Click $win "载入全部物品" | Out-Null
Start-Sleep -Seconds 12
Click $win "刷新目标" | Out-Null
Start-Sleep -Seconds 2
Select-ListIndex $win 5 | Out-Null   # items[5]
Start-Sleep -Milliseconds 300

# select state combo: "完整·未鉴定(-12)" = ComboBox index 1
$cc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox)
$combos = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cc)
if ($combos.Count -ge 2) {
    $stateCombo = $combos[1]
    $expand = $stateCombo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern)
    $expand.Expand()
    Start-Sleep -Milliseconds 400
    $lc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $stateCombo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $lc)
    if ($items.Count -ge 2) {
        $items[1].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
        Write-Output "状态已选: 完整·未鉴定(-12)"
    } else {
        Write-Output "WARN: 状态项不足"
    }
    try { $expand.Collapse() } catch {}
} else {
    Write-Output "WARN: ComboBox 数量 $($combos.Count)"
}

Click $win "发放" | Out-Null
Start-Sleep -Seconds 3

$stats = & $cli "game.party.members[0].bagStat[0]" "game.party.members[0].bagStat[1]" "game.party.members[0].bagStat[2]" "game.party.members[0].bagStat[3]" 2>&1 | Out-String
Write-Output "bagStat: $stats"
if ($stats -match "-12") { Write-Output "PASS: 未鉴定物品发放成功" } else { Write-Output "FAIL: 未找到 -12 状态" }

$crash = "C:\Users\1\Desktop\CR\DXCXN\trainer\crash.log"
if (Test-Path $crash) { Write-Output "FAIL: crash.log: $((Get-Content $crash | Select-Object -Last 3) -join ' | ')" } else { Write-Output "PASS: no crash" }
