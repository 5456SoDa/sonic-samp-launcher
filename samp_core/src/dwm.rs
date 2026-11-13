use std::collections::HashMap;
use std::ffi::c_void;
use std::sync::atomic::{AtomicU32, Ordering};
use std::sync::{Mutex, OnceLock};
use windows_sys::Win32::Foundation::{HWND, RECT};
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryA};

const DWMWA_NCRENDERING_POLICY: u32 = 2;
const DWMWA_USE_IMMERSIVE_DARK_MODE_OLD: u32 = 19;
const DWMWA_USE_IMMERSIVE_DARK_MODE: u32 = 20;
const DWMWA_WINDOW_CORNER_PREFERENCE: u32 = 33;
const DWMWA_BORDER_COLOR: u32 = 34;

const DWMNCRP_ENABLED: i32 = 2;
const DWMWCP_ROUND: i32 = 2;

const GWL_STYLE: i32 = -16;
const WS_CAPTION: i32 = 0x00C0_0000;
const WS_BORDER: i32 = 0x0080_0000;
const WS_DLGFRAME: i32 = 0x0040_0000;
const WS_THICKFRAME: i32 = 0x0004_0000;
const WS_MINIMIZEBOX: i32 = 0x0002_0000;
const WS_MAXIMIZEBOX: i32 = 0x0001_0000;
const WS_SYSMENU: i32 = 0x0008_0000;

const WS_CLIPCHILDREN: i32 = 0x0200_0000;

const SWP_NOSIZE: u32 = 0x0001;
const SWP_NOMOVE: u32 = 0x0002;
const SWP_NOZORDER: u32 = 0x0004;
const SWP_NOACTIVATE: u32 = 0x0010;
const SWP_FRAMECHANGED: u32 = 0x0020;

const WM_PAINT: u32 = 0x000F;
const WM_ERASEBKGND: u32 = 0x0014;
const WM_NCDESTROY: u32 = 0x0082;
const WM_NCCALCSIZE: u32 = 0x0083;
const WM_NCPAINT: u32 = 0x0085;
const WM_NCACTIVATE: u32 = 0x0086;
const WM_NCUAHDRAWCAPTION: u32 = 0x00AE;
const WM_NCUAHDRAWFRAME: u32 = 0x00AF;

const LVM_GETBKCOLOR: u32 = 0x1000;
const LVM_GETITEMCOUNT: u32 = 0x1004;
const LVM_GETITEMRECT: u32 = 0x100E;
const LVM_GETHEADER: u32 = 0x101F;
const HDM_GETITEMCOUNT: u32 = 0x1200;
const HDM_GETITEMRECT: u32 = 0x1207;

const SM_CXSIZEFRAME: i32 = 32;
const SM_CYSIZEFRAME: i32 = 33;
const SM_CXPADDEDBORDER: i32 = 92;

const FRAME_SUBCLASS_ID: usize = 0x534F_4E43;
const HEADER_SUBCLASS_ID: usize = 0x534F_4E48;
const LISTVIEW_SUBCLASS_ID: usize = 0x534F_4E4C;

#[repr(C)]
struct Margins {
    cx_left_width: i32,
    cx_right_width: i32,
    cy_top_height: i32,
    cy_bottom_height: i32,
}

type FnDwmSetWindowAttribute =
    unsafe extern "system" fn(hwnd: HWND, dw_attribute: u32, pv_attribute: *const c_void, cb_attribute: u32) -> i32;
type FnDwmExtendFrameIntoClientArea =
    unsafe extern "system" fn(hwnd: HWND, p_mar_inset: *const Margins) -> i32;

type FnGetWindowLongW = unsafe extern "system" fn(hwnd: HWND, n_index: i32) -> i32;
type FnSetWindowLongW = unsafe extern "system" fn(hwnd: HWND, n_index: i32, dw_new_long: i32) -> i32;
type FnSetWindowPos = unsafe extern "system" fn(
    hwnd: HWND,
    h_wnd_insert_after: HWND,
    x: i32,
    y: i32,
    cx: i32,
    cy: i32,
    u_flags: u32,
) -> i32;
type FnSetWindowRgn = unsafe extern "system" fn(hwnd: HWND, h_rgn: *mut c_void, b_redraw: i32) -> i32;
type FnSendMessageW =
    unsafe extern "system" fn(hwnd: HWND, msg: u32, w_param: usize, l_param: isize) -> isize;
