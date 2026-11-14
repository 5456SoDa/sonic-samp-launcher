use crate::health;
use crate::win_ro;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use std::path::Path;
use windows_sys::Win32::Foundation::HANDLE;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows_sys::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::SystemInformation::{
    GetSystemInfo, GlobalMemoryStatusEx, MEMORYSTATUSEX, SYSTEM_INFO,
};
use windows_sys::Win32::System::Threading::{
    GetCurrentProcess, SetPriorityClass, SetProcessPriorityBoost, HIGH_PRIORITY_CLASS,
};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum ResourceMode {
    Auto,
    Max,
    Conservative,
}

impl ResourceMode {
    pub fn from_str_lossy(s: &str) -> Self {
        let t = s.trim().to_ascii_lowercase();
        match t.as_str() {
            "max" | "high" | "1024" => ResourceMode::Max,
            "conservative" | "low" | "512" => ResourceMode::Conservative,
            _ => ResourceMode::Auto,
        }
    }

    pub fn as_ini_str(self) -> &'static str {
        match self {
            ResourceMode::Auto => "auto",
            ResourceMode::Max => "max",
            ResourceMode::Conservative => "conservative",
        }
    }
}

#[derive(Debug, Clone, Serialize)]
pub struct SystemResourceInfo {
    pub total_phys_mb: u64,
    pub avail_phys_mb: u64,
    pub memory_load_pct: u32,
    pub logical_cores: u32,
    pub recommended_stream_mb: u32,
    pub recommended_limit_mb: u32,
    pub launcher_working_set_mb: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SonicGuardSettings {
    pub enable: bool,
    pub borderless: bool,
    pub catch_crash: bool,
    pub high_precision_timer: bool,
    pub telemetry: bool,
    pub net_shield: bool,
    pub audio_shield: bool,
    pub preferred_mic: String,
    pub gpu_boost: bool,
    pub force_immediate_present: bool,
    pub resource_mode: String,
    pub cpu_affinity: String,
}

impl Default for SonicGuardSettings {
    fn default() -> Self {
        Self {
            enable: true,
            borderless: false,
            catch_crash: true,
            high_precision_timer: true,
            telemetry: true,
            net_shield: true,
            audio_shield: true,
            preferred_mic: String::new(),
            gpu_boost: true,
            force_immediate_present: true,
            resource_mode: "auto".to_string(),
            cpu_affinity: "auto".to_string(),
        }
    }
}

pub fn compute_adaptive_stream_budget_kb(avail_phys_mb: u64, mode: ResourceMode) -> (u32, u32) {
    match mode {
        ResourceMode::Max => (1_048_576, 1024),
        ResourceMode::Conservative => (524_288, 512),
        ResourceMode::Auto => {
            if avail_phys_mb >= 4096 {
                (1_048_576, 1024)
            } else if avail_phys_mb >= 2048 {
                (786_432, 768)
            } else {
                (524_288, 512)
            }
        }
    }
}

fn current_working_set_mb() -> u64 {
    unsafe {
        let mut pmc: PROCESS_MEMORY_COUNTERS_EX = std::mem::zeroed();
        pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
        if GetProcessMemoryInfo(GetCurrentProcess(), &mut pmc as *mut _ as _, pmc.cb) != 0 {
            return (pmc.WorkingSetSize as u64) / (1024 * 1024);
        }
    }
    0
}

pub fn query_system_memory(mode: ResourceMode) -> SystemResourceInfo {
    let mut ms: MEMORYSTATUSEX = unsafe { std::mem::zeroed() };
    ms.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
    unsafe {
        GlobalMemoryStatusEx(&mut ms);
    }

    let mut si: SYSTEM_INFO = unsafe { std::mem::zeroed() };
    unsafe {
        GetSystemInfo(&mut si);
    }

    let total_phys_mb = (ms.ullTotalPhys) / (1024 * 1024);
    let avail_phys_mb = (ms.ullAvailPhys) / (1024 * 1024);
    let (kb, mb) = compute_adaptive_stream_budget_kb(avail_phys_mb, mode);

    SystemResourceInfo {
        total_phys_mb,
        avail_phys_mb,
        memory_load_pct: ms.dwMemoryLoad,
        logical_cores: si.dwNumberOfProcessors.max(1),
        recommended_stream_mb: kb / 1024,
        recommended_limit_mb: mb,
        launcher_working_set_mb: current_working_set_mb(),
    }
}

#[repr(C)]
struct ProcessPowerThrottlingState {
    version: u32,
    control_mask: u32,
    state_mask: u32,
}

/// Applies temporary, process-local CPU & power-throttling boosts to `gta_sa.exe` ONLY.
/// Uses HIGH_PRIORITY_CLASS (never REALTIME) and disables EcoQoS; ends automatically when game closes.
pub fn apply_process_launch_boost(h_process: HANDLE) {
    if h_process.is_null() {
        return;
    }
    unsafe {
        SetPriorityClass(h_process, HIGH_PRIORITY_CLASS);
        SetProcessPriorityBoost(h_process, 0);

        let mut sys_info: SYSTEM_INFO = std::mem::zeroed();
        GetSystemInfo(&mut sys_info);
        let num_cpus = sys_info.dwNumberOfProcessors;
        if num_cpus > 4 {
            let mask: usize = if num_cpus >= 16 {
                0x00FF
            } else if num_cpus >= 8 {
                0x003F
            } else {
                0x000F
            };
            let k32_w = win_ro::to_wide("kernel32.dll");
            let h_k32 = GetModuleHandleW(k32_w.as_ptr());
            if !h_k32.is_null() {
                if let Some(p) = GetProcAddress(h_k32, b"SetProcessAffinityMask\0".as_ptr()) {
                    type FnSetAffinity = unsafe extern "system" fn(HANDLE, usize) -> i32;
                    let set_aff: FnSetAffinity = std::mem::transmute(p);
                    let _ = set_aff(h_process, mask);
                }
            }
        }

        let k32_w = win_ro::to_wide("kernel32.dll");
        let h_k32 = GetModuleHandleW(k32_w.as_ptr());
        if !h_k32.is_null() {
            if let Some(p) = GetProcAddress(h_k32, b"SetProcessInformation\0".as_ptr()) {
                type FnSetProcInfo = unsafe extern "system" fn(HANDLE, u32, *const ProcessPowerThrottlingState, u32) -> i32;
                let set_info: FnSetProcInfo = std::mem::transmute(p);
                let state = ProcessPowerThrottlingState {
                    version: 1,
                    control_mask: 1, // PROCESS_POWER_THROTTLING_EXECUTION_SPEED
                    state_mask: 0,   // Force off
                };
                // ProcessPowerThrottling == 4
                let _ = set_info(
                    h_process,
                    4,
                    &state,
                    std::mem::size_of::<ProcessPowerThrottlingState>() as u32,
                );
            }
        }
    }
}

/// Trims working-set pages of the current Launcher process ONLY to free physical RAM for the game.
pub fn trim_current_process_working_set() -> u64 {
    unsafe {
        let k32_w = win_ro::to_wide("kernel32.dll");
        let h_k32 = GetModuleHandleW(k32_w.as_ptr());
        if !h_k32.is_null() {
            if let Some(p) = GetProcAddress(h_k32, b"SetProcessWorkingSetSize\0".as_ptr()) {
                type FnTrim = unsafe extern "system" fn(HANDLE, usize, usize) -> i32;
                let trim_fn: FnTrim = std::mem::transmute(p);
                let _ = trim_fn(GetCurrentProcess(), usize::MAX, usize::MAX);
            }
        }
    }
    current_working_set_mb()
}

fn parse_bool(v: &str) -> bool {
    let t = v.trim();
    t == "1" || t.eq_ignore_ascii_case("true")
}

fn strip_inline_comment(s: &str) -> &str {
    let mut cut = s.len();
    if let Some(i) = s.find(';') {
        cut = cut.min(i);
    }
    if let Some(i) = s.find(" #") {
        cut = cut.min(i);
    }
    if let Some(i) = s.find("\t#") {
        cut = cut.min(i);
    }
    s[..cut].trim()
}

pub fn read_sonic_guard_settings(game_dir: &Path) -> SonicGuardSettings {
    let mut cfg = SonicGuardSettings::default();
    let ini_path = game_dir.join("scripts").join("SonicGuard.ini");
    let Ok(raw) = fs::read_to_string(&ini_path) else {
        return cfg;
    };
    for line in raw.lines() {
        let t = line.trim().trim_start_matches('\u{feff}');
        if t.is_empty() || t.starts_with(';') || t.starts_with('#') || t.starts_with('[') {
            continue;
        }
        if let Some((k, v)) = t.split_once('=') {
            let kl = k.trim().to_ascii_lowercase();
            let val = strip_inline_comment(v);
            match kl.as_str() {
                "enable" | "enabled" => cfg.enable = parse_bool(val),
                "borderless" => cfg.borderless = parse_bool(val),
                "catchcrash" => cfg.catch_crash = parse_bool(val),
                "highprecisiontimer" => cfg.high_precision_timer = parse_bool(val),
                "telemetry" => cfg.telemetry = parse_bool(val),
                "netshield" => cfg.net_shield = parse_bool(val),
                "audioshield" => cfg.audio_shield = parse_bool(val),
                "preferredmic" => cfg.preferred_mic = val.to_string(),
                "gpuboost" => cfg.gpu_boost = parse_bool(val),
                "forceimmediatepresent" => cfg.force_immediate_present = parse_bool(val),
                "resourcemode" => cfg.resource_mode = ResourceMode::from_str_lossy(val).as_ini_str().to_string(),
                "cpuaffinity" | "affinity" => cfg.cpu_affinity = val.to_string(),
                _ => {}
            }
        }
    }
    cfg
}

const DEFAULT_SONIC_GUARD_INI: &str = "\
[SonicGuard]\r\n\
; เปิด/ปิดระบบคุ้มกันและปรับแต่ง (1 = เปิด, 0 = ปิด)\r\n\
Enable = 1\r\n\
; โหมด Borderless Windowed หน้าต่างไร้ขอบ (0 = ปิด, 1 = เปิด ช่วยแก้ปัญหาจอดำเวลา Alt+Tab)\r\n\
Borderless = 0\r\n\
; ระบบจับข้อผิดพลาด Crash Analyzer (1 = เปิด, 0 = ปิด)\r\n\
CatchCrash = 1\r\n\
; ระบบปรับความละเอียดไทม์เมอร์ 1ms เพื่อลดเฟรมดรอป (1 = เปิด, 0 = ปิด)\r\n\
HighPrecisionTimer = 1\r\n\
; ระบบตรวจสอบการใช้ RAM telemetry (1 = เปิด, 0 = ปิด)\r\n\
Telemetry = 1\r\n\
; ป้องกันซ็อกเก็ต UDP หลุด (SIO_UDP_CONNRESET & Buffer 256KB, Anti-Cheat Safe)\r\n\
NetShield = 1\r\n\
; ป้องกันไมโครโฟนหลุด/กระตุก (MMCSS Pro Audio + ล็อกไมค์ตาม PreferredMic)\r\n\
AudioShield = 1\r\n\
; ชื่อไมโครโฟนสำหรับใช้ในเกม (ว่าง = อัตโนมัติ, ไม่แก้ค่า Windows OS)\r\n\
PreferredMic =\r\n\
; เร่ง GPU Scheduler Priority และปิด EcoQoS ชั่วคราวเฉพาะตอนเล่นเกม\r\n\
GpuBoost = 1\r\n\
; จัด PresentationInterval ลด Stutter เมื่อปลดล็อก FPS (เข้ากันได้กับ sampvoice/skygfx)\r\n\
ForceImmediatePresent = 1\r\n\
; โหมดจัดสรร RAM สตรีม (auto | max | conservative)\r\n\
ResourceMode = auto\r\n\
; ระบบจัดสรรคอร์ CPU Affinity (auto | p_cores | all)\r\n\
CpuAffinity = auto\r\n";

/// Updates keys strictly inside `<game_dir>\scripts\SonicGuard.ini`. Creates default INI if absent.
pub fn save_sonic_guard_settings(game_dir: &Path, updates: &HashMap<String, String>) -> Result<(), String> {
    let scripts_dir = game_dir.join("scripts");
    if !scripts_dir.exists() {
        fs::create_dir_all(&scripts_dir)
            .map_err(|e| format!("สร้างโฟลเดอร์ scripts ไม่ได้: {}", e))?;
    }
    let ini_path = scripts_dir.join("SonicGuard.ini");
    if !ini_path.exists() {
        fs::write(&ini_path, DEFAULT_SONIC_GUARD_INI)
            .map_err(|e| format!("สร้าง SonicGuard.ini ไม่ได้: {}", e))?;
    }
    let cleaned_pairs: Vec<(String, String)> = updates
        .iter()
        .map(|(k, v)| {
            (
                k.replace(['\r', '\n', '='], "").trim().to_string(),
                v.replace(['\r', '\n'], "").trim().to_string(),
            )
        })
        .filter(|(k, _)| !k.is_empty())
        .collect();
    let owned_pairs: Vec<(&str, &str)> = cleaned_pairs
        .iter()
        .map(|(k, v)| (k.as_str(), v.as_str()))
        .collect();
    if !owned_pairs.is_empty() {
        health::update_ini_keys(&ini_path, &owned_pairs, true, Some("SonicGuard"))?;
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_adaptive_stream_budget() {
        assert_eq!(compute_adaptive_stream_budget_kb(1150, ResourceMode::Auto), (524_288, 512));
        assert_eq!(compute_adaptive_stream_budget_kb(2500, ResourceMode::Auto), (786_432, 768));
        assert_eq!(compute_adaptive_stream_budget_kb(6144, ResourceMode::Auto), (1_048_576, 1024));
        assert_eq!(compute_adaptive_stream_budget_kb(1024, ResourceMode::Max), (1_048_576, 1024));
        assert_eq!(compute_adaptive_stream_budget_kb(8192, ResourceMode::Conservative), (524_288, 512));
    }

    #[test]
    fn test_save_and_read_sonic_guard_settings() {
        let tmp = std::env::temp_dir().join("sonic_guard_ini_test_v2");
        let _ = fs::remove_dir_all(&tmp);
        fs::create_dir_all(&tmp).unwrap();

        let mut map = HashMap::new();
        map.insert("PreferredMic".to_string(), "Microphone Array (Realtek(R) Audio)".to_string());
        map.insert("NetShield".to_string(), "1".to_string());
        map.insert("ResourceMode".to_string(), "conservative".to_string());
        save_sonic_guard_settings(&tmp, &map).unwrap();

        let read_back = read_sonic_guard_settings(&tmp);
        assert_eq!(read_back.preferred_mic, "Microphone Array (Realtek(R) Audio)");
        assert!(read_back.net_shield);
        assert_eq!(read_back.resource_mode, "conservative");

        let _ = fs::remove_dir_all(&tmp);
    }
}
