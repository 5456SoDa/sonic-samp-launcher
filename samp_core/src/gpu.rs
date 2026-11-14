use crate::win_ro::{self, HKEY_CURRENT_USER};
use serde::Serialize;
use std::path::Path;
use std::ptr::null_mut;
use windows_sys::Win32::Foundation::FreeLibrary;
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};

const USER_GPU_PREF_SUBKEY: &str = r"Software\Microsoft\DirectX\UserGpuPreferences";

#[derive(Debug, Clone, Serialize)]
pub struct GpuAdapterInfo {
    pub name: String,
    pub vendor_id: u32,
    pub vram_mb: u64,
    pub is_discrete: bool,
}

#[derive(Debug, Clone, Serialize)]
pub struct GpuDiagnostics {
    pub adapters: Vec<GpuAdapterInfo>,
    pub is_hybrid: bool,
    pub igpu_name: String,
    pub dgpu_name: String,
    pub windows_pref_for_gta: String,
    pub status_summary: String,
}

#[repr(C)]
#[allow(non_snake_case)]
struct DxgiAdapterDesc1 {
    description: [u16; 128],
    vendor_id: u32,
    device_id: u32,
    sub_sys_id: u32,
    revision: u32,
    dedicated_video_memory: usize,
    dedicated_system_memory: usize,
    shared_system_memory: usize,
    adapter_luid_low: u32,
    adapter_luid_high: i32,
    flags: u32,
}

#[repr(C)]
#[derive(Clone, Copy)]
struct Guid {
    data1: u32,
    data2: u16,
    data3: u16,
    data4: [u8; 8],
}

const IID_IDXGIFACTORY1: Guid = Guid {
    data1: 0x770aae78,
    data2: 0xf26f,
    data3: 0x4dba,
    data4: [0xa8, 0x29, 0x25, 0x3c, 0x83, 0xd1, 0xb3, 0x87],
};

struct ComPtr(*mut *const usize);

impl Drop for ComPtr {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe {
                let vt = *self.0;
                let release_fn: extern "system" fn(*mut *const usize) -> u32 =
                    std::mem::transmute(*vt.add(2));
                release_fn(self.0);
            }
        }
    }
}

pub fn classify_discrete_gpu(vendor_id: u32, name: &str, vram_mb: u64, shared_mb: u64) -> bool {
    let low = name.to_ascii_lowercase();
    if low.contains("uhd graphics")
        || low.contains("iris")
        || low.contains("hd graphics")
        || low.contains("radeon(tm) graphics")
        || low.contains("vega")
        || low.contains("radeon 6")
        || low.contains("radeon 7")
        || low.contains("radeon 8")
    {
        return false;
    }
    if vendor_id == 0x10DE {
        return true; // NVIDIA
    }
    if vendor_id == 0x8086 {
        return low.contains("arc") || vram_mb >= 3072;
    }
    if vendor_id == 0x1002 {
        // AMD dGPU (RX/R5/R7/R9/PRO) vs APU UMA frame buffer
        return low.contains("rx ")
            || low.contains("r9")
            || low.contains("r7")
            || low.contains("pro ")
            || (vram_mb >= 3072 && vram_mb * 2 >= shared_mb);
    }
    vram_mb >= 3072
}

pub fn enumerate_dxgi_adapters() -> Vec<GpuAdapterInfo> {
    let mut out = Vec::new();
    let dxgi_w = win_ro::to_wide("dxgi.dll");
    let h_mod = unsafe { LoadLibraryW(dxgi_w.as_ptr()) };
    if h_mod.is_null() {
        return out;
    }

    let proc = unsafe { GetProcAddress(h_mod, b"CreateDXGIFactory1\0".as_ptr()) };
    if let Some(create_ptr) = proc {
        type FnCreateFactory1 = unsafe extern "system" fn(*const Guid, *mut *mut *const usize) -> i32;
        let create_fn: FnCreateFactory1 = unsafe { std::mem::transmute(create_ptr) };
        let mut raw_fac: *mut *const usize = null_mut();
        if unsafe { create_fn(&IID_IDXGIFACTORY1, &mut raw_fac) } == 0 && !raw_fac.is_null() {
            let fac = ComPtr(raw_fac);
            unsafe {
                let f_vt = *fac.0;
                // IDXGIFactory1::EnumAdapters1 is vtable slot 12
                type FnEnum1 =
                    unsafe extern "system" fn(*mut *const usize, u32, *mut *mut *const usize) -> i32;
                let enum1: FnEnum1 = std::mem::transmute(*f_vt.add(12));

                let mut idx: u32 = 0;
                loop {
                    let mut raw_adp: *mut *const usize = null_mut();
                    if enum1(fac.0, idx, &mut raw_adp) != 0 || raw_adp.is_null() {
                        break;
                    }
                    let adp = ComPtr(raw_adp);
                    let a_vt = *adp.0;
                    // IDXGIAdapter1::GetDesc1 is vtable slot 10
                    type FnGetDesc1 =
                        unsafe extern "system" fn(*mut *const usize, *mut DxgiAdapterDesc1) -> i32;
                    let get_desc1: FnGetDesc1 = std::mem::transmute(*a_vt.add(10));
                    let mut desc: DxgiAdapterDesc1 = std::mem::zeroed();
                    if get_desc1(adp.0, &mut desc) == 0 {
                        // Skip Microsoft Basic Render Driver (0x1414) and DXGI software flag (0x2)
                        if desc.vendor_id != 0x1414 && (desc.flags & 2) == 0 {
                            let name = win_ro::utf16_slice_to_string(&desc.description);
                            let vram_mb = (desc.dedicated_video_memory as u64) / (1024 * 1024);
                            let shared_mb = (desc.shared_system_memory as u64) / (1024 * 1024);
                            let is_discrete =
                                classify_discrete_gpu(desc.vendor_id, &name, vram_mb, shared_mb);
                            out.push(GpuAdapterInfo {
                                name,
                                vendor_id: desc.vendor_id,
                                vram_mb,
                                is_discrete,
                            });
                        }
                    }
                    idx += 1;
                }
            }
        }
    }

    unsafe {
        FreeLibrary(h_mod);
    }
    out
}

