use std::fs;
use std::path::PathBuf;
use std::ptr::null_mut;
use std::sync::atomic::{AtomicBool, Ordering};
use std::thread;
use std::time::Duration;
use windows_sys::Win32::Foundation::MAX_PATH;
use windows_sys::Win32::Media::{timeBeginPeriod, timeEndPeriod};
use windows_sys::Win32::System::LibraryLoader::GetModuleFileNameW;
use windows_sys::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::SystemInformation::{
    GetLocalTime, GlobalMemoryStatusEx, MEMORYSTATUSEX,
};
use windows_sys::Win32::System::Threading::{GetCurrentProcess, GetCurrentProcessId};

static RUNNING: AtomicBool = AtomicBool::new(false);

#[derive(serde::Serialize)]
struct TelemetryInfo {
    pid: u32,
    working_set_mb: u64,
    peak_working_set_mb: u64,
    private_bytes_mb: u64,
    virtual_mem_mb: u64,
    timestamp: String,
}

fn game_root_dir() -> PathBuf {
    let mut buf = [0u16; MAX_PATH as usize];
    let len = unsafe { GetModuleFileNameW(null_mut(), buf.as_mut_ptr(), MAX_PATH) };
    if len > 0 {
        if let Some(parent) = PathBuf::from(String::from_utf16_lossy(&buf[..len as usize])).parent() {
            return parent.to_path_buf();
        }
    }
    PathBuf::from(".")
}

pub fn init_perf() {
    unsafe {
        timeBeginPeriod(1);
    }
    if RUNNING.swap(true, Ordering::SeqCst) {
        return;
    }

    thread::spawn(|| {
        let root = game_root_dir();
        let bkp = root.join("_mod_backup");
        let _ = fs::create_dir_all(&bkp);
        let tele_path = bkp.join("game_telemetry.json");
        let tmp_path = bkp.join("game_telemetry.json.tmp");

        let pid = unsafe { GetCurrentProcessId() };

        while RUNNING.load(Ordering::SeqCst) {
            unsafe {
                let mut pmc: PROCESS_MEMORY_COUNTERS_EX = std::mem::zeroed();
                pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
                GetProcessMemoryInfo(
                    GetCurrentProcess(),
                    &mut pmc as *mut _ as _,
                    pmc.cb,
                );

                let mut mem_stat: MEMORYSTATUSEX = std::mem::zeroed();
                mem_stat.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
                GlobalMemoryStatusEx(&mut mem_stat);

                let mut st = std::mem::zeroed();
                GetLocalTime(&mut st);
                let year = if st.wYear >= 2400 { st.wYear - 543 } else { st.wYear };
                let stamp = format!(
                    "{:04}-{:02}-{:02} {:02}:{:02}:{:02}",
                    year, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond
                );

                let data = TelemetryInfo {
                    pid,
                    working_set_mb: (pmc.WorkingSetSize as u64) / (1024 * 1024),
                    peak_working_set_mb: (pmc.PeakWorkingSetSize as u64) / (1024 * 1024),
                    private_bytes_mb: (pmc.PrivateUsage as u64) / (1024 * 1024),
                    virtual_mem_mb: ((mem_stat.ullTotalVirtual - mem_stat.ullAvailVirtual) as u64)
                        / (1024 * 1024),
                    timestamp: stamp,
                };

                if let Ok(json) = serde_json::to_string(&data) {
                    if fs::write(&tmp_path, json).is_ok() {
                        let _ = fs::rename(&tmp_path, &tele_path);
                    }
                }
            }
            thread::sleep(Duration::from_millis(3000));
        }
    });
}

pub fn shutdown_perf() {
    RUNNING.store(false, Ordering::SeqCst);
    unsafe {
        timeEndPeriod(1);
    }
}
