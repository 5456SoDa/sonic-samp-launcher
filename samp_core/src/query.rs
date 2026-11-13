use crate::protocol::{
    build_query_packet, has_open_mp_rule, parse_info_packet, parse_players_basic,
    parse_players_detailed, parse_rules_packet, QueryInfo,
};
use serde::{Deserialize, Serialize};
use std::collections::{HashMap, VecDeque};
use std::net::{IpAddr, Ipv4Addr, SocketAddr, ToSocketAddrs, UdpSocket};
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::{Duration, Instant};
use tokio::net::UdpSocket as TokioUdpSocket;
use tokio::sync::Semaphore;

const DNS_TTL_SECS: u64 = 300;

struct DnsCacheEntry {
    ip: Ipv4Addr,
    expires_at: Instant,
}

fn dns_cache() -> &'static Mutex<HashMap<String, DnsCacheEntry>> {
    static CACHE: OnceLock<Mutex<HashMap<String, DnsCacheEntry>>> = OnceLock::new();
    CACHE.get_or_init(|| Mutex::new(HashMap::new()))
}

pub fn resolve_host(host: &str) -> Option<Ipv4Addr> {
    let trimmed = host.trim();
    if trimmed.is_empty() {
        return None;
    }
    if let Ok(ip) = trimmed.parse::<Ipv4Addr>() {
        return Some(ip);
    }

    let now = Instant::now();
    if let Ok(guard) = dns_cache().lock() {
        if let Some(entry) = guard.get(trimmed) {
            if entry.expires_at > now {
                return Some(entry.ip);
            }
        }
    }

    let addr_iter = (trimmed, 0u16).to_socket_addrs().ok()?;
    for sa in addr_iter {
        if let IpAddr::V4(v4) = sa.ip() {
            if let Ok(mut guard) = dns_cache().lock() {
                guard.insert(
                    trimmed.to_string(),
                    DnsCacheEntry {
                        ip: v4,
                        expires_at: now + Duration::from_secs(DNS_TTL_SECS),
                    },
                );
            }
            return Some(v4);
        }
    }
    None
}

fn query_raw_sync(sock: &UdpSocket, ip: Ipv4Addr, port: u16, op: u8) -> Option<Vec<u8>> {
    let pkt = build_query_packet(ip, port, op);
    sock.send(&pkt).ok()?;
    let mut buf = vec![0u8; 65536];
    let n = sock.recv(&mut buf).ok()?;
    if n > 11 && buf[10] == op {
        buf.truncate(n);
        Some(buf)
    } else {
        None
    }
}

pub fn query_server_sync(host: &str, port: u16) -> QueryInfo {
    let Some(ip) = resolve_host(host) else {
        return QueryInfo::default();
    };
    let Ok(sock) = UdpSocket::bind("0.0.0.0:0") else {
        return QueryInfo::default();
    };
    let _ = sock.set_read_timeout(Some(Duration::from_millis(1500)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(1500)));
    if sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).is_err() {
        return QueryInfo::default();
    }

    let pkt = build_query_packet(ip, port, b'i');
    let t0 = Instant::now();
    if sock.send(&pkt).is_err() {
        return QueryInfo::default();
    }
    let mut buf = [0u8; 640];
    let Ok(n) = sock.recv(&mut buf) else {
        return QueryInfo::default();
    };
    let ping_ms = t0.elapsed().as_millis() as i32;

    let Some(mut info) = parse_info_packet(&buf[..n], ping_ms) else {
        return QueryInfo::default();
    };

    if let Some(rules_raw) = query_raw_sync(&sock, ip, port, b'r') {
        if let Some(rules) = parse_rules_packet(&rules_raw) {
            if has_open_mp_rule(&rules) {
                info.is_open_mp = true;
            }
        }
    }
    info
}

pub fn query_players_sync(host: &str, port: u16) -> Option<Vec<[String; 4]>> {
    let ip = resolve_host(host)?;
    let sock = UdpSocket::bind("0.0.0.0:0").ok()?;
    let _ = sock.set_read_timeout(Some(Duration::from_millis(2500)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(2500)));
    sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).ok()?;

    if let Some(raw_d) = query_raw_sync(&sock, ip, port, b'd') {
        if let Some(pl) = parse_players_detailed(&raw_d) {
            return Some(pl);
        }
    }
    let raw_c = query_raw_sync(&sock, ip, port, b'c')?;
    parse_players_basic(&raw_c)
}

