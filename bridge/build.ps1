# Build tb_bridge.dll with zig cc
$zig = "C:\Users\1\Desktop\CR\DXCXN\tools\zig-x86_64-windows-0.16.0\zig.exe"
if (-not (Test-Path $zig)) { throw "zig not found: $zig" }
New-Item -ItemType Directory -Path "C:\Users\1\Desktop\CR\DXCXN\bridge\bin" -Force | Out-Null
& $zig cc -target x86-windows-gnu -shared -O2 `
    -o "C:\Users\1\Desktop\CR\DXCXN\bridge\bin\tb_bridge.dll" `
    "C:\Users\1\Desktop\CR\DXCXN\bridge\src\bridge.c" `
    "C:\Users\1\Desktop\CR\DXCXN\bridge\src\bridge.def"
if ($LASTEXITCODE -ne 0) { throw "build failed" }
Write-Output "OK: $((Get-Item 'C:\Users\1\Desktop\CR\DXCXN\bridge\bin\tb_bridge.dll').Length) bytes"
