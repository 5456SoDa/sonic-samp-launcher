# =====================================================================
# ทำความสะอาด server_stats.txt ของ SampLauncher
#   1) สำรองไฟล์เดิมไปยัง _launcher_dev/backup/
#   2) ลบบรรทัดที่ซ้ำกันเป๊ะ ๆ (บั๊ก launcher บันทึกซ้ำตอน refresh/ปิดโปรแกรม)
#   3) แปลงปี พ.ศ. (>=2400) เป็น ค.ศ. เพื่อให้ sort/parse ข้ามเครื่องได้
# ใช้: powershell -NoProfile -ExecutionPolicy Bypass -File clean_server_stats.ps1
#      สวิตช์: -KeepBuddhistYear  (ลบเฉพาะซ้ำ ไม่แปลงปี)
# =====================================================================
param([switch]$KeepBuddhistYear)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot    # _launcher_dev
$game = Split-Path -Parent $root            # โฟลเดอร์เกม
$src  = Join-Path $game 'server_stats.txt'

if (-not (Test-Path $src)) { Write-Error "ไม่พบ $src"; exit 1 }

# 1) สำรองก่อนเสมอ
$backupDir = Join-Path $root 'backup'
New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
$stamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
$backup = Join-Path $backupDir ("server_stats_$stamp.bak")
Copy-Item $src $backup -Force

# 2+3) dedup บรรทัดที่ซ้ำกันเป๊ะ + แปลงปี พ.ศ. -> ค.ศ.
$lines = [IO.File]::ReadAllLines($src, [System.Text.Encoding]::UTF8) | Where-Object { $_.Trim() -ne '' }
$before = @($lines).Count

$out  = New-Object 'System.Collections.Generic.List[string]'
$seen = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($line in $lines) {
    $parts = $line -split '\|'
    if (-not $KeepBuddhistYear -and $parts.Count -ge 4 -and $parts[2].Length -ge 4) {
        $y = 0
        if ([int]::TryParse($parts[2].Substring(0,4), [ref]$y) -and $y -ge 2400) {
            $parts[2] = ($y - 543).ToString('0000') + $parts[2].Substring(4)
        }
    }
    $fixed = $parts -join '|'
    if ($seen.Add($fixed)) { $out.Add($fixed) }
}
[IO.File]::WriteAllLines($src, $out, [System.Text.Encoding]::UTF8)

Write-Host ("Backup  : " + $backup)
Write-Host ("Before  : " + $before + " lines")
Write-Host ("After   : " + $out.Count + " lines (removed " + ($before - $out.Count) + " duplicate lines)")
if (-not $KeepBuddhistYear) { Write-Host "Years   : พ.ศ. -> ค.ศ. แล้ว (เช่น 2569 -> 2026)" }
