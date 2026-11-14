use crate::config::{ansi_cstr_to_string, get_guard_config, to_wide};
use crate::iat;
use std::cell::Cell;
use std::ffi::c_void;
use std::ptr::null;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use std::sync::{Mutex, OnceLock};
use windows_sys::Win32::Foundation::HANDLE;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress, LoadLibraryW};
use windows_sys::Win32::System::Threading::{
    GetCurrentThread, SetThreadPriority, THREAD_PRIORITY_ABOVE_NORMAL,
};

static AUDIO_SHIELD_ACTIVE: AtomicBool = AtomicBool::new(false);
static PATCHED_SV_BASS: AtomicBool = AtomicBool::new(false);

static ORIG_BASS_RECORD_INIT: AtomicUsize = AtomicUsize::new(0);
static ORIG_BASS_RECORD_START: AtomicUsize = AtomicUsize::new(0);
static FN_BASS_GET_DEV_INFO: AtomicUsize = AtomicUsize::new(0);
static FN_BASS_GET_DEVICE: AtomicUsize = AtomicUsize::new(0);

static REAL_RECORD_PROC: AtomicUsize = AtomicUsize::new(0);
static AVRT_MOD: AtomicUsize = AtomicUsize::new(0);

struct ThreadMmcssGuard(Cell<usize>);

impl ThreadMmcssGuard {
    const fn new() -> Self {
        Self(Cell::new(0))
    }

    fn ensure_registered(&self, task_name: &str) {
        if self.0.get() != 0 {
            return;
        }
        unsafe {
            if AVRT_MOD.load(Ordering::Relaxed) == 0 {
                let avrt_w = to_wide("avrt.dll");
                let mut hm = GetModuleHandleW(avrt_w.as_ptr());
                if hm.is_null() {
                    hm = LoadLibraryW(avrt_w.as_ptr());
                }
                if !hm.is_null() {
                    let _ = AVRT_MOD.compare_exchange(
                        0,
                        hm as usize,
                        Ordering::SeqCst,
                        Ordering::SeqCst,
                    );
                }
            }
            let hm = AVRT_MOD.load(Ordering::Relaxed) as *mut c_void;
            if hm.is_null() {
                return;
            }
            if let Some(p) = GetProcAddress(hm as _, b"AvSetMmThreadCharacteristicsW\0".as_ptr()) {
                type FnAvSet = unsafe extern "system" fn(*const u16, *mut u32) -> HANDLE;
                let av_set: FnAvSet = std::mem::transmute(p);
                let task_w = to_wide(task_name);
                let mut task_idx: u32 = 0;
                let h = av_set(task_w.as_ptr(), &mut task_idx);
                if !h.is_null() {
                    self.0.set(h as usize);
                }
                let _ = SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_ABOVE_NORMAL);
            }
        }
    }
}

impl Drop for ThreadMmcssGuard {
    fn drop(&mut self) {
        let h = self.0.replace(0);
        let avrt = AVRT_MOD.load(Ordering::Relaxed);
        if h != 0 && avrt != 0 {
            unsafe {
                if let Some(p) =
                    GetProcAddress(avrt as _, b"AvRevertMmThreadCharacteristics\0".as_ptr())
                {
                    type FnRev = unsafe extern "system" fn(HANDLE) -> i32;
                    let rev: FnRev = std::mem::transmute(p);
                    let _ = rev(h as HANDLE);
                }
            }
        }
    }
}

thread_local! {
    // MMCSS handles have strict thread affinity: always register and revert on the SAME thread.
    static TLS_MMCSS_GUARD: ThreadMmcssGuard = const { ThreadMmcssGuard::new() };
}

const BASS_DEVICE_ENABLED: u32 = 1;
const BASS_DEVICE_LOOPBACK: u32 = 8;

#[repr(C)]
struct BassDeviceInfo {
    name: *const i8,
    driver: *const i8,
    flags: u32,
}

