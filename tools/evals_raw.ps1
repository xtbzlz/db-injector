$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class PipeRaw2 {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
}
'@
$h = [PipeRaw2]::CreateFileW('\\.\pipe\tbc_bridge', [uint32]3221225472, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
if ($h -eq -1) { Write-Output "connect failed"; exit }
$code = [System.IO.File]::ReadAllText($args[0], [System.Text.Encoding]::UTF8)
$codeBytes = [System.Text.Encoding]::UTF8.GetBytes($code)
$head = [System.Text.Encoding]::ASCII.GetBytes("EVALS`n")
$len = [BitConverter]::GetBytes([int]$codeBytes.Length)
$w = 0
[PipeRaw2]::WriteFile($h, $head, [uint32]$head.Length, [ref]$w, [IntPtr]::Zero) | Out-Null
[PipeRaw2]::WriteFile($h, $len, 4, [ref]$w, [IntPtr]::Zero) | Out-Null
[PipeRaw2]::WriteFile($h, $codeBytes, [uint32]$codeBytes.Length, [ref]$w, [IntPtr]::Zero) | Out-Null
$buf = New-Object byte[] 4096
$read = 0
[PipeRaw2]::ReadFile($h, $buf, 4096, [ref]$read, [IntPtr]::Zero) | Out-Null
if ($read -gt 0) { Write-Output ("RESP: " + [System.Text.Encoding]::UTF8.GetString($buf, 0, $read)) }
[PipeRaw2]::CloseHandle($h) | Out-Null
