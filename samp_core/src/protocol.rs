use encoding_rs::WINDOWS_874;
use serde::{Deserialize, Serialize};
use std::net::Ipv4Addr;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct QueryInfo {
    pub online: bool,
    pub name: String,
    pub players: i32,
    pub max: i32,
    pub ping: i32,
    pub mode: String,
    pub language: String,
    pub is_open_mp: bool,
    #[serde(default)]
    pub password_locked: bool,
}

impl Default for QueryInfo {
    fn default() -> Self {
        Self {
            online: false,
            name: String::new(),
            players: -1,
            max: 0,
            ping: -1,
            mode: String::new(),
            language: String::new(),
            is_open_mp: false,
            password_locked: false,
        }
    }
}

pub fn decode_cp874(bytes: &[u8]) -> String {
    let (cow, _, _) = WINDOWS_874.decode(bytes);
    cow.into_owned()
}

pub fn encode_cp874(s: &str) -> Vec<u8> {
    let (cow, _, _) = WINDOWS_874.encode(s);
    cow.into_owned()
}

pub fn build_query_packet(ip: Ipv4Addr, port: u16, opcode: u8) -> [u8; 11] {
    let octets = ip.octets();
    [
        b'S',
        b'A',
        b'M',
        b'P',
        octets[0],
        octets[1],
        octets[2],
        octets[3],
        (port & 0xFF) as u8,
        ((port >> 8) & 0xFF) as u8,
        opcode,
    ]
}

pub fn build_ping_packet(ip: Ipv4Addr, port: u16, cookie: [u8; 4]) -> [u8; 15] {
    let q = build_query_packet(ip, port, b'p');
    [
        q[0], q[1], q[2], q[3], q[4], q[5], q[6], q[7], q[8], q[9], q[10], cookie[0], cookie[1],
        cookie[2], cookie[3],
    ]
}

fn le16(b: &[u8], off: usize) -> Option<u16> {
    if off + 2 > b.len() {
        None
    } else {
        Some(u16::from_le_bytes([b[off], b[off + 1]]))
    }
}

fn le32(b: &[u8], off: usize) -> Option<i32> {
    if off + 4 > b.len() {
        None
    } else {
        Some(i32::from_le_bytes([
            b[off],
            b[off + 1],
            b[off + 2],
            b[off + 3],
        ]))
    }
}

fn read_str32(b: &[u8], off: &mut usize) -> String {
    let Some(len_i32) = le32(b, *off) else {
        *off = b.len();
        return String::new();
    };
    *off += 4;
    if len_i32 <= 0 {
        return String::new();
    }
    let len = len_i32 as usize;
    if *off + len > b.len() {
        *off = b.len();
        return String::new();
    }
    let s = decode_cp874(&b[*off..*off + len]);
    *off += len;
    s
}

/// Parses SA-MP 'i' (105) info response packet.
/// Format: "SAMP" (4) + ip (4) + port (2) + 'i' (1) + password (1) + players (2) + max (2)
///       + hostname_len (4) + hostname + gamemode_len (4) + gamemode + language_len (4) + language
pub fn parse_info_packet(buf: &[u8], ping_ms: i32) -> Option<QueryInfo> {
    if buf.len() < 28 || &buf[0..4] != b"SAMP" || buf[10] != b'i' {
        return None;
    }
    let password_locked = buf[11] != 0;
    let players = le16(buf, 12)? as i32;
    let max = le16(buf, 14)? as i32;
    let mut off = 16usize;
    let name = read_str32(buf, &mut off);
    let mode = read_str32(buf, &mut off);
    let language = read_str32(buf, &mut off);
    Some(QueryInfo {
        online: true,
        name,
        players,
        max,
        ping: ping_ms,
        mode,
        language,
        is_open_mp: false,
        password_locked,
    })
}

