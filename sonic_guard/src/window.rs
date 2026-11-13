use std::ffi::CStr;
use std::fs;
use std::path::PathBuf;
use std::ptr::null_mut;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use windows_sys::Win32::Foundation::{BOOL, HWND, MAX_PATH, RECT};
use windows_sys::Win32::Graphics::Gdi::{
    GetMonitorInfoW, MonitorFromWindow, MONITORINFO, MONITOR_DEFAULTTONEAREST,
};
use windows_sys::Win32::System::LibraryLoader::{GetModuleFileNameW, GetModuleHandleW};
use windows_sys::Win32::System::Memory::{VirtualProtect, PAGE_EXECUTE_READWRITE, PAGE_READWRITE};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    GetSystemMetrics, SetWindowLongW, SetWindowPos, GWL_EXSTYLE, GWL_STYLE, HWND_TOP,
    SM_CXSCREEN, SM_CYSCREEN, SWP_FRAMECHANGED, SWP_SHOWWINDOW, WS_EX_APPWINDOW, WS_POPUP,
    WS_VISIBLE,
};

static BORDERLESS_ACTIVE: AtomicBool = AtomicBool::new(false);
static ORIG_D3D_CREATE9: AtomicUsize = AtomicUsize::new(0);
static ORIG_CREATE_DEVICE: AtomicUsize = AtomicUsize::new(0);
static ORIG_RESET: AtomicUsize = AtomicUsize::new(0);
static TARGET_HWND: AtomicUsize = AtomicUsize::new(0);

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

fn read_ini_borderless() -> bool {
    let mut buf = [0u16; MAX_PATH as usize];
    let len = unsafe { GetModuleFileNameW(null_mut(), buf.as_mut_ptr(), MAX_PATH) };
    if len == 0 {
        return false;
    }
    let exe_path = PathBuf::from(String::from_utf16_lossy(&buf[..len as usize]));
    let Some(root) = exe_path.parent() else {
        return false;
    };
    let ini_path = root.join("scripts").join("SonicGuard.ini");
    let Ok(content) = fs::read_to_string(&ini_path) else {
        return false;
    };
    let mut enabled = true;
    let mut borderless = false;
    for raw_line in content.lines() {
        let line = raw_line.trim();
        if line.is_empty() || line.starts_with(';') || line.starts_with('#') || line.starts_with('[') {
            continue;
        }
        if let Some(eq) = line.find('=') {
            let k = line[..eq].trim().to_ascii_lowercase();
            let v = line[eq + 1..].trim();
            if k == "enable" || k == "enabled" {
                enabled = v == "1" || v.eq_ignore_ascii_case("true");
            } else if k == "borderless" {
                borderless = v == "1" || v.eq_ignore_ascii_case("true");
            }
        }
    }
    enabled && borderless
}

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
    SetWindowPos(hwnd, HWND_TOP, x, y, w, h, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
}

unsafe extern "system" fn hooked_reset(
    this: *mut usize,
    pp: *mut D3dPresentParameters,
) -> i32 {
    if !pp.is_null() {
        (*pp).windowed = 1;
        (*pp).full_screen_refresh_rate_in_hz = 0;
        let hwnd = if !(*pp).h_device_window.is_null() {
            (*pp).h_device_window
        } else {
            TARGET_HWND.load(Ordering::Relaxed) as HWND
        };
        if !hwnd.is_null() {
            let (_, _, w, h) = monitor_bounds_for(hwnd);
            if (*pp).back_buffer_width == 0 || (*pp).back_buffer_height == 0 {
                (*pp).back_buffer_width = w as u32;
                (*pp).back_buffer_height = h as u32;
            }
        }
    }
    let orig: FnReset = std::mem::transmute(ORIG_RESET.load(Ordering::Relaxed));
    let hr = orig(this, pp);
    if hr >= 0 {
        let hwnd = TARGET_HWND.load(Ordering::Relaxed) as HWND;
        apply_borderless_style(hwnd);
    }
    hr
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
    let hwnd = if !pp.is_null() && !(*pp).h_device_window.is_null() {
        (*pp).h_device_window
    } else {
        h_focus_window
    };

    if !pp.is_null() {
        (*pp).windowed = 1;
        (*pp).full_screen_refresh_rate_in_hz = 0;
        let (_, _, w, h) = monitor_bounds_for(hwnd);
        if w > 0 && h > 0 {
            (*pp).back_buffer_width = w as u32;
            (*pp).back_buffer_height = h as u32;
        }
    }

    let orig: FnCreateDevice = std::mem::transmute(ORIG_CREATE_DEVICE.load(Ordering::Relaxed));
    let hr = orig(
        this,
        adapter,
        device_type,
        h_focus_window,
        behavior_flags,
        pp,
        pp_returned_device,
    );

    if hr >= 0 {
        apply_borderless_style(hwnd);
        if !pp_returned_device.is_null() && !(*pp_returned_device).is_null() {
            let dev_vtable = *(*pp_returned_device);
            patch_vtable_slot(dev_vtable, 16, hooked_reset as *const () as usize, &ORIG_RESET);
        }
    }
    hr
}

