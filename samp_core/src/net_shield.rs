use crate::audio;
use crate::protocol::build_ping_packet;
use crate::query::resolve_host;
use crate::win_ro::{self, HKEY_LOCAL_MACHINE};
use serde::Serialize;
use std::fs;
use std::net::{IpAddr, SocketAddr, UdpSocket};
use std::path::Path;
use std::time::{Duration, Instant};

const FIREWALL_RULES_SUBKEY: &str =
    r"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules";

#[derive(Debug, Clone, Serialize)]
pub struct FirewallStatus {
    pub checked: bool,
    pub gta_sa_allowed: bool,
    pub gta_sa_blocked: bool,
    pub launcher_allowed: bool,
    pub total_rules_scanned: usize,
    pub summary: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct PrewarmReport {
    pub host: String,
    pub resolved_ip: String,
    pub game_port: u16,
    pub voice_port: Option<u16>,
    pub dns_ms: u32,
    pub game_ping_ms: i32,
    pub voice_route_warmed: bool,
    pub summary: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct NetDiagnostics {
    pub firewall: FirewallStatus,
    pub alt_tab_nopause: bool,
    pub alt_tab_note: String,
    pub last_voice_server: String,
    pub last_voice_port: u16,
}

fn norm_exe_path(p: &Path) -> String {
    p.to_string_lossy().replace('/', "\\").to_ascii_lowercase()
}

pub fn inspect_firewall_rules_list(
    rules: &[String],
    gta_exe: &Path,
    launcher_exe: Option<&Path>,
) -> FirewallStatus {
    let gta_target = norm_exe_path(gta_exe);
    let launcher_target = launcher_exe.map(norm_exe_path);

    let mut gta_allowed = false;
    let mut gta_blocked = false;
    let mut launcher_allowed = false;

    for raw in rules {
        if !raw.to_ascii_lowercase().contains("app=") {
            continue;
        }
        let mut active = false;
        let mut allow = false;
        let mut block = false;
        let mut app_val: Option<String> = None;

        for field in raw.split('|') {
            if let Some((k, v)) = field.split_once('=') {
                let kl = k.trim();
                let vl = v.trim();
                if kl.eq_ignore_ascii_case("Active") {
                    active = vl.eq_ignore_ascii_case("TRUE");
                } else if kl.eq_ignore_ascii_case("Action") {
                    allow = vl.eq_ignore_ascii_case("Allow");
                    block = vl.eq_ignore_ascii_case("Block");
                } else if kl.eq_ignore_ascii_case("App") {
                    app_val = Some(vl.replace('/', "\\").to_ascii_lowercase());
                }
            }
        }

        if !active {
            continue;
        }
        let Some(app) = app_val else {
            continue;
        };

        let matches_gta = app == gta_target || app.ends_with("\\gta_sa.exe");
        if matches_gta {
            if block {
                gta_blocked = true;
            } else if allow {
                gta_allowed = true;
            }
        }

        if let Some(ref lt) = launcher_target {
            if app == *lt || app.ends_with("\\samplauncher.exe") {
                if allow && !block {
                    launcher_allowed = true;
                }
            }
        }
    }

    let summary = if gta_blocked {
        "ตรวจพบ Rule บล็อก gta_sa.exe ใน Windows Firewall (อาจทำให้เข้าเซิร์ฟเวอร์หรือไมค์ไม่ได้)".to_string()
    } else if gta_allowed && launcher_allowed {
        "Windows Firewall อนุญาต gta_sa.exe และ Launcher แล้ว".to_string()
    } else if gta_allowed {
        "Windows Firewall อนุญาต gta_sa.exe แล้ว".to_string()
    } else {
        "ยังไม่มี Explicit Rule สำหรับ gta_sa.exe ใน Firewall (ปกติ Windows Private Network อาจอนุญาตอัตโนมัติ แต่หากหลุดบ่อยแนะนำอนุญาตใน Windows Firewall เอง)".to_string()
    };

    FirewallStatus {
        checked: true,
        gta_sa_allowed: gta_allowed,
        gta_sa_blocked: gta_blocked,
        launcher_allowed,
        total_rules_scanned: rules.len(),
        summary,
    }
}

/// Inspects Windows Firewall application rules strictly READ-ONLY. Never alters OS firewall settings.
pub fn check_firewall_readonly(game_dir: &Path) -> FirewallStatus {
    let pairs = win_ro::reg_enum_string_values(HKEY_LOCAL_MACHINE, FIREWALL_RULES_SUBKEY, false);
    if pairs.is_empty() {
        return FirewallStatus {
            checked: false,
            gta_sa_allowed: false,
            gta_sa_blocked: false,
            launcher_allowed: false,
            total_rules_scanned: 0,
            summary: "ไม่สามารถอ่านสถานะ Firewall Rule ได้ (Read-Only)".into(),
        };
    }
    let rules: Vec<String> = pairs.into_iter().map(|(_, v)| v).collect();
    let gta_exe = game_dir.join("gta_sa.exe");
    let current_exe = std::env::current_exe().ok();
    inspect_firewall_rules_list(&rules, &gta_exe, current_exe.as_deref())
}

pub fn is_alt_tab_nopause_enabled(game_dir: &Path) -> bool {
    let ws_ini = game_dir.join("scripts").join("GTASA.WidescreenFix.ini");
    let Ok(raw) = fs::read_to_string(ws_ini) else {
        return false;
    };
    for line in raw.lines() {
        let t = line.trim();
        if t.starts_with(';') || t.starts_with('#') || t.starts_with('[') {
            continue;
        }
        if let Some((k, v)) = t.split_once('=') {
            if k.trim().eq_ignore_ascii_case("AllowAltTabbingWithoutPausing") {
                let val = v.trim();
                return val == "1" || val.eq_ignore_ascii_case("true");
            }
        }
    }
    false
}

pub fn inspect_net_diagnostics(game_dir: &Path) -> NetDiagnostics {
    let fw = check_firewall_readonly(game_dir);
    let alt_tab = is_alt_tab_nopause_enabled(game_dir);
    let alt_tab_note = if alt_tab {
        "เปิดไม่ค้างตอนพับจอแล้ว (ลดปัญหาหลุดเซิร์ฟเวอร์ & ไมค์ตัดเวลา Alt+Tab)".to_string()
    } else {
        "AllowAltTabbingWithoutPausing=0 — การพับจอ (Alt+Tab) อาจหยุดเฟรมเกมและทำให้ SampVoice ปล่อยคีย์ไมค์/หลุดชั่วคราว".to_string()
    };

    let sv = audio::read_svlog_summary();
    NetDiagnostics {
        firewall: fw,
        alt_tab_nopause: alt_tab,
        alt_tab_note,
        last_voice_server: sv.last_voice_server,
        last_voice_port: sv.last_voice_port,
    }
}

/// Resolves DNS and warms local UDP route / NAT mapping safely using standard SA-MP `'p'` ping.
/// Never blocks longer than ~150 ms so game launch remains instant.
pub fn prewarm_connection(host: &str, game_port: u16, voice_port: Option<u16>) -> PrewarmReport {
    let trimmed = host.trim();
    if trimmed.is_empty() || game_port == 0 {
        return PrewarmReport {
            host: trimmed.to_string(),
            resolved_ip: String::new(),
            game_port,
            voice_port,
            dns_ms: 0,
            game_ping_ms: -1,
            voice_route_warmed: false,
            summary: "ยังไม่ได้ระบุเซิร์ฟเวอร์สำหรับ Pre-warm".into(),
        };
    }

    let t_dns = Instant::now();
    let Some(ip) = resolve_host(trimmed) else {
        return PrewarmReport {
            host: trimmed.to_string(),
            resolved_ip: String::new(),
            game_port,
            voice_port,
            dns_ms: t_dns.elapsed().as_millis() as u32,
            game_ping_ms: -1,
            voice_route_warmed: false,
            summary: format!("Resolve DNS ของ {} ไม่สำเร็จ", trimmed),
        };
    };
    let dns_ms = t_dns.elapsed().as_millis() as u32;

    let mut game_ping_ms = -1;
    if let Ok(sock) = UdpSocket::bind("0.0.0.0:0") {
        let _ = sock.set_read_timeout(Some(Duration::from_millis(120)));
        let _ = sock.set_write_timeout(Some(Duration::from_millis(80)));
        let pkt = build_ping_packet(ip, game_port, *b"WARM");
        let dst = SocketAddr::new(IpAddr::V4(ip), game_port);
        let t_ping = Instant::now();
        if sock.send_to(&pkt, dst).is_ok() {
            let mut rbuf = [0u8; 64];
            if let Ok((n, _)) = sock.recv_from(&mut rbuf) {
                if n >= 11 && &rbuf[..4] == b"SAMP" && rbuf[10] == b'p' {
                    game_ping_ms = t_ping.elapsed().as_millis() as i32;
                }
            }
        }
    }

    // Warm kernel/NAT UDP outbound mapping on voice port with a single empty datagram.
    let mut voice_route_warmed = false;
    let effective_voice_port = voice_port.filter(|&p| p > 0).or_else(|| {
        let sv = audio::read_svlog_summary();
        if sv.last_voice_port > 0
            && (sv.last_voice_server == ip.to_string()
                || sv.last_voice_server.eq_ignore_ascii_case(trimmed))
        {
            Some(sv.last_voice_port)
        } else {
            None
        }
    });
    if let Some(vp) = effective_voice_port {
        if let Ok(vsock) = UdpSocket::bind("0.0.0.0:0") {
            let _ = vsock.set_write_timeout(Some(Duration::from_millis(60)));
            let vdst = SocketAddr::new(IpAddr::V4(ip), vp);
            if vsock.connect(vdst).is_ok() {
                let _ = vsock.send(&[0u8; 1]);
                voice_route_warmed = true;
            }
        }
    }

    let summary = if game_ping_ms >= 0 {
        format!(
            "Pre-warm สำเร็จ ({}:{}) • Ping {} ms{}",
            ip,
            game_port,
            game_ping_ms,
            if voice_route_warmed { " • Voice Route พร้อม" } else { "" }
        )
    } else {
        format!(
            "อุ่นเส้นทาง DNS & UDP แล้ว ({}:{}, {} ms)",
            ip, game_port, dns_ms
        )
    };

    PrewarmReport {
        host: trimmed.to_string(),
        resolved_ip: ip.to_string(),
        game_port,
        voice_port: effective_voice_port,
        dns_ms,
        game_ping_ms,
        voice_route_warmed,
        summary,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    #[test]
    fn test_firewall_parser_detects_allow_and_block() {
        let gta = PathBuf::from(r"C:\Games\GTA\gta_sa.exe");
        let launcher = PathBuf::from(r"C:\Games\GTA\SampLauncher.exe");
        let rules = vec![
            r"v2.33|Action=Allow|Active=TRUE|Dir=In|App=c:\games\gta\gta_sa.exe|Name=GTA|".to_string(),
            r"v2.33|Action=Allow|Active=FALSE|Dir=Out|App=c:\games\gta\samplauncher.exe|".to_string(),
        ];
        let st = inspect_firewall_rules_list(&rules, &gta, Some(&launcher));
        assert!(st.gta_sa_allowed);
        assert!(!st.gta_sa_blocked);
        assert!(!st.launcher_allowed);

        let blocked_rules = vec![
            r"v2.33|Action=Block|Active=TRUE|Dir=Out|App=D:\Other\gta_sa.exe|".to_string(),
        ];
        let st2 = inspect_firewall_rules_list(&blocked_rules, &gta, None);
        assert!(st2.gta_sa_blocked);
    }

    #[test]
    fn test_prewarm_sends_voice_udp_probe() {
        let game_sock = UdpSocket::bind("127.0.0.1:0").unwrap();
        let voice_sock = UdpSocket::bind("127.0.0.1:0").unwrap();
        voice_sock
            .set_read_timeout(Some(Duration::from_millis(500)))
            .unwrap();
        let game_port = game_sock.local_addr().unwrap().port();
        let voice_port = voice_sock.local_addr().unwrap().port();

        let rep = prewarm_connection("127.0.0.1", game_port, Some(voice_port));
        assert!(rep.voice_route_warmed);
        assert_eq!(rep.voice_port, Some(voice_port));

        let mut buf = [0u8; 8];
        let (n, _) = voice_sock.recv_from(&mut buf).expect("voice UDP probe packet");
        assert_eq!(n, 1);
    }
}
