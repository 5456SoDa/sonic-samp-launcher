use crate::config::{get_guard_config, to_wide};
use std::ffi::c_void;
use std::sync::atomic::{AtomicBool, Ordering};
use windows_sys::Win32::Foundation::{FreeLibrary, HANDLE};
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress, LoadLibraryW};
use windows_sys::Win32::System::Threading::GetCurrentProcess;

static GPU_SHIELD_ACTIVE: AtomicBool = AtomicBool::new(false);

const D3DKMT_SCHEDULINGPRIORITYCLASS_HIGH: i32 = 2;

#[repr(C)]
struct ProcessPowerThrottlingState {
    version: u32,
    control_mask: u32,
    state_mask: u32,
}

/// Applies temporary, process-local GPU Scheduler priority and disables PowerThrottling / EcoQoS
/// strictly on `gta_sa.exe` while the game runs. Zero Registry or global OS modifications.
pub fn init_gpu_shield() {
    let cfg = get_guard_config();
    if !cfg.enabled || !cfg.gpu_boost {
        return;
    }

    unsafe {
        let k32_w = to_wide("kernel32.dll");
        let h_k32 = GetModuleHandleW(k32_w.as_ptr());
        if !h_k32.is_null() {
            if let Some(p) = GetProcAddress(h_k32, b"SetProcessInformation\0".as_ptr()) {
                type FnSetProcInfo = unsafe extern "system" fn(
                    HANDLE,
                    u32,
                    *const ProcessPowerThrottlingState,
                    u32,
                ) -> i32;
                let set_info: FnSetProcInfo = std::mem::transmute(p);
                let state = ProcessPowerThrottlingState {
                    version: 1,
                    control_mask: 1, // PROCESS_POWER_THROTTLING_EXECUTION_SPEED
                    state_mask: 0,   // Force off EcoQoS throttling
                };
                if set_info(
                    GetCurrentProcess(),
                    4, // ProcessPowerThrottling
                    &state,
                    std::mem::size_of::<ProcessPowerThrottlingState>() as u32,
                ) != 0
                {
                    GPU_SHIELD_ACTIVE.store(true, Ordering::SeqCst);
                }
            }
        }

        let gdi_w = to_wide("gdi32.dll");
        let mut h_gdi = GetModuleHandleW(gdi_w.as_ptr());
        let mut loaded_gdi = false;
        if h_gdi.is_null() {
            h_gdi = LoadLibraryW(gdi_w.as_ptr());
            loaded_gdi = !h_gdi.is_null();
        }
        if !h_gdi.is_null() {
            if let Some(p) = GetProcAddress(
                h_gdi as *mut c_void as _,
                b"D3DKMTSetProcessSchedulingPriorityClass\0".as_ptr(),
            ) {
                type FnD3DKmtPrio = unsafe extern "system" fn(HANDLE, i32) -> i32;
                let set_gpu_prio: FnD3DKmtPrio = std::mem::transmute(p);
                let status = set_gpu_prio(GetCurrentProcess(), D3DKMT_SCHEDULINGPRIORITYCLASS_HIGH);
                if status == 0 {
                    GPU_SHIELD_ACTIVE.store(true, Ordering::SeqCst);
                }
            }
            if loaded_gdi {
                let _ = FreeLibrary(h_gdi);
            }
        }
    }
}

pub fn is_gpu_shield_active() -> bool {
    GPU_SHIELD_ACTIVE.load(Ordering::Relaxed)
}

#[cfg(test)]
mod tests {
    #[test]
    fn test_vertex_processing_upgrade_logic() {
        // Check pure bit logic used by window.rs for D3DCREATE flags
        const SW_VP: u32 = 0x0000_0020;
        const HW_VP: u32 = 0x0000_0040;
        const FPU_PRESERVE: u32 = 0x0000_0002;
        let orig = SW_VP | FPU_PRESERVE;
        let upgraded = (orig & !SW_VP) | HW_VP;
        assert_eq!(upgraded, HW_VP | FPU_PRESERVE);
    }
}
