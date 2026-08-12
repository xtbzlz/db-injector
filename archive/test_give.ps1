# test_give.ps1 — give 3 items via UI, verify bag grows by exactly 3
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
    if (-not $btn) { Write-Output "FAIL: no button $name"; return $false }
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

$before = (& $cli "game.party.members[0].bag.count" 2>&1 | Select-String "= (\d+)" | ForEach-Object { $_.Matches[0].Groups[1].Value })
Write-Output "发放前 bag.count = $before"

Click $win "物品发放" | Out-Null
Start-Sleep -Milliseconds 400
Click $win "载入全部物品" | Out-Null
Start-Sleep -Seconds 12
Click $win "刷新目标" | Out-Null
Start-Sleep -Seconds 2
Select-ListIndex $win 1 | Out-Null   # items[1]
Start-Sleep -Milliseconds 300

# set quantity to 3: find the count TextBox (second textbox on the page)
$tc = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
$edits = $win.FindAll([System.Windows.Automation.TreeScope]::Descendants, $tc)
$countBox = $null
foreach ($e in $edits) { if ($e.Current.Name -eq "" -or $e.Current.Name -eq $e.Current.HelpText) { $countBox = $e; break } }
# fallback: the edit with value "1"
foreach ($e in $edits) {
    $vp = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    if ($vp.Current.Value -eq "1") { $countBox = $e; break }
}
if ($countBox) {
    $vp = $countBox.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $vp.SetValue("3")
    Write-Output "数量已设为 3"
} else {
    Write-Output "WARN: 未找到数量框，使用默认 1"
}

Click $win "发放" | Out-Null
Start-Sleep -Seconds 3

$after = (& $cli "game.party.members[0].bag.count" 2>&1 | Select-String "= (\d+)" | ForEach-Object { $_.Matches[0].Groups[1].Value })
Write-Output "发放后 bag.count = $after"
$diff = [int]$after - [int]$before
if ($diff -eq 3) { Write-Output "PASS: 背包精确 +3" }
elseif ($diff -eq 1) { Write-Output "PARTIAL: +1（数量框可能未找到）" }
else { Write-Output "FAIL: 背包 +$diff（异常！）" }

$crash = "C:\Users\1\Desktop\CR\DXCXN\trainer\crash.log"
if (Test-Path $crash) { Write-Output "FAIL: crash.log: $((Get-Content $crash | Select-Object -Last 3) -join ' | ')" } else { Write-Output "PASS: no crash" }