/// Parses SA-MP 'r' (114) rules response packet into [key, value] pairs.
pub fn parse_rules_packet(buf: &[u8]) -> Option<Vec<[String; 2]>> {
    if buf.len() < 13 || &buf[0..4] != b"SAMP" || buf[10] != b'r' {
        return None;
    }
    let count = le16(buf, 11)? as usize;
    let mut off = 13usize;
    let mut out = Vec::with_capacity(count.min(64));
    for _ in 0..count {
        if off >= buf.len() {
            break;
        }
        let klen = buf[off] as usize;
        off += 1;
        if off + klen >= buf.len() {
            break;
        }
        let key = decode_cp874(&buf[off..off + klen]);
        off += klen;
        if off >= buf.len() {
            break;
        }
        let vlen = buf[off] as usize;
        off += 1;
        if off + vlen > buf.len() {
            break;
        }
        let val = decode_cp874(&buf[off..off + vlen]);
        off += vlen;
        out.push([key, val]);
    }
    Some(out)
}

/// Parses SA-MP 'd' (100) detailed player list: [id, name, score, ping].
pub fn parse_players_detailed(buf: &[u8]) -> Option<Vec<[String; 4]>> {
    if buf.len() < 13 || &buf[0..4] != b"SAMP" || buf[10] != b'd' {
        return None;
    }
    let count = le16(buf, 11)? as usize;
    if count == 0 {
        return Some(Vec::new());
    }
    let mut off = 13usize;
    let mut out = Vec::with_capacity(count.min(256));
    for _ in 0..count {
        if off + 2 > buf.len() {
            break;
        }
        let pid = buf[off];
        off += 1;
        let nlen = buf[off] as usize;
        off += 1;
        if off + nlen + 8 > buf.len() {
            break;
        }
        let name = decode_cp874(&buf[off..off + nlen]);
        off += nlen;
        let score = le32(buf, off)?;
        off += 4;
        let ping = le32(buf, off)?;
        off += 4;
        out.push([
            pid.to_string(),
            name,
            score.to_string(),
            ping.to_string(),
        ]);
    }
    if out.is_empty() {
        None
    } else {
        Some(out)
    }
}

/// Parses SA-MP 'c' (99) basic player list: ["-", name, score, "?"].
pub fn parse_players_basic(buf: &[u8]) -> Option<Vec<[String; 4]>> {
    if buf.len() < 13 || &buf[0..4] != b"SAMP" || buf[10] != b'c' {
        return None;
    }
    let count = le16(buf, 11)? as usize;
    let mut off = 13usize;
    let mut out = Vec::with_capacity(count.min(256));
    for _ in 0..count {
        if off + 5 > buf.len() {
            break;
        }
        let nlen = buf[off] as usize;
        off += 1;
        if off + nlen + 4 > buf.len() {
            break;
        }
        let name = decode_cp874(&buf[off..off + nlen]);
        off += nlen;
        let score = le32(buf, off)?;
        off += 4;
        out.push([
            "-".to_string(),
            name,
            score.to_string(),
            "?".to_string(),
        ]);
    }
    Some(out)
}

