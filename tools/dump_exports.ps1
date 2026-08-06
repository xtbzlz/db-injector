Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class D32 {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr LoadLibrary(string name);
    [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr h);
    [DllImport("kernel32.dll", CharSet=CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr h, string name);
}
'@
$dll = $env:DZB_DLL
$h = [D32]::LoadLibrary($dll)
Write-Output "dll=$dll handle=$h"
if ($h -eq [IntPtr]::Zero) { Write-Output "LOAD FAILED err=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())"; exit }
foreach ($n in @('TVPGetGlobals','TVPGetScriptEngine','TVPExecuteScript','TVPCompileScript','V2Link','TVPGetFunctionExporter','TVPExecScript','TJSGetGlobals','Hook','GetProcAddress','TVPGetScriptDispatch','TVPEvalScript','tjsEval')) {
    $p = [D32]::GetProcAddress($h, $n)
    if ($p -ne [IntPtr]::Zero) { Write-Output ("EXPORT: " + $n + " -> 0x" + $p.ToInt64().ToString('X')) }
}
[D32]::FreeLibrary($h) | Out-Null