type FnBassRecordInit = unsafe extern "system" fn(i32) -> i32;
type FnBassRecordGetDeviceInfo = unsafe extern "system" fn(u32, *mut BassDeviceInfo) -> i32;
type FnBassRecordGetDevice = unsafe extern "system" fn() -> i32;
type FnBassRecordStart = unsafe extern "system" fn(u32, u32, u32, usize, *mut c_void) -> u32;
type FnBassRecordProc = unsafe extern "system" fn(u32, *const c_void, u32, *mut c_void) -> i32;

fn active_mic_store() -> &'static Mutex<String> {
    static MIC: OnceLock<Mutex<String>> = OnceLock::new();
    MIC.get_or_init(|| Mutex::new(String::new()))
}

fn set_active_mic(name: &str) {
    if let Ok(mut g) = active_mic_store().lock() {
        *g = name.to_string();
    }
}

pub fn get_active_mic() -> String {
    active_mic_store()
        .lock()
        .map(|g| g.clone())
        .unwrap_or_default()
}

pub fn is_audio_shield_active() -> bool {
    AUDIO_SHIELD_ACTIVE.load(Ordering::Relaxed)
}

/// Applies Windows MMCSS task scheduling (`"Pro Audio"` or `"Games"`) temporarily to the current thread.
/// Automatically reverts on that exact same thread via TLS Drop when the thread exits.
pub fn apply_mmcss_to_current_thread(task_name: &str) {
    TLS_MMCSS_GUARD.with(|g| g.ensure_registered(task_name));
}

unsafe fn find_bass_mic_by_name(needle: &str) -> Option<(i32, String)> {
    let info_addr = FN_BASS_GET_DEV_INFO.load(Ordering::Relaxed);
    if info_addr == 0 {
        return None;
    }
    let get_info: FnBassRecordGetDeviceInfo = std::mem::transmute(info_addr);
    let needle_low = needle.trim().to_ascii_lowercase();
    if needle_low.is_empty() {
        return None;
    }

    let mut sub_match: Option<(i32, String)> = None;
    for idx in 0u32..64u32 {
        let mut info = BassDeviceInfo {
            name: null(),
            driver: null(),
            flags: 0,
        };
        if get_info(idx, &mut info) == 0 {
            break;
        }
        let enabled = (info.flags & BASS_DEVICE_ENABLED) != 0;
        let loopback = (info.flags & BASS_DEVICE_LOOPBACK) != 0;
        if !enabled || loopback || info.name.is_null() {
            continue;
        }
        let dev_name = ansi_cstr_to_string(info.name);
        let dev_low = dev_name.to_ascii_lowercase();
        if dev_low == needle_low {
            return Some((idx as i32, dev_name));
        }
        if sub_match.is_none() && (dev_low.contains(&needle_low) || needle_low.contains(&dev_low)) {
            sub_match = Some((idx as i32, dev_name));
        }
    }
    sub_match
}

unsafe fn resolve_bass_device_name(idx: i32) -> String {
    let info_addr = FN_BASS_GET_DEV_INFO.load(Ordering::Relaxed);
    if info_addr == 0 || idx < 0 {
        return String::new();
    }
    let get_info: FnBassRecordGetDeviceInfo = std::mem::transmute(info_addr);
    let mut info = BassDeviceInfo {
        name: null(),
        driver: null(),
        flags: 0,
    };
    if get_info(idx as u32, &mut info) != 0 && !info.name.is_null() {
        return ansi_cstr_to_string(info.name);
    }
    String::new()
}

unsafe extern "system" fn hooked_bass_record_init(device: i32) -> i32 {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let orig_addr = ORIG_BASS_RECORD_INIT.load(Ordering::SeqCst);
        if orig_addr == 0 {
            return 0;
        }
        let orig: FnBassRecordInit = std::mem::transmute(orig_addr);
        let pref = &get_guard_config().preferred_mic;

        if !pref.trim().is_empty() {
            if let Some((target_idx, matched_name)) = find_bass_mic_by_name(pref) {
                let res = orig(target_idx);
                if res != 0 {
                    set_active_mic(&matched_name);
                    return res;
                }
            }
        }

        let res = orig(device);
        if res != 0 {
            let cur_idx = if device >= 0 {
                device
            } else {
                let gd = FN_BASS_GET_DEVICE.load(Ordering::Relaxed);
                if gd != 0 {
                    let fn_gd: FnBassRecordGetDevice = std::mem::transmute(gd);
                    fn_gd()
                } else {
                    device
                }
            };
            let nm = resolve_bass_device_name(cur_idx);
            if !nm.is_empty() {
                set_active_mic(&nm);
            }
        }
        res
    }))
    .unwrap_or(0)
}

