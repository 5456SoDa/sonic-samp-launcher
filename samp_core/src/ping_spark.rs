use crate::protocol::build_ping_packet;
use crate::query::resolve_host;
use serde::Serialize;
use std::collections::VecDeque;
use std::net::{IpAddr, SocketAddr, UdpSocket};
use std::sync::{Condvar, Mutex, OnceLock};
use std::time::{Duration, Instant};

const MAX_SAMPLES: usize = 30;
const POLL_INTERVAL_MS: u64 = 1500;

#[derive(Debug, Clone, Serialize)]
pub struct PingSparkReport {
    pub host: String,
    pub port: u16,
    pub history: Vec<i32>,
    pub latest: i32,
    pub min: i32,
    pub max: i32,
    pub avg: i32,
    pub jitter: f64,
    pub loss: f64,
}

struct SparkInner {
    active: bool,
    worker_spawned: bool,
    target_host: String,
    target_port: u16,
    gen: u64,
    samples: VecDeque<i32>, // -1 means timeout/loss
}

struct SparkHub {
    lock: Mutex<SparkInner>,
    cv: Condvar,
}

fn hub() -> &'static SparkHub {
    static HUB: OnceLock<SparkHub> = OnceLock::new();
    HUB.get_or_init(|| SparkHub {
        lock: Mutex::new(SparkInner {
            active: false,
            worker_spawned: false,
            target_host: String::new(),
            target_port: 0,
            gen: 0,
            samples: VecDeque::with_capacity(MAX_SAMPLES),
        }),
        cv: Condvar::new(),
    })
}

fn ping_once(host: &str, port: u16) -> i32 {
    let Some(ip) = resolve_host(host) else {
        return -1;
    };
    let Ok(sock) = UdpSocket::bind("0.0.0.0:0") else {
        return -1;
    };
    let _ = sock.set_read_timeout(Some(Duration::from_millis(1100)));
    let _ = sock.set_write_timeout(Some(Duration::from_millis(1100)));
    if sock.connect(SocketAddr::new(IpAddr::V4(ip), port)).is_err() {
        return -1;
    }
    let pkt = build_ping_packet(ip, port, *b"SONC");
    let t0 = Instant::now();
    if sock.send(&pkt).is_err() {
        return -1;
    }
    let mut buf = [0u8; 512];
    match sock.recv(&mut buf) {
        Ok(n) if n >= 11 && &buf[..4] == b"SAMP" && buf[10] == b'p' => {
            let ms = t0.elapsed().as_millis() as i32;
            ms.max(1)
        }
        _ => -1,
    }
}

fn worker_loop() {
    let h = hub();
    let mut last_gen: u64 = 0;
    let mut next_tick = Instant::now();
    loop {
        let (host, port, gen) = {
            let mut guard = match h.lock.lock() {
                Ok(g) => g,
                Err(_) => return,
            };
            while !guard.active || guard.target_host.is_empty() || guard.target_port == 0 {
                guard = match h.cv.wait(guard) {
                    Ok(g) => g,
                    Err(_) => return,
                };
            }
            (guard.target_host.clone(), guard.target_port, guard.gen)
        };

        if gen != last_gen {
            last_gen = gen;
            next_tick = Instant::now();
        }
        next_tick += Duration::from_millis(POLL_INTERVAL_MS);

        let rtt = ping_once(&host, port);

        {
            let mut guard = match h.lock.lock() {
                Ok(g) => g,
                Err(_) => return,
            };
            if guard.active && guard.gen == gen {
                if guard.samples.len() >= MAX_SAMPLES {
                    guard.samples.pop_front();
                }
                guard.samples.push_back(rtt);
            }
            let current_gen = guard.gen;
            let wait_dur = next_tick.saturating_duration_since(Instant::now());
            if guard.active && current_gen == gen && !wait_dur.is_zero() {
                let _ = h.cv.wait_timeout_while(guard, wait_dur, |st| {
                    st.active && st.gen == current_gen
                });
            }
        }
    }
}

