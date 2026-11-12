mod inject;
mod protocol;
mod public_list;
mod query;

use query::ServerTarget;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::ptr::null_mut;

unsafe fn read_cstr(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    Some(CStr::from_ptr(ptr).to_string_lossy().into_owned())
}

fn into_cstr(s: String) -> *mut c_char {
    let cleaned = s.replace('\0', "");
    match CString::new(cleaned) {
        Ok(cs) => cs.into_raw(),
        Err(_) => null_mut(),
    }
}

fn guard_ptr<F: FnOnce() -> *mut c_char>(f: F) -> *mut c_char {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or(null_mut())
}

fn guard_u64<F: FnOnce() -> u64>(f: F) -> u64 {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or(0)
}

#[no_mangle]
pub extern "C" fn core_version() -> *mut c_char {
    guard_ptr(|| into_cstr(format!("samp_core {}", env!("CARGO_PKG_VERSION"))))
}

#[no_mangle]
pub extern "C" fn core_free_str(ptr: *mut c_char) {
    if !ptr.is_null() {
        let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
            drop(CString::from_raw(ptr));
        }));
    }
}

#[no_mangle]
pub extern "C" fn core_resolve_host(host: *const c_char) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::resolve_host(&h) {
            Some(ip) => into_cstr(ip.to_string()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_server(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        let qr = query::query_server_sync(&h, port);
        match serde_json::to_string(&qr) {
            Ok(json) => into_cstr(json),
            Err(_) => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_players(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::query_players_sync(&h, port) {
            Some(pl) => serde_json::to_string(&pl).map(into_cstr).unwrap_or(null_mut()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_rules(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::query_rules_sync(&h, port) {
            Some(rl) => serde_json::to_string(&rl).map(into_cstr).unwrap_or(null_mut()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_launch_and_inject(
    gta_path: *const c_char,
    dll_path: *const c_char,
    work_dir: *const c_char,
    nick: *const c_char,
    ip: *const c_char,
    port: *const c_char,
    pass: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(gta), Some(dll), Some(cwd), Some(n), Some(i), Some(p)) = (
            read_cstr(gta_path),
            read_cstr(dll_path),
            read_cstr(work_dir),
            read_cstr(nick),
            read_cstr(ip),
            read_cstr(port),
        ) else {
            return into_cstr("ข้อมูล parameter ไม่ครบ".to_string());
        };
        let pw = read_cstr(pass).unwrap_or_default();
        match inject::launch_and_inject(&gta, &dll, &cwd, &n, &i, &p, &pw) {
            Ok(()) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_scan_start(servers_json: *const c_char, concurrency: u32) -> u64 {
    guard_u64(|| {
        let Some(raw) = (unsafe { read_cstr(servers_json) }) else {
            return 0;
        };
        let Ok(targets) = serde_json::from_str::<Vec<ServerTarget>>(&raw) else {
            return 0;
        };
        let conc = if concurrency == 0 { 64 } else { concurrency as usize };
        query::start_batch_scan(targets, conc)
    })
}

#[no_mangle]
pub extern "C" fn core_public_scan_start(cache_path: *const c_char, concurrency: u32) -> u64 {
    guard_u64(|| {
        let cp = unsafe { read_cstr(cache_path) }.filter(|s| !s.is_empty());
        let conc = if concurrency == 0 { 64 } else { concurrency as usize };
        public_list::start_public_scan(cp, conc)
    })
}

#[no_mangle]
pub extern "C" fn core_scan_poll(handle: u64, max_items: u32) -> *mut c_char {
    guard_ptr(|| {
        let max_n = if max_items == 0 { 64 } else { max_items as usize };
        let batch = query::poll_scan(handle, max_n);
        serde_json::to_string(&batch)
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_scan_cancel(handle: u64) {
    let _ = catch_unwind(AssertUnwindSafe(|| {
        query::remove_scan(handle);
    }));
}
