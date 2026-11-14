use crate::query::{
    register_scan, run_batch_queries, shared_runtime, PublicListItem, ScanState, ServerTarget,
};
use crate::win_ro::to_wide;
use serde::Deserialize;
use std::ffi::c_void;
use std::fs;
use std::path::Path;
use std::ptr::{null, null_mut};
use std::sync::atomic::Ordering;
use windows_sys::Win32::Networking::WinHttp::{
    WinHttpCloseHandle, WinHttpConnect, WinHttpOpen, WinHttpOpenRequest, WinHttpQueryDataAvailable,
    WinHttpQueryHeaders, WinHttpReadData, WinHttpReceiveResponse, WinHttpSendRequest,
    WinHttpSetTimeouts, WINHTTP_ACCESS_TYPE_DEFAULT_PROXY, WINHTTP_FLAG_SECURE,
    WINHTTP_QUERY_FLAG_NUMBER, WINHTTP_QUERY_STATUS_CODE,
};

const MAX_PUBLIC_LIST_BYTES: usize = 32 * 1024 * 1024;

#[derive(Deserialize)]
struct OpenMpApiServer {
    ip: Option<String>,
    hn: Option<String>,
    pc: Option<i32>,
    pm: Option<i32>,
    gm: Option<String>,
    la: Option<String>,
    pa: Option<bool>,
    vn: Option<String>,
}

struct WinHttpHandle(*mut c_void);
impl Drop for WinHttpHandle {
    fn drop(&mut self) {
        if !self.0.is_null() {
            unsafe {
                WinHttpCloseHandle(self.0);
            }
        }
    }
}

fn winhttp_get(host: &str, path: &str, state: &ScanState) -> Result<String, String> {
    unsafe {
        let ua = to_wide("SonicSampLauncher/2.4");
        let h_session = WinHttpOpen(
            ua.as_ptr(),
            WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
            std::ptr::null(),
            std::ptr::null(),
            0,
        );
        if h_session.is_null() {
            return Err("WinHttpOpen failed".into());
        }
        let session = WinHttpHandle(h_session);
        let _ = WinHttpSetTimeouts(session.0, 5000, 5000, 8000, 12000);

        let host_w = to_wide(host);
        let h_connect = WinHttpConnect(session.0, host_w.as_ptr(), 443, 0);
        if h_connect.is_null() {
            return Err("WinHttpConnect failed".into());
        }
        let connect = WinHttpHandle(h_connect);

        let verb_w = to_wide("GET");
        let path_w = to_wide(path);
        let h_request = WinHttpOpenRequest(
            connect.0,
            verb_w.as_ptr(),
            path_w.as_ptr(),
            null(),
            null(),
            null_mut(),
            WINHTTP_FLAG_SECURE,
        );
        if h_request.is_null() {
            return Err("WinHttpOpenRequest failed".into());
        }
        let request = WinHttpHandle(h_request);

        let ok_send = WinHttpSendRequest(request.0, null(), 0, null_mut(), 0, 0, 0);
        if ok_send == 0 || WinHttpReceiveResponse(request.0, null_mut()) == 0 {
            return Err("HTTP request failed".into());
        }

        let mut status: u32 = 0;
        let mut status_len: u32 = std::mem::size_of::<u32>() as u32;
        WinHttpQueryHeaders(
            request.0,
            WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
            std::ptr::null(),
            &mut status as *mut u32 as *mut _,
            &mut status_len,
            std::ptr::null_mut(),
        );
        if status < 200 || status >= 300 {
            return Err(format!("HTTP status {}", status));
        }

        let mut body = Vec::new();
        loop {
            if state.cancelled.load(Ordering::Relaxed) {
                return Err("Cancelled".into());
            }
            let mut avail: u32 = 0;
            if WinHttpQueryDataAvailable(request.0, &mut avail) == 0 || avail == 0 {
                break;
            }
            let read_cap = avail.min(262_144);
            if body.len().saturating_add(avail as usize) > MAX_PUBLIC_LIST_BYTES {
                return Err("Public server response exceeded 32 MB safety cap".into());
            }
            let mut chunk = vec![0u8; read_cap as usize];
            let mut downloaded: u32 = 0;
            if WinHttpReadData(
                request.0,
                chunk.as_mut_ptr() as *mut _,
                read_cap,
                &mut downloaded,
            ) == 0
                || downloaded == 0
            {
                break;
            }
            body.extend_from_slice(&chunk[..downloaded as usize]);
        }

        String::from_utf8(body).map_err(|e| format!("Invalid UTF-8: {}", e))
    }
}

