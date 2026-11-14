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
use tokio::sync::{oneshot, Notify, Semaphore};

const DNS_TTL_SECS: u64 = 300;
const DNS_MAX_ENTRIES: usize = 2048;

pub fn shared_runtime() -> &'static tokio::runtime::Runtime {
    static RT: OnceLock<tokio::runtime::Runtime> = OnceLock::new();
    RT.get_or_init(|| {
        tokio::runtime::Builder::new_multi_thread()
            .worker_threads(2)
            .thread_name("samp-core-tokio")
            .enable_all()
            .build()
            .expect("Failed to initialize shared Tokio runtime")
    })
}

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

    let key = trimmed.to_ascii_lowercase();
    let now = Instant::now();
    if let Ok(guard) = dns_cache().lock() {
        if let Some(entry) = guard.get(&key) {
            if entry.expires_at > now {
                return Some(entry.ip);
            }
        }
    }

    let addr_iter = (trimmed, 0u16).to_socket_addrs().ok()?;
    for sa in addr_iter {
        if let IpAddr::V4(v4) = sa.ip() {
            if let Ok(mut guard) = dns_cache().lock() {
                if guard.len() >= DNS_MAX_ENTRIES {
                    guard.retain(|_, e| e.expires_at > now);
                    if guard.len() >= DNS_MAX_ENTRIES {
                        guard.clear();
                    }
                }
                guard.insert(
                    key,
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

async fn resolve_host_async(host: &str) -> Option<Ipv4Addr> {
    let trimmed = host.trim();
    if let Ok(ip) = trimmed.parse::<Ipv4Addr>() {
        return Some(ip);
    }
    let key = trimmed.to_ascii_lowercase();
    let now = Instant::now();
    if let Ok(guard) = dns_cache().lock() {
        if let Some(entry) = guard.get(&key) {
            if entry.expires_at > now {
                return Some(entry.ip);
            }
        }
    }
    let owned = trimmed.to_string();
    tokio::task::spawn_blocking(move || resolve_host(&owned))
        .await
        .ok()
        .flatten()
}

fn query_raw_sync(sock: &UdpSocket, ip: Ipv4Addr, port: u16, op: u8) -> Option<Vec<u8>> {
    let pkt = build_query_packet(ip, port, op);
    sock.send(&pkt).ok()?;
    let mut buf = vec![0u8; 65536];
    let n = sock.recv(&mut buf).ok()?;
    if n >= 11 && &buf[..4] == b"SAMP" && buf[10] == op {
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
    let _ = sock.set_read_timeout(Some(Duration::from_millis(1400)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(1400)));
    if sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).is_err() {
        return QueryInfo::default();
    }

    let pkt = build_query_packet(ip, port, b'i');
    let t0 = Instant::now();
    if sock.send(&pkt).is_err() {
        return QueryInfo::default();
    }
    let mut buf = [0u8; 1024];
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
    let _ = sock.set_read_timeout(Some(Duration::from_millis(2000)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(2000)));
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
    let _ = sock.set_read_timeout(Some(Duration::from_millis(2000)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(2000)));
    sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).ok()?;

    let raw_r = query_raw_sync(&sock, ip, port, b'r')?;
    parse_rules_packet(&raw_r)
}

#[derive(Debug, Clone, Default, Serialize)]
pub struct ServerDetailsResult {
    pub info: QueryInfo,
    pub players: Option<Vec<[String; 4]>>,
    pub rules: Option<Vec<[String; 2]>>,
}

async fn send_and_recv_op(ip: Ipv4Addr, port: u16, op: u8, timeout_ms: u64) -> Option<(i32, Vec<u8>)> {
    let sock = TokioUdpSocket::bind("0.0.0.0:0").await.ok()?;
    sock.connect(SocketAddr::new(IpAddr::V4(ip), port))
        .await
        .ok()?;
    let pkt = build_query_packet(ip, port, op);
    let t0 = Instant::now();
    sock.send(&pkt).await.ok()?;
    let mut buf = vec![0u8; 65536];
    let n = tokio::time::timeout(Duration::from_millis(timeout_ms), sock.recv(&mut buf))
        .await
        .ok()?
        .ok()?;
    if n >= 11 && &buf[..4] == b"SAMP" && buf[10] == op {
        buf.truncate(n);
        Some((t0.elapsed().as_millis() as i32, buf))
    } else {
        None
    }
}

pub fn query_server_details_sync(host: &str, port: u16) -> ServerDetailsResult {
    shared_runtime().block_on(async {
        let Some(ip) = resolve_host_async(host).await else {
            return ServerDetailsResult::default();
        };
        let (res_i, res_r, res_d) = tokio::join!(
            send_and_recv_op(ip, port, b'i', 1400),
            send_and_recv_op(ip, port, b'r', 1500),
            send_and_recv_op(ip, port, b'd', 1600),
        );

        let mut info = match res_i {
            Some((ping, raw_i)) => parse_info_packet(&raw_i, ping).unwrap_or_default(),
            None => QueryInfo::default(),
        };
        let rules = res_r.and_then(|(_, raw_r)| parse_rules_packet(&raw_r));
        if let Some(ref rl) = rules {
            if has_open_mp_rule(rl) {
                info.is_open_mp = true;
            }
        }
        let mut players = res_d.and_then(|(_, raw_d)| parse_players_detailed(&raw_d));
        if info.online && players.is_none() && info.players > 0 {
            if let Some((_, raw_c)) = send_and_recv_op(ip, port, b'c', 1200).await {
                players = parse_players_basic(&raw_c);
            }
        }

        ServerDetailsResult {
            info,
            players,
            rules,
        }
    })
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
    pub locked: bool,
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

type PendingMap = Mutex<HashMap<(SocketAddr, u8), oneshot::Sender<(Instant, Vec<u8>)>>>;

async fn mux_query_op(
    sock: &TokioUdpSocket,
    pending: &PendingMap,
    ip: Ipv4Addr,
    port: u16,
    op: u8,
    timeout_ms: u64,
) -> Option<(i32, Vec<u8>)> {
    let dst = SocketAddr::new(IpAddr::V4(ip), port);
    let key = (dst, op);
    let (tx, rx) = oneshot::channel();
    if let Ok(mut g) = pending.lock() {
        g.insert(key, tx);
    }

    let pkt = build_query_packet(ip, port, op);
    let t0 = Instant::now();
    if sock.send_to(&pkt, dst).await.is_err() {
        if let Ok(mut g) = pending.lock() {
            g.remove(&key);
        }
        return None;
    }

    match tokio::time::timeout(Duration::from_millis(timeout_ms), rx).await {
        Ok(Ok((recv_at, bytes))) => {
            let ms = recv_at.saturating_duration_since(t0).as_millis() as i32;
            Some((ms.max(1), bytes))
        }
        _ => {
            if let Ok(mut g) = pending.lock() {
                g.remove(&key);
            }
            None
        }
    }
}

pub async fn run_batch_queries(targets: Vec<ServerTarget>, concurrency: usize, state: Arc<ScanState>) {
    let Ok(sock) = TokioUdpSocket::bind("0.0.0.0:0").await else {
        if let Ok(mut err) = state.error.lock() {
            *err = Some("Cannot bind batch UDP socket".into());
        }
        state.done.store(true, Ordering::Relaxed);
        return;
    };
    let sock = Arc::new(sock);
    let pending: Arc<PendingMap> = Arc::new(Mutex::new(HashMap::new()));
    let stop_recv = Arc::new(Notify::new());

    let rx_sock = sock.clone();
    let rx_pending = pending.clone();
    let rx_stop = stop_recv.clone();
    let recv_task = tokio::spawn(async move {
        let mut buf = vec![0u8; 4096];
        loop {
            tokio::select! {
                _ = rx_stop.notified() => break,
                res = rx_sock.recv_from(&mut buf) => {
                    if let Ok((n, peer)) = res {
                        if n >= 11 && &buf[..4] == b"SAMP" {
                            let op = buf[10];
                            if let Ok(mut g) = rx_pending.lock() {
                                if let Some(tx) = g.remove(&(peer, op)) {
                                    let _ = tx.send((Instant::now(), buf[..n].to_vec()));
                                }
                            }
                        }
                    }
                }
            }
        }
    });

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
        let sk = sock.clone();
        let pend = pending.clone();
        let jh = tokio::spawn(async move {
            let _permit = permit;
            if st.cancelled.load(Ordering::Relaxed) {
                return;
            }
            let Some(ip) = resolve_host_async(&t.ip).await else {
                let upd = ScanUpdate {
                    ip: t.ip,
                    port: t.port,
                    online: false,
                    name: String::new(),
                    players: -1,
                    max: 0,
                    ping: -1,
                    mode: String::new(),
                    language: String::new(),
                    is_open_mp: false,
                    locked: false,
                };
                if let Ok(mut q) = st.queue.lock() {
                    q.push_back(upd);
                }
                return;
            };

            let mut qr = match mux_query_op(&sk, &pend, ip, t.port, b'i', 1400).await {
                Some((ping_ms, raw_i)) => parse_info_packet(&raw_i, ping_ms).unwrap_or_default(),
                None => QueryInfo::default(),
            };

            if qr.online && !st.cancelled.load(Ordering::Relaxed) {
                if let Some((_, raw_r)) = mux_query_op(&sk, &pend, ip, t.port, b'r', 900).await {
                    if let Some(rules) = parse_rules_packet(&raw_r) {
                        if has_open_mp_rule(&rules) {
                            qr.is_open_mp = true;
                        }
                    }
                }
            }

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
                locked: qr.password_locked,
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
    stop_recv.notify_one();
    let _ = recv_task.await;
    state.done.store(true, Ordering::Relaxed);
}

pub fn start_batch_scan(targets: Vec<ServerTarget>, concurrency: usize) -> u64 {
    let state = ScanState::new();
    let handle = register_scan(state.clone());
    shared_runtime().spawn(async move {
        run_batch_queries(targets, concurrency, state).await;
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

#[derive(Debug, Clone, Default, Serialize)]
pub struct SlotWatchStatus {
    pub watching: bool,
    pub found_slot: bool,
    pub ip: String,
    pub port: u16,
    pub name: String,
    pub players: i32,
    pub max: i32,
    pub ping: i32,
}

struct SlotWatchState {
    gen: u64,
    status: SlotWatchStatus,
    cancel: Option<Arc<Notify>>,
}

fn slot_watch_hub() -> &'static Mutex<SlotWatchState> {
    static SW: OnceLock<Mutex<SlotWatchState>> = OnceLock::new();
    SW.get_or_init(|| {
        Mutex::new(SlotWatchState {
            gen: 0,
            status: SlotWatchStatus::default(),
            cancel: None,
        })
    })
}

pub fn start_slot_watch(host: &str, port: u16) -> bool {
    let trimmed = host.trim().to_string();
    if trimmed.is_empty() || port == 0 {
        stop_slot_watch();
        return false;
    }

    let notify = Arc::new(Notify::new());
    let my_gen = {
        let Ok(mut g) = slot_watch_hub().lock() else {
            return false;
        };
        if let Some(prev) = g.cancel.take() {
            prev.notify_one();
        }
        g.gen = g.gen.wrapping_add(1);
        g.status = SlotWatchStatus {
            watching: true,
            found_slot: false,
            ip: trimmed.clone(),
            port,
            name: String::new(),
            players: -1,
            max: 0,
            ping: -1,
        };
        g.cancel = Some(notify.clone());
        g.gen
    };

    shared_runtime().spawn(async move {
        loop {
            if let Some(ip) = resolve_host_async(&trimmed).await {
                if let Some((ping, raw_i)) = send_and_recv_op(ip, port, b'i', 1400).await {
                    if let Some(qr) = parse_info_packet(&raw_i, ping) {
                        let Ok(mut g) = slot_watch_hub().lock() else {
                            return;
                        };
                        if g.gen != my_gen || !g.status.watching {
                            return;
                        }
                        if !qr.name.is_empty() {
                            g.status.name = qr.name;
                        }
                        g.status.players = qr.players;
                        g.status.max = qr.max;
                        g.status.ping = qr.ping;
                        if qr.online && qr.max > 0 && qr.players >= 0 && qr.players < qr.max {
                            g.status.found_slot = true;
                            g.status.watching = false;
                            return;
                        }
                    }
                }
            }

            tokio::select! {
                _ = notify.notified() => return,
                _ = tokio::time::sleep(Duration::from_secs(2)) => {}
            }

            let still_active = slot_watch_hub()
                .lock()
                .map(|g| g.gen == my_gen && g.status.watching)
                .unwrap_or(false);
            if !still_active {
                return;
            }
        }
    });
    true
}

pub fn stop_slot_watch() {
    if let Ok(mut g) = slot_watch_hub().lock() {
        g.gen = g.gen.wrapping_add(1);
        g.status.watching = false;
        if let Some(n) = g.cancel.take() {
            n.notify_one();
        }
    }
}

pub fn poll_slot_watch() -> SlotWatchStatus {
    let Ok(mut g) = slot_watch_hub().lock() else {
        return SlotWatchStatus::default();
    };
    let out = g.status.clone();
    if g.status.found_slot {
        g.status.found_slot = false;
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::protocol::encode_cp874;

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
            locked: false,
        };
        let v: serde_json::Value = serde_json::to_value(&upd).expect("serialize ScanUpdate");
        assert_eq!(v["language"], "ไทย");
        assert_eq!(v["mode"], "RP");
        assert_eq!(v["is_open_mp"], true);
    }

    fn spawn_mock_samp_server(name: &str, players: u16, max: u16) -> (u16, std::thread::JoinHandle<()>) {
        let srv = UdpSocket::bind("127.0.0.1:0").unwrap();
        let port = srv.local_addr().unwrap().port();
        srv.set_read_timeout(Some(Duration::from_secs(2))).unwrap();
        let name_owned = name.to_string();
        let jh = std::thread::spawn(move || {
            let mut buf = [0u8; 256];
            for _ in 0..8 {
                let Ok((n, peer)) = srv.recv_from(&mut buf) else {
                    break;
                };
                if n < 11 || &buf[..4] != b"SAMP" {
                    continue;
                }
                let op = buf[10];
                let mut reply = buf[..11].to_vec();
                match op {
                    b'i' => {
                        reply.push(1); // password locked
                        reply.extend_from_slice(&players.to_le_bytes());
                        reply.extend_from_slice(&max.to_le_bytes());
                        let nb = encode_cp874(&name_owned);
                        reply.extend_from_slice(&(nb.len() as u32).to_le_bytes());
                        reply.extend_from_slice(&nb);
                        reply.extend_from_slice(&2u32.to_le_bytes());
                        reply.extend_from_slice(b"RP");
                        reply.extend_from_slice(&2u32.to_le_bytes());
                        reply.extend_from_slice(b"TH");
                        let _ = srv.send_to(&reply, peer);
                    }
                    b'r' => {
                        reply.extend_from_slice(&1u16.to_le_bytes());
                        reply.push(7);
                        reply.extend_from_slice(b"version");
                        reply.push(3);
                        reply.extend_from_slice(b"1.4");
                        let _ = srv.send_to(&reply, peer);
                    }
                    b'd' => {
                        reply.extend_from_slice(&1u16.to_le_bytes());
                        reply.push(3);
                        reply.push(4);
                        reply.extend_from_slice(b"Test");
                        reply.extend_from_slice(&50i32.to_le_bytes());
                        reply.extend_from_slice(&12i32.to_le_bytes());
                        let _ = srv.send_to(&reply, peer);
                    }
                    _ => {}
                }
            }
        });
        (port, jh)
    }

    #[test]
    fn test_loopback_details_concurrent_and_pooled_batch_scan() {
        let (p1, j1) = spawn_mock_samp_server("เซิร์ฟ 1", 10, 100);
        let (p2, j2) = spawn_mock_samp_server("Server Two", 55, 200);

        let det = query_server_details_sync("127.0.0.1", p1);
        assert!(det.info.online);
        assert!(det.info.password_locked);
        assert!(det.info.is_open_mp);
        assert_eq!(det.info.name, "เซิร์ฟ 1");
        assert_eq!(det.players.as_ref().map(|v| v.len()), Some(1));
        assert_eq!(det.rules.as_ref().map(|v| v.len()), Some(1));

        let handle = start_batch_scan(
            vec![
                ServerTarget { ip: "127.0.0.1".into(), port: p1 },
                ServerTarget { ip: "127.0.0.1".into(), port: p2 },
            ],
            8,
        );
        let mut updates = Vec::new();
        for _ in 0..40 {
            let b = poll_scan(handle, 32);
            updates.extend(b.updates);
            if b.done {
                break;
            }
            std::thread::sleep(Duration::from_millis(20));
        }
        assert_eq!(updates.len(), 2);
        assert!(updates.iter().any(|u| u.port == p1 && u.name == "เซิร์ฟ 1"));
        assert!(updates.iter().any(|u| u.port == p2 && u.name == "Server Two"));

        let _ = j1.join();
        let _ = j2.join();
    }
}