pub fn query_rules_sync(host: &str, port: u16) -> Option<Vec<[String; 2]>> {
    let ip = resolve_host(host)?;
    let sock = UdpSocket::bind("0.0.0.0:0").ok()?;
    let _ = sock.set_read_timeout(Some(Duration::from_millis(2500)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(2500)));
    sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).ok()?;

    let raw_r = query_raw_sync(&sock, ip, port, b'r')?;
    parse_rules_packet(&raw_r)
}

#[derive(Debug, Clone, Deserialize, Serialize)]
pub struct ServerTarget {
    pub ip: String,
    pub port: u16,
}

#[derive(Debug, Clone, Serialize)]
pub struct ScanUpdate {
    pub ip: String,
    pub port: u16,
    pub online: bool,
    pub name: String,
    pub players: i32,
    pub max: i32,
    pub ping: i32,
    pub mode: String,
    pub language: String,
    pub is_open_mp: bool,
}

#[derive(Debug, Clone, Serialize)]
pub struct PublicListItem {
    pub ip: String,
    pub port: String,
    pub name: String,
    pub players: i32,
    pub max: i32,
    pub mode: String,
    pub locked: bool,
    pub is_open_mp: bool,
    pub language: String,
}

#[derive(Debug, Default, Serialize)]
pub struct PollBatch {
    pub done: bool,
    pub from_cache: bool,
    pub list: Option<Vec<PublicListItem>>,
    pub updates: Vec<ScanUpdate>,
    pub error: Option<String>,
}

pub struct ScanState {
    pub queue: Mutex<VecDeque<ScanUpdate>>,
    pub initial_list: Mutex<Option<(Vec<PublicListItem>, bool)>>,
    pub error: Mutex<Option<String>>,
    pub done: AtomicBool,
    pub cancelled: AtomicBool,
}

impl ScanState {
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            queue: Mutex::new(VecDeque::new()),
            initial_list: Mutex::new(None),
            error: Mutex::new(None),
            done: AtomicBool::new(false),
            cancelled: AtomicBool::new(false),
        })
    }
}

static NEXT_HANDLE: AtomicU64 = AtomicU64::new(1);

fn scan_map() -> &'static Mutex<HashMap<u64, Arc<ScanState>>> {
    static MAP: OnceLock<Mutex<HashMap<u64, Arc<ScanState>>>> = OnceLock::new();
    MAP.get_or_init(|| Mutex::new(HashMap::new()))
}

pub fn register_scan(state: Arc<ScanState>) -> u64 {
    let h = NEXT_HANDLE.fetch_add(1, Ordering::Relaxed);
    if let Ok(mut guard) = scan_map().lock() {
        guard.insert(h, state);
    }
    h
}

pub fn get_scan(handle: u64) -> Option<Arc<ScanState>> {
    scan_map().lock().ok()?.get(&handle).cloned()
}

pub fn remove_scan(handle: u64) {
    if let Ok(mut guard) = scan_map().lock() {
        if let Some(st) = guard.remove(&handle) {
            st.cancelled.store(true, Ordering::Relaxed);
        }
    }
}

async fn query_server_async(host: String, port: u16) -> QueryInfo {
    let host_clone = host.clone();
    let ip_opt = tokio::task::spawn_blocking(move || resolve_host(&host_clone))
        .await
        .ok()
        .flatten();
    let Some(ip) = ip_opt else {
        return QueryInfo::default();
    };

    let Ok(sock) = TokioUdpSocket::bind("0.0.0.0:0").await else {
        return QueryInfo::default();
    };
    if sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).await.is_err() {
        return QueryInfo::default();
    }

    let pkt = build_query_packet(ip, port, b'i');
    let t0 = Instant::now();
    if sock.send(&pkt).await.is_err() {
        return QueryInfo::default();
    }
    let mut buf = [0u8; 640];
    let Ok(Ok(n)) = tokio::time::timeout(Duration::from_millis(1400), sock.recv(&mut buf)).await else {
        return QueryInfo::default();
    };
    let ping_ms = t0.elapsed().as_millis() as i32;

    let Some(mut info) = parse_info_packet(&buf[..n], ping_ms) else {
        return QueryInfo::default();
    };

    // Quick optional rules check for open.mp badge (short timeout so batch stays fast)
    let r_pkt = build_query_packet(ip, port, b'r');
    if sock.send(&r_pkt).await.is_ok() {
        let mut rbuf = [0u8; 4096];
        if let Ok(Ok(rn)) = tokio::time::timeout(Duration::from_millis(900), sock.recv(&mut rbuf)).await {
            if let Some(rules) = parse_rules_packet(&rbuf[..rn]) {
                if has_open_mp_rule(&rules) {
                    info.is_open_mp = true;
                }
            }
        }
    }
    info
}