unsafe fn patch_vtable_slot(
    vtable: *mut usize,
    index: usize,
    hook_addr: usize,
    save_orig: &AtomicUsize,
) {
    if vtable.is_null() {
        return;
    }
    let slot = vtable.add(index);
    if *slot == hook_addr {
        return;
    }
    let mut old_prot = 0u32;
    if VirtualProtect(slot as _, 4, PAGE_EXECUTE_READWRITE, &mut old_prot) != 0 {
        let _ = save_orig.compare_exchange(0, *slot, Ordering::SeqCst, Ordering::SeqCst);
        *slot = hook_addr;
        let mut unused = 0u32;
        VirtualProtect(slot as _, 4, old_prot, &mut unused);
    }
}

unsafe extern "system" fn hooked_direct3d_create9(sdk_version: u32) -> *mut *mut usize {
    let orig: FnDirect3DCreate9 = std::mem::transmute(ORIG_D3D_CREATE9.load(Ordering::Relaxed));
    let d3d9 = orig(sdk_version);
    if !d3d9.is_null() {
        let vtable = *d3d9;
        patch_vtable_slot(vtable, 16, hooked_create_device as *const () as usize, &ORIG_CREATE_DEVICE);
    }
    d3d9
}

#[repr(C)]
struct ImageImportDescriptor {
    original_first_thunk: u32,
    time_date_stamp: u32,
    forwarder_chain: u32,
    name: u32,
    first_thunk: u32,
}

unsafe fn hook_exe_iat_d3d9() -> bool {
    let base = GetModuleHandleW(null_mut()) as *const u8;
    if base.is_null() {
        return false;
    }
    let dos_magic = *(base as *const u16);
    if dos_magic != 0x5A4D {
        return false;
    }
    let e_lfanew = *(base.add(0x3C) as *const i32);
    if e_lfanew <= 0 {
        return false;
    }
    let nt = base.add(e_lfanew as usize);
    if *(nt as *const u32) != 0x00004550 {
        return false;
    }
    // OptionalHeader (at +24) -> DataDirectory[1] (Import Table) at +96 + 8 = +104 from OptionalHeader
    let import_rva = *(nt.add(24 + 104) as *const u32);
    if import_rva == 0 {
        return false;
    }

    let mut desc = base.add(import_rva as usize) as *const ImageImportDescriptor;
    while (*desc).name != 0 {
        let dll_name_ptr = base.add((*desc).name as usize) as *const i8;
        if let Ok(dll_name) = CStr::from_ptr(dll_name_ptr).to_str() {
            if dll_name.eq_ignore_ascii_case("d3d9.dll") {
                let oft_rva = if (*desc).original_first_thunk != 0 {
                    (*desc).original_first_thunk
                } else {
                    (*desc).first_thunk
                };
                let mut oft = base.add(oft_rva as usize) as *const u32;
                let mut ft = base.add((*desc).first_thunk as usize) as *mut usize;
                while *oft != 0 && *ft != 0 {
                    if (*oft & 0x8000_0000) == 0 {
                        let name_ptr = base.add((*oft as usize) + 2) as *const i8;
                        if let Ok(fn_name) = CStr::from_ptr(name_ptr).to_str() {
                            if fn_name == "Direct3DCreate9" {
                                let mut old_prot = 0u32;
                                if VirtualProtect(ft as _, 4, PAGE_READWRITE, &mut old_prot) != 0 {
                                    ORIG_D3D_CREATE9.store(*ft, Ordering::SeqCst);
                                    *ft = hooked_direct3d_create9 as *const () as usize;
                                    let mut unused = 0u32;
                                    VirtualProtect(ft as _, 4, old_prot, &mut unused);
                                    return true;
                                }
                            }
                        }
                    }
                    oft = oft.add(1);
                    ft = ft.add(1);
                }
            }
        }
        desc = desc.add(1);
    }
    false
}

pub fn init_borderless_if_enabled() {
    if read_ini_borderless() {
        if unsafe { hook_exe_iat_d3d9() } {
            BORDERLESS_ACTIVE.store(true, Ordering::SeqCst);
        }
    }
}