pub fn has_open_mp_rule(rules: &[[String; 2]]) -> bool {
    rules
        .iter()
        .any(|[k, v]| k == "version" && !v.is_empty() && !v.starts_with("0.3"))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_build_query_packet() {
        let pkt = build_query_packet(Ipv4Addr::new(127, 0, 0, 1), 7777, b'i');
        assert_eq!(&pkt[0..4], b"SAMP");
        assert_eq!(&pkt[4..8], &[127, 0, 0, 1]);
        assert_eq!(u16::from_le_bytes([pkt[8], pkt[9]]), 7777);
        assert_eq!(pkt[10], b'i');
    }

    #[test]
    fn test_parse_info_packet_thai_cp874() {
        let mut pkt = Vec::new();
        pkt.extend_from_slice(b"SAMP");
        pkt.extend_from_slice(&[127, 0, 0, 1]);
        pkt.extend_from_slice(&7777u16.to_le_bytes());
        pkt.push(b'i');
        pkt.push(0); // password = 0
        pkt.extend_from_slice(&42u16.to_le_bytes());
        pkt.extend_from_slice(&500u16.to_le_bytes());
        let name_bytes = encode_cp874("เซิร์ฟไทย Test");
        pkt.extend_from_slice(&(name_bytes.len() as u32).to_le_bytes());
        pkt.extend_from_slice(&name_bytes);
        let mode_bytes = encode_cp874("Roleplay");
        pkt.extend_from_slice(&(mode_bytes.len() as u32).to_le_bytes());
        pkt.extend_from_slice(&mode_bytes);

        let info = parse_info_packet(&pkt, 23).expect("should parse");
        assert!(info.online);
        assert_eq!(info.players, 42);
        assert_eq!(info.max, 500);
        assert_eq!(info.ping, 23);
        assert_eq!(info.name, "เซิร์ฟไทย Test");
        assert_eq!(info.mode, "Roleplay");
        assert_eq!(info.language, "");

        let lang_bytes = encode_cp874("ไทย / English");
        let mut full_pkt = pkt.clone();
        full_pkt.extend_from_slice(&(lang_bytes.len() as u32).to_le_bytes());
        full_pkt.extend_from_slice(&lang_bytes);
        let full_info = parse_info_packet(&full_pkt, 19).expect("should parse with language");
        assert_eq!(full_info.language, "ไทย / English");
        assert_eq!(full_info.ping, 19);
    }

    #[test]
    fn test_parse_rules_and_open_mp_detection() {
        let mut pkt = Vec::new();
        pkt.extend_from_slice(b"SAMP");
        pkt.extend_from_slice(&[127, 0, 0, 1]);
        pkt.extend_from_slice(&7777u16.to_le_bytes());
        pkt.push(b'r');
        pkt.extend_from_slice(&2u16.to_le_bytes());
        pkt.push(7);
        pkt.extend_from_slice(b"version");
        pkt.push(5);
        pkt.extend_from_slice(b"1.4.0");
        pkt.push(3);
        pkt.extend_from_slice(b"map");
        pkt.push(11);
        pkt.extend_from_slice(b"San Andreas");

        let rules = parse_rules_packet(&pkt).expect("rules");
        assert_eq!(rules.len(), 2);
        assert_eq!(rules[0], ["version", "1.4.0"]);
        assert!(has_open_mp_rule(&rules));
    }

    #[test]
    fn test_parse_players_detailed_and_basic() {
        let mut pkt = Vec::new();
        pkt.extend_from_slice(b"SAMP");
        pkt.extend_from_slice(&[127, 0, 0, 1]);
        pkt.extend_from_slice(&7777u16.to_le_bytes());
        pkt.push(b'd');
        pkt.extend_from_slice(&1u16.to_le_bytes());
        pkt.push(7);
        pkt.push(4);
        pkt.extend_from_slice(b"SoDa");
        pkt.extend_from_slice(&150i32.to_le_bytes());
        pkt.extend_from_slice(&35i32.to_le_bytes());

        let pl = parse_players_detailed(&pkt).expect("detailed players");
        assert_eq!(pl.len(), 1);
        assert_eq!(pl[0], ["7", "SoDa", "150", "35"]);
    }

    #[test]
    fn test_truncated_packet_safe() {
        let short = b"SAMP\x7f\x00\x00\x01\x61\x1ei\x00";
        assert!(parse_info_packet(short, 10).is_none());
        let short_27 = vec![b'S', b'A', b'M', b'P', 127, 0, 0, 1, 0x61, 0x1e, b'i', 1];
        let mut p27 = short_27;
        p27.resize(27, 0);
        assert!(parse_info_packet(&p27, 10).is_none());
        let mut p28 = p27;
        p28.push(0);
        let parsed = parse_info_packet(&p28, 12).expect("28-byte minimal packet valid");
        assert!(parsed.password_locked);
    }
}