unsafe extern "system" fn hooked_record_proc_trampoline(
    handle: u32,
    buffer: *const c_void,
    length: u32,
    user: *mut c_void,
) -> i32 {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        apply_mmcss_to_current_thread("Pro Audio");
        let real_addr = REAL_RECORD_PROC.load(Ordering::Acquire);
        if real_addr == 0 {
            return 1;
        }
        let real_fn: FnBassRecordProc = std::mem::transmute(real_addr);
        real_fn(handle, buffer, length, user)
    }))
    .unwrap_or(1)
}

unsafe extern "system" fn hooked_bass_record_start(
    freq: u32,
    chans: u32,
    flags: u32,
    proc_ptr: usize,
    user: *mut c_void,
) -> u32 {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let orig_addr = ORIG_BASS_RECORD_START.load(Ordering::SeqCst);
        if orig_addr == 0 {
            return 0;
        }
        let orig: FnBassRecordStart = std::mem::transmute(orig_addr);

        if proc_ptr != 0 {
            REAL_RECORD_PROC.store(proc_ptr, Ordering::Release);
            orig(
                freq,
                chans,
                flags,
                hooked_record_proc_trampoline as *const () as usize,
                user,
            )
        } else {
            apply_mmcss_to_current_thread("Pro Audio");
            orig(freq, chans, flags, 0, user)
        }
    }))
    .unwrap_or(0)
}

/// Polls for `sampvoice.asi` + `bass.dll` appearance after Loader Lock exits and hooks BASS IAT safely.
pub fn poll_and_attach_audio_shield() {
    if PATCHED_SV_BASS.load(Ordering::Relaxed) {
        return;
    }
    let sv_w = to_wide("sampvoice.asi");
    let bass_w = to_wide("bass.dll");
    let sv_mod = unsafe { GetModuleHandleW(sv_w.as_ptr()) };
    let bass_mod = unsafe { GetModuleHandleW(bass_w.as_ptr()) };
    if sv_mod.is_null() || bass_mod.is_null() {
        return;
    }

    unsafe {
        if let Some(p) = GetProcAddress(bass_mod, b"BASS_RecordGetDeviceInfo\0".as_ptr()) {
            let _ = FN_BASS_GET_DEV_INFO.compare_exchange(
                0,
                p as usize,
                Ordering::SeqCst,
                Ordering::SeqCst,
            );
        }
        if let Some(p) = GetProcAddress(bass_mod, b"BASS_RecordGetDevice\0".as_ptr()) {
            let _ =
                FN_BASS_GET_DEVICE.compare_exchange(0, p as usize, Ordering::SeqCst, Ordering::SeqCst);
        }

        let mut hooked_any = false;
        if let Some(orig) = iat::patch_module_iat(
            sv_mod,
            "bass.dll",
            "BASS_RecordInit",
            hooked_bass_record_init as *const () as usize,
        ) {
            let _ =
                ORIG_BASS_RECORD_INIT.compare_exchange(0, orig, Ordering::SeqCst, Ordering::SeqCst);
            hooked_any = true;
        }
        if let Some(orig) = iat::patch_module_iat(
            sv_mod,
            "bass.dll",
            "BASS_RecordStart",
            hooked_bass_record_start as *const () as usize,
        ) {
            let _ = ORIG_BASS_RECORD_START.compare_exchange(
                0,
                orig,
                Ordering::SeqCst,
                Ordering::SeqCst,
            );
            hooked_any = true;
        }

        if hooked_any {
            PATCHED_SV_BASS.store(true, Ordering::SeqCst);
            AUDIO_SHIELD_ACTIVE.store(true, Ordering::SeqCst);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_tls_mmcss_guard_registers_and_reverts_cleanly_on_thread_exit() {
        let jh = std::thread::spawn(|| {
            apply_mmcss_to_current_thread("Pro Audio");
        });
        jh.join().unwrap();
    }
}
