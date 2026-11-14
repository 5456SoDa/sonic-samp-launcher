use crate::config::to_wide;
use std::ptr::null_mut;
use std::sync::atomic::{AtomicUsize, Ordering};
use windows_sys::Win32::Foundation::HMODULE;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};
use windows_sys::Win32::System::Memory::{
    VirtualProtect, VirtualQuery, MEMORY_BASIC_INFORMATION, PAGE_NOACCESS, PAGE_READWRITE,
};

#[repr(C)]
struct ImageImportDescriptor {
    original_first_thunk: u32,
    time_date_stamp: u32,
    forwarder_chain: u32,
    name: u32,
    first_thunk: u32,
}

unsafe fn is_readable(addr: *const u8, size: usize) -> bool {
    if addr.is_null() || size == 0 {
        return false;
    }
    let mut mbi: MEMORY_BASIC_INFORMATION = std::mem::zeroed();
    if VirtualQuery(
        addr as *const _,
        &mut mbi,
        std::mem::size_of::<MEMORY_BASIC_INFORMATION>(),
    ) == 0
    {
        return false;
    }
    if mbi.State != 0x1000 /*MEM_COMMIT*/
        || (mbi.Protect & PAGE_NOACCESS) != 0
        || (mbi.Protect & 0x100 /*PAGE_GUARD*/) != 0
    {
        return false;
    }
    let end = (mbi.BaseAddress as usize).saturating_add(mbi.RegionSize);
    match (addr as usize).checked_add(size) {
        Some(req_end) => req_end <= end,
        None => false,
    }
}

/// Pure helper checking `rva + len <= image_size` safely without integer overflow.
#[inline]
pub(crate) fn rva_in_bounds(rva: u32, len: usize, image_size: usize) -> bool {
    match (rva as usize).checked_add(len) {
        Some(end) => end <= image_size,
        None => false,
    }
}

/// Compares a NUL-terminated PE string at `ptr` against `target`, checking page readability
/// byte-by-byte up to `max_len` so malformed headers never cross into unmapped memory.
unsafe fn cstr_eq_bounded(ptr: *const u8, max_len: usize, target: &str, case_insensitive: bool) -> bool {
    let tb = target.as_bytes();
    let limit = max_len.min(260);
    if tb.len().saturating_add(1) > limit || !is_readable(ptr, tb.len() + 1) {
        return false;
    }
    for (i, &expected) in tb.iter().enumerate() {
        let actual = *ptr.add(i);
        if actual == 0 {
            return false;
        }
        let same = if case_insensitive {
            actual.eq_ignore_ascii_case(&expected)
        } else {
            actual == expected
        };
        if !same {
            return false;
        }
    }
    *ptr.add(tb.len()) == 0
}

unsafe fn resolve_pe32_import_dir(base: *const u8) -> Option<(u32, usize)> {
    if !is_readable(base, 0x40) || *(base as *const u16) != 0x5A4D {
        return None;
    }
    let e_lfanew = *(base.add(0x3C) as *const i32);
    if e_lfanew < 0x40 || e_lfanew > 0x4000 {
        return None;
    }
    let nt = base.add(e_lfanew as usize);
    if !is_readable(nt, 24 + 128) || *(nt as *const u32) != 0x0000_4550 {
        return None;
    }
    let magic = *(nt.add(24) as *const u16);
    if magic != 0x010B {
        return None;
    }
    let image_size = *(nt.add(24 + 56) as *const u32) as usize;
    let import_rva = *(nt.add(24 + 104) as *const u32);
    if import_rva == 0 || image_size < 0x100 || !rva_in_bounds(import_rva, 20, image_size) {
        return None;
    }
    Some((import_rva, image_size))
}

unsafe fn patch_slot_atomic(slot: *mut usize, expected_prev: usize, hook_addr: usize) -> Option<usize> {
    let mut old_prot = 0u32;
    if VirtualProtect(slot as _, std::mem::size_of::<usize>(), PAGE_READWRITE, &mut old_prot) == 0 {
        return None;
    }
    let atomic = &*(slot as *const AtomicUsize);
    let prev = match atomic.compare_exchange(expected_prev, hook_addr, Ordering::SeqCst, Ordering::SeqCst) {
        Ok(p) => Some(p),
        Err(_) => None,
    };
    let mut unused = 0u32;
    VirtualProtect(slot as _, std::mem::size_of::<usize>(), old_prot, &mut unused);
    prev
}

