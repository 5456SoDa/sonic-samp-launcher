use crate::win_ro::to_wide;
use serde_json::json;
use std::fs::{File, OpenOptions};
use std::io::{Read, Write};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Condvar, Mutex, OnceLock};
use std::time::{Duration, SystemTime, UNIX_EPOCH};
use windows_sys::Win32::System::Pipes::WaitNamedPipeW;

const DEFAULT_CLIENT_ID: &str = "1088484931343753236";
const MAX_DISCORD_STR_BYTES: usize = 128;

#[derive(Clone)]
struct PresencePayload {
    details: String,
    state: String,
    large_image: String,
    large_text: String,
    small_image: String,
    small_text: String,
    start_epoch: Option<u64>,
}

struct DiscordState {
    enabled: bool,
    running: bool,
    worker_spawned: bool,
    connected: bool,
    client_id: String,
    activity: Option<PresencePayload>,
    dirty: bool,
}

struct DiscordHub {
    lock: Mutex<DiscordState>,
    cv: Condvar,
}

fn hub() -> &'static DiscordHub {
    static HUB: OnceLock<DiscordHub> = OnceLock::new();
    HUB.get_or_init(|| DiscordHub {
        lock: Mutex::new(DiscordState {
            enabled: true,
            running: false,
            worker_spawned: false,
            connected: false,
            client_id: DEFAULT_CLIENT_ID.to_string(),
            activity: None,
            dirty: false,
        }),
        cv: Condvar::new(),
    })
}

static NONCE_COUNTER: AtomicU64 = AtomicU64::new(1);

/// Truncates a string so its UTF-8 byte length (including `"..."` suffix if cut)
/// never exceeds `max_bytes` and always lands on a valid Unicode scalar boundary.
pub fn truncate_utf8_bytes(s: &str, max_bytes: usize) -> String {
    if s.len() <= max_bytes {
        return s.to_string();
    }
    if max_bytes <= 3 {
        let mut end = max_bytes;
        while end > 0 && !s.is_char_boundary(end) {
            end -= 1;
        }
        return s[..end].to_string();
    }
    let limit = max_bytes - 3;
    let mut end = limit;
    while end > 0 && !s.is_char_boundary(end) {
        end -= 1;
    }
    format!("{}...", &s[..end])
}

pub fn encode_ipc_frame(opcode: u32, payload_json: &str) -> Vec<u8> {
    let payload = payload_json.as_bytes();
    let len = payload.len() as u32;
    let mut frame = Vec::with_capacity(8 + payload.len());
    frame.extend_from_slice(&opcode.to_le_bytes());
    frame.extend_from_slice(&len.to_le_bytes());
    frame.extend_from_slice(payload);
    frame
}

fn write_ipc_frame(pipe: &mut File, opcode: u32, json: &str) -> std::io::Result<()> {
    let frame = encode_ipc_frame(opcode, json);
    pipe.write_all(&frame)?;
    pipe.flush()
}

fn read_ipc_frame(pipe: &mut File) -> std::io::Result<()> {
    let mut hdr = [0u8; 8];
    pipe.read_exact(&mut hdr)?;
    let len = u32::from_le_bytes([hdr[4], hdr[5], hdr[6], hdr[7]]) as usize;
    if len > 0 && len < 65536 {
        let mut buf = vec![0u8; len];
        pipe.read_exact(&mut buf)?;
    }
    Ok(())
}

fn try_connect(client_id: &str) -> Option<File> {
    for i in 0..10 {
        let pipe_name = format!(r"\\.\pipe\discord-ipc-{}", i);
        let wide = to_wide(&pipe_name);
        // Wait briefly only if pipe server exists or busy; non-existent returns immediately
        let ready = unsafe { WaitNamedPipeW(wide.as_ptr(), 150) };
        let mut pipe = match OpenOptions::new().read(true).write(true).open(&pipe_name) {
            Ok(f) => f,
            Err(_) => {
                if ready == 0 {
                    continue;
                }
                continue;
            }
        };

        let hs = json!({
            "v": 1,
            "client_id": client_id,
        })
        .to_string();
        if write_ipc_frame(&mut pipe, 0, &hs).is_err() {
            continue;
        }
        if read_ipc_frame(&mut pipe).is_err() {
            continue;
        }
        return Some(pipe);
    }
    None
}