pub async fn run_batch_queries(targets: Vec<ServerTarget>, concurrency: usize, state: Arc<ScanState>) {
    let conc = concurrency.clamp(1, 128);
    let sem = Arc::new(Semaphore::new(conc));
    let mut handles = Vec::with_capacity(targets.len());

    for t in targets {
        if state.cancelled.load(Ordering::Relaxed) {
            break;
        }
        let Ok(permit) = sem.clone().acquire_owned().await else {
            break;
        };
        if state.cancelled.load(Ordering::Relaxed) {
            drop(permit);
            break;
        }
        let st = state.clone();
        let jh = tokio::spawn(async move {
            let _permit = permit;
            if st.cancelled.load(Ordering::Relaxed) {
                return;
            }
            let qr = query_server_async(t.ip.clone(), t.port).await;
            if st.cancelled.load(Ordering::Relaxed) {
                return;
            }
            let upd = ScanUpdate {
                ip: t.ip,
                port: t.port,
                online: qr.online,
                name: qr.name,
                players: qr.players,
                max: qr.max,
                ping: qr.ping,
                mode: qr.mode,
                language: qr.language,
                is_open_mp: qr.is_open_mp,
            };
            if let Ok(mut q) = st.queue.lock() {
                q.push_back(upd);
            }
        });
        handles.push(jh);
    }

    for jh in handles {
        let _ = jh.await;
    }
    state.done.store(true, Ordering::Relaxed);
}

pub fn start_batch_scan(targets: Vec<ServerTarget>, concurrency: usize) -> u64 {
    let state = ScanState::new();
    let handle = register_scan(state.clone());
    std::thread::spawn(move || {
        let rt = tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build();
        match rt {
            Ok(rt) => rt.block_on(run_batch_queries(targets, concurrency, state)),
            Err(e) => {
                if let Ok(mut err) = state.error.lock() {
                    *err = Some(e.to_string());
                }
                state.done.store(true, Ordering::Relaxed);
            }
        }
    });
    handle
}

pub fn poll_scan(handle: u64, max_items: usize) -> PollBatch {
    let Some(state) = get_scan(handle) else {
        return PollBatch {
            done: true,
            ..Default::default()
        };
    };

    let mut batch = PollBatch::default();
    if let Ok(mut init) = state.initial_list.lock() {
        if let Some((list, from_cache)) = init.take() {
            batch.list = Some(list);
            batch.from_cache = from_cache;
        }
    }
    if let Ok(mut q) = state.queue.lock() {
        let take_n = max_items.min(q.len());
        for _ in 0..take_n {
            if let Some(item) = q.pop_front() {
                batch.updates.push(item);
            }
        }
        batch.done = state.done.load(Ordering::Relaxed) && q.is_empty();
    } else {
        batch.done = state.done.load(Ordering::Relaxed);
    }
    if let Ok(mut err) = state.error.lock() {
        batch.error = err.take();
    }
    if batch.done {
        remove_scan(handle);
    }
    batch
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_scan_update_serializes_language() {
        let upd = ScanUpdate {
            ip: "127.0.0.1".to_string(),
            port: 7777,
            online: true,
            name: "Thai RP".to_string(),
            players: 10,
            max: 100,
            ping: 25,
            mode: "RP".to_string(),
            language: "ไทย".to_string(),
            is_open_mp: true,
        };
        let v: serde_json::Value = serde_json::to_value(&upd).expect("serialize ScanUpdate");
        assert_eq!(v["language"], "ไทย");
        assert_eq!(v["mode"], "RP");
        assert_eq!(v["is_open_mp"], true);
    }
}
