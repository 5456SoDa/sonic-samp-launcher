use crate::config::to_wide;
use crate::iat;
use std::ptr::{null, null_mut};
use std::sync::atomic::{AtomicBool, AtomicU32, AtomicUsize, Ordering};
use windows_sys::Win32::Foundation::HMODULE;
use windows_sys::Win32::System::LibraryLoader::{GetModuleHandleW, GetProcAddress};

static NET_SHIELD_ACTIVE: AtomicBool = AtomicBool::new(false);
static TUNED_UDP_SOCKETS: AtomicU32 = AtomicU32::new(0);
static PATCHED_EXE_WS2: AtomicBool = AtomicBool::new(false);
static PATCHED_VOICE_WS2: AtomicBool = AtomicBool::new(false);

static ORIG_SOCKET: AtomicUsize = AtomicUsize::new(0);
static ORIG_WSASOCKETA: AtomicUsize = AtomicUsize::new(0);
static ORIG_WSASOCKETW: AtomicUsize = AtomicUsize::new(0);

static FN_SETSOCKOPT: AtomicUsize = AtomicUsize::new(0);
static FN_GETSOCKOPT: AtomicUsize = AtomicUsize::new(0);
static FN_WSAIOCTL: AtomicUsize = AtomicUsize::new(0);

const AF_INET: i32 = 2;
const SOCK_DGRAM: i32 = 2;
const IPPROTO_UDP: i32 = 17;
const SOL_SOCKET: i32 = 0xFFFF;
const SO_SNDBUF: i32 = 0x1001;
const SO_RCVBUF: i32 = 0x1002;
const SIO_UDP_CONNRESET: u32 = 0x9800_000C;
const UDP_TARGET_BUF: i32 = 262_144;
const INVALID_SOCKET: usize = usize::MAX;

type FnSocket = unsafe extern "system" fn(i32, i32, i32) -> usize;
type FnWsaSocket = unsafe extern "system" fn(i32, i32, i32, *const u8, u32, u32) -> usize;
type FnSetSockOpt = unsafe extern "system" fn(usize, i32, i32, *const i32, i32) -> i32;
type FnGetSockOpt = unsafe extern "system" fn(usize, i32, i32, *mut i32, *mut i32) -> i32;
type FnWsaIoctl = unsafe extern "system" fn(
    usize,
    u32,
    *const u32,
    u32,
    *mut u8,
    u32,
    *mut u32,
    *mut u8,
    *mut u8,
) -> i32;

fn ensure_ws2_helpers() {
    if FN_SETSOCKOPT.load(Ordering::Relaxed) != 0 {
        return;
    }
    let ws2_w = to_wide("ws2_32.dll");
    let h_ws2 = unsafe { GetModuleHandleW(ws2_w.as_ptr()) };
    if h_ws2.is_null() {
        return;
    }
    unsafe {
        if let Some(p) = GetProcAddress(h_ws2, b"setsockopt\0".as_ptr()) {
            let _ = FN_SETSOCKOPT.compare_exchange(0, p as usize, Ordering::SeqCst, Ordering::SeqCst);
        }
        if let Some(p) = GetProcAddress(h_ws2, b"getsockopt\0".as_ptr()) {
            let _ = FN_GETSOCKOPT.compare_exchange(0, p as usize, Ordering::SeqCst, Ordering::SeqCst);
        }
        if let Some(p) = GetProcAddress(h_ws2, b"WSAIoctl\0".as_ptr()) {
            let _ = FN_WSAIOCTL.compare_exchange(0, p as usize, Ordering::SeqCst, Ordering::SeqCst);
        }
    }
}

unsafe fn raise_buf_if_smaller(sock: usize, opt: i32, target: i32) {
    let g_ptr = FN_GETSOCKOPT.load(Ordering::Relaxed);
    let s_ptr = FN_SETSOCKOPT.load(Ordering::Relaxed);
    if s_ptr == 0 {
        return;
    }
    let mut cur: i32 = 0;
    let mut len: i32 = 4;
    if g_ptr != 0 {
        let getsock: FnGetSockOpt = std::mem::transmute(g_ptr);
        if getsock(sock, SOL_SOCKET, opt, &mut cur, &mut len) == 0 && cur >= target {
            return;
        }
    }
    let setsock: FnSetSockOpt = std::mem::transmute(s_ptr);
    let _ = setsock(sock, SOL_SOCKET, opt, &target, 4);
}

unsafe fn tune_udp_socket(sock: usize, af: i32, sock_type: i32, proto: i32) {
    if sock == INVALID_SOCKET || af != AF_INET || sock_type != SOCK_DGRAM {
        return;
    }
    if proto != 0 && proto != IPPROTO_UDP {
        return;
    }
    ensure_ws2_helpers();

    // Disable SIO_UDP_CONNRESET to prevent ICMP Port Unreachable from falsely breaking recvfrom loops
    let ioctl_ptr = FN_WSAIOCTL.load(Ordering::Relaxed);
    if ioctl_ptr != 0 {
        let ioctl: FnWsaIoctl = std::mem::transmute(ioctl_ptr);
        let new_behavior: u32 = 0;
        let mut bytes_ret: u32 = 0;
        let _ = ioctl(
            sock,
            SIO_UDP_CONNRESET,
            &new_behavior,
            4,
            null_mut(),
            0,
            &mut bytes_ret,
            null_mut(),
            null_mut(),
        );
    }

    raise_buf_if_smaller(sock, SO_RCVBUF, UDP_TARGET_BUF);
    raise_buf_if_smaller(sock, SO_SNDBUF, UDP_TARGET_BUF);
    TUNED_UDP_SOCKETS.fetch_add(1, Ordering::Relaxed);
}

