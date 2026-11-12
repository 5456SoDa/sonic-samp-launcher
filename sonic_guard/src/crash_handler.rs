use std::fs::{self, OpenOptions};
use std::io::Write;
use std::path::PathBuf;
use std::ptr::null_mut;
use std::sync::atomic::{AtomicBool, Ordering};
use windows_sys::Win32::Foundation::MAX_PATH;
use windows_sys::Win32::System::Diagnostics::Debug::{
    SetUnhandledExceptionFilter, EXCEPTION_CONTINUE_SEARCH, EXCEPTION_EXECUTE_HANDLER,
    EXCEPTION_POINTERS,
};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, Module32FirstW, Module32NextW, MODULEENTRY32W, TH32CS_SNAPMODULE,
    TH32CS_SNAPMODULE32,
};
use windows_sys::Win32::System::LibraryLoader::GetModuleFileNameW;
use windows_sys::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS};
use windows_sys::Win32::System::SystemInformation::{
    GetLocalTime, GlobalMemoryStatusEx, MEMORYSTATUSEX,
};
use windows_sys::Win32::System::Threading::{GetCurrentProcess, GetCurrentProcessId};
use windows_sys::Win32::UI::WindowsAndMessaging::{MessageBoxW, MB_ICONERROR, MB_OK, MB_SYSTEMMODAL};

static CRASH_RECORDED: AtomicBool = AtomicBool::new(false);

#[derive(serde::Serialize)]
pub struct CrashReport {
    pub timestamp: String,
    pub exception_code: String,
    pub exception_name: String,
    pub fault_address: String,
    pub module_name: String,
    pub module_offset: String,
    pub thai_explanation: String,
    pub suggestion: String,
    pub working_set_mb: u64,
    pub virtual_mem_mb: u64,
    pub loaded_modules: Vec<String>,
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

fn thai_explanation_for(module: &str, offset: u64, code: u32) -> (&'static str, &'static str) {
    let mod_lower = module.to_ascii_lowercase();
    if mod_lower == "gta_sa.exe" {
        match offset {
            0x00746900..=0x007469FF => (
                "ไฟล์โมเดล 3D (.dff) หรือสกิน/รถยนต์ใน modloader/GTA3.img มีโครงสร้างผิดพลาดหรือ ID โมเดลไม่ถูกต้อง",
                "ลองเปิด 'โหมดปลอดภัย' ในหน้าม็อดเพื่อปิด modloader หรือใช้ตัวสแกน RenderWare ตรวจหาไฟล์ .dff ที่เสียหาย"
            ),
            0x004C6700..=0x004C67FF => (
                "เท็กซ์เจอร์ (.txd) หายไป, เสียหาย, หรือมีขนาดใหญ่เกินไปจนหน่วยความจำโหลดไม่ทัน",
                "สแกนไฟล์ .txd ในโฟลเดอร์ม็อด และหลีกเลี่ยงการใช้เท็กซ์เจอร์ความละเอียดสูงเกิน 2048x2048"
            ),
            0x00534100..=0x005341FF => (
                "ไฟล์เสียง/เพลง (audio/streams) ในโฟลเดอร์เกมหรือใน modloader เสียหายหรือไม่สมบูรณ์",
                "ตรวจสอบไฟล์ในโฟลเดอร์ audio หรือปิดม็อดหมวดเสียง (Sounds) ใน modloader"
            ),
            0x007F0B00..=0x007F0BFF => (
                "การโหลด Collision (.col) หรือไฟล์แผนที่ (Maps) เกิดข้อผิดพลาดหรือขอบเขตเกินค่าที่เกมรองรับ",
                "ปิดม็อดแผนที่/สิ่งก่อสร้างใน modloader หรือตรวจสอบไฟล์ .col"
            ),
            0x006E3D00..=0x006E3DFF => (
                "สคริปต์ CLEO (.cs) เกิดข้อผิดพลาดในการเข้าถึงหน่วยความจำ (Pointer เข้าผิดจุด)",
                "ตรวจสอบไฟล์ .cs ล่าสุดที่ลงในโฟลเดอร์ CLEO/"
            ),
            _ => (
                "GTA San Andreas เกิดข้อผิดพลาดในการประมวลผลคำสั่งของตัวเกมหลัก",
                "แนะนำเปิด 'โหมดปลอดภัย' ในหน้าม็อดของ Launcher เพื่อทดสอบว่าเกิดจากม็อดตัวใด"
            ),
        }
    } else if mod_lower.contains("samp.dll") {
        (
            "SA-MP ประสบปัญหาการประมวลผลแพ็กเก็ต, TextDraw, ออบเจ็กต์สตรีม หรือผู้เล่นในเซิร์ฟเวอร์",
            "หากเซิร์ฟเวอร์มีสกินหรือแมปพิเศษจำนวนมาก ให้ลองสแกนไฟล์ม็อดหรือรีสตาร์ทตัวเกมใหม่"
        )
    } else if mod_lower.contains("modloader.asi") {
        (
            "ModLoader พบไฟล์ม็อดที่มีโครงสร้างไฟล์ผิดปกติ หรือเกิดข้อขัดแย้งระหว่างม็อดหลายโฟลเดอร์",
            "ใช้ตัวสแกน RenderWare ใน Launcher ตรวจหาไฟล์ชนกัน และเปิดโหมดปลอดภัยเพื่อแยกม็อดที่มีปัญหา"
        )
    } else if mod_lower.contains("skygfx.asi") {
        (
            "SkyGfx ประสบปัญหาในการประมวลผลกราฟิกหรือ Shader ร่วมกับ DirectX",
            "ลองสลับพรีเซ็ต SkyGfx ในหน้าตั้งค่าของ Launcher หรือเลือกปิด SkyGfx ชั่วคราว"
        )
    } else if mod_lower.contains("d3d9.dll") || mod_lower.contains("d3dx9") {
        (
            "DirectX 9 หรือไดรเวอร์การ์ดจอเกิดข้อผิดพลาดในการวาดภาพ",
            "ตรวจสอบว่าเครื่องได้ติดตั้ง DirectX 9 End-User Runtime ครบถ้วนแล้วหรือไม่"
        )
    } else if mod_lower.contains("cleo.asi") {
        (
            "ระบบ CLEO พบปัญหากับสคริปต์ตัวใดตัวหนึ่ง",
            "ลองย้ายสคริปต์ .cs ออกชั่วคราวเพื่อหาตัวที่มีปัญหา"
        )
    } else if mod_lower.contains("sampvoice.asi") {
        (
            "ระบบไมค์เสียง SA-MP Voice Chat ประสบปัญหาเกี่ยวกับอุปกรณ์ไมโครโฟนหรือเสียง",
            "ตรวจสอบการตั้งค่าหูฟัง/ไมค์ใน Windows หรือลองอัปเดตไดรเวอร์เสียง"
        )
    } else if code == 0xC0000005 {
        (
            "เกิดการเข้าถึงหน่วยความจำไม่ถูกต้อง (Access Violation) จากโมดูลภายนอก",
            "ตรวจสอบม็อดหรือปลั๊กอิน .asi ที่เพิ่งติดตั้งล่าสุด"
        )
    } else {
        (
            "โมดูลปลั๊กอินปิดตัวกะทันหันเนื่องจากข้อยกเว้นของระบบ",
            "เปิดดูรายละเอียดโมดูลและโค้ดข้อผิดพลาดในหน้าต่างม็อดของ Launcher"
        )
    }
}

fn exception_name(code: u32) -> &'static str {
    match code {
        0xC0000005 => "ACCESS_VIOLATION (0xC0000005)",
        0xC000001D => "ILLEGAL_INSTRUCTION (0xC000001D)",
        0xC00000FD => "STACK_OVERFLOW (0xC00000FD)",
        0xC0000094 => "INTEGER_DIVIDE_BY_ZERO (0xC0000094)",
        0xC000008C => "ARRAY_BOUNDS_EXCEEDED (0xC000008C)",
        0xC0000025 => "NONCONTINUABLE_EXCEPTION (0xC0000025)",
        0x80000003 => "BREAKPOINT (0x80000003)",
        _ => "FATAL_EXCEPTION",
    }
}

