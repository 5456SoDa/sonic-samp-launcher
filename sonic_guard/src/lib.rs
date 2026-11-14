mod audio_shield;
mod config;
mod crash_handler;
mod gpu_shield;
mod iat;
mod net;
mod perf;
mod window;

use config::get_guard_config;
use std::ffi::c_void;
use windows_sys::Win32::Foundation::{BOOL, HMODULE, TRUE};
use windows_sys::Win32::System::LibraryLoader::DisableThreadLibraryCalls;
use windows_sys::Win32::System::SystemServices::{DLL_PROCESS_ATTACH, DLL_PROCESS_DETACH};

// Process-local discrete GPU hint exports (no Registry or OS settings touched)
#[no_mangle]
pub static NvOptimusEnablement: u32 = 1;

#[no_mangle]
pub static AmdPowerXpressRequestHighPerformance: i32 = 1;

#[no_mangle]
pub extern "system" fn DllMain(
    hinst_dll: HMODULE,
    fdw_reason: u32,
    lpv_reserved: *mut c_void,
) -> BOOL {
    let _ = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| match fdw_reason {
        DLL_PROCESS_ATTACH => {
            unsafe { DisableThreadLibraryCalls(hinst_dll) };
            let cfg = get_guard_config();
            if cfg.enabled {
                if cfg.catch_crash {
                    crash_handler::install_crash_filter();
                }
                window::init_window_and_gfx_hooks();
                if cfg.high_precision_timer
                    || cfg.telemetry
                    || cfg.net_shield
                    || cfg.audio_shield
                    || cfg.gpu_boost
                {
                    perf::init_perf();
                }
            }
        }
        DLL_PROCESS_DETACH => {
            // Only wait/join worker thread on explicit FreeLibrary; during process teardown OS reaps threads.
            if lpv_reserved.is_null() {
                perf::shutdown_perf();
            }
        }
        _ => {}
    }));
    TRUE
}
