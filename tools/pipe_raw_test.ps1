$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class PipeRaw {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr sa, uint disp, uint flags, IntPtr tpl);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool ReadFile(IntPtr h, byte[] buf, uint n, out uint read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError=true)] public static extern bool WriteFile(IntPtr h, byte[] buf, uint n, out uint written, IntPtr ov);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
}
'@
$h = [PipeRaw]::CreateFileW('\\.\pipe\tbc_bridge', [uint32]3221225472, 3, [IntPtr]::Zero, 3, 0, [IntPtr]::Zero)
Write-Output "handle=$h"
$code = [System.Text.Encoding]::UTF8.GetBytes('1+1')
$head = [System.Text.Encoding]::ASCII.GetBytes("EVAL`n")
$len = [BitConverter]::GetBytes([int]$code.Length)
$w = 0
[PipeRaw]::WriteFile($h, $head, [uint32]$head.Length, [ref]$w, [IntPtr]::Zero) | Out-Null
[PipeRaw]::WriteFile($h, $len, 4, [ref]$w, [IntPtr]::Zero) | Out-Null
[PipeRaw]::WriteFile($h, $code, [uint32]$code.Length, [ref]$w, [IntPtr]::Zero) | Out-Null
Write-Output "sent $($head.Length)+4+$($code.Length) bytes"
$buf = New-Object byte[] 1024
$read = 0
$ok = [PipeRaw]::ReadFile($h, $buf, 1024, [ref]$read, [IntPtr]::Zero)
Write-Output "ReadFile ok=$ok read=$read err=$([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
if ($read -gt 0) {
    $txt = [System.Text.Encoding]::UTF8.GetString($buf, 0, $read)
    Write-Output "response bytes: $([BitConverter]::ToString($buf, 0, $read))"
    Write-Output "response text: [$txt]"
}
[PipeRaw]::CloseHandle($h) | Out-Null