fn snapshot_loaded_modules() -> Vec<String> {
    let pid = unsafe { GetCurrentProcessId() };
    let snap = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid) };
    if snap.is_null() || snap == -1isize as *mut _ {
        return Vec::new();
    }
    let mut entry: MODULEENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<MODULEENTRY32W>() as u32;

    let mut list = Vec::new();
    let mut ok = unsafe { Module32FirstW(snap, &mut entry) };
    while ok != 0 {
        let name_len = entry
            .szModule
            .iter()
            .position(|&c| c == 0)
            .unwrap_or(entry.szModule.len());
        let name = String::from_utf16_lossy(&entry.szModule[..name_len]);
        let base = entry.modBaseAddr as usize as u64;
        let size = entry.modBaseSize as u64;
        list.push(format!("{} (0x{:08X}, {} KB)", name, base, size / 1024));
        ok = unsafe { Module32NextW(snap, &mut entry) };
    }
    unsafe { windows_sys::Win32::Foundation::CloseHandle(snap) };
    list
}

fn find_module_for_address(addr: u64) -> (String, u64) {
    let pid = unsafe { GetCurrentProcessId() };
    let snap = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, pid) };
    if snap.is_null() || snap == -1isize as *mut _ {
        return ("unknown".into(), addr);
    }
    let mut entry: MODULEENTRY32W = unsafe { std::mem::zeroed() };
    entry.dwSize = std::mem::size_of::<MODULEENTRY32W>() as u32;

    let mut found = None;
    let mut ok = unsafe { Module32FirstW(snap, &mut entry) };
    while ok != 0 {
        let base = entry.modBaseAddr as usize as u64;
        let size = entry.modBaseSize as u64;
        if addr >= base && addr < base + size {
            let name_len = entry
                .szModule
                .iter()
                .position(|&c| c == 0)
                .unwrap_or(entry.szModule.len());
            let name = String::from_utf16_lossy(&entry.szModule[..name_len]);
            found = Some((name, addr - base));
            break;
        }
        ok = unsafe { Module32NextW(snap, &mut entry) };
    }
    unsafe { windows_sys::Win32::Foundation::CloseHandle(snap) };
    found.unwrap_or_else(|| ("unknown".into(), addr))
}

