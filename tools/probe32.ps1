Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class PEProbe4 {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr LoadLibrary(string name);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr h, string name);
    [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr h);
}
'@
$h = [PEProbe4]::LoadLibrary("C:\Users\1\Desktop\CR\DXCXN\bridge\bin\tb_bridge.dll")
Write-Output "handle=$h"
foreach ($n in @('V2Link','V2Link@4','V2Link@8','_V2Link','_V2Link@4','V2Unlink','V2Unlink@0','_V2Unlink')) {
    $p = [PEProbe4]::GetProcAddress($h, $n)
    if ($p -ne [IntPtr]::Zero) { Write-Output "FOUND: $n -> 0x$('{0:X}' -f $p.ToInt64())" }
}
[PEProbe4]::FreeLibrary($h) | Out-Null
