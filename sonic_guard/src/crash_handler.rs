use crate::config::{game_root_dir, to_wide};
use std::fmt::{self, Write as FmtWrite};
use std::path::Path;
use std::ptr::null_mut;
use std::sync::atomic::{AtomicBool, Ordering};
use windows_sys::Win32::Foundation::{CloseHandle, HMODULE, INVALID_HANDLE_VALUE};
use windows_sys::Win32::Storage::FileSystem::{
    CreateDirectoryW, CreateFileW, MoveFileExW, WriteFile, CREATE_ALWAYS, FILE_APPEND_DATA,
    FILE_ATTRIBUTE_NORMAL, FILE_GENERIC_WRITE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    MOVEFILE_REPLACE_EXISTING, OPEN_ALWAYS,
};
use windows_sys::Win32::System::Diagnostics::Debug::{
    SetUnhandledExceptionFilter, EXCEPTION_CONTINUE_SEARCH, EXCEPTION_EXECUTE_HANDLER,
    EXCEPTION_POINTERS,
};
use windows_sys::Win32::System::LibraryLoader::{
    GetModuleFileNameW, GetModuleHandleExW, GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
    GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
};
use windows_sys::Win32::System::ProcessStatus::{GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS};
use windows_sys::Win32::System::SystemInformation::{
    GetLocalTime, GlobalMemoryStatusEx, MEMORYSTATUSEX,
};
use windows_sys::Win32::System::Threading::GetCurrentProcess;

static CRASH_RECORDED: AtomicBool = AtomicBool::new(false);

/// Fixed stack buffer writer: allows crash reporting without touching the process heap.
struct StackBuf<const N: usize> {
    buf: [u8; N],
    len: usize,
}

impl<const N: usize> StackBuf<N> {
    fn new() -> Self {
        Self {
            buf: [0u8; N],
            len: 0,
        }
    }

    fn as_bytes(&self) -> &[u8] {
        &self.buf[..self.len]
    }

    #[cfg(test)]
    fn as_str(&self) -> &str {
        std::str::from_utf8(self.as_bytes()).unwrap_or("")
    }

    fn push_bytes(&mut self, data: &[u8]) {
        let avail = N.saturating_sub(self.len);
        let take = data.len().min(avail);
        self.buf[self.len..self.len + take].copy_from_slice(&data[..take]);
        self.len += take;
    }

    fn push_json_escaped(&mut self, s: &str) {
        for b in s.as_bytes() {
            match *b {
                b'"' => self.push_bytes(b"\\\""),
                b'\\' => self.push_bytes(b"\\\\"),
                b'\n' => self.push_bytes(b"\\n"),
                b'\r' => self.push_bytes(b"\\r"),
                b'\t' => self.push_bytes(b"\\t"),
                0..=0x1F => {
                    let _ = write!(self, "\\u{:04x}", b);
                }
                _ => self.push_bytes(&[*b]),
            }
        }
    }
}

impl<const N: usize> FmtWrite for StackBuf<N> {
    fn write_str(&mut self, s: &str) -> fmt::Result {
        self.push_bytes(s.as_bytes());
        Ok(())
    }
}