fn send_activity(pipe: &mut File, payload: Option<&PresencePayload>) -> std::io::Result<()> {
    let nonce = NONCE_COUNTER.fetch_add(1, Ordering::Relaxed);
    let act_val = match payload {
        Some(p) => {
            let mut obj = serde_json::Map::new();
            obj.insert(
                "details".to_string(),
                json!(truncate_utf8_bytes(&p.details, MAX_DISCORD_STR_BYTES)),
            );
            obj.insert(
                "state".to_string(),
                json!(truncate_utf8_bytes(&p.state, MAX_DISCORD_STR_BYTES)),
            );
            if let Some(ts) = p.start_epoch {
                obj.insert("timestamps".to_string(), json!({ "start": ts }));
            }
            obj.insert(
                "assets".to_string(),
                json!({
                    "large_image": p.large_image,
                    "large_text": truncate_utf8_bytes(&p.large_text, MAX_DISCORD_STR_BYTES),
                    "small_image": p.small_image,
                    "small_text": truncate_utf8_bytes(&p.small_text, MAX_DISCORD_STR_BYTES),
                }),
            );
            serde_json::Value::Object(obj)
        }
        None => serde_json::Value::Null,
    };

    let msg = json!({
        "cmd": "SET_ACTIVITY",
        "args": {
            "pid": std::process::id(),
            "activity": act_val,
        },
        "nonce": format!("sonic_{}", nonce),
    })
    .to_string();

    write_ipc_frame(pipe, 1, &msg)
}

fn worker_loop() {
    let h = hub();
    let mut pipe: Option<File> = None;

    loop {
        let (running, enabled, client_id, dirty, activity) = {
            let mut guard = match h.lock.lock() {
                Ok(g) => g,
                Err(_) => return,
            };
            while guard.running && (!guard.enabled || (!guard.dirty && guard.connected)) {
                guard = match h.cv.wait_timeout(guard, Duration::from_secs(15)) {
                    Ok((g, _)) => g,
                    Err(_) => return,
                };
            }
            if !guard.running {
                break;
            }
            let act = guard.activity.clone();
            let d = guard.dirty;
            if d && guard.connected {
                guard.dirty = false;
            }
            (guard.running, guard.enabled, guard.client_id.clone(), d, act)
        };

        if !running {
            break;
        }
        if !enabled {
            if let Some(mut p) = pipe.take() {
                let _ = send_activity(&mut p, None);
            }
            if let Ok(mut g) = h.lock.lock() {
                g.connected = false;
                g.dirty = false;
                let _ = h.cv.wait_timeout(g, Duration::from_secs(15));
            }
            continue;
        }

        if pipe.is_none() {
            if let Some(p) = try_connect(&client_id) {
                pipe = Some(p);
                if let Ok(mut g) = h.lock.lock() {
                    g.connected = true;
                    g.dirty = true;
                }
                continue;
            } else {
                if let Ok(mut g) = h.lock.lock() {
                    g.connected = false;
                }
                if let Ok(g) = h.lock.lock() {
                    let _ = h.cv.wait_timeout(g, Duration::from_secs(15));
                }
                continue;
            }
        }

        if dirty {
            if let Some(ref mut p) = pipe {
                if send_activity(p, activity.as_ref()).is_err() {
                    pipe = None;
                    if let Ok(mut g) = h.lock.lock() {
                        g.connected = false;
                        g.dirty = true;
                    }
                }
            }
        }
    }

    if let Some(mut p) = pipe {
        let _ = send_activity(&mut p, None);
    }
    if let Ok(mut g) = h.lock.lock() {
        g.connected = false;
    }
}

