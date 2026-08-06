# test_give2.ps1 — simple give flow: quantity 3 via individual evals
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

$occ = "(game.party.members[0].bag[0]!==void?1:0)+(game.party.members[0].bag[1]!==void?1:0)+(game.party.members[0].bag[2]!==void?1:0)+(game.party.members[0].bag[3]!==void?1:0)+(game.party.members[0].bag[4]!==void?1:0)"
$before = & $cli $occ 2>&1 | Select-String "= (\d+)" | ForEach-Object { $_.Matches[0].Groups[1].Value }
Write-Output "发放前占用 = $before"

Click $win "物品发放" | Out-Null
Start-Sleep -Milliseconds 400
Click $win "载入全部物品" | Out-Null
Start-Sleep -Seconds 12
Click $win "刷新目标" | Out-Null
Start-Sleep -Seconds 2
Select-ListIndex $win 1 | Out-Null
Start-Sleep -Milliseconds 300

# set quantity 3
$tc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc)
foreach ($e in $edits) {
    try {
        $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
        if ($vp.Current.Value -eq "1") { $vp.SetValue("3"); Write-Output "数量=3"; break }
    } catch {}
}

Click $win "发放" | Out-Null
Start-Sleep -Seconds 4

$after = & $cli $occ 2>&1 | Select-String "= (\d+)" | ForEach-Object { $_.Matches[0].Groups[1].Value }
Write-Output "发放后占用 = $after"
$diff = [int]$after - [int]$before
if ($diff -eq 3) { Write-Output "PASS: 占用 +3（简单逐条发放生效）" }
else { Write-Output "FAIL: 占用 +$diff" }

$crash = "C:\Users\1\Desktop\CR\DXCXN\trainer\crash.log"
if (Test-Path $crash) { Write-Output "FAIL: crash.log: $((Get-Content $crash | Select-Object -Last 3) -join ' | ')" } else { Write-Output "PASS: no crash" }