/// Safely patches a PE32 module's Import Address Table slot matching either import symbol name
/// (only when a valid `OriginalFirstThunk` ILT exists) or resolved target address (which catches
/// ordinal imports and bound/OFT==0 tables safely without treating bound pointers as RVAs).
/// Never modifies executable code bytes.
pub unsafe fn patch_module_iat(
    module: HMODULE,
    target_dll: &str,
    fn_name: &str,
    hook_addr: usize,
) -> Option<usize> {
    let base = if module.is_null() {
        GetModuleHandleW(null_mut()) as *const u8
    } else {
        module as *const u8
    };
    let (import_rva, image_size) = resolve_pe32_import_dir(base)?;

    let dll_w = to_wide(target_dll);
    let h_target = GetModuleHandleW(dll_w.as_ptr());
    let mut target_cstr = Vec::with_capacity(fn_name.len() + 1);
    target_cstr.extend_from_slice(fn_name.as_bytes());
    target_cstr.push(0);
    let real_addr = if !h_target.is_null() {
        GetProcAddress(h_target, target_cstr.as_ptr())
            .map(|p| p as usize)
            .unwrap_or(0)
    } else {
        0
    };

    let desc_size = std::mem::size_of::<ImageImportDescriptor>();
    let mut desc_rva = import_rva;
    let mut guard_loops = 0usize;
    while guard_loops < 256 && rva_in_bounds(desc_rva, desc_size, image_size) {
        let desc = base.add(desc_rva as usize) as *const ImageImportDescriptor;
        if !is_readable(desc as *const u8, desc_size) {
            break;
        }
        let name_rva = (*desc).name;
        let ft_rva = (*desc).first_thunk;
        if name_rva == 0 || ft_rva == 0 {
            break;
        }

        if rva_in_bounds(name_rva, target_dll.len() + 1, image_size)
            && rva_in_bounds(ft_rva, 4, image_size)
        {
            let dll_ptr = base.add(name_rva as usize);
            let max_dll_span = image_size.saturating_sub(name_rva as usize);
            if cstr_eq_bounded(dll_ptr, max_dll_span, target_dll, true) {
                let oft_rva = (*desc).original_first_thunk;
                let has_oft = oft_rva != 0 && rva_in_bounds(oft_rva, 4, image_size);

                let mut fn_idx = 0u32;
                while fn_idx < 2048 {
                    let off = fn_idx.saturating_mul(4);
                    let Some(cur_ft_rva) = ft_rva.checked_add(off) else {
                        break;
                    };
                    if !rva_in_bounds(cur_ft_rva, 4, image_size) {
                        break;
                    }
                    let ft = base.add(cur_ft_rva as usize) as *mut usize;
                    if !is_readable(ft as *const u8, 4) {
                        break;
                    }
                    let ft_val = *ft;
                    if ft_val == 0 {
                        break;
                    }
                    if ft_val == hook_addr {
                        return None;
                    }

                    let mut matched = real_addr != 0 && ft_val == real_addr;
                    if !matched && has_oft {
                        if let Some(cur_oft_rva) = oft_rva.checked_add(off) {
                            if rva_in_bounds(cur_oft_rva, 4, image_size) {
                                let oft_ptr = base.add(cur_oft_rva as usize) as *const u32;
                                if is_readable(oft_ptr as *const u8, 4) {
                                    let thunk = *oft_ptr;
                                    if thunk != 0 && (thunk & 0x8000_0000) == 0 {
                                        if let Some(imp_name_rva) = thunk.checked_add(2) {
                                            if rva_in_bounds(imp_name_rva, fn_name.len() + 1, image_size) {
                                                let imp_ptr = base.add(imp_name_rva as usize);
                                                let max_span =
                                                    image_size.saturating_sub(imp_name_rva as usize);
                                                if cstr_eq_bounded(imp_ptr, max_span, fn_name, false) {
                                                    matched = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if matched {
                        if let Some(prev) = patch_slot_atomic(ft, ft_val, hook_addr) {
                            return Some(prev);
                        }
                    }
                    fn_idx += 1;
                }
            }
        }

        let Some(next_desc) = desc_rva.checked_add(desc_size as u32) else {
            break;
        };
        desc_rva = next_desc;
        guard_loops += 1;
    }
    None
}

pub unsafe fn patch_vtable_slot(
    vtable: *mut usize,
    index: usize,
    hook_addr: usize,
    save_orig: &AtomicUsize,
) -> bool {
    if vtable.is_null() {
        return false;
    }
    let slot = vtable.add(index);
    if !is_readable(slot as *const u8, std::mem::size_of::<usize>()) {
        return false;
    }
    let prev = *slot;
    if prev == hook_addr {
        return true;
    }
    if let Some(old) = patch_slot_atomic(slot, prev, hook_addr) {
        let _ = save_orig.compare_exchange(0, old, Ordering::SeqCst, Ordering::SeqCst);
        return true;
    }
    false
}

#[cfg(test)]
mod tests {
    use super::*;

    fn write_u16(buf: &mut [u8], off: usize, val: u16) {
        buf[off..off + 2].copy_from_slice(&val.to_le_bytes());
    }

    fn write_u32(buf: &mut [u8], off: usize, val: u32) {
        buf[off..off + 4].copy_from_slice(&val.to_le_bytes());
    }

    #[test]
    fn test_rva_bounds_and_cstr() {
        assert!(rva_in_bounds(0x100, 20, 0x200));
        assert!(!rva_in_bounds(u32::MAX, 4, 0x200));
        assert!(!rva_in_bounds(0x1F0, 20, 0x200));

        unsafe {
            let s = b"KERNEL32.dll\0";
            assert!(cstr_eq_bounded(s.as_ptr(), s.len(), "kernel32.dll", true));
            assert!(!cstr_eq_bounded(s.as_ptr(), s.len(), "kernel32.dll", false));
            assert!(!cstr_eq_bounded(s.as_ptr(), 4, "kernel32.dll", true));
        }
    }

    #[test]
    fn test_pe32_iat_oft_zero_does_not_deref_ft_as_rva() {
        // Synthetic 4KB PE32 layout with OriginalFirstThunk == 0 and pre-hooked FT value 0x12345678
        // Previously fell back to oft = first_thunk and treated 0x12345678 as RVA (crash!).
        let mut pe = vec![0u8; 4096];
        let img_sz = pe.len() as u32;
        write_u16(&mut pe, 0x00, 0x5A4D); // MZ
        write_u32(&mut pe, 0x3C, 0x80); // e_lfanew
        write_u32(&mut pe, 0x80, 0x0000_4550); // PE\0\0
        write_u16(&mut pe, 0x80 + 24, 0x010B); // PE32 magic
        write_u32(&mut pe, 0x80 + 24 + 56, img_sz); // SizeOfImage
        write_u32(&mut pe, 0x80 + 24 + 104, 0x200); // Import Directory RVA

        // Descriptor at 0x200: OFT=0, Name=0x260 ("fake_unused.dll"), FT=0x280
        write_u32(&mut pe, 0x200 + 0, 0);
        write_u32(&mut pe, 0x200 + 12, 0x260);
        write_u32(&mut pe, 0x200 + 16, 0x280);
        pe[0x260..0x270].copy_from_slice(b"fake_unused.dll\0");
        write_u32(&mut pe, 0x280, 0x1234_5678); // Non-matching resolved address

        let res = unsafe { patch_module_iat(pe.as_mut_ptr() as HMODULE, "fake_unused.dll", "Foo", 0xDEAD_BEEF) };
        assert_eq!(res, None);
        assert_eq!(u32::from_le_bytes([pe[0x280], pe[0x281], pe[0x282], pe[0x283]]), 0x1234_5678);
    }

    #[test]
    fn test_pe32_iat_patches_by_oft_name_safely() {
        let mut pe = vec![0u8; 4096];
        let img_sz = pe.len() as u32;
        write_u16(&mut pe, 0x00, 0x5A4D);
        write_u32(&mut pe, 0x3C, 0x80);
        write_u32(&mut pe, 0x80, 0x0000_4550);
        write_u16(&mut pe, 0x80 + 24, 0x010B);
        write_u32(&mut pe, 0x80 + 24 + 56, img_sz);
        write_u32(&mut pe, 0x80 + 24 + 104, 0x200);

        // Descriptor: OFT=0x240, Name=0x260, FT=0x280
        write_u32(&mut pe, 0x200 + 0, 0x240);
        write_u32(&mut pe, 0x200 + 12, 0x260);
        write_u32(&mut pe, 0x200 + 16, 0x280);
        pe[0x260..0x270].copy_from_slice(b"fake_unused.dll\0");
        // OFT[0] points to Hint+Name at 0x300
        write_u32(&mut pe, 0x240, 0x300);
        write_u16(&mut pe, 0x300, 0);
        pe[0x302..0x30E].copy_from_slice(b"Direct3D9Ex\0");
        write_u32(&mut pe, 0x280, 0x1111_2222);

        let prev = unsafe { patch_module_iat(pe.as_mut_ptr() as HMODULE, "fake_unused.dll", "Direct3D9Ex", 0x3333_4444) };
        assert_eq!(prev, Some(0x1111_2222));
        assert_eq!(u32::from_le_bytes([pe[0x280], pe[0x281], pe[0x282], pe[0x283]]), 0x3333_4444);
    }
}