pub fn parse_gpu_preference_value(val: Option<&str>) -> String {
    let Some(raw) = val else {
        return "Default (ยังไม่ตั้งค่าใน Windows)".into();
    };
    if raw.contains("GpuPreference=2") {
        "High Performance (ตั้งค่าใช้การ์ดจอแยกแล้ว)".into()
    } else if raw.contains("GpuPreference=1") {
        "Power Saving (ประหยัดพลังงาน / ออนบอร์ด)".into()
    } else {
        "Let Windows Decide".into()
    }
}

/// Inspects GPU hardware and Windows graphics preference strictly READ-ONLY. Never alters Registry.
pub fn inspect_gpu_status(game_dir: &Path) -> GpuDiagnostics {
    let adapters = enumerate_dxgi_adapters();
    let igpu = adapters.iter().find(|a| !a.is_discrete).map(|a| a.name.clone()).unwrap_or_default();
    let dgpu = adapters.iter().find(|a| a.is_discrete).map(|a| a.name.clone()).unwrap_or_default();
    let is_hybrid = !igpu.is_empty() && !dgpu.is_empty();

    let gta_exe_str = game_dir.join("gta_sa.exe").to_string_lossy().to_string();
    let prefs = win_ro::reg_enum_string_values(HKEY_CURRENT_USER, USER_GPU_PREF_SUBKEY, false);
    let matched_pref = prefs
        .iter()
        .find(|(k, _)| k.eq_ignore_ascii_case(&gta_exe_str))
        .map(|(_, v)| v.as_str());
    let windows_pref_for_gta = parse_gpu_preference_value(matched_pref);

    let status_summary = if is_hybrid && !windows_pref_for_gta.starts_with("High Performance") {
        format!(
            "พบการ์ดจอคู่ ({} + {}) แต่ Windows ยังไม่ล็อก gta_sa.exe กับ {} — SonicGuard จะเร่ง GPU Priority + No-EcoQoS ในเกมให้ (หรือตั้งค่าการ์ดจอแยกเพิ่มได้เองใน Windows Settings -> Display -> Graphics)",
            igpu, dgpu, dgpu
        )
    } else if !dgpu.is_empty() {
        format!("พร้อมทำงานร่วมกับการ์ดจอแยก: {} (เปิด GPU Scheduler Boost ในเกม)", dgpu)
    } else if !igpu.is_empty() {
        format!("ใช้งานกราฟิก {} • เปิดโหมดเพิ่มประสิทธิภาพ Draw Call และ D3D9 Safe Coexistence", igpu)
    } else {
        "ไม่พบข้อมูล DXGI Adapter • เปิดโหมดป้องกัน D3D9 Crash & Frame Pacing ในเกม".into()
    };

    GpuDiagnostics {
        adapters,
        is_hybrid,
        igpu_name: igpu,
        dgpu_name: dgpu,
        windows_pref_for_gta,
        status_summary,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_gpu_pref_parser() {
        assert!(parse_gpu_preference_value(Some("GpuPreference=2;")).starts_with("High Performance"));
        assert!(parse_gpu_preference_value(Some("GpuPreference=1;")).starts_with("Power Saving"));
        assert!(parse_gpu_preference_value(None).starts_with("Default"));
    }

    #[test]
    fn test_classify_discrete_vs_uma_apu() {
        assert!(!classify_discrete_gpu(0x1002, "AMD Radeon(TM) Graphics", 2048, 8192));
        assert!(!classify_discrete_gpu(0x8086, "Intel(R) UHD Graphics 770", 128, 8192));
        assert!(classify_discrete_gpu(0x10DE, "NVIDIA GeForce GTX 1650", 4096, 8192));
        assert!(classify_discrete_gpu(0x1002, "AMD Radeon RX 6600", 8192, 8192));
        assert!(classify_discrete_gpu(0x8086, "Intel(R) Arc(TM) A750 Graphics", 8192, 8192));
    }
}
