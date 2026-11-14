use crate::audio_shield;
use crate::config::{get_guard_config, to_wide};
use crate::iat;
use std::ffi::{c_void, CStr};
use std::ptr::null_mut;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use windows_sys::Win32::Foundation::{BOOL, HMODULE, HWND, RECT};
use windows_sys::Win32::Graphics::Gdi::{
    GetMonitorInfoW, MonitorFromWindow, MONITORINFO, MONITOR_DEFAULTTONEAREST,
};
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress, LoadLibraryW};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetSystemMetrics, SetWindowLongW, SetWindowPos, GWL_EXSTYLE, GWL_STYLE, HWND_TOP,
    SM_CXSCREEN, SM_CYSCREEN, SWP_FRAMECHANGED, SWP_SHOWWINDOW, WS_EX_APPWINDOW, WS_POPUP,
    WS_VISIBLE,
};

static BORDERLESS_ACTIVE: AtomicBool = AtomicBool::new(false);
static RENDER_MMCSS_APPLIED: AtomicBool = AtomicBool::new(false);
static ORIG_GET_PROC_ADDR: AtomicUsize = AtomicUsize::new(0);
static ORIG_D3D_CREATE9: AtomicUsize = AtomicUsize::new(0);
static ORIG_CREATE_DEVICE: AtomicUsize = AtomicUsize::new(0);
static ORIG_RESET: AtomicUsize = AtomicUsize::new(0);
static TARGET_HWND: AtomicUsize = AtomicUsize::new(0);

const D3DCREATE_SOFTWARE_VERTEXPROCESSING: u32 = 0x0000_0020;
const D3DCREATE_HARDWARE_VERTEXPROCESSING: u32 = 0x0000_0040;
const D3DPRESENT_INTERVAL_IMMEDIATE: u32 = 0x8000_0000;

#[repr(C)]
struct D3dPresentParameters {
    back_buffer_width: u32,
    back_buffer_height: u32,
    back_buffer_format: u32,
    back_buffer_count: u32,
    multi_sample_type: u32,
    multi_sample_quality: u32,
    swap_effect: u32,
    h_device_window: HWND,
    windowed: BOOL,
    enable_auto_depth_stencil: BOOL,
    auto_depth_stencil_format: u32,
    flags: u32,
    full_screen_refresh_rate_in_hz: u32,
    presentation_interval: u32,
}

type FnGetProcAddress = unsafe extern "system" fn(HMODULE, *const u8) -> *const c_void;
type FnDirect3DCreate9 = unsafe extern "system" fn(u32) -> *mut *mut usize;
type FnCreateDevice = unsafe extern "system" fn(
    *mut usize,
    u32,
    u32,
    HWND,
    u32,
    *mut D3dPresentParameters,
    *mut *mut *mut usize,
) -> i32;
type FnReset = unsafe extern "system" fn(*mut usize, *mut D3dPresentParameters) -> i32;

unsafe fn monitor_bounds_for(hwnd: HWND) -> (i32, i32, i32, i32) {
    let mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    if !mon.is_null() {
        let mut mi: MONITORINFO = std::mem::zeroed();
        mi.cbSize = std::mem::size_of::<MONITORINFO>() as u32;
        if GetMonitorInfoW(mon, &mut mi) != 0 {
            let r: RECT = mi.rcMonitor;
            let w = r.right - r.left;
            let h = r.bottom - r.top;
            if w > 0 && h > 0 {
                return (r.left, r.top, w, h);
            }
        }
    }
    (0, 0, GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN))
}

unsafe fn apply_borderless_style(hwnd: HWND) {
    if hwnd.is_null() {
        return;
    }
    TARGET_HWND.store(hwnd as usize, Ordering::Relaxed);
    let (x, y, w, h) = monitor_bounds_for(hwnd);
    SetWindowLongW(hwnd, GWL_STYLE, (WS_POPUP | WS_VISIBLE) as i32);
    SetWindowLongW(hwnd, GWL_EXSTYLE, WS_EX_APPWINDOW as i32);
    SetWindowPos(
        hwnd,
        HWND_TOP,
        x,
        y,
        w,
        h,
        SWP_FRAMECHANGED | SWP_SHOWWINDOW,
    );
}

pub fn tune_behavior_flags(flags: u32, gpu_boost: bool) -> u32 {
    if gpu_boost && (flags & D3DCREATE_SOFTWARE_VERTEXPROCESSING) != 0 {
        (flags & !D3DCREATE_SOFTWARE_VERTEXPROCESSING) | D3DCREATE_HARDWARE_VERTEXPROCESSING
    } else {
        flags
    }
}

pub fn should_force_immediate_present(windowed: bool, force_immediate: bool) -> bool {
    windowed && force_immediate
}

