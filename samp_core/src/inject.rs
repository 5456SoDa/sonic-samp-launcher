use crate::query::resolve_host;
use std::ffi::CString;
use std::ptr::null;
use windows_sys::Win32::Foundation::{
    CloseHandle, GetLastError, HANDLE, WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::System::Diagnostics::Debug::WriteProcessMemory;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows_sys::Win32::System::Memory::{
    VirtualAllocEx, VirtualFreeEx, MEM_COMMIT, MEM_RELEASE, MEM_RESERVE, PAGE_READWRITE,
};
use windows_sys::Win32::System::Threading::{
    CreateProcessW, CreateRemoteThread, GetExitCodeThread, OpenProcess, ResumeThread,
    TerminateProcess, WaitForSingleObject, CREATE_SUSPENDED, PROCESS_ALL_ACCESS,
    PROCESS_INFORMATION, STARTUPINFOW,
};

fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

struct HandleGuard(HANDLE);
impl Drop for HandleGuard {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe {
                CloseHandle(self.0);
            }
        }
    }
}

pub fn launch_and_inject(
    gta_path: &str,
    dll_path: &str,
    work_dir: &str,
    nick: &str,
    ip: &str,
    port: &str,
    pass: &str,
) -> Result<(), String> {
    let target_ip = resolve_host(ip)
        .map(|v| v.to_string())
        .unwrap_or_else(|| ip.to_string());

    let mut cmd = format!(
        "\"{}\" -c -n {} -h {} -p {}",
        gta_path, nick, target_ip, port
    );
    if !pass.is_empty() {
        cmd.push_str(&format!(" -z {}", pass));
    }

    let app_w = to_wide(gta_path);
    let mut cmd_w = to_wide(&cmd);
    let cwd_w = to_wide(work_dir);

    let mut si: STARTUPINFOW = unsafe { std::mem::zeroed() };
    si.cb = std::mem::size_of::<STARTUPINFOW>() as u32;
    let mut pi: PROCESS_INFORMATION = unsafe { std::mem::zeroed() };

    let ok = unsafe {
        CreateProcessW(
            app_w.as_ptr(),
            cmd_w.as_mut_ptr(),
            null(),
            null(),
            0,
            CREATE_SUSPENDED,
            null(),
            cwd_w.as_ptr(),
            &si,
            &mut pi,
        )
    };
    if ok == 0 {
        let err = unsafe { GetLastError() };
        return Err(format!("เปิด gta_sa.exe ไม่ได้ (Win32 error {})", err));
    }

    let proc_guard = HandleGuard(pi.hProcess);
    let thread_guard = HandleGuard(pi.hThread);

    if let Err(e) = inject_dll(pi.dwProcessId, dll_path) {
        unsafe {
            TerminateProcess(proc_guard.0, 1);
        }
        return Err(e);
    }

    unsafe {
        ResumeThread(thread_guard.0);
    }
    Ok(())
}

fn inject_dll(pid: u32, dll_path: &str) -> Result<(), String> {
    let h_proc = unsafe { OpenProcess(PROCESS_ALL_ACCESS, 0, pid) };
    if h_proc.is_null() {
        let err = unsafe { GetLastError() };
        return Err(format!("OpenProcess ล้มเหลว (error {})", err));
    }
    let proc_guard = HandleGuard(h_proc);

    let dll_w = to_wide(dll_path);
    let byte_len = dll_w.len() * 2;
    let remote_buf = unsafe {
        VirtualAllocEx(
            proc_guard.0,
            null(),
            byte_len,
            MEM_COMMIT | MEM_RESERVE,
            PAGE_READWRITE,
        )
    };
    if remote_buf.is_null() {
        let err = unsafe { GetLastError() };
        return Err(format!("VirtualAllocEx ล้มเหลว (error {})", err));
    }

    let res = (|| -> Result<(), String> {
        let mut written: usize = 0;
        let ok_write = unsafe {
            WriteProcessMemory(
                proc_guard.0,
                remote_buf,
                dll_w.as_ptr() as *const _,
                byte_len,
                &mut written,
            )
        };
        if ok_write == 0 || written != byte_len {
            let err = unsafe { GetLastError() };
            return Err(format!("WriteProcessMemory ล้มเหลว (error {})", err));
        }

        let k32_w = to_wide("kernel32.dll");
        let h_k32 = unsafe { GetModuleHandleW(k32_w.as_ptr()) };
        if h_k32.is_null() {
            return Err("หา kernel32.dll ไม่เจอ".into());
        }
        let llw = CString::new("LoadLibraryW").unwrap();
        let proc_addr = unsafe { GetProcAddress(h_k32, llw.as_ptr() as *const u8) };
        let Some(start_fn) = proc_addr else {
            return Err("หา LoadLibraryW ไม่เจอ".into());
        };

        let start_routine: unsafe extern "system" fn(*mut std::ffi::c_void) -> u32 =
            unsafe { std::mem::transmute(start_fn) };

        let mut tid: u32 = 0;
        let h_thread = unsafe {
            CreateRemoteThread(
                proc_guard.0,
                null(),
                0,
                Some(start_routine),
                remote_buf,
                0,
                &mut tid,
            )
        };
        if h_thread.is_null() {
            let err = unsafe { GetLastError() };
            return Err(format!(
                "CreateRemoteThread ล้มเหลว (error {} — antivirus อาจบล็อก)",
                err
            ));
        }
        let thread_guard = HandleGuard(h_thread);

        let wait = unsafe { WaitForSingleObject(thread_guard.0, 15000) };
        if wait == WAIT_TIMEOUT {
            return Err("LoadLibrary ไม่ตอบสนอง (timeout)".into());
        }
        if wait != WAIT_OBJECT_0 {
            return Err(format!("WaitForSingleObject ล้มเหลว (code {})", wait));
        }

        let mut exit_code: u32 = 0;
        let ok_exit = unsafe { GetExitCodeThread(thread_guard.0, &mut exit_code) };
        if ok_exit == 0 || exit_code == 0 {
            return Err(
                "โหลด samp.dll ไม่สำเร็จ (LoadLibraryW คืนค่า 0 — ไฟล์อาจเสียหายหรือถูก antivirus บล็อก)".into(),
            );
        }

        Ok(())
    })();

    unsafe {
        VirtualFreeEx(proc_guard.0, remote_buf, 0, MEM_RELEASE);
    }
    res
}
