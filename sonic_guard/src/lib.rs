mod crash_handler;
mod perf;
mod window;

use std::ffi::c_void;
use std::fs;
use std::path::PathBuf;
use windows_sys::Win32::Foundation::{BOOL, HMODULE, MAX_PATH, TRUE};
use windows_sys::Win32::System::LibraryLoader::{DisableThreadLibraryCalls, GetModuleFileNameW};
use windows_sys::Win32::System::SystemServices::{DLL_PROCESS_ATTACH, DLL_PROCESS_DETACH};

fn is_plugin_enabled() -> bool {
    let mut buf = [0u16; MAX_PATH as usize];
    let len = unsafe { GetModuleFileNameW(std::ptr::null_mut(), buf.as_mut_ptr(), MAX_PATH) };
    if len == 0 {
        return true;
    }
    let exe = PathBuf::from(String::from_utf16_lossy(&buf[..len as usize]));
    let Some(root) = exe.parent() else {
        return true;
    };
    let ini = root.join("scripts").join("SonicGuard.ini");
    let Ok(text) = fs::read_to_string(&ini) else {
        return true;
    };
    for raw in text.lines() {
        let line = raw.trim();
        if let Some(eq) = line.find('=') {
            if line[..eq].trim().eq_ignore_ascii_case("enabled") {
                let v = line[eq + 1..].trim();
                return v == "1" || v.eq_ignore_ascii_case("true");
            }
        }
    }
    true
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
            if is_plugin_enabled() {
                crash_handler::install_crash_filter();
                window::init_borderless_if_enabled();
                perf::init_perf();
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