unsafe extern "system" fn hooked_reset(this: *mut usize, pp: *mut D3dPresentParameters) -> i32 {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let cfg = get_guard_config();
        let mut resolved_hwnd = TARGET_HWND.load(Ordering::Relaxed) as HWND;
        if !pp.is_null() {
            if !(*pp).h_device_window.is_null() {
                resolved_hwnd = (*pp).h_device_window;
            }
            if cfg.borderless {
                let was_fullscreen = (*pp).windowed == 0;
                (*pp).windowed = 1;
                (*pp).full_screen_refresh_rate_in_hz = 0;
                if !resolved_hwnd.is_null() {
                    let (_, _, w, h) = monitor_bounds_for(resolved_hwnd);
                    if w > 0
                        && h > 0
                        && (was_fullscreen
                            || (*pp).back_buffer_width == 0
                            || (*pp).back_buffer_height == 0)
                    {
                        (*pp).back_buffer_width = w as u32;
                        (*pp).back_buffer_height = h as u32;
                    }
                }
            }
            if should_force_immediate_present((*pp).windowed != 0, cfg.force_immediate_present) {
                (*pp).presentation_interval = D3DPRESENT_INTERVAL_IMMEDIATE;
            }
        }

        let orig_addr = ORIG_RESET.load(Ordering::SeqCst);
        if orig_addr == 0 {
            return -1;
        }
        let orig: FnReset = std::mem::transmute(orig_addr);
        let hr = orig(this, pp);
        if hr >= 0 && cfg.borderless && !resolved_hwnd.is_null() {
            apply_borderless_style(resolved_hwnd);
        }
        hr
    }))
    .unwrap_or(-1)
}

unsafe extern "system" fn hooked_create_device(
    this: *mut usize,
    adapter: u32,
    device_type: u32,
    h_focus_window: HWND,
    behavior_flags: u32,
    pp: *mut D3dPresentParameters,
    pp_returned_device: *mut *mut *mut usize,
) -> i32 {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        if !RENDER_MMCSS_APPLIED.swap(true, Ordering::SeqCst) {
            audio_shield::apply_mmcss_to_current_thread("Games");
        }

        let cfg = get_guard_config();
        let hwnd = if !pp.is_null() && !(*pp).h_device_window.is_null() {
            (*pp).h_device_window
        } else {
            h_focus_window
        };

        let adjusted_flags = tune_behavior_flags(behavior_flags, cfg.gpu_boost);

        if !pp.is_null() {
            if cfg.borderless {
                (*pp).windowed = 1;
                (*pp).full_screen_refresh_rate_in_hz = 0;
                let (_, _, w, h) = monitor_bounds_for(hwnd);
                if w > 0 && h > 0 {
                    (*pp).back_buffer_width = w as u32;
                    (*pp).back_buffer_height = h as u32;
                }
            }
            if should_force_immediate_present((*pp).windowed != 0, cfg.force_immediate_present) {
                (*pp).presentation_interval = D3DPRESENT_INTERVAL_IMMEDIATE;
            }
        }

        let orig_addr = ORIG_CREATE_DEVICE.load(Ordering::SeqCst);
        if orig_addr == 0 {
            return -1;
        }
        let orig: FnCreateDevice = std::mem::transmute(orig_addr);
        let hr = orig(
            this,
            adapter,
            device_type,
            h_focus_window,
            adjusted_flags,
            pp,
            pp_returned_device,
        );

        if hr >= 0 {
            if cfg.borderless {
                apply_borderless_style(hwnd);
                BORDERLESS_ACTIVE.store(true, Ordering::SeqCst);
            }
            if !pp_returned_device.is_null() && !(*pp_returned_device).is_null() {
                let dev_vtable = *(*pp_returned_device);
                iat::patch_vtable_slot(
                    dev_vtable,
                    16,
                    hooked_reset as *const () as usize,
                    &ORIG_RESET,
                );
            }
        }
        hr
    }))
    .unwrap_or(-1)
}

