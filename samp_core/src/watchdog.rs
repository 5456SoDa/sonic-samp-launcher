use crate::crash::{self, CrashInfo};
use crate::db;
use crate::win_ro;
use serde::Serialize;
use std::fs;
use std::path::{Path, PathBuf};
use std::sync::{Mutex, OnceLock};
use std::time::{Duration, Instant, SystemTime};
use windows_sys::Win32::Foundation::{CloseHandle, HANDLE, INVALID_HANDLE_VALUE, WAIT_TIMEOUT};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Process32FirstW, Process32NextW, PROCESSENTRY32W, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::Threading::{GetExitCodeProcess, WaitForSingleObject};

struct OwnedProcessHandle(HANDLE);
unsafe impl Send for OwnedProcessHandle {}

impl Drop for OwnedProcessHandle {
    fn drop(&mut self) {
        if !self.0.is_null() && self.0 != INVALID_HANDLE_VALUE {
            unsafe {
                CloseHandle(self.0);
            }
        }
    }
}

struct WatchSession {
    pid: u32,
    proc_handle: OwnedProcessHandle,
    game_dir: PathBuf,
    server_key: String,
    prev_crash_mtime: Option<SystemTime>,
    started_at: SystemTime,
    last_tick: Instant,
    accum_nanos: u64,
    session_seconds: u64,
}

#[derive(Debug, Clone, Default, Serialize)]
pub struct WatchdogStatus {
    pub active: bool,
    pub running: bool,
    pub exited: bool,
    pub crashed: bool,
    pub pid: u32,
    pub server_key: String,
    pub session_seconds: u64,
    pub total_playtime: i64,
    pub exit_code: u32,
    pub crash: Option<CrashInfo>,
}

fn global_watchdog() -> &'static Mutex<Option<WatchSession>> {
    static WD: OnceLock<Mutex<Option<WatchSession>>> = OnceLock::new();
    WD.get_or_init(|| Mutex::new(None))
}

fn read_crash_mtime(game_dir: &Path) -> Option<SystemTime> {
    let p = game_dir.join("_mod_backup").join("crash_latest.json");
    fs::metadata(p).ok()?.modified().ok()
}

pub fn clamp_active_delta(elapsed: Duration) -> Option<Duration> {
    if elapsed.as_secs() > 30 {
        None
    } else {
        Some(elapsed)
    }
}

pub fn is_new_crash_file(
    prev_mtime: Option<SystemTime>,
    cur_mtime: Option<SystemTime>,
    started_at: SystemTime,
) -> bool {
    let Some(cur) = cur_mtime else {
        return false;
    };
    match prev_mtime {
        Some(prev) => cur > prev,
        None => {
            let floor = started_at
                .checked_sub(Duration::from_secs(2))
                .unwrap_or(started_at);
            cur >= floor
        }
    }
}

pub fn register_launch(
    pid: u32,
    proc_handle: HANDLE,
    game_dir: &Path,
    ip: &str,
    port: &str,
) {
    let server_key = if !ip.trim().is_empty() && !port.trim().is_empty() {
        db::normalize_server_key(ip, port)
    } else {
        String::new()
    };
    let prev_crash_mtime = read_crash_mtime(game_dir);
    let sess = WatchSession {
        pid,
        proc_handle: OwnedProcessHandle(proc_handle),
        game_dir: game_dir.to_path_buf(),
        server_key,
        prev_crash_mtime,
        started_at: SystemTime::now(),
        last_tick: Instant::now(),
        accum_nanos: 0,
        session_seconds: 0,
    };
    if let Ok(mut guard) = global_watchdog().lock() {
        *guard = Some(sess);
    }
}

fn flush_elapsed(sess: &mut WatchSession, now: Instant) -> i64 {
    let elapsed = now.saturating_duration_since(sess.last_tick);
    sess.last_tick = now;
    if let Some(valid) = clamp_active_delta(elapsed) {
        sess.accum_nanos = sess.accum_nanos.saturating_add(valid.as_nanos() as u64);
    }
    let whole_secs = sess.accum_nanos / 1_000_000_000;
    sess.accum_nanos %= 1_000_000_000;
    if whole_secs > 0 {
        sess.session_seconds = sess.session_seconds.saturating_add(whole_secs);
        if !sess.server_key.is_empty() {
            return db::add_playtime(&sess.server_key, whole_secs as i64).unwrap_or(-1);
        }
    }
    if !sess.server_key.is_empty() {
        db::add_playtime(&sess.server_key, 0).unwrap_or(-1)
    } else {
        0
    }
}

