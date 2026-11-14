use crate::audio_shield;
use crate::config::{game_root_dir, get_guard_config, to_wide};
use crate::gpu_shield;
use crate::net;
use crate::window;
use std::fs;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::{Mutex, OnceLock};
use std::thread;
use std::time::{Duration, Instant};
use windows_sys::Win32::Media::{timeBeginPeriod, timeEndPeriod};
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows_sys::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::SystemInformation::{
    GetLocalTime, GetSystemInfo, GlobalMemoryStatusEx, MEMORYSTATUSEX, SYSTEM_INFO,
};
use windows_sys::Win32::System::Threading::{
    GetCurrentProcess, GetCurrentProcessId, SetPriorityClass, SetProcessAffinityMask,
    SetProcessPriorityBoost, HIGH_PRIORITY_CLASS,
};

static RUNNING: AtomicBool = AtomicBool::new(false);

fn worker_slot() -> &'static Mutex<Option<thread::JoinHandle<()>>> {
    static HANDLE_SLOT: OnceLock<Mutex<Option<thread::JoinHandle<()>>>> = OnceLock::new();
    HANDLE_SLOT.get_or_init(|| Mutex::new(None))
}

const MOVEFILE_REPLACE_EXISTING: u32 = 0x1;

#[derive(serde::Serialize)]
struct TelemetryInfo {
    pid: u32,
    working_set_mb: u64,
    peak_working_set_mb: u64,
    private_bytes_mb: u64,
    virtual_mem_mb: u64,
    timestamp: String,
    system_free_ram_mb: u64,
    active_mic: String,
    net_shield_active: bool,
    audio_shield_active: bool,
    gpu_shield_active: bool,
    borderless_active: bool,
    tuned_udp_sockets: u32,
    cpu_cores: u32,
    affinity_mask: String,
}

pub fn calculate_cpu_affinity_mask(num_cpus: u32, mode: &str) -> Option<usize> {
    let m = mode.trim().to_ascii_lowercase();
    if m == "all" || m == "none" || m == "0" {
        return None;
    }
    if let Ok(custom) = usize::from_str_radix(m.trim_start_matches("0x"), 16) {
        if custom > 0 {
            return Some(custom);
        }
    }
    // "auto" or "p_cores" / "pcore"
    if num_cpus <= 4 {
        // Quad-core or dual-core: do not restrict affinity
        return None;
    }
    // On modern multi-core / hybrid CPUs (8, 12, 16, 24, 32 cores):
    // P-Cores on Intel Alder/Raptor Lake are mapped to the lowest indices (0..7).
    // Restricting gta_sa.exe to the primary performance cores prevents thread scheduler
    // bouncing onto E-Cores or cross-CCD cache latency spikes.
    if num_cpus >= 16 {
        Some(0x00FF) // First 8 cores (covers 4-8 P-cores)
    } else if num_cpus >= 8 {
        Some(0x003F) // First 6 cores (covers 3-6 P-cores)
    } else {
        Some(0x000F) // 6 cores: bind to first 4 cores
    }
}

unsafe fn atomic_replace_file(src: &std::path::Path, dst: &std::path::Path) -> bool {
    let k32_w = to_wide("kernel32.dll");
    let h_k32 = GetModuleHandleW(k32_w.as_ptr());
    if !h_k32.is_null() {
        if let Some(p) = GetProcAddress(h_k32, b"MoveFileExW\0".as_ptr()) {
            type FnMoveEx = unsafe extern "system" fn(*const u16, *const u16, u32) -> i32;
            let mv: FnMoveEx = std::mem::transmute(p);
            let src_w = to_wide(&src.to_string_lossy());
            let dst_w = to_wide(&dst.to_string_lossy());
            return mv(src_w.as_ptr(), dst_w.as_ptr(), MOVEFILE_REPLACE_EXISTING) != 0;
        }
    }
    fs::rename(src, dst).is_ok()
}