fn thai_explanation_for(module: &str, offset: u64, code: u32) -> (&'static str, &'static str) {
    let is_mod = |needle: &str| {
        module
            .as_bytes()
            .windows(needle.len())
            .any(|w| w.eq_ignore_ascii_case(needle.as_bytes()))
    };
    if module.eq_ignore_ascii_case("gta_sa.exe") {
        match offset {
            0x00746900..=0x007469FF => (
                "ไฟล์โมเดล 3D (.dff) หรือสกิน/รถยนต์ใน modloader/GTA3.img มีโครงสร้างผิดพลาดหรือ ID โมเดลไม่ถูกต้อง",
                "ลองเปิด 'โหมดปลอดภัย' ในหน้าม็อดเพื่อปิด modloader หรือใช้ตัวสแกน RenderWare ตรวจหาไฟล์ .dff ที่เสียหาย",
            ),
            0x004C6700..=0x004C67FF => (
                "เท็กซ์เจอร์ (.txd) หายไป, เสียหาย, หรือมีขนาดใหญ่เกินไปจนหน่วยความจำโหลดไม่ทัน",
                "สแกนไฟล์ .txd ในโฟลเดอร์ม็อด และหลีกเลี่ยงการใช้เท็กซ์เจอร์ความละเอียดสูงเกิน 2048x2048",
            ),
            0x00534100..=0x005341FF => (
                "ไฟล์เสียง/เพลง (audio/streams) ในโฟลเดอร์เกมหรือใน modloader เสียหายหรือไม่สมบูรณ์",
                "ตรวจสอบไฟล์ในโฟลเดอร์ audio หรือปิดม็อดหมวดเสียง (Sounds) ใน modloader",
            ),
            0x007F0B00..=0x007F0BFF => (
                "การโหลด Collision (.col) หรือไฟล์แผนที่ (Maps) เกิดข้อผิดพลาดหรือขอบเขตเกินค่าที่เกมรองรับ",
                "ปิดม็อดแผนที่/สิ่งก่อสร้างใน modloader หรือตรวจสอบไฟล์ .col",
            ),
            0x006E3D00..=0x006E3DFF => (
                "สคริปต์ CLEO (.cs) เกิดข้อผิดพลาดในการเข้าถึงหน่วยความจำ (Pointer เข้าผิดจุด)",
                "ตรวจสอบไฟล์ .cs ล่าสุดที่ลงในโฟลเดอร์ CLEO/",
            ),
            _ => (
                "GTA San Andreas เกิดข้อผิดพลาดในการประมวลผลคำสั่งของตัวเกมหลัก",
                "แนะนำเปิด 'โหมดปลอดภัย' ในหน้าม็อดของ Launcher เพื่อทดสอบว่าเกิดจากม็อดตัวใด",
            ),
        }
    } else if is_mod("samp.dll") {
        (
            "SA-MP ประสบปัญหาการประมวลผลแพ็กเก็ต, TextDraw, ออบเจ็กต์สตรีม หรือผู้เล่นในเซิร์ฟเวอร์",
            "หากเซิร์ฟเวอร์มีสกินหรือแมปพิเศษจำนวนมาก ให้ลองสแกนไฟล์ม็อดหรือรีสตาร์ทตัวเกมใหม่",
        )
    } else if is_mod("modloader.asi") {
        (
            "ModLoader พบไฟล์ม็อดที่มีโครงสร้างไฟล์ผิดปกติ หรือเกิดข้อขัดแย้งระหว่างม็อดหลายโฟลเดอร์",
            "ใช้ตัวสแกน RenderWare ใน Launcher ตรวจหาไฟล์ชนกัน และเปิดโหมดปลอดภัยเพื่อแยกม็อดที่มีปัญหา",
        )
    } else if is_mod("skygfx.asi") {
        (
            "SkyGfx ประสบปัญหาในการประมวลผลกราฟิกหรือ Shader ร่วมกับ DirectX",
            "ลองสลับพรีเซ็ต SkyGfx ในหน้าตั้งค่าของ Launcher หรือเลือกปิด SkyGfx ชั่วคราว",
        )
    } else if is_mod("d3d9.dll") || is_mod("d3dx9") {
        (
            "DirectX 9 หรือไดรเวอร์การ์ดจอเกิดข้อผิดพลาดในการวาดภาพ",
            "ตรวจสอบว่าเครื่องได้ติดตั้ง DirectX 9 End-User Runtime ครบถ้วนแล้วหรือไม่",
        )
    } else if is_mod("cleo.asi") {
        (
            "ระบบ CLEO พบปัญหากับสคริปต์ตัวใดตัวหนึ่ง",
            "ลองย้ายสคริปต์ .cs ออกชั่วคราวเพื่อหาตัวที่มีปัญหา",
        )
    } else if is_mod("sampvoice.asi") {
        (
            "ระบบไมค์เสียง SA-MP Voice Chat ประสบปัญหาเกี่ยวกับอุปกรณ์ไมโครโฟนหรือเสียง",
            "ตรวจสอบการตั้งค่าหูฟัง/ไมค์ใน Windows หรือลองอัปเดตไดรเวอร์เสียง",
        )
    } else if code == 0xC0000005 {
        (
            "เกิดการเข้าถึงหน่วยความจำไม่ถูกต้อง (Access Violation) จากโมดูลภายนอก",
            "ตรวจสอบม็อดหรือปลั๊กอิน .asi ที่เพิ่งติดตั้งล่าสุด",
        )
    } else {
        (
            "โมดูลปลั๊กอินปิดตัวกะทันหันเนื่องจากข้อยกเว้นของระบบ",
            "เปิดดูรายละเอียดโมดูลและโค้ดข้อผิดพลาดในหน้าต่างม็อดของ Launcher",
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

/// Resolves the faulting module basename + relative offset without allocating ToolHelp snapshots.
unsafe fn resolve_fault_module_noalloc(addr: u64) -> ([u8; 96], usize, u64) {
    let mut name_utf8 = [0u8; 96];
    let mut hmod: HMODULE = null_mut();
    if addr != 0
        && GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            addr as usize as *const u16,
            &mut hmod,
        ) != 0
        && !hmod.is_null()
    {
        let mut wpath = [0u16; 260];
        let wlen = GetModuleFileNameW(hmod, wpath.as_mut_ptr(), wpath.len() as u32) as usize;
        if wlen > 0 && wlen <= wpath.len() {
            let slice = &wpath[..wlen];
            let start = slice
                .iter()
                .rposition(|&c| c == b'\\' as u16 || c == b'/' as u16)
                .map(|i| i + 1)
                .unwrap_or(0);
            let s = String::from_utf16_lossy(&slice[start..]);
            let sb = s.as_bytes();
            let take = sb.len().min(name_utf8.len());
            name_utf8[..take].copy_from_slice(&sb[..take]);
            let base = hmod as usize as u64;
            let off = addr.saturating_sub(base);
            return (name_utf8, take, off);
        }
    }
    let fallback = b"unknown";
    name_utf8[..fallback.len()].copy_from_slice(fallback);
    (name_utf8, fallback.len(), addr)
}

#[allow(clippy::too_many_arguments)]
fn format_crash_json_stack<const N: usize>(
    stamp: &str,
    code: u32,
    fault_addr: u64,
    mod_name: &str,
    offset: u64,
    explanation: &str,
    suggestion: &str,
    working_set_mb: u64,
    virtual_mem_mb: u64,
) -> StackBuf<N> {
    let mut out = StackBuf::<N>::new();
    out.push_bytes(b"{\"timestamp\":\"");
    out.push_json_escaped(stamp);
    out.push_bytes(b"\",\"exception_code\":\"");
    let _ = write!(out, "0x{:08X}", code);
    out.push_bytes(b"\",\"exception_name\":\"");
    out.push_json_escaped(exception_name(code));
    out.push_bytes(b"\",\"fault_address\":\"");
    let _ = write!(out, "0x{:08X}", fault_addr);
    out.push_bytes(b"\",\"module_name\":\"");
    out.push_json_escaped(mod_name);
    out.push_bytes(b"\",\"module_offset\":\"");
    let _ = write!(out, "0x{:08X}", offset);
    out.push_bytes(b"\",\"thai_explanation\":\"");
    out.push_json_escaped(explanation);
    out.push_bytes(b"\",\"suggestion\":\"");
    out.push_json_escaped(suggestion);
    out.push_bytes(b"\",\"working_set_mb\":");
    let _ = write!(out, "{}", working_set_mb);
    out.push_bytes(b",\"virtual_mem_mb\":");
    let _ = write!(out, "{}", virtual_mem_mb);
    out.push_bytes(b",\"loaded_modules\":[]}\n");
    out
}

unsafe fn write_all_win32(path: &Path, data: &[u8], append: bool) -> bool {
    let w = to_wide(&path.to_string_lossy());
    let access = if append {
        FILE_APPEND_DATA
    } else {
        FILE_GENERIC_WRITE
    };
    let disp = if append { OPEN_ALWAYS } else { CREATE_ALWAYS };
    let h = CreateFileW(
        w.as_ptr(),
        access,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        null_mut(),
        disp,
        FILE_ATTRIBUTE_NORMAL,
        null_mut(),
    );
    if h.is_null() || h == INVALID_HANDLE_VALUE {
        return false;
    }
    let mut written: u32 = 0;
    let ok = WriteFile(
        h,
        data.as_ptr(),
        data.len() as u32,
        &mut written,
        null_mut(),
    ) != 0
        && written as usize == data.len();
    CloseHandle(h);
    ok
}

unsafe extern "system" fn unhandled_exception_filter(
    pointers: *const EXCEPTION_POINTERS,
) -> i32 {
    let res = std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        if pointers.is_null() {
            return EXCEPTION_CONTINUE_SEARCH;
        }
        let ex_rec_ptr = (*pointers).ExceptionRecord;
        if ex_rec_ptr.is_null() {
            return EXCEPTION_CONTINUE_SEARCH;
        }
        if CRASH_RECORDED.swap(true, Ordering::SeqCst) {
            return EXCEPTION_EXECUTE_HANDLER;
        }

        let rec = &*ex_rec_ptr;
        let code = rec.ExceptionCode as u32;
        let ctx_ptr = (*pointers).ContextRecord;
        let fault_addr = if !ctx_ptr.is_null() {
            (*ctx_ptr).Eip as usize as u64
        } else {
            rec.ExceptionAddress as usize as u64
        };

        let (mod_bytes, mod_len, offset) = resolve_fault_module_noalloc(fault_addr);
        let mod_name = std::str::from_utf8(&mod_bytes[..mod_len]).unwrap_or("unknown");
        let (explanation, suggestion) = thai_explanation_for(mod_name, offset, code);

        let mut mem_stat: MEMORYSTATUSEX = std::mem::zeroed();
        mem_stat.dwLength = std::mem::size_of::<MEMORYSTATUSEX>() as u32;
        GlobalMemoryStatusEx(&mut mem_stat);

        let mut pmc: PROCESS_MEMORY_COUNTERS = std::mem::zeroed();
        pmc.cb = std::mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32;
        GetProcessMemoryInfo(GetCurrentProcess(), &mut pmc, pmc.cb);

        let working_set_mb = (pmc.WorkingSetSize as u64) / (1024 * 1024);
        let virtual_mem_mb =
            (mem_stat.ullTotalVirtual.saturating_sub(mem_stat.ullAvailVirtual)) / (1024 * 1024);

        let mut st = std::mem::zeroed();
        GetLocalTime(&mut st);
        let year = if st.wYear >= 2400 {
            st.wYear - 543
        } else {
            st.wYear
        };
        let mut stamp = StackBuf::<32>::new();
        let _ = write!(
            stamp,
            "{:04}-{:02}-{:02} {:02}:{:02}:{:02}",
            year, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond
        );
        let stamp_str = std::str::from_utf8(stamp.as_bytes()).unwrap_or("1970-01-01 00:00:00");

        let json_buf = format_crash_json_stack::<4096>(
            stamp_str,
            code,
            fault_addr,
            mod_name,
            offset,
            explanation,
            suggestion,
            working_set_mb,
            virtual_mem_mb,
        );

        let root = game_root_dir();
        let bkp = root.join("_mod_backup");
        let bkp_w = to_wide(&bkp.to_string_lossy());
        let _ = CreateDirectoryW(bkp_w.as_ptr(), null_mut());

        let tmp_json = bkp.join("crash_latest.json.tmp");
        let final_json = bkp.join("crash_latest.json");
        if write_all_win32(&tmp_json, json_buf.as_bytes(), false) {
            let tmp_w = to_wide(&tmp_json.to_string_lossy());
            let dst_w = to_wide(&final_json.to_string_lossy());
            let _ = MoveFileExW(tmp_w.as_ptr(), dst_w.as_ptr(), MOVEFILE_REPLACE_EXISTING);
        }

        let mut log_buf = StackBuf::<1024>::new();
        let _ = write!(
            log_buf,
            "[{}] Crash: {} at {}+0x{:08X} (Addr: 0x{:08X}) | RAM: {} MB | {}\r\n",
            stamp_str,
            exception_name(code),
            mod_name,
            offset,
            fault_addr,
            working_set_mb,
            explanation
        );
        let _ = write_all_win32(&bkp.join("crash_history.log"), log_buf.as_bytes(), true);

        // Do NOT display MessageBoxW inside a broken/fullscreen D3D9 process:
        // Launcher Rust Watchdog picks up crash_latest.json immediately on exit without screen hang.
        EXCEPTION_EXECUTE_HANDLER
    }));
    res.unwrap_or(EXCEPTION_EXECUTE_HANDLER)
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
        let (exp, _sug) = thai_explanation_for("GTA_SA.EXE", 0x00746910, 0xC0000005);
        assert!(exp.contains(".dff"));

        let (exp2, _) = thai_explanation_for("samp.dll", 0x1000, 0xC0000005);
        assert!(exp2.contains("SA-MP"));
    }

    #[test]
    fn test_stack_crash_json_escaping_and_valid() {
        let buf = format_crash_json_stack::<4096>(
            "2026-11-14 03:40:00",
            0xC0000005,
            0x00746910,
            "mod\"\\test.asi",
            0x10,
            "เกิด \"ข้อผิดพลาด\"\nบรรทัด 2",
            "ตรวจสอบไฟล์\\ม็อด",
            450,
            1200,
        );
        let val: serde_json::Value = serde_json::from_str(buf.as_str()).unwrap();
        assert_eq!(val["module_name"], "mod\"\\test.asi");
        assert_eq!(val["working_set_mb"], 450);
    }

    #[test]
    fn test_null_exception_pointers_safe() {
        unsafe {
            assert_eq!(unhandled_exception_filter(std::ptr::null()), EXCEPTION_CONTINUE_SEARCH);
            let mut ep: EXCEPTION_POINTERS = std::mem::zeroed();
            assert_eq!(unhandled_exception_filter(&ep), EXCEPTION_CONTINUE_SEARCH);
            ep.ExceptionRecord = null_mut();
            assert_eq!(unhandled_exception_filter(&ep), EXCEPTION_CONTINUE_SEARCH);
        }
    }
}