pub fn poll_watchdog() -> WatchdogStatus {
    let Ok(mut guard) = global_watchdog().lock() else {
        return WatchdogStatus::default();
    };
    let Some(ref mut sess) = *guard else {
        return WatchdogStatus::default();
    };

    let now = Instant::now();
    let total_playtime = flush_elapsed(sess, now);
    let wait = unsafe { WaitForSingleObject(sess.proc_handle.0, 0) };

    if wait == WAIT_TIMEOUT {
        return WatchdogStatus {
            active: true,
            running: true,
            exited: false,
            crashed: false,
            pid: sess.pid,
            server_key: sess.server_key.clone(),
            session_seconds: sess.session_seconds,
            total_playtime,
            exit_code: 0,
            crash: None,
        };
    }

    let mut exit_code: u32 = 0;
    unsafe {
        let _ = GetExitCodeProcess(sess.proc_handle.0, &mut exit_code);
    }

    let cur_mtime = read_crash_mtime(&sess.game_dir);
    let mut crashed = is_new_crash_file(sess.prev_crash_mtime, cur_mtime, sess.started_at);
    let crash_info = if crashed {
        let st = crash::get_guard_status(&sess.game_dir);
        if st.crash.is_none() {
            crashed = false;
        }
        st.crash
    } else {
        None
    };

    let res = WatchdogStatus {
        active: false,
        running: false,
        exited: true,
        crashed,
        pid: sess.pid,
        server_key: sess.server_key.clone(),
        session_seconds: sess.session_seconds,
        total_playtime,
        exit_code,
        crash: crash_info,
    };

    *guard = None;
    res
}

pub fn stop_watchdog() {
    if let Ok(mut guard) = global_watchdog().lock() {
        if let Some(ref mut sess) = *guard {
            let _ = flush_elapsed(sess, Instant::now());
        }
        *guard = None;
    }
}

pub fn is_gta_sa_running() -> bool {
    if let Ok(guard) = global_watchdog().lock() {
        if let Some(ref sess) = *guard {
            let w = unsafe { WaitForSingleObject(sess.proc_handle.0, 0) };
            if w == WAIT_TIMEOUT {
                return true;
            }
            // Wait until poll_watchdog picks up the crash transition instead of dropping session here
            return false;
        }
        drop(guard);
    }

    let snap = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snap.is_null() || snap == INVALID_HANDLE_VALUE {
        return false;
    }
    let _snap_guard = OwnedProcessHandle(snap);

    let mut entry: PROCESSENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<PROCESSENTRY32W>() as u32;
    let mut ok = unsafe { Process32FirstW(snap, &mut entry) };
    while ok != 0 {
        let exe_name = win_ro::utf16_slice_to_string(&entry.szExeFile);
        if exe_name.eq_ignore_ascii_case("gta_sa.exe") {
            return true;
        }
        ok = unsafe { Process32NextW(snap, &mut entry) };
    }
    false
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_sleep_gap_delta_clamping() {
        assert_eq!(clamp_active_delta(Duration::from_secs(2)), Some(Duration::from_secs(2)));
        assert_eq!(clamp_active_delta(Duration::from_secs(30)), Some(Duration::from_secs(30)));
        assert_eq!(clamp_active_delta(Duration::from_secs(31)), None);
        assert_eq!(clamp_active_delta(Duration::from_secs(3600)), None);
    }

    #[test]
    fn test_crash_mtime_detection() {
        let t0 = SystemTime::now();
        let t1 = t0 + Duration::from_secs(5);
        assert!(!is_new_crash_file(Some(t0), Some(t0), t1));
        assert!(is_new_crash_file(Some(t0), Some(t1), t0));
        assert!(is_new_crash_file(None, Some(t1), t0));
        assert!(!is_new_crash_file(None, None, t0));
    }
}