unsafe extern "system" fn hooked_socket(af: i32, sock_type: i32, proto: i32) -> usize {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let addr = ORIG_SOCKET.load(Ordering::SeqCst);
        if addr == 0 {
            return INVALID_SOCKET;
        }
        let orig: FnSocket = std::mem::transmute(addr);
        let sock = orig(af, sock_type, proto);
        tune_udp_socket(sock, af, sock_type, proto);
        sock
    }))
    .unwrap_or(INVALID_SOCKET)
}

unsafe extern "system" fn hooked_wsa_socket_a(
    af: i32,
    sock_type: i32,
    proto: i32,
    info: *const u8,
    g: u32,
    flags: u32,
) -> usize {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let addr = ORIG_WSASOCKETA.load(Ordering::SeqCst);
        if addr == 0 {
            return INVALID_SOCKET;
        }
        let orig: FnWsaSocket = std::mem::transmute(addr);
        let sock = orig(af, sock_type, proto, info, g, flags);
        tune_udp_socket(sock, af, sock_type, proto);
        sock
    }))
    .unwrap_or(INVALID_SOCKET)
}

unsafe extern "system" fn hooked_wsa_socket_w(
    af: i32,
    sock_type: i32,
    proto: i32,
    info: *const u8,
    g: u32,
    flags: u32,
) -> usize {
    std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| {
        let addr = ORIG_WSASOCKETW.load(Ordering::SeqCst);
        if addr == 0 {
            return INVALID_SOCKET;
        }
        let orig: FnWsaSocket = std::mem::transmute(addr);
        let sock = orig(af, sock_type, proto, info, g, flags);
        tune_udp_socket(sock, af, sock_type, proto);
        sock
    }))
    .unwrap_or(INVALID_SOCKET)
}

unsafe fn patch_ws2_in_module(module: HMODULE) -> bool {
    let mut any = false;
    if let Some(orig) = iat::patch_module_iat(
        module,
        "WS2_32.dll",
        "socket",
        hooked_socket as *const () as usize,
    ) {
        let _ = ORIG_SOCKET.compare_exchange(0, orig, Ordering::SeqCst, Ordering::SeqCst);
        any = true;
    }
    if let Some(orig) = iat::patch_module_iat(
        module,
        "WS2_32.dll",
        "WSASocketA",
        hooked_wsa_socket_a as *const () as usize,
    ) {
        let _ = ORIG_WSASOCKETA.compare_exchange(0, orig, Ordering::SeqCst, Ordering::SeqCst);
        any = true;
    }
    if let Some(orig) = iat::patch_module_iat(
        module,
        "WS2_32.dll",
        "WSASocketW",
        hooked_wsa_socket_w as *const () as usize,
    ) {
        let _ = ORIG_WSASOCKETW.compare_exchange(0, orig, Ordering::SeqCst, Ordering::SeqCst);
        any = true;
    }
    any
}

/// Safely attempts IAT socket stabilization on loaded modules (no loader lock, no RakNet hooks).
pub fn poll_and_attach_net_shield() {
    ensure_ws2_helpers();
    if !PATCHED_EXE_WS2.load(Ordering::Relaxed) {
        let exe = unsafe { GetModuleHandleW(null()) };
        if unsafe { patch_ws2_in_module(exe) } {
            PATCHED_EXE_WS2.store(true, Ordering::SeqCst);
            NET_SHIELD_ACTIVE.store(true, Ordering::SeqCst);
        }
    }
    if !PATCHED_VOICE_WS2.load(Ordering::Relaxed) {
        let sv_w = to_wide("sampvoice.asi");
        let sv_mod = unsafe { GetModuleHandleW(sv_w.as_ptr()) };
        if !sv_mod.is_null() && unsafe { patch_ws2_in_module(sv_mod) } {
            PATCHED_VOICE_WS2.store(true, Ordering::SeqCst);
            NET_SHIELD_ACTIVE.store(true, Ordering::SeqCst);
        }
    }
}

pub fn is_net_shield_active() -> bool {
    NET_SHIELD_ACTIVE.load(Ordering::Relaxed)
}

pub fn tuned_socket_count() -> u32 {
    TUNED_UDP_SOCKETS.load(Ordering::Relaxed)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_non_udp_or_invalid_socket_ignored() {
        let before = tuned_socket_count();
        unsafe {
            tune_udp_socket(INVALID_SOCKET, AF_INET, SOCK_DGRAM, IPPROTO_UDP);
            tune_udp_socket(1234, AF_INET, 1 /* SOCK_STREAM */, 6);
        }
        assert_eq!(tuned_socket_count(), before);
    }
}
