# 🚀 Sonic SAMP Launcher (Cyber Edition v2.4)

> High-Performance SA-MP 0.3.7 R2 Multiplayer Client powered by **Rust Engine** (`samp_core.dll`) & In-Game Guard (`SonicGuard.asi`).

[![Platform](https://img.shields.io/badge/platform-Windows%20(x86%20%7C%20x64)-blue.svg)](#)
[![Rust](https://img.shields.io/badge/rust-2021%20edition-orange.svg)](https://www.rust-lang.org/)
[![.NET](https://img.shields.io/badge/.NET%20Framework-4.0%2B-purple.svg)](#)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

---

## 🇹🇭 ภาษาไทย (Thai)

### 🌟 คุณสมบัติเด่น (Features)
- **Rust-First Core Engine (`samp_core.dll`)**:
  - ประมวลผล Network UDP Query, Ping Sparkwave, RCON, และ File I/O แบบ Asynchronous ผ่าน Tokio runtime
  - ฐานข้อมูลเซิร์ฟเวอร์โปรดและประวัติการเล่นจัดเก็บใน SQLite ความเร็วสูง พร้อมดัชนีค้นหาเร็วพิเศษ
  - ป้องกันรหัสผ่านเซิร์ฟเวอร์ด้วย Windows Data Protection API (DPAPI) เข้ารหัสเฉพาะเครื่อง
  - ติดตามชั่วโมงการเล่น (Playtime Watchdog) และวิเคราะห์บันทึกแครชอัตโนมัติ
- **SonicGuard Game ASI (`scripts\SonicGuard.asi`)**:
  - ดักจับแครชในเกมพร้อมแปลงตำแหน่ง Memory และ Stack Trace เป็นภาษาที่เข้าใจง่าย
  - โหมดไร้ขอบ (Borderless Fullscreen) สลับหน้าจอได้ไม่สะดุด
  - ยกระดับตัวนับเวลาความละเอียดสูง (1ms High-Precision Timer) ลด Micro-stutter
  - ปรับปรุงการส่งต่อแพ็กเก็ต UDP Voice และลดการดีเลย์ไมโครโฟน
- **Modern Cyber UI (`SampLauncher.exe`)**:
  - หน้าต่างดีไซน์ Cyber Dark พร้อมระบบ Smooth GDI+ Double Buffering และ DWM Accent Integration
  - รองรับ DPI Scaling สมบูรณ์แบบ (100%, 125%, 150%, 200%)
  - ระบบคัดกรองเซิร์ฟเวอร์เรียลไทม์ (ทั้งหมด, เซิร์ฟเวอร์โปรด, ประวัติ, ซ่อนเซิร์ฟเวอร์เต็ม/ว่าง/มีรหัส)
  - เครื่องมือตรวจสุขภาพเกมและม็อดอัตโนมัติ (LAA 4GB, LimitAdjuster, Stream memory, SkyGfx, SilentPatch)

---

### 📦 วิธีติดตั้งสำหรับผู้เล่น (Player Installation)
1. ดาวน์โหลดไฟล์ `SonicSampLauncher_v2.4_Portable.zip` จากแท็บ **Releases**
2. แตกไฟล์ทั้งหมดลงในโฟลเดอร์หลักของเกม **GTA San Andreas** ของคุณ
3. เปิดไฟล์ `SampLauncher.exe` เพื่อเข้าเล่นได้ทันที!

---

### 🛠️ วิธีการคอมไพล์จากโค้ดต้นฉบับ (Building from Source)

#### สิ่งที่จำเป็นในการคอมไพล์ (Prerequisites)
1. **Rust (MSVC toolchain i686)**:
   ```bash
   rustup target add i686-pc-windows-msvc
   ```
2. **.NET Framework 4.0 หรือใหม่กว่า** (มี `csc.exe` ใน `C:\Windows\Microsoft.NET\Framework\v4.0.30319`)

#### ขั้นตอนการคอมไพล์
รันสคริปต์ `build.bat` จากโฟลเดอร์โครงการ:
```cmd
build.bat
```
หรือหากต้องการแพ็กเป็น ZIP พร้อมแจกจ่าย:
```cmd
package_release.bat
```

---

## 🇬🇧 English

### 🌟 Key Highlights
- **High Performance Rust Backend**: Tokio-powered asynchronous UDP server query, real-time live ping wave, and bundled SQLite database for favorite & history management.
- **In-Game Guard (`SonicGuard.asi`)**: Lightweight Rust ASI plugin providing SEH crash interception, 1ms system timer resolution, borderless windowed mode, and voice packet prewarming.
- **Modern Windows Forms Frontend**: Ultra-responsive dark cyber theme, hardware DPI awareness, DWM window accents, and Discord Rich Presence.
- **Game Health Diagnostics**: Detects and auto-fixes 4GB LAA flags, streaming memory limits, modloader priorities, and configuration conflicts.

### 📜 License
Distributed under the **MIT License**. See `LICENSE` for more information.
