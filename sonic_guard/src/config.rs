use std::ffi::CStr;
use std::fs;
use std::path::PathBuf;
use std::ptr::null_mut;
use std::sync::OnceLock;
use windows_sys::Win32::Globalization::{MultiByteToWideChar, CP_ACP};
use windows_sys::Win32::System::LibraryLoader::GetModuleFileNameW;

#[derive(Debug, Clone)]
pub struct GuardIniConfig {
    pub enabled: bool,
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

impl Default for GuardIniConfig {
    fn default() -> Self {
        Self {
            enabled: true,
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

pub fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

/// Converts a NUL-terminated Windows ANSI (`CP_ACP`, e.g., Windows-874 Thai) C string into UTF-8.
pub unsafe fn ansi_cstr_to_string(ptr: *const i8) -> String {
    if ptr.is_null() {
        return String::new();
    }
    let bytes = CStr::from_ptr(ptr).to_bytes();
    if bytes.is_empty() {
        return String::new();
    }
    if bytes.is_ascii() {
        return String::from_utf8_lossy(bytes).into_owned();
    }
    let wlen = MultiByteToWideChar(CP_ACP, 0, bytes.as_ptr(), bytes.len() as i32, null_mut(), 0);
    if wlen > 0 {
        let mut wbuf = vec![0u16; wlen as usize];
        let written = MultiByteToWideChar(
            CP_ACP,
            0,
            bytes.as_ptr(),
            bytes.len() as i32,
            wbuf.as_mut_ptr(),
            wlen,
        );
        if written > 0 {
            return String::from_utf16_lossy(&wbuf[..written as usize]);
        }
    }
    String::from_utf8_lossy(bytes).into_owned()
}

/// Returns the executable directory (`gta_sa.exe` root) with growing buffer support for long paths.
pub fn game_root_dir() -> PathBuf {
    let mut cap = 512usize;
    while cap <= 32_768 {
        let mut buf = vec![0u16; cap];
        let len = unsafe { GetModuleFileNameW(null_mut(), buf.as_mut_ptr(), buf.len() as u32) };
        if len == 0 {
            break;
        }
        if (len as usize) < buf.len().saturating_sub(1) {
            if let Some(parent) =
                PathBuf::from(String::from_utf16_lossy(&buf[..len as usize])).parent()
            {
                return parent.to_path_buf();
            }
            break;
        }
        cap *= 2;
    }
    PathBuf::from(".")
}

fn strip_inline_comment(val: &str, require_space_prefix: bool) -> &str {
    let bytes = val.as_bytes();
    for (idx, &b) in bytes.iter().enumerate() {
        if b == b';' || b == b'#' {
            if !require_space_prefix
                || idx == 0
                || bytes[idx - 1] == b' '
                || bytes[idx - 1] == b'\t'
            {
                return val[..idx].trim();
            }
        }
    }
    val.trim()
}

pub fn parse_bool_val(v: &str) -> bool {
    let t = strip_inline_comment(v, false);
    t == "1" || t.eq_ignore_ascii_case("true")
}

pub fn parse_guard_ini_str(text: &str) -> GuardIniConfig {
    let mut cfg = GuardIniConfig::default();
    for raw in text.lines() {
        let line = raw.trim();
        if line.is_empty()
            || line.starts_with(';')
            || line.starts_with('#')
            || line.starts_with('[')
        {
            continue;
        }
        if let Some(eq) = line.find('=') {
            let k = line[..eq].trim().to_ascii_lowercase();
            let raw_v = line[eq + 1..].trim();
            match k.as_str() {
                "enable" | "enabled" => cfg.enabled = parse_bool_val(raw_v),
                "borderless" => cfg.borderless = parse_bool_val(raw_v),
                "catchcrash" => cfg.catch_crash = parse_bool_val(raw_v),
                "highprecisiontimer" => cfg.high_precision_timer = parse_bool_val(raw_v),
                "telemetry" => cfg.telemetry = parse_bool_val(raw_v),
                "netshield" => cfg.net_shield = parse_bool_val(raw_v),
                "audioshield" => cfg.audio_shield = parse_bool_val(raw_v),
                "preferredmic" => {
                    cfg.preferred_mic = strip_inline_comment(raw_v, true).to_string();
                }
                "gpuboost" => cfg.gpu_boost = parse_bool_val(raw_v),
                "forceimmediatepresent" => cfg.force_immediate_present = parse_bool_val(raw_v),
                "resourcemode" => {
                    cfg.resource_mode = strip_inline_comment(raw_v, false).to_ascii_lowercase();
                }
                "cpuaffinity" | "affinity" => {
                    cfg.cpu_affinity = strip_inline_comment(raw_v, false).to_ascii_lowercase();
                }
                _ => {}
            }
        }
    }
    cfg
}

pub fn get_guard_config() -> &'static GuardIniConfig {
    static CFG: OnceLock<GuardIniConfig> = OnceLock::new();
    CFG.get_or_init(|| {
        let ini = game_root_dir().join("scripts").join("SonicGuard.ini");
        match fs::read_to_string(&ini) {
            Ok(text) => parse_guard_ini_str(&text),
            Err(_) => GuardIniConfig::default(),
        }
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_guard_ini_defaults_and_overrides() {
        let raw = "\
[SonicGuard]
Enable = 1
Borderless = 1
NetShield = 0
AudioShield = 1
PreferredMic = Microphone Array (Realtek(R) Audio)
GpuBoost = 1
ForceImmediatePresent = 0
ResourceMode = Conservative
CpuAffinity = p_cores
";
        let cfg = parse_guard_ini_str(raw);
        assert!(cfg.enabled);
        assert!(cfg.borderless);
        assert!(!cfg.net_shield);
        assert!(cfg.audio_shield);
        assert_eq!(cfg.preferred_mic, "Microphone Array (Realtek(R) Audio)");
        assert!(cfg.gpu_boost);
        assert!(!cfg.force_immediate_present);
        assert_eq!(cfg.resource_mode, "conservative");
        assert_eq!(cfg.cpu_affinity, "p_cores");

        let commented = "\
Borderless = 1 ; fullscreen windowed
PreferredMic = Headset (#2) ; real mic
ResourceMode = Max # cap
";
        let cfg2 = parse_guard_ini_str(commented);
        assert!(cfg2.borderless);
        assert_eq!(cfg2.preferred_mic, "Headset (#2)");
        assert_eq!(cfg2.resource_mode, "max");
    }

    #[test]
    fn test_ansi_cstr_ascii_and_null() {
        unsafe {
            assert_eq!(ansi_cstr_to_string(null_mut()), "");
            let s = b"Realtek Audio\0";
            assert_eq!(ansi_cstr_to_string(s.as_ptr() as *const i8), "Realtek Audio");
        }
    }
}