unsafe extern "system" fn hooked_direct3d_create9(sdk_version: u32) -> *mut *mut usize {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let mut orig_addr = ORIG_D3D_CREATE9.load(Ordering::SeqCst);
        let self_addr = hooked_direct3d_create9 as *const () as usize;
        if orig_addr == 0 || orig_addr == self_addr {
            let d3d_w = to_wide("d3d9.dll");
            let h_d3d = GetModuleHandleW(d3d_w.as_ptr());
            let gp_addr = ORIG_GET_PROC_ADDR.load(Ordering::SeqCst);
            if !h_d3d.is_null() && gp_addr != 0 {
                let orig_gp: FnGetProcAddress = std::mem::transmute(gp_addr);
                orig_addr = orig_gp(h_d3d, b"Direct3DCreate9\0".as_ptr()) as usize;
            }
        }
        if orig_addr == 0 || orig_addr == self_addr {
            return null_mut();
        }
        let orig: FnDirect3DCreate9 = std::mem::transmute(orig_addr);
        let d3d9 = orig(sdk_version);
        if !d3d9.is_null() {
            let vtable = *d3d9;
            iat::patch_vtable_slot(
                vtable,
                16,
                hooked_create_device as *const () as usize,
                &ORIG_CREATE_DEVICE,
            );
        }
        d3d9
    }))
    .unwrap_or(null_mut())
}

unsafe extern "system" fn hooked_get_proc_address(
    h_module: HMODULE,
    lp_proc_name: *const u8,
) -> *const c_void {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let gp_addr = ORIG_GET_PROC_ADDR.load(Ordering::SeqCst);
        if gp_addr == 0 {
            return std::ptr::null();
        }
        let orig_gp: FnGetProcAddress = std::mem::transmute(gp_addr);
        let real_ptr = orig_gp(h_module, lp_proc_name);

        // Must guard against ordinal values (< 0x10000) passed in lpProcName
        if !real_ptr.is_null() && (lp_proc_name as usize) > 0xFFFF {
            if let Ok(name_cstr) = CStr::from_ptr(lp_proc_name as *const i8).to_str() {
                if name_cstr == "Direct3DCreate9" {
                    let real_u = real_ptr as usize;
                    if real_u != hooked_direct3d_create9 as *const () as usize {
                        let _ = ORIG_D3D_CREATE9.compare_exchange(
                            0,
                            real_u,
                            Ordering::SeqCst,
                            Ordering::SeqCst,
                        );
                    }
                    return hooked_direct3d_create9 as *const c_void;
                }
            }
        }
        real_ptr
    }))
    .unwrap_or(std::ptr::null())
}

pub fn enable_dpi_awareness_if_borderless() {
    if !get_guard_config().borderless {
        return;
    }
    unsafe {
        let u32_w = to_wide("user32.dll");
        let mut h_u32 = GetModuleHandleW(u32_w.as_ptr());
        let mut loaded = false;
        if h_u32.is_null() {
            h_u32 = LoadLibraryW(u32_w.as_ptr());
            loaded = !h_u32.is_null();
        }
        if !h_u32.is_null() {
            if let Some(p) = GetProcAddress(h_u32, b"SetProcessDPIAware\0".as_ptr()) {
                type FnDpi = unsafe extern "system" fn() -> i32;
                let dpi_fn: FnDpi = std::mem::transmute(p);
                let _ = dpi_fn();
            }
            if loaded {
                let _ = windows_sys::Win32::Foundation::FreeLibrary(h_u32);
            }
        }
    }
}

/// Hooks D3D9 safely via BOTH static d3d9.dll IAT (if present) and gta_sa.exe KERNEL32!GetProcAddress IAT
/// (since RenderWare loads d3d9.dll dynamically). Leaves sampvoice.asi / skygfx.asi hook chains intact.
pub fn init_window_and_gfx_hooks() {
    let cfg = get_guard_config();
    if !cfg.enabled || (!cfg.borderless && !cfg.gpu_boost && !cfg.force_immediate_present) {
        return;
    }

    unsafe {
        if let Some(orig) = iat::patch_module_iat(
            null_mut(),
            "d3d9.dll",
            "Direct3DCreate9",
            hooked_direct3d_create9 as *const () as usize,
        ) {
            let _ = ORIG_D3D_CREATE9.compare_exchange(0, orig, Ordering::SeqCst, Ordering::SeqCst);
        }

        if let Some(orig_gp) = iat::patch_module_iat(
            null_mut(),
            "KERNEL32.dll",
            "GetProcAddress",
            hooked_get_proc_address as *const () as usize,
        ) {
            let _ =
                ORIG_GET_PROC_ADDR.compare_exchange(0, orig_gp, Ordering::SeqCst, Ordering::SeqCst);
        }
    }
}

pub fn is_borderless_active() -> bool {
    BORDERLESS_ACTIVE.load(Ordering::Relaxed)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_d3d9_behavior_flags_and_present_guards() {
        assert_eq!(tune_behavior_flags(0x22, true), 0x42);
        assert_eq!(tune_behavior_flags(0x42, true), 0x42);
        assert_eq!(tune_behavior_flags(0x22, false), 0x22);
        assert!(should_force_immediate_present(true, true));
        assert!(!should_force_immediate_present(false, true));
        assert!(!should_force_immediate_present(true, false));
    }
}
