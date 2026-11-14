$ErrorActionPreference = 'Stop'
$devDir = $PSScriptRoot
$gameDir = Split-Path $devDir -Parent

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Sonic SAMP Launcher - Release Packaging" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

Write-Host "`n[1/3] Building clean release binaries via build.bat..." -ForegroundColor Yellow
$proc = Start-Process -FilePath "cmd.exe" -ArgumentList "/c `"$devDir\build.bat`"" -WorkingDirectory $devDir -Wait -PassThru -NoNewWindow
if ($proc.ExitCode -ne 0) {
    Write-Error "Build failed with exit code $($proc.ExitCode)! Aborting packaging."
}

Write-Host "`n[2/3] Preparing release folder structure..." -ForegroundColor Yellow
$distDir = Join-Path $devDir "dist\release"
if (Test-Path $distDir) {
    Remove-Item $distDir -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $distDir "scripts") -Force | Out-Null

Copy-Item -LiteralPath (Join-Path $gameDir "SampLauncher.exe") -Destination $distDir -Force
Copy-Item -LiteralPath (Join-Path $gameDir "samp_core.dll") -Destination $distDir -Force

$asiPath = Join-Path $gameDir "scripts\SonicGuard.asi"
if (-not (Test-Path $asiPath)) {
    $asiPath = Join-Path $gameDir "scripts\SonicGuard.asi.disabled"
}
Copy-Item -LiteralPath $asiPath -Destination (Join-Path $distDir "scripts\SonicGuard.asi") -Force

$iniPath = Join-Path $gameDir "scripts\SonicGuard.ini"
if (Test-Path $iniPath) {
    Copy-Item -LiteralPath $iniPath -Destination (Join-Path $distDir "scripts\SonicGuard.ini") -Force
}

$readmeContent = @"
========================================================
  Sonic SAMP Launcher (Cyber Edition v2.4)
  High-Performance SA-MP 0.3.7 R2 Client with Rust Engine
========================================================

[วิธีติดตั้ง / Installation]
1. นำไฟล์ทั้งหมดในโฟลเดอร์นี้ไปวางในโฟลเดอร์เกม GTA San Andreas ของคุณ
   (Copy all files from this folder directly into your GTA San Andreas root folder)

2. เปิดโปรแกรมด้วย 'SampLauncher.exe'
   (Run 'SampLauncher.exe')

3. ตั้งชื่อตัวละครและกดเข้าเล่นเซิร์ฟเวอร์ที่คุณชื่นชอบได้ทันที!
   (Enter your player nickname, select your favorite server, and launch the game!)

[ไฟล์ที่รวมอยู่ในชุดนี้ / Included Files]
- SampLauncher.exe       : ตัวโปรแกรม Launcher (C# WinForms, Cyber Edition UI)
- samp_core.dll          : ระบบหลังบ้าน Rust ความเร็วสูง (Tokio Async, SQLite, DWM)
- scripts\SonicGuard.asi : ตัวช่วยรันเกม Rust (Crash Interceptor, 1ms Timer, Borderless)
- scripts\SonicGuard.ini : ไฟล์ตั้งค่า SonicGuard

[ความต้องการระบบ / Requirements]
- Windows 10 / 11 (รองรับทั้ง 32-bit และ 64-bit)
- .NET Framework 4.0 หรือใหม่กว่า (มีมาพร้อมกับ Windows อยู่แล้ว)
- ตัวเกม GTA San Andreas v1.0 US พร้อมติดตั้ง SA-MP 0.3.7 R2
"@
[System.IO.File]::WriteAllText((Join-Path $distDir "README_RELEASE.txt"), $readmeContent, [System.Text.Encoding]::UTF8)

Write-Host "`n[3/3] Creating ZIP archive..." -ForegroundColor Yellow
$distParent = Join-Path $devDir "dist"
$zipPath = Join-Path $distParent "SonicSampLauncher_v2.4_Portable.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -Force

if (Test-Path $zipPath) {
    $zipItem = Get-Item $zipPath
    Write-Host "`n========================================================" -ForegroundColor Green
    Write-Host "  PACKAGE SUCCESS!" -ForegroundColor Green
    Write-Host "  Archive: $($zipItem.FullName) ($([math]::Round($zipItem.Length / 1MB, 2)) MB)" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
} else {
    Write-Error "Failed to create ZIP archive."
}