pub fn init(client_id: &str) {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        let cid = client_id.trim();
        if !cid.is_empty() {
            g.client_id = cid.to_string();
        }
        g.running = true;
        if !g.worker_spawned {
            g.worker_spawned = true;
            std::thread::Builder::new()
                .name("samp-discord-ipc".to_string())
                .spawn(worker_loop)
                .ok();
        }
        h.cv.notify_all();
    }
}

pub fn set_enabled(enabled: bool) {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        g.enabled = enabled;
        if !enabled {
            g.activity = None;
        }
        g.dirty = true;
        h.cv.notify_all();
    }
}

pub fn update_launcher_presence() {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        if !g.enabled {
            return;
        }
        g.activity = Some(PresencePayload {
            details: "อยู่ในเมนูหลัก (Browsing Servers)".to_string(),
            state: "SONIC CLIENT v2.4".to_string(),
            large_image: "gtasa".to_string(),
            large_text: "GTA San Andreas - Sonic Client".to_string(),
            small_image: "online".to_string(),
            small_text: "Online".to_string(),
            start_epoch: None,
        });
        g.dirty = true;
        h.cv.notify_all();
    }
}

pub fn update_game_presence(server_name: &str, ip: &str, port: &str, players: i32, max: i32) {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        if !g.enabled {
            return;
        }
        let s_name = if server_name.trim().is_empty() {
            "GTA San Andreas Multiplayer"
        } else {
            server_name.trim()
        };
        let details = format!("เล่นอยู่ใน {}", s_name);
        let state = if players >= 0 && max > 0 {
            format!("ผู้เล่น: {}/{} คน ({}:{})", players, max, ip, port)
        } else {
            format!("เซิร์ฟเวอร์: {}:{}", ip, port)
        };
        let epoch = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.as_secs())
            .unwrap_or(0);

        g.activity = Some(PresencePayload {
            details,
            state,
            large_image: "gtasa".to_string(),
            large_text: s_name.to_string(),
            small_image: "play".to_string(),
            small_text: "In-Game".to_string(),
            start_epoch: Some(epoch),
        });
        g.dirty = true;
        h.cv.notify_all();
    }
}

pub fn clear_presence() {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        g.activity = None;
        g.dirty = true;
        h.cv.notify_all();
    }
}

pub fn shutdown() {
    let h = hub();
    if let Ok(mut g) = h.lock.lock() {
        g.running = false;
        g.activity = None;
        g.dirty = true;
        h.cv.notify_all();
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_truncate_utf8_bytes_thai_and_emoji_respects_128_bytes() {
        let thai = "เล่นอยู่ใน ".to_string() + &"เซิร์ฟเวอร์ภาษาไทยทดสอบความยาว".repeat(6);
        let out = truncate_utf8_bytes(&thai, 128);
        assert!(out.len() <= 128, "Byte len {} exceeded 128", out.len());
        assert!(out.ends_with("..."));
        assert!(std::str::from_utf8(out.as_bytes()).is_ok());

        let emoji = "🔥🚀".repeat(50);
        let out2 = truncate_utf8_bytes(&emoji, 128);
        assert!(out2.len() <= 128);
        assert!(std::str::from_utf8(out2.as_bytes()).is_ok());
    }

    #[test]
    fn test_ipc_frame_encoding() {
        let frame = encode_ipc_frame(1, "{\"ok\":true}");
        assert_eq!(u32::from_le_bytes([frame[0], frame[1], frame[2], frame[3]]), 1);
        assert_eq!(u32::from_le_bytes([frame[4], frame[5], frame[6], frame[7]]), 11);
        assert_eq!(&frame[8..], b"{\"ok\":true}");
    }
}
