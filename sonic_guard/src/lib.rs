mod crash_handler;
mod perf;
mod window;

use std::ffi::c_void;
use std::fs;
use std::path::PathBuf;
use windows_sys::Win32::Foundation::{BOOL, HMODULE, MAX_PATH, TRUE};
use windows_sys::Win32::System::LibraryLoader::{DisableThreadLibraryCalls, GetModuleFileNameW};
use windows_sys::Win32::System::SystemServices::{DLL_PROCESS_ATTACH, DLL_PROCESS_DETACH};

struct GuardIniConfig {
    enabled: bool,
    catch_crash: bool,
    high_precision_timer: bool,
    telemetry: bool,
}

impl Default for GuardIniConfig {
    fn default() -> Self {
        Self {
            enabled: true,
            catch_crash: true,
            high_precision_timer: true,
            telemetry: true,
        }
    }
}

fn parse_bool_val(v: &str) -> bool {
    v == "1" || v.eq_ignore_ascii_case("true")
}

fn read_guard_config() -> GuardIniConfig {
    let mut cfg = GuardIniConfig::default();
    let mut buf = [0u16; MAX_PATH as usize];
    let len = unsafe { GetModuleFileNameW(std::ptr::null_mut(), buf.as_mut_ptr(), MAX_PATH) };
    if len == 0 {
        return cfg;
    }
    let exe = PathBuf::from(String::from_utf16_lossy(&buf[..len as usize]));
    let Some(root) = exe.parent() else {
        return cfg;
    };
    let ini = root.join("scripts").join("SonicGuard.ini");
    let Ok(text) = fs::read_to_string(&ini) else {
        return cfg;
    };
    for raw in text.lines() {
        let line = raw.trim();
        if line.is_empty() || line.starts_with(';') || line.starts_with('#') || line.starts_with('[') {
            continue;
        }
        if let Some(eq) = line.find('=') {
            let k = line[..eq].trim().to_ascii_lowercase();
            let v = line[eq + 1..].trim();
            match k.as_str() {
                "enable" | "enabled" => cfg.enabled = parse_bool_val(v),
                "catchcrash" => cfg.catch_crash = parse_bool_val(v),
                "highprecisiontimer" => cfg.high_precision_timer = parse_bool_val(v),
                "telemetry" => cfg.telemetry = parse_bool_val(v),
                _ => {}
            }
        }
    }
    cfg
}

#[no_mangle]
pub extern "system" fn DllMain(
    hinst_dll: HMODULE,
    fdw_reason: u32,
    lpv_reserved: *mut c_void,
) -> BOOL {
    match fdw_reason {
        DLL_PROCESS_ATTACH => {
            unsafe { DisableThreadLibraryCalls(hinst_dll) };
            let cfg = read_guard_config();
            if cfg.enabled {
                if cfg.catch_crash {
                    crash_handler::install_crash_filter();
                }
                window::init_borderless_if_enabled();
                if cfg.high_precision_timer || cfg.telemetry {
                    perf::init_perf();
                }
            }
        }
        DLL_PROCESS_DETACH => {
            if lpv_reserved.is_null() {
                perf::shutdown_perf();
            }
        }
        _ => {}
    }
    TRUE
}