/// Starts or switches the background sparkline ping target.
pub fn start_ping(host: &str, port: u16) {
    let trimmed = host.trim();
    if trimmed.is_empty() || port == 0 {
        stop_ping();
        return;
    }
    let h = hub();
    if let Ok(mut guard) = h.lock.lock() {
        if guard.active
            && guard.target_host.eq_ignore_ascii_case(trimmed)
            && guard.target_port == port
        {
            return;
        }
        guard.target_host = trimmed.to_string();
        guard.target_port = port;
        guard.active = true;
        guard.gen = guard.gen.wrapping_add(1);
        guard.samples.clear();

        if !guard.worker_spawned {
            guard.worker_spawned = true;
            std::thread::Builder::new()
                .name("samp-spark-ping".to_string())
                .spawn(worker_loop)
                .ok();
        }
        h.cv.notify_all();
    }
}

/// Pauses background sparkline pinging without terminating the daemon thread.
pub fn stop_ping() {
    let h = hub();
    if let Ok(mut guard) = h.lock.lock() {
        guard.active = false;
        guard.gen = guard.gen.wrapping_add(1);
        h.cv.notify_all();
    }
}

pub fn compute_stats(host: &str, port: u16, samples: &[i32]) -> PingSparkReport {
    let valid: Vec<i32> = samples.iter().copied().filter(|&v| v >= 0).collect();
    let latest = samples.last().copied().unwrap_or(-1);
    let min = valid.iter().copied().min().unwrap_or(0);
    let max = valid.iter().copied().max().unwrap_or(0);
    let avg = if valid.is_empty() {
        0
    } else {
        (valid.iter().map(|&v| v as i64).sum::<i64>() / valid.len() as i64) as i32
    };

    let mut jitter_sum = 0i64;
    let mut jitter_cnt = 0usize;
    for pair in valid.windows(2) {
        jitter_sum += (pair[1] - pair[0]).abs() as i64;
        jitter_cnt += 1;
    }
    let jitter = if jitter_cnt > 0 {
        ((jitter_sum as f64 / jitter_cnt as f64) * 10.0).round() / 10.0
    } else {
        0.0
    };

    let dropped = samples.iter().filter(|&&v| v < 0).count();
    let loss = if samples.is_empty() {
        0.0
    } else {
        ((dropped as f64 / samples.len() as f64) * 1000.0).round() / 10.0
    };

    PingSparkReport {
        host: host.to_string(),
        port,
        history: samples.to_vec(),
        latest,
        min,
        max,
        avg,
        jitter,
        loss,
    }
}

/// Returns a JSON snapshot of ping history if matching the current target.
pub fn get_spark_report(host: &str, port: u16) -> PingSparkReport {
    let h = hub();
    let samples: Vec<i32> = if let Ok(guard) = h.lock.lock() {
        if guard.target_host.eq_ignore_ascii_case(host.trim()) && guard.target_port == port {
            guard.samples.iter().copied().collect()
        } else {
            Vec::new()
        }
    } else {
        Vec::new()
    };
    compute_stats(host.trim(), port, &samples)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_compute_stats_with_jitter_and_loss() {
        let samples = vec![20, 24, 22, -1, 26];
        let rep = compute_stats("127.0.0.1", 7777, &samples);
        assert_eq!(rep.min, 20);
        assert_eq!(rep.max, 26);
        assert_eq!(rep.avg, 23);
        assert_eq!(rep.latest, 26);
        // Valid: 20, 24, 22, 26 -> diffs: 4, 2, 4 -> avg 10/3 = 3.3
        assert!((rep.jitter - 3.3).abs() < 0.01);
        // 1 dropped of 5 = 20.0%
        assert!((rep.loss - 20.0).abs() < 0.01);
    }

    #[test]
    fn test_spark_loopback_udp_echo() {
        let sock = UdpSocket::bind("127.0.0.1:0").unwrap();
        let port = sock.local_addr().unwrap().port();
        sock.set_read_timeout(Some(Duration::from_secs(2))).unwrap();

        let srv = std::thread::spawn(move || {
            let mut buf = [0u8; 64];
            for _ in 0..2 {
                if let Ok((n, peer)) = sock.recv_from(&mut buf) {
                    if n >= 11 && &buf[..4] == b"SAMP" {
                        let _ = sock.send_to(&buf[..n], peer);
                    }
                }
            }
        });

        start_ping("127.0.0.1", port);
        std::thread::sleep(Duration::from_millis(250));
        let rep = get_spark_report("127.0.0.1", port);
        stop_ping();
        let _ = srv.join();

        assert!(!rep.history.is_empty(), "Expected at least one ping sample");
        assert!(rep.avg >= 1, "Expected valid avg latency");
        assert_eq!(rep.loss, 0.0);
    }
}