unsafe extern "system" fn unhandled_exception_filter(
    pointers: *const EXCEPTION_POINTERS,
) -> i32 {
    if pointers.is_null() {
        return EXCEPTION_CONTINUE_SEARCH;
    }
    if CRASH_RECORDED.swap(true, Ordering::SeqCst) {
        return EXCEPTION_EXECUTE_HANDLER;
    }

    let rec = &*(*pointers).ExceptionRecord;
    let ctx = &*(*pointers).ContextRecord;
    let code = rec.ExceptionCode as u32;
    let fault_addr = ctx.Eip as usize as u64;

    let (mod_name, offset) = find_module_for_address(fault_addr);
    let (explanation, suggestion) = thai_explanation_for(&mod_name, offset, code);

    let mut mem_stat: MEMORYSTATUSEX = std::mem::zeroed();
    mem_stat.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
    GlobalMemoryStatusEx(&mut mem_stat);

    let mut pmc: PROCESS_MEMORY_COUNTERS = std::mem::zeroed();
    pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32;
    GetProcessMemoryInfo(GetCurrentProcess(), &mut pmc, pmc.cb);

    let working_set_mb = (pmc.WorkingSetSize as u64) / (1024 * 1024);
    let virtual_mem_mb =
        ((mem_stat.ullTotalVirtual - mem_stat.ullAvailVirtual) as u64) / (1024 * 1024);

    let mut st = std::mem::zeroed();
    GetLocalTime(&mut st);
    let year = if st.wYear >= 2400 {
        st.wYear - 543
    } else {
        st.wYear
    };
    let stamp = format!(
        "{:04}-{:02}-{:02} {:02}:{:02}:{:02}",
        year, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond
    );

    let loaded_mods = snapshot_loaded_modules();

    let rep = CrashReport {
        timestamp: stamp.clone(),
        exception_code: format!("0x{:08X}", code),
        exception_name: exception_name(code).to_string(),
        fault_address: format!("0x{:08X}", fault_addr),
        module_name: mod_name.clone(),
        module_offset: format!("0x{:08X}", offset),
        thai_explanation: explanation.to_string(),
        suggestion: suggestion.to_string(),
        working_set_mb,
        virtual_mem_mb,
        loaded_modules: loaded_mods,
    };

    let root = game_root_dir();
    let bkp = root.join("_mod_backup");
    let _ = fs::create_dir_all(&bkp);

    if let Ok(json) = serde_json::to_string_pretty(&rep) {
        let _ = fs::write(bkp.join("crash_latest.json"), &json);
    }

    let log_line = format!(
        "[{}] Crash: {} at {}+0x{:08X} (Addr: 0x{:08X}) | RAM: {} MB | {}\r\n",
        stamp,
        exception_name(code),
        mod_name,
        offset,
        fault_addr,
        working_set_mb,
        explanation
    );
    if let Ok(mut f) = OpenOptions::new()
        .create(true)
        .append(true)
        .open(bkp.join("crash_history.log"))
    {
        let _ = f.write_all(log_line.as_bytes());
    }

    let msg_text = format!(
        "⚠️ GTA San Andreas ปิดตัวกะทันหัน (Crash Detected)\n\n\
         • สาเหตุ: {}\n\
         • ตำแหน่ง: {}+0x{:08X} ({})\n\
         • หน่วยความจำ RAM: {} MB\n\n\
         คำแนะนำ: {}\n\n\
         (รายละเอียดถูกส่งไปยัง Launcher แล้ว)",
        explanation,
        mod_name,
        offset,
        exception_name(code),
        working_set_mb,
        suggestion
    );
    let wide_msg: Vec<u16> = msg_text.encode_utf16().chain(std::iter::once(0)).collect();
    let wide_title: Vec<u16> = "SonicGuard • วิเคราะห์สาเหตุเกมเด้ง"
        .encode_utf16()
        .chain(std::iter::once(0))
        .collect();

    MessageBoxW(
        null_mut(),
        wide_msg.as_ptr(),
        wide_title.as_ptr(),
        MB_OK | MB_ICONERROR | MB_SYSTEMMODAL,
    );

    EXCEPTION_EXECUTE_HANDLER
}

pub fn install_crash_filter() {
    unsafe {
        SetUnhandledExceptionFilter(Some(unhandled_exception_filter));
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_crash_translation_and_json() {
        let (exp, sug) = thai_explanation_for("gta_sa.exe", 0x00746929, 0xC0000005);
        assert!(exp.contains("DFF"));
        assert!(sug.contains("Safe") || sug.contains("โหมดปลอดภัย"));

        let (exp_samp, _) = thai_explanation_for("samp.dll", 0x1234, 0xC0000005);
        assert!(exp_samp.contains("SA-MP"));
        assert_eq!(exception_name(0xC0000005), "ACCESS_VIOLATION (0xC0000005)");
    }
}