type FnGetClientRect = unsafe extern "system" fn(hwnd: HWND, lp_rect: *mut RECT) -> i32;
type FnFillRect = unsafe extern "system" fn(h_dc: *mut c_void, lprc: *const RECT, hbr: *mut c_void) -> i32;
type FnIsZoomed = unsafe extern "system" fn(hwnd: HWND) -> i32;
type FnGetSystemMetrics = unsafe extern "system" fn(n_index: i32) -> i32;
type FnInvalidateRect =
    unsafe extern "system" fn(hwnd: HWND, lp_rect: *const RECT, b_erase: i32) -> i32;
type FnGetDC = unsafe extern "system" fn(hwnd: HWND) -> *mut c_void;
type FnReleaseDC = unsafe extern "system" fn(hwnd: HWND, h_dc: *mut c_void) -> i32;

type FnCreateSolidBrush = unsafe extern "system" fn(color: u32) -> *mut c_void;
type FnDeleteObject = unsafe extern "system" fn(ho: *mut c_void) -> i32;
type FnCreatePen = unsafe extern "system" fn(i_style: i32, c_width: i32, color: u32) -> *mut c_void;
type FnSelectObject = unsafe extern "system" fn(hdc: *mut c_void, h: *mut c_void) -> *mut c_void;
type FnMoveToEx =
    unsafe extern "system" fn(hdc: *mut c_void, x: i32, y: i32, lppt: *mut c_void) -> i32;
type FnLineTo = unsafe extern "system" fn(hdc: *mut c_void, x: i32, y: i32) -> i32;

type FnSetWindowTheme =
    unsafe extern "system" fn(hwnd: HWND, psz_sub_app_name: *const u16, psz_sub_id_list: *const u16) -> i32;

type SubclassProc = unsafe extern "system" fn(
    hwnd: HWND,
    u_msg: u32,
    w_param: usize,
    l_param: isize,
    u_id_subclass: usize,
    dw_ref_data: usize,
) -> isize;

type FnSetWindowSubclass = unsafe extern "system" fn(
    hwnd: HWND,
    pfn_subclass: SubclassProc,
    u_id_subclass: usize,
    dw_ref_data: usize,
) -> i32;
type FnDefSubclassProc =
    unsafe extern "system" fn(hwnd: HWND, u_msg: u32, w_param: usize, l_param: isize) -> isize;
type FnRemoveWindowSubclass = unsafe extern "system" fn(
    hwnd: HWND,
    pfn_subclass: SubclassProc,
    u_id_subclass: usize,
) -> i32;

struct Win32Api {
    dwm_set_attr: Option<FnDwmSetWindowAttribute>,
    dwm_extend_frame: Option<FnDwmExtendFrameIntoClientArea>,
    get_window_long: Option<FnGetWindowLongW>,
    set_window_long: Option<FnSetWindowLongW>,
    set_window_pos: Option<FnSetWindowPos>,
    set_window_rgn: Option<FnSetWindowRgn>,
    send_message: Option<FnSendMessageW>,
    get_client_rect: Option<FnGetClientRect>,
    fill_rect: Option<FnFillRect>,
    is_zoomed: Option<FnIsZoomed>,
    get_system_metrics: Option<FnGetSystemMetrics>,
    invalidate_rect: Option<FnInvalidateRect>,
    get_dc: Option<FnGetDC>,
    release_dc: Option<FnReleaseDC>,
    create_solid_brush: Option<FnCreateSolidBrush>,
    delete_object: Option<FnDeleteObject>,
    create_pen: Option<FnCreatePen>,
    select_object: Option<FnSelectObject>,
    move_to_ex: Option<FnMoveToEx>,
    line_to: Option<FnLineTo>,
    set_window_theme: Option<FnSetWindowTheme>,
    set_window_subclass: Option<FnSetWindowSubclass>,
    def_subclass_proc: Option<FnDefSubclassProc>,
    remove_window_subclass: Option<FnRemoveWindowSubclass>,
}

