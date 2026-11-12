use std::ffi::c_void;
use windows_sys::Win32::Foundation::{FreeLibrary, HMODULE, HWND};
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryA};

type FnDwmSetWindowAttribute =
    unsafe extern "system" fn(hwnd: HWND, dw_attribute: u32, pv_attribute: *const c_void, cb_attribute: u32) -> i32;

const DWMWA_USE_IMMERSIVE_DARK_MODE_OLD: u32 = 19;
const DWMWA_USE_IMMERSIVE_DARK_MODE: u32 = 20;
const DWMWA_WINDOW_CORNER_PREFERENCE: u32 = 33;
const DWMWA_BORDER_COLOR: u32 = 34;

const DWMWCP_ROUND: i32 = 2;

/// Applies native Windows 10/11 DWM visual attributes (Dark Mode, Rounded Corners, Cyber Border)
/// to a borderless or standard WinForms window handle safely without hard-linking dwmapi.dll.
pub fn apply_dwm(hwnd_val: usize) -> bool {
    if hwnd_val == 0 {
        return false;
    }
    unsafe {
        let hmod: HMODULE = LoadLibraryA(b"dwmapi.dll\0".as_ptr());
        if hmod.is_null() {
            return false;
        }
        let proc = GetProcAddress(hmod, b"DwmSetWindowAttribute\0".as_ptr());
        let Some(func_ptr) = proc else {
            FreeLibrary(hmod);
            return false;
        };
        let dwm_set_attr: FnDwmSetWindowAttribute = std::mem::transmute(func_ptr);
        let hwnd = hwnd_val as HWND;

        let dark: i32 = 1;
        let hr_dark = dwm_set_attr(
            hwnd,
            DWMWA_USE_IMMERSIVE_DARK_MODE,
            (&dark as *const i32).cast(),
            std::mem::size_of::<i32>() as u32,
        );
        if hr_dark < 0 {
            let _ = dwm_set_attr(
                hwnd,
                DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                (&dark as *const i32).cast(),
                std::mem::size_of::<i32>() as u32,
            );
        }

        // Win11 GPU-accelerated rounded window corners
        let corner = DWMWCP_ROUND;
        let _ = dwm_set_attr(
            hwnd,
            DWMWA_WINDOW_CORNER_PREFERENCE,
            (&corner as *const i32).cast(),
            std::mem::size_of::<i32>() as u32,
        );

        // Win11 subtle cyber cyan border COLORREF 0x00BBGGRR (RGB: 42, 130, 195 -> 0x00C3822A)
        let border_color: u32 = 0x00C3_822A;
        let _ = dwm_set_attr(
            hwnd,
            DWMWA_BORDER_COLOR,
            (&border_color as *const u32).cast(),
            std::mem::size_of::<u32>() as u32,
        );
        FreeLibrary(hmod);
        true
    }
}
