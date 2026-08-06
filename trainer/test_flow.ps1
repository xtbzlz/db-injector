# test_flow.ps1 — end-to-end functional test: give item + learn magic via UI
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
    if (-not $btn) { Write-Output "FAIL: button not found: $name"; return $false }
    $btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    return $true
}

function Select-ListIndex($root, $index) {
    # find all list items and select the given one
    $lc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $items = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $lc)
    if ($index -ge $items.Count) { Write-Output "FAIL: list item $index out of range ($($items.Count))"; return $false }
    $items[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    return $true
}

function G() { & $cli "game.party.members[0].bag.count" "game.party.members[0].mmagic[0]" 2>&1 | Out-String }

# fresh trainer
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
if (-not $win) { Write-Output "FAIL: window not found"; exit 1 }

Write-Output "== 初始状态 =="
G

Write-Output "== 物品页: 载入物品+目标+发放 =="
Click $win "物品发放" | Out-Null
Start-Sleep -Milliseconds 400
Click $win "载入全部物品" | Out-Null
Start-Sleep -Seconds 12
Click $win "刷新目标" | Out-Null
Start-Sleep -Seconds 2
Select-ListIndex $win 1 | Out-Null   # items[1]
Start-Sleep -Milliseconds 300
Click $win "发放" | Out-Null
Start-Sleep -Seconds 2
Write-Output "== 发放后 bag.count =="
G

Write-Output "== 魔法页: 目标+载入+学会 =="
Click $win "魔法学习" | Out-Null
Start-Sleep -Milliseconds 400
Click $win "刷新目标" | Out-Null
Start-Sleep -Seconds 2
Click $win "载入精灵魔法" | Out-Null
Start-Sleep -Seconds 2
Select-ListIndex $win 0 | Out-Null   # magic[0]
Start-Sleep -Milliseconds 300
Click $win "学会所选" | Out-Null
Start-Sleep -Seconds 2
Write-Output "== 学会后 mmagic[0] =="
G

Write-Output "== 进程状态 =="
if ($p.HasExited) { Write-Output "FAIL: trainer exited" } else { Write-Output "PASS: trainer alive" }
$crash = "C:\Users\1\Desktop\CR\DXCXN\trainer\crash.log"
if (Test-Path $crash) { Write-Output "FAIL: crash.log: $((Get-Content $crash | Select-Object -Last 3) -join ' | ')" } else { Write-Output "PASS: no crash" }
