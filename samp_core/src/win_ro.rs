use std::ffi::c_void;
use std::path::PathBuf;
use std::ptr::null_mut;
use windows_sys::Win32::System::Environment::ExpandEnvironmentStringsW;

pub type Hkey = *mut c_void;

pub const HKEY_CURRENT_USER: Hkey = 0x8000_0001usize as Hkey;
pub const HKEY_LOCAL_MACHINE: Hkey = 0x8000_0002usize as Hkey;

const KEY_READ: u32 = 0x0002_0019;
const KEY_WOW64_64KEY: u32 = 0x0100;
const ERROR_SUCCESS: i32 = 0;
const ERROR_MORE_DATA: i32 = 234;
const ERROR_NO_MORE_ITEMS: i32 = 259;
const REG_SZ: u32 = 1;
const REG_EXPAND_SZ: u32 = 2;
const REG_DWORD: u32 = 4;

#[link(name = "advapi32")]
extern "system" {
    fn RegOpenKeyExW(
        h_key: Hkey,
        lp_sub_key: *const u16,
        ul_options: u32,
        sam_desired: u32,
        phk_result: *mut Hkey,
    ) -> i32;
    fn RegCloseKey(h_key: Hkey) -> i32;
    fn RegQueryValueExW(
        h_key: Hkey,
        lp_value_name: *const u16,
        lp_reserved: *mut u32,
        lp_type: *mut u32,
        lp_data: *mut u8,
        lpcb_data: *mut u32,
    ) -> i32;
    fn RegEnumValueW(
        h_key: Hkey,
        dw_index: u32,
        lp_value_name: *mut u16,
        lpcch_value_name: *mut u32,
        lp_reserved: *mut u32,
        lp_type: *mut u32,
        lp_data: *mut u8,
        lpcb_data: *mut u32,
    ) -> i32;
    fn RegEnumKeyExW(
        h_key: Hkey,
        dw_index: u32,
        lp_name: *mut u16,
        lpcch_name: *mut u32,
        lp_reserved: *mut u32,
        lp_class: *mut u16,
        lpcch_class: *mut u32,
        lpft_last_write_time: *mut c_void,
    ) -> i32;
}

pub struct RegKeyGuard(pub Hkey);

impl Drop for RegKeyGuard {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe {
                RegCloseKey(self.0);
            }
        }
    }
}

pub fn to_wide(s: &str) -> Vec<u16> {
    s.encode_utf16().chain(std::iter::once(0)).collect()
}

pub fn utf16_slice_to_string(slice: &[u16]) -> String {
    let end = slice.iter().position(|&c| c == 0).unwrap_or(slice.len());
    String::from_utf16_lossy(&slice[..end])
}

/// Opens a Windows Registry key strictly in read-only mode (`KEY_READ`).
pub fn reg_open_readonly(root: Hkey, subkey: &str, wow64_64: bool) -> Option<RegKeyGuard> {
    let sub_w = to_wide(subkey);
    let mut handle: Hkey = null_mut();
    let sam = if wow64_64 {
        KEY_READ | KEY_WOW64_64KEY
    } else {
        KEY_READ
    };
    let rc = unsafe { RegOpenKeyExW(root, sub_w.as_ptr(), 0, sam, &mut handle) };
    if rc == ERROR_SUCCESS && !handle.is_null() {
        Some(RegKeyGuard(handle))
    } else {
        None
    }
}

pub fn reg_read_u32(root: Hkey, subkey: &str, value_name: &str, wow64_64: bool) -> Option<u32> {
    let key = reg_open_readonly(root, subkey, wow64_64)?;
    let val_w = to_wide(value_name);
    let mut val_type: u32 = 0;
    let mut data: u32 = 0;
    let mut size: u32 = 4;
    let rc = unsafe {
        RegQueryValueExW(
            key.0,
            val_w.as_ptr(),
            null_mut(),
            &mut val_type,
            &mut data as *mut u32 as *mut u8,
            &mut size,
        )
    };
    if rc == ERROR_SUCCESS && val_type == REG_DWORD && size == 4 {
        Some(data)
    } else {
        None
    }
}

pub fn reg_read_string(root: Hkey, subkey: &str, value_name: &str, wow64_64: bool) -> Option<String> {
    let key = reg_open_readonly(root, subkey, wow64_64)?;
    let val_w = to_wide(value_name);
    let mut val_type: u32 = 0;
    let mut bytes_needed: u32 = 0;
    let rc = unsafe {
        RegQueryValueExW(
            key.0,
            val_w.as_ptr(),
            null_mut(),
            &mut val_type,
            null_mut(),
            &mut bytes_needed,
        )
    };
    if rc != ERROR_SUCCESS || bytes_needed == 0 || (val_type != REG_SZ && val_type != REG_EXPAND_SZ) {
        return None;
    }
    let u16_len = (bytes_needed as usize + 1) / 2;
    let mut buf = vec![0u16; u16_len.max(1)];
    let mut size = (buf.len() * 2) as u32;
    let rc2 = unsafe {
        RegQueryValueExW(
            key.0,
            val_w.as_ptr(),
            null_mut(),
            &mut val_type,
            buf.as_mut_ptr() as *mut u8,
            &mut size,
        )
    };
    if rc2 == ERROR_SUCCESS {
        Some(utf16_slice_to_string(&buf))
    } else {
        None
    }
}