static WIN32_API: OnceLock<Win32Api> = OnceLock::new();
static ACTIVE_ACCENT_COLORREF: AtomicU32 = AtomicU32::new(0x00C3_822A);
static HEADER_COLORS: OnceLock<Mutex<HashMap<usize, (u32, u32)>>> = OnceLock::new();

unsafe fn load_proc<T>(dll: &[u8], name: &[u8]) -> Option<T> {
    let hmod = LoadLibraryA(dll.as_ptr());
    if hmod.is_null() {
        return None;
    }
    let proc = GetProcAddress(hmod, name.as_ptr())?;
    Some(std::mem::transmute_copy(&proc))
}

fn api() -> &'static Win32Api {
    WIN32_API.get_or_init(|| unsafe {
        Win32Api {
            dwm_set_attr: load_proc(b"dwmapi.dll\0", b"DwmSetWindowAttribute\0"),
            dwm_extend_frame: load_proc(b"dwmapi.dll\0", b"DwmExtendFrameIntoClientArea\0"),
            get_window_long: load_proc(b"user32.dll\0", b"GetWindowLongW\0"),
            set_window_long: load_proc(b"user32.dll\0", b"SetWindowLongW\0"),
            set_window_pos: load_proc(b"user32.dll\0", b"SetWindowPos\0"),
            set_window_rgn: load_proc(b"user32.dll\0", b"SetWindowRgn\0"),
            send_message: load_proc(b"user32.dll\0", b"SendMessageW\0"),
            get_client_rect: load_proc(b"user32.dll\0", b"GetClientRect\0"),
            fill_rect: load_proc(b"user32.dll\0", b"FillRect\0"),
            is_zoomed: load_proc(b"user32.dll\0", b"IsZoomed\0"),
            get_system_metrics: load_proc(b"user32.dll\0", b"GetSystemMetrics\0"),
            invalidate_rect: load_proc(b"user32.dll\0", b"InvalidateRect\0"),
            get_dc: load_proc(b"user32.dll\0", b"GetDC\0"),
            release_dc: load_proc(b"user32.dll\0", b"ReleaseDC\0"),
            create_solid_brush: load_proc(b"gdi32.dll\0", b"CreateSolidBrush\0"),
            delete_object: load_proc(b"gdi32.dll\0", b"DeleteObject\0"),
            create_pen: load_proc(b"gdi32.dll\0", b"CreatePen\0"),
            select_object: load_proc(b"gdi32.dll\0", b"SelectObject\0"),
            move_to_ex: load_proc(b"gdi32.dll\0", b"MoveToEx\0"),
            line_to: load_proc(b"gdi32.dll\0", b"LineTo\0"),
            set_window_theme: load_proc(b"uxtheme.dll\0", b"SetWindowTheme\0"),
            set_window_subclass: load_proc(b"comctl32.dll\0", b"SetWindowSubclass\0"),
            def_subclass_proc: load_proc(b"comctl32.dll\0", b"DefSubclassProc\0"),
            remove_window_subclass: load_proc(b"comctl32.dll\0", b"RemoveWindowSubclass\0"),
        }
    })
}

fn header_map() -> &'static Mutex<HashMap<usize, (u32, u32)>> {
    HEADER_COLORS.get_or_init(|| Mutex::new(HashMap::new()))
}

pub fn rgb_to_colorref(r: u8, g: u8, b: u8) -> u32 {
    ((b as u32) << 16) | ((g as u32) << 8) | (r as u32)
}

pub fn packed_rgb_to_colorref(rgb: u32) -> u32 {
    let r = ((rgb >> 16) & 0xFF) as u8;
    let g = ((rgb >> 8) & 0xFF) as u8;
    let b = (rgb & 0xFF) as u8;
    rgb_to_colorref(r, g, b)
}