pub fn init_perf() {
    let cfg = get_guard_config();
    if cfg.high_precision_timer {
        unsafe {
            timeBeginPeriod(1);
        }
    }

    if RUNNING.swap(true, Ordering::SeqCst) {
        return;
    }

    let handle = thread::spawn(move || {
        // Apply process-local runtime boosts after Loader Lock releases cleanly.
        let mut sys_info: SYSTEM_INFO = unsafe { std::mem::zeroed() };
        unsafe {
            GetSystemInfo(&mut sys_info);
            SetPriorityClass(GetCurrentProcess(), HIGH_PRIORITY_CLASS);
            SetProcessPriorityBoost(GetCurrentProcess(), 0);
        }
        let num_cpus = sys_info.dwNumberOfProcessors;
        let applied_mask = calculate_cpu_affinity_mask(num_cpus, &cfg.cpu_affinity);
        let mask_str = match applied_mask {
            Some(m) => {
                unsafe {
                    SetProcessAffinityMask(GetCurrentProcess(), m);
                }
                format!("0x{:X}", m)
            }
            None => "all".to_string(),
        };
        window::enable_dpi_awareness_if_borderless();
        gpu_shield::init_gpu_shield();

        let root = game_root_dir();
        let bkp = root.join("_mod_backup");
        let _ = fs::create_dir_all(&bkp);
        let tele_path = bkp.join("game_telemetry.json");
        let tmp_path = bkp.join("game_telemetry.json.tmp");
        let pid = unsafe { GetCurrentProcessId() };

        let start_time = Instant::now();
        let mut last_tele = Instant::now() - Duration::from_secs(3);

        while RUNNING.load(Ordering::SeqCst) {
            if cfg.net_shield {
                net::poll_and_attach_net_shield();
            }
            if cfg.audio_shield {
                audio_shield::poll_and_attach_audio_shield();
            }

            if cfg.telemetry && last_tele.elapsed() >= Duration::from_millis(3000) {
                last_tele = Instant::now();
                unsafe {
                    let mut pmc: PROCESS_MEMORY_COUNTERS_EX = std::mem::zeroed();
                    pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
                    GetProcessMemoryInfo(GetCurrentProcess(), &mut pmc as *mut _ as _, pmc.cb);

                    let mut mem_stat: MEMORYSTATUSEX = std::mem::zeroed();
                    mem_stat.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
                    GlobalMemoryStatusEx(&mut mem_stat);

                    let free_ram_mb = mem_stat.ullAvailPhys / (1024 * 1024);

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
                        virtual_mem_mb: ((mem_stat.ullTotalVirtual - mem_stat.ullAvailVirtual)
                            as u64)
                            / (1024 * 1024),
                        timestamp: stamp,
                        system_free_ram_mb: free_ram_mb,
                        active_mic: audio_shield::get_active_mic(),
                        net_shield_active: net::is_net_shield_active(),
                        audio_shield_active: audio_shield::is_audio_shield_active(),
                        gpu_shield_active: gpu_shield::is_gpu_shield_active(),
                        borderless_active: window::is_borderless_active(),
                        tuned_udp_sockets: net::tuned_socket_count(),
                        cpu_cores: num_cpus,
                        affinity_mask: mask_str.clone(),
                    };

                    if let Ok(json) = serde_json::to_string(&data) {
                        if fs::write(&tmp_path, json).is_ok() {
                            let _ = atomic_replace_file(&tmp_path, &tele_path);
                        }
                    }
                }
            }

            // Sleep in responsive short steps so shutdown_perf joins promptly on FreeLibrary.
            let target_ms = if start_time.elapsed() < Duration::from_secs(30) {
                300
            } else {
                1200
            };
            let mut waited = 0;
            while waited < target_ms && RUNNING.load(Ordering::SeqCst) {
                thread::sleep(Duration::from_millis(100));
                waited += 100;
            }
        }
    });

    if let Ok(mut g) = worker_slot().lock() {
        *g = Some(handle);
    }
}

pub fn shutdown_perf() {
    RUNNING.store(false, Ordering::SeqCst);
    if let Ok(mut g) = worker_slot().lock() {
        if let Some(h) = g.take() {
            let _ = h.join();
        }
    }
    if get_guard_config().high_precision_timer {
        unsafe {
            timeEndPeriod(1);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_cpu_affinity_mask_calculation() {
        // Dual-core / Quad-core -> unconstrained
        assert_eq!(calculate_cpu_affinity_mask(2, "auto"), None);
        assert_eq!(calculate_cpu_affinity_mask(4, "auto"), None);

        // 6-core -> first 4 cores
        assert_eq!(calculate_cpu_affinity_mask(6, "auto"), Some(0x000F));

        // 8-12 cores -> first 6 cores
        assert_eq!(calculate_cpu_affinity_mask(8, "auto"), Some(0x003F));
        assert_eq!(calculate_cpu_affinity_mask(12, "p_cores"), Some(0x003F));

        // 16+ cores (Intel 12th-14th Gen hybrid or Ryzen 16-core) -> first 8 cores
        assert_eq!(calculate_cpu_affinity_mask(16, "auto"), Some(0x00FF));
        assert_eq!(calculate_cpu_affinity_mask(24, "auto"), Some(0x00FF));

        // Explicit "all" or "0" -> unconstrained
        assert_eq!(calculate_cpu_affinity_mask(16, "all"), None);
        assert_eq!(calculate_cpu_affinity_mask(16, "0"), None);

        // Custom hex mask
        assert_eq!(calculate_cpu_affinity_mask(16, "0x55"), Some(0x55));
    }
}