pub fn reg_enum_subkeys(root: Hkey, subkey: &str, wow64_64: bool) -> Vec<String> {
    let Some(key) = reg_open_readonly(root, subkey, wow64_64) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    let mut idx: u32 = 0;
    let mut name_buf = [0u16; 512];
    loop {
        let mut name_len = name_buf.len() as u32;
        let rc = unsafe {
            RegEnumKeyExW(
                key.0,
                idx,
                name_buf.as_mut_ptr(),
                &mut name_len,
                null_mut(),
                null_mut(),
                null_mut(),
                null_mut(),
            )
        };
        if rc == ERROR_NO_MORE_ITEMS {
            break;
        }
        if rc == ERROR_SUCCESS {
            out.push(String::from_utf16_lossy(&name_buf[..name_len as usize]));
        }
        idx += 1;
    }
    out
}

pub fn reg_enum_string_values(root: Hkey, subkey: &str, wow64_64: bool) -> Vec<(String, String)> {
    let Some(key) = reg_open_readonly(root, subkey, wow64_64) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    let mut idx: u32 = 0;
    let mut name_buf = vec![0u16; 512];
    let mut data_buf = vec![0u8; 4096];

    loop {
        let mut retries = 0;
        let mut rc;
        loop {
            let mut name_len = name_buf.len() as u32;
            let mut data_len = data_buf.len() as u32;
            let mut val_type: u32 = 0;
            rc = unsafe {
                RegEnumValueW(
                    key.0,
                    idx,
                    name_buf.as_mut_ptr(),
                    &mut name_len,
                    null_mut(),
                    &mut val_type,
                    data_buf.as_mut_ptr(),
                    &mut data_len,
                )
            };

            if rc == ERROR_MORE_DATA && retries < 8 {
                retries += 1;
                if data_len as usize > data_buf.len() {
                    data_buf.resize(data_len as usize + 16, 0);
                } else {
                    name_buf.resize(name_buf.len() * 2, 0);
                }
                continue;
            }
            if rc == ERROR_SUCCESS && (val_type == REG_SZ || val_type == REG_EXPAND_SZ) {
                let name = String::from_utf16_lossy(&name_buf[..name_len as usize]);
                let u16_count = (data_len as usize) / 2;
                let wide: Vec<u16> = data_buf[..u16_count * 2]
                    .chunks_exact(2)
                    .map(|c| u16::from_le_bytes([c[0], c[1]]))
                    .collect();
                out.push((name, utf16_slice_to_string(&wide)));
            }
            break;
        }

        if rc == ERROR_NO_MORE_ITEMS {
            break;
        }
        idx += 1;
    }
    out
}

pub fn expand_env_vars(raw: &str) -> String {
    if !raw.contains('%') {
        return raw.to_string();
    }
    let src_w = to_wide(raw);
    let needed = unsafe { ExpandEnvironmentStringsW(src_w.as_ptr(), null_mut(), 0) };
    if needed == 0 {
        return raw.to_string();
    }
    let mut buf = vec![0u16; needed as usize + 2];
    let written = unsafe { ExpandEnvironmentStringsW(src_w.as_ptr(), buf.as_mut_ptr(), buf.len() as u32) };
    if written == 0 || written as usize > buf.len() {
        return raw.to_string();
    }
    utf16_slice_to_string(&buf)
}

/// Locates `<Documents>\GTA San Andreas User Files` purely read-only.
pub fn gta_user_files_dir() -> Option<PathBuf> {
    if let Some(docs) = reg_read_string(
        HKEY_CURRENT_USER,
        r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
        "Personal",
        false,
    ) {
        let expanded = expand_env_vars(&docs);
        let p = PathBuf::from(expanded).join("GTA San Andreas User Files");
        if p.exists() {
            return Some(p);
        }
    }
    if let Some(docs_std) = reg_read_string(
        HKEY_CURRENT_USER,
        r"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders",
        "Personal",
        false,
    ) {
        let p = PathBuf::from(docs_std).join("GTA San Andreas User Files");
        if p.exists() {
            return Some(p);
        }
    }
    for env_key in ["OneDrive", "OneDriveConsumer", "USERPROFILE"] {
        if let Ok(base) = std::env::var(env_key) {
            for docs_sub in ["Documents", "เอกสาร"] {
                let p = PathBuf::from(&base)
                    .join(docs_sub)
                    .join("GTA San Andreas User Files");
                if p.exists() {
                    return Some(p);
                }
            }
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_expand_env_vars_works() {
        let out = expand_env_vars(r"%SystemRoot%\System32");
        assert!(!out.contains("%SystemRoot%"));
        assert!(out.to_ascii_lowercase().ends_with(r"\system32"));
    }
}