unsafe extern "system" fn frame_subclass_proc(
    hwnd: HWND,
    u_msg: u32,
    w_param: usize,
    l_param: isize,
    u_id_subclass: usize,
    _dw_ref_data: usize,
) -> isize {
    let w32 = api();
    match u_msg {
        WM_NCACTIVATE => 1,
        WM_NCPAINT | WM_NCUAHDRAWCAPTION | WM_NCUAHDRAWFRAME => 0,
        WM_NCCALCSIZE => {
            if w_param != 0 && l_param != 0 {
                if let (Some(is_zoomed), Some(get_metrics)) = (w32.is_zoomed, w32.get_system_metrics) {
                    if is_zoomed(hwnd) != 0 {
                        let rc = &mut *(l_param as *mut RECT);
                        let padded = get_metrics(SM_CXPADDEDBORDER);
                        let cx = get_metrics(SM_CXSIZEFRAME) + padded;
                        let cy = get_metrics(SM_CYSIZEFRAME) + padded;
                        rc.left += cx;
                        rc.top += cy;
                        rc.right -= cx;
                        rc.bottom -= cy;
                    }
                }
            }
            0
        }
        WM_NCDESTROY => {
            if let Some(rm) = w32.remove_window_subclass {
                rm(hwnd, frame_subclass_proc, u_id_subclass);
            }
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
        _ => {
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
    }
}

unsafe extern "system" fn header_subclass_proc(
    hwnd: HWND,
    u_msg: u32,
    w_param: usize,
    l_param: isize,
    u_id_subclass: usize,
    _dw_ref_data: usize,
) -> isize {
    let w32 = api();
    let (hdr_bg_cr, accent_cr) = header_map()
        .lock()
        .ok()
        .and_then(|m| m.get(&(hwnd as usize)).copied())
        .unwrap_or((0x001E_130E, 0x00FF_CC33));

    match u_msg {
        WM_ERASEBKGND => {
            if let (Some(get_cr), Some(fill_rect), Some(mk_br), Some(del_obj)) = (
                w32.get_client_rect,
                w32.fill_rect,
                w32.create_solid_brush,
                w32.delete_object,
            ) {
                let mut rc = RECT {
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                };
                if get_cr(hwnd, &mut rc) != 0 && w_param != 0 {
                    let br = mk_br(hdr_bg_cr);
                    if !br.is_null() {
                        fill_rect(w_param as *mut c_void, &rc, br);
                        del_obj(br);
                        return 1;
                    }
                }
            }
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                1
            }
        }
        WM_PAINT => {
            let res = if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            };

            if let (
                Some(send_msg),
                Some(get_cr),
                Some(get_dc),
                Some(rel_dc),
                Some(fill_rect),
                Some(mk_br),
                Some(del_obj),
            ) = (
                w32.send_message,
                w32.get_client_rect,
                w32.get_dc,
                w32.release_dc,
                w32.fill_rect,
                w32.create_solid_brush,
                w32.delete_object,
            ) {
                let mut client_rc = RECT {
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                };
                if get_cr(hwnd, &mut client_rc) != 0 {
                    let count = send_msg(hwnd, HDM_GETITEMCOUNT, 0, 0);
                    let mut max_right = 0i32;
                    for i in 0..count {
                        let mut item_rc = RECT {
                            left: 0,
                            top: 0,
                            right: 0,
                            bottom: 0,
                        };
                        if send_msg(
                            hwnd,
                            HDM_GETITEMRECT,
                            i as usize,
                            (&mut item_rc as *mut RECT) as isize,
                        ) != 0
                        {
                            if item_rc.right > max_right {
                                max_right = item_rc.right;
                            }
                        }
                    }

                    if client_rc.right > max_right {
                        let hdc = get_dc(hwnd);
                        if !hdc.is_null() {
                            let trail_rc = RECT {
                                left: max_right,
                                top: client_rc.top,
                                right: client_rc.right,
                                bottom: client_rc.bottom,
                            };
                            let br = mk_br(hdr_bg_cr);
                            if !br.is_null() {
                                fill_rect(hdc, &trail_rc, br);
                                del_obj(br);
                            }

                            if let (Some(mk_pen), Some(sel_obj), Some(move_to), Some(line_to)) =
                                (w32.create_pen, w32.select_object, w32.move_to_ex, w32.line_to)
                            {
                                let pen = mk_pen(0, 1, accent_cr);
                                if !pen.is_null() {
                                    let old = sel_obj(hdc, pen);
                                    let y = (client_rc.bottom - 1).max(0);
                                    move_to(hdc, max_right, y, std::ptr::null_mut());
                                    line_to(hdc, client_rc.right, y);
                                    sel_obj(hdc, old);
                                    del_obj(pen);
                                }
                            }
                            rel_dc(hwnd, hdc);
                        }
                    }
                }
            }
            res
        }
        WM_NCDESTROY => {
            if let Ok(mut m) = header_map().lock() {
                m.remove(&(hwnd as usize));
            }
            if let Some(rm) = w32.remove_window_subclass {
                rm(hwnd, header_subclass_proc, u_id_subclass);
            }
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
        _ => {
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
    }
}

/// Applies native Windows 10/11 DWM visual attributes and installs a Win32 non-client
/// subclass on `hwnd_val` so Windows never paints the classic white caption bar over custom chrome.
pub fn apply_dwm(hwnd_val: usize) -> bool {
    if hwnd_val == 0 {
        return false;
    }
    let w32 = api();
    let Some(dwm_set_attr) = w32.dwm_set_attr else {
        return false;
    };
    let hwnd = hwnd_val as HWND;

    unsafe {
        if let (Some(get_wl), Some(set_wl)) = (w32.get_window_long, w32.set_window_long) {
            let mut style = get_wl(hwnd, GWL_STYLE);
            style &= !(WS_CAPTION | WS_BORDER | WS_DLGFRAME);
            style |= WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU;
            set_wl(hwnd, GWL_STYLE, style);
        }

        if let Some(set_rgn) = w32.set_window_rgn {
            set_rgn(hwnd, std::ptr::null_mut(), 1);
        }

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

        let nc_policy = DWMNCRP_ENABLED;
        let _ = dwm_set_attr(
            hwnd,
            DWMWA_NCRENDERING_POLICY,
            (&nc_policy as *const i32).cast(),
            std::mem::size_of::<i32>() as u32,
        );

        let corner = DWMWCP_ROUND;
        let _ = dwm_set_attr(
            hwnd,
            DWMWA_WINDOW_CORNER_PREFERENCE,
            (&corner as *const i32).cast(),
            std::mem::size_of::<i32>() as u32,
        );

        let border_color = ACTIVE_ACCENT_COLORREF.load(Ordering::Relaxed);
        let _ = dwm_set_attr(
            hwnd,
            DWMWA_BORDER_COLOR,
            (&border_color as *const u32).cast(),
            std::mem::size_of::<u32>() as u32,
        );

        if let Some(extend) = w32.dwm_extend_frame {
            let margins = Margins {
                cx_left_width: 0,
                cx_right_width: 0,
                cy_top_height: 0,
                cy_bottom_height: 1,
            };
            let _ = extend(hwnd, &margins);
        }

        if let Some(subclass) = w32.set_window_subclass {
            let _ = subclass(hwnd, frame_subclass_proc, FRAME_SUBCLASS_ID, 0);
        }

        if let Some(set_pos) = w32.set_window_pos {
            set_pos(
                hwnd,
                std::ptr::null_mut(),
                0,
                0,
                0,
                0,
                SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE,
            );
        }
        true
    }
}

/// Updates the active Win11 DWM border accent color for `hwnd_val`.
pub fn set_dwm_accent(hwnd_val: usize, r: u8, g: u8, b: u8) -> bool {
    let cr = rgb_to_colorref(r, g, b);
    ACTIVE_ACCENT_COLORREF.store(cr, Ordering::Relaxed);
    if hwnd_val == 0 {
        return false;
    }
    let w32 = api();
    let Some(dwm_set_attr) = w32.dwm_set_attr else {
        return false;
    };
    unsafe {
        let _ = dwm_set_attr(
            hwnd_val as HWND,
            DWMWA_BORDER_COLOR,
            (&cr as *const u32).cast(),
            std::mem::size_of::<u32>() as u32,
        );
    }
    true
}

/// Resolves the ListView background COLORREF via LVM_GETBKCOLOR, falling back to colCard.
unsafe fn resolve_listview_bg_cr(w32: &Win32Api, hwnd: HWND) -> u32 {
    if let Some(send_msg) = w32.send_message {
        let bk = send_msg(hwnd, LVM_GETBKCOLOR, 0, 0) as u32;
        if bk != 0xFFFF_FFFF && bk != 0xFF00_0000 {
            return bk & 0x00FF_FFFF;
        }
    }
    0x002B_1B13 // RGB(19, 27, 43)
}

/// Subclass for DarkMode_Explorer SysListView32: overpaints the empty region below the
/// last item (and right of the last column) so Windows 11 uxtheme column divider lines
/// never extend down into empty space.
unsafe extern "system" fn listview_subclass_proc(
    hwnd: HWND,
    u_msg: u32,
    w_param: usize,
    l_param: isize,
    u_id_subclass: usize,
    _dw_ref_data: usize,
) -> isize {
    let w32 = api();
    match u_msg {
        WM_ERASEBKGND => {
            if let (Some(get_cr), Some(fill_rect), Some(mk_br), Some(del_obj)) = (
                w32.get_client_rect,
                w32.fill_rect,
                w32.create_solid_brush,
                w32.delete_object,
            ) {
                let mut rc = RECT {
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                };
                if get_cr(hwnd, &mut rc) != 0 && w_param != 0 {
                    let bg_cr = resolve_listview_bg_cr(w32, hwnd);
                    let br = mk_br(bg_cr);
                    if !br.is_null() {
                        fill_rect(w_param as *mut c_void, &rc, br);
                        del_obj(br);
                        return 1;
                    }
                }
            }
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                1
            }
        }
        WM_PAINT => {
            let res = if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            };

            if let (
                Some(send_msg),
                Some(get_cr),
                Some(get_dc),
                Some(rel_dc),
                Some(fill_rect),
                Some(mk_br),
                Some(del_obj),
            ) = (
                w32.send_message,
                w32.get_client_rect,
                w32.get_dc,
                w32.release_dc,
                w32.fill_rect,
                w32.create_solid_brush,
                w32.delete_object,
            ) {
                let mut client_rc = RECT {
                    left: 0,
                    top: 0,
                    right: 0,
                    bottom: 0,
                };
                if get_cr(hwnd, &mut client_rc) != 0 {
                    let mut hdr_h = 0i32;
                    let hdr_raw = send_msg(hwnd, LVM_GETHEADER, 0, 0);
                    if hdr_raw != 0 {
                        let mut hrc = RECT {
                            left: 0,
                            top: 0,
                            right: 0,
                            bottom: 0,
                        };
                        if get_cr(hdr_raw as HWND, &mut hrc) != 0 {
                            hdr_h = (hrc.bottom - hrc.top).max(0);
                        }
                    }

                    let count = send_msg(hwnd, LVM_GETITEMCOUNT, 0, 0) as i32;
                    let mut empty_top = hdr_h;
                    let mut max_right = 0i32;
                    if count > 0 {
                        // LVIR_BOUNDS == 0 -> left == 0 on entry
                        let mut last_rc = RECT {
                            left: 0,
                            top: 0,
                            right: 0,
                            bottom: 0,
                        };
                        if send_msg(
                            hwnd,
                            LVM_GETITEMRECT,
                            (count - 1) as usize,
                            (&mut last_rc as *mut RECT) as isize,
                        ) != 0
                        {
                            empty_top = last_rc.bottom.max(hdr_h);
                            max_right = last_rc.right;
                        }
                    }

                    let need_bottom = empty_top < client_rc.bottom;
                    let need_right =
                        count > 0 && max_right > 0 && max_right < client_rc.right && empty_top > hdr_h;
                    if need_bottom || need_right {
                        let hdc = get_dc(hwnd);
                        if !hdc.is_null() {
                            let bg_cr = resolve_listview_bg_cr(w32, hwnd);
                            let br = mk_br(bg_cr);
                            if !br.is_null() {
                                if need_bottom {
                                    let fill_rc = RECT {
                                        left: client_rc.left,
                                        top: empty_top,
                                        right: client_rc.right,
                                        bottom: client_rc.bottom,
                                    };
                                    fill_rect(hdc, &fill_rc, br);
                                }
                                if need_right {
                                    let right_rc = RECT {
                                        left: max_right,
                                        top: hdr_h,
                                        right: client_rc.right,
                                        bottom: empty_top,
                                    };
                                    fill_rect(hdc, &right_rc, br);
                                }
                                del_obj(br);
                            }
                            rel_dc(hwnd, hdc);
                        }
                    }
                }
            }

            res
        }
        WM_NCDESTROY => {
            if let Some(rm) = w32.remove_window_subclass {
                rm(hwnd, listview_subclass_proc, u_id_subclass);
            }
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
        _ => {
            if let Some(def_proc) = w32.def_subclass_proc {
                def_proc(hwnd, u_msg, w_param, l_param)
            } else {
                0
            }
        }
    }
}

/// Applies DarkMode_Explorer scrollbar theming on a ListView, subclasses SysListView32
/// to suppress empty-area column gridlines, and subclasses its child SysHeader32 so
/// trailing header space never flashes white on Windows 10/11.
pub fn style_listview(hwnd_val: usize, header_bg_rgb: u32, accent_rgb: u32) -> bool {
    if hwnd_val == 0 {
        return false;
    }
    let w32 = api();
    let lv_hwnd = hwnd_val as HWND;
    let hdr_bg_cr = packed_rgb_to_colorref(header_bg_rgb);
    let accent_cr = packed_rgb_to_colorref(accent_rgb);

    unsafe {
        if let (Some(get_wl), Some(set_wl)) = (w32.get_window_long, w32.set_window_long) {
            let style = get_wl(lv_hwnd, GWL_STYLE);
            if (style & WS_CLIPCHILDREN) == 0 {
                let _ = set_wl(lv_hwnd, GWL_STYLE, style | WS_CLIPCHILDREN);
            }
        }

        if let Some(set_theme) = w32.set_window_theme {
            // "DarkMode_Explorer" UTF-16 null-terminated
            const DARK_EXPLORER: &[u16] = &[
                68, 97, 114, 107, 77, 111, 100, 101, 95, 69, 120, 112, 108, 111, 114, 101, 114, 0,
            ];
            let _ = set_theme(lv_hwnd, DARK_EXPLORER.as_ptr(), std::ptr::null());
        }

        if let Some(subclass) = w32.set_window_subclass {
            let _ = subclass(lv_hwnd, listview_subclass_proc, LISTVIEW_SUBCLASS_ID, 0);
        }

        if let Some(send_msg) = w32.send_message {
            let hdr_raw = send_msg(lv_hwnd, LVM_GETHEADER, 0, 0);
            if hdr_raw != 0 {
                let hdr_hwnd = hdr_raw as HWND;
                if let Ok(mut m) = header_map().lock() {
                    m.insert(hdr_raw as usize, (hdr_bg_cr, accent_cr));
                }
                if let Some(subclass) = w32.set_window_subclass {
                    let _ = subclass(hdr_hwnd, header_subclass_proc, HEADER_SUBCLASS_ID, 0);
                }
                if let Some(inv) = w32.invalidate_rect {
                    inv(hdr_hwnd, std::ptr::null(), 1);
                }
                return true;
            }
        }
    }
    false
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_rgb_to_colorref_and_null_hwnd_safe() {
        assert_eq!(rgb_to_colorref(51, 204, 255), 0x00FF_CC33);
        assert_eq!(packed_rgb_to_colorref(0x33CCFF), 0x00FF_CC33);
        assert!(!apply_dwm(0));
        assert!(!set_dwm_accent(0, 138, 43, 226));
        assert!(!style_listview(0, 0x0E131E, 0x33CCFF));
    }
}