pub fn parse_openmp_json(raw: &str) -> Result<Vec<PublicListItem>, String> {
    let entries: Vec<OpenMpApiServer> =
        serde_json::from_str(raw).map_err(|e| format!("JSON parse error: {}", e))?;
    let mut items = Vec::with_capacity(entries.len());
    for s in entries {
        let Some(ip_port) = s.ip else { continue };
        let Some(colon) = ip_port.rfind(':') else {
            continue;
        };
        if colon == 0 {
            continue;
        }
        let ip = ip_port[..colon].to_string();
        let port = ip_port[colon + 1..].to_string();
        if port.parse::<u16>().is_err() {
            continue;
        }
        let vn = s.vn.unwrap_or_default();
        let is_open_mp = !vn.is_empty() && !vn.starts_with("0.3");
        items.push(PublicListItem {
            ip,
            port,
            name: s.hn.unwrap_or_default(),
            players: s.pc.unwrap_or(-1),
            max: s.pm.unwrap_or(0),
            mode: s.gm.unwrap_or_default(),
            locked: s.pa.unwrap_or(false),
            is_open_mp,
            language: s.la.unwrap_or_default(),
        });
    }
    items.sort_by(|a, b| b.players.cmp(&a.players));
    Ok(items)
}

pub fn start_public_scan(cache_path: Option<String>, concurrency: usize) -> u64 {
    let state = ScanState::new();
    let handle = register_scan(state.clone());
    let scan_state = state.clone();

    shared_runtime().spawn(async move {
        let cp_clone = cache_path.clone();
        let st_http = scan_state.clone();
        let raw_json = tokio::task::spawn_blocking(move || {
            let mut from_cache = false;
            let res = match winhttp_get("api.open.mp", "/servers", &st_http) {
                Ok(body) => {
                    if let Some(ref cp) = cp_clone {
                        let _ = fs::write(cp, &body);
                    }
                    Ok(body)
                }
                Err(http_err) => {
                    if !st_http.cancelled.load(Ordering::Relaxed) {
                        if let Some(ref cp) = cp_clone {
                            if Path::new(cp).exists() {
                                if let Ok(cached) = fs::read_to_string(cp) {
                                    from_cache = true;
                                    return (Ok(cached), from_cache);
                                }
                            }
                        }
                    }
                    Err(http_err)
                }
            };
            (res, from_cache)
        })
        .await
        .unwrap_or_else(|e| (Err(e.to_string()), false));

        if scan_state.cancelled.load(Ordering::Relaxed) {
            scan_state.done.store(true, Ordering::Relaxed);
            return;
        }

        let (body_res, from_cache) = raw_json;
        let body = match body_res {
            Ok(b) => b,
            Err(e) => {
                if let Ok(mut err) = scan_state.error.lock() {
                    *err = Some(e);
                }
                scan_state.done.store(true, Ordering::Relaxed);
                return;
            }
        };

        if scan_state.cancelled.load(Ordering::Relaxed) {
            scan_state.done.store(true, Ordering::Relaxed);
            return;
        }

        let items = match parse_openmp_json(&body) {
            Ok(v) => v,
            Err(e) => {
                if let Ok(mut err) = scan_state.error.lock() {
                    *err = Some(e);
                }
                scan_state.done.store(true, Ordering::Relaxed);
                return;
            }
        };

        let targets: Vec<ServerTarget> = items
            .iter()
            .filter_map(|it| {
                it.port.parse::<u16>().ok().map(|p| ServerTarget {
                    ip: it.ip.clone(),
                    port: p,
                })
            })
            .collect();

        if let Ok(mut init) = scan_state.initial_list.lock() {
            *init = Some((items, from_cache));
        }

        if !scan_state.cancelled.load(Ordering::Relaxed) {
            run_batch_queries(targets, concurrency, scan_state).await;
        } else {
            scan_state.done.store(true, Ordering::Relaxed);
        }
    });

    handle
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_parse_openmp_json_no_limit_and_sorted() {
        let json = r#"[
            {"ip":"1.1.1.1:7777","hn":"Low","pc":5,"pm":100,"gm":"RP","la":"TH","pa":false,"vn":"0.3.7-R2"},
            {"ip":"2.2.2.2:7778","hn":"High OMP","pc":200,"pm":500,"gm":"Freeroam","la":"EN","pa":true,"vn":"open.mp 1.4"}
        ]"#;
        let items = parse_openmp_json(json).expect("parse");
        assert_eq!(items.len(), 2);
        assert_eq!(items[0].ip, "2.2.2.2");
        assert_eq!(items[0].port, "7778");
        assert!(items[0].is_open_mp);
        assert!(items[0].locked);
        assert!(!items[1].is_open_mp);
    }
}
