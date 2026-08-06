Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class PEProbe6 {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr LoadLibrary(string name);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr h, string name);
}
'@
$plug = $env:DZB_PLUGIN_PATH
Write-Output "plugin: $plug"
$h = [PEProbe6]::LoadLibrary($plug)
Write-Output "handle=$h"
foreach ($n in @('V2Link','V2Link@4','_V2Link@4','V2Unlink','V2Unlink@0','_V2Unlink@0','TVPRegisterImportFuncs')) {
    $p = [PEProbe6]::GetProcAddress($h, $n)
    if ($p -ne [IntPtr]::Zero) { Write-Output ("FOUND: " + $n + " -> 0x" + $p.ToInt64().ToString('X')) }
}
