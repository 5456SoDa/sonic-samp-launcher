use crate::protocol::{decode_cp874, encode_cp874};
use crate::query::resolve_host;
use std::net::{IpAddr, Ipv4Addr, SocketAddr, UdpSocket};
use std::time::Duration;

const RCON_OPCODE: u8 = b'x';

/// Builds a SA-MP UDP RCON ('x') command packet with CP874-encoded command text.
pub fn build_rcon_packet(ip: Ipv4Addr, port: u16, pass: &str, cmd: &str) -> Vec<u8> {
    let octets = ip.octets();
    let pass_bytes = encode_cp874(pass);
    let cmd_bytes = encode_cp874(cmd);
    let pass_len = pass_bytes.len().min(u16::MAX as usize);
    let cmd_len = cmd_bytes.len().min(u16::MAX as usize);

    let mut pkt = Vec::with_capacity(15 + pass_len + cmd_len);
    pkt.extend_from_slice(b"SAMP");
    pkt.extend_from_slice(&octets);
    pkt.extend_from_slice(&port.to_le_bytes());
    pkt.push(RCON_OPCODE);
    pkt.extend_from_slice(&(pass_len as u16).to_le_bytes());
    pkt.extend_from_slice(&pass_bytes[..pass_len]);
    pkt.extend_from_slice(&(cmd_len as u16).to_le_bytes());
    pkt.extend_from_slice(&cmd_bytes[..cmd_len]);
    pkt
}

/// Parses a single SA-MP UDP RCON ('x') response line packet.
/// Layout: "SAMP"(4) + ip(4) + port(2) + 'x'(1) + len(u16 LE at 11..13) + payload(13..)
pub fn parse_rcon_packet(buf: &[u8]) -> Option<String> {
    if buf.len() < 13 || &buf[0..4] != b"SAMP" || buf[10] != RCON_OPCODE {
        return None;
    }
    let declared = u16::from_le_bytes([buf[11], buf[12]]) as usize;
    let avail = buf.len() - 13;
    let len = declared.min(avail);
    if len == 0 {
        return Some(String::new());
    }
    Some(decode_cp874(&buf[13..13 + len]))
}

pub fn rcon_send_to_addr(addr: SocketAddr, pass: &str, cmd: &str) -> Result<String, String> {
    let ip_v4 = match addr.ip() {
        IpAddr::V4(v4) => v4,
        IpAddr::V6(_) => return Err("ไม่รองรับ IPv6 สำหรับ SA-MP RCON".into()),
    };
    let sock = UdpSocket::bind("0.0.0.0:0").map_err(|e| format!("ERR: bind: {}", e))?;
    sock.set_write_timeout(Some(Duration::from_millis(2500)))
        .map_err(|e| format!("ERR: timeout: {}", e))?;
    sock.set_read_timeout(Some(Duration::from_millis(2500)))
        .map_err(|e| format!("ERR: timeout: {}", e))?;
    sock.connect(addr)
        .map_err(|e| format!("ERR: connect: {}", e))?;

    let pkt = build_rcon_packet(ip_v4, addr.port(), pass, cmd);
    sock.send(&pkt).map_err(|e| format!("ERR: send: {}", e))?;

    let mut lines = Vec::new();
    let mut total_chars = 0usize;
    let mut buf = [0u8; 4096];

    for i in 0..64 {
        if i == 1 {
            // Subsequent multi-packet lines arrive quickly; avoid waiting a full 2.5s tail.
            let _ = sock.set_read_timeout(Some(Duration::from_millis(250)));
        }
        match sock.recv(&mut buf) {
            Ok(n) => {
                if let Some(line) = parse_rcon_packet(&buf[..n]) {
                    if !line.is_empty() {
                        total_chars += line.len();
                        lines.push(line);
                    }
                }
            }
            Err(_) => break,
        }
        if total_chars > 16384 {
            break;
        }
    }

    Ok(lines.join("\r\n"))
}

/// Sends an RCON command to `host:port` and returns collected response lines or an error message.
pub fn rcon_send_sync(host: &str, port: u16, pass: &str, cmd: &str) -> String {
    let Some(ip) = resolve_host(host) else {
        return "(แก้ hostname ไม่สำเร็จ)".into();
    };
    match rcon_send_to_addr(SocketAddr::new(IpAddr::V4(ip), port), pass, cmd) {
        Ok(out) => out,
        Err(e) => e,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn build_rcon_reply(ip: Ipv4Addr, port: u16, msg: &str) -> Vec<u8> {
        let mut p = Vec::new();
        p.extend_from_slice(b"SAMP");
        p.extend_from_slice(&ip.octets());
        p.extend_from_slice(&port.to_le_bytes());
        p.push(b'x');
        let bytes = encode_cp874(msg);
        p.extend_from_slice(&(bytes.len() as u16).to_le_bytes());
        p.extend_from_slice(&bytes);
        p
    }

    #[test]
    fn test_build_rcon_packet() {
        let pkt = build_rcon_packet(Ipv4Addr::new(127, 0, 0, 1), 7777, "secret", "echo hi");
        assert_eq!(&pkt[0..4], b"SAMP");
        assert_eq!(&pkt[4..8], &[127, 0, 0, 1]);
        assert_eq!(u16::from_le_bytes([pkt[8], pkt[9]]), 7777);
        assert_eq!(pkt[10], b'x');
        let plen = u16::from_le_bytes([pkt[11], pkt[12]]) as usize;
        assert_eq!(plen, 6);
        assert_eq!(&pkt[13..19], b"secret");
        let clen = u16::from_le_bytes([pkt[19], pkt[20]]) as usize;
        assert_eq!(clen, 7);
        assert_eq!(&pkt[21..28], b"echo hi");
    }

    #[test]
    fn test_parse_rcon_packet_cp874() {
        let reply = build_rcon_reply(Ipv4Addr::new(127, 0, 0, 1), 7777, "ทดสอบ RCON ผ่าน");
        let parsed = parse_rcon_packet(&reply).expect("valid rcon packet");
        assert_eq!(parsed, "ทดสอบ RCON ผ่าน");
    }

    #[test]
    fn test_parse_rcon_invalid_packet() {
        assert!(parse_rcon_packet(b"SAMP\x7f\x00\x00\x01").is_none());
        let mut wrong_op = build_rcon_reply(Ipv4Addr::new(127, 0, 0, 1), 7777, "x");
        wrong_op[10] = b'i';
        assert!(parse_rcon_packet(&wrong_op).is_none());
    }

    #[test]
    fn test_rcon_send_loopback_multipacket() {
        let srv = UdpSocket::bind("127.0.0.1:0").expect("bind loopback");
        let addr = srv.local_addr().unwrap();
        let port = addr.port();
        let handle = std::thread::spawn(move || {
            let mut buf = [0u8; 512];
            let (n, peer) = srv.recv_from(&mut buf).expect("recv");
            assert!(n > 15 && buf[10] == b'x');
            let r1 = build_rcon_reply(Ipv4Addr::new(127, 0, 0, 1), port, "Line 1: OK");
            let r2 = build_rcon_reply(Ipv4Addr::new(127, 0, 0, 1), port, "Line 2: ไทย");
            srv.send_to(&r1, peer).unwrap();
            srv.send_to(&r2, peer).unwrap();
        });

        let res = rcon_send_to_addr(addr, "pw", "varlist").expect("rcon ok");
        handle.join().unwrap();
        assert_eq!(res, "Line 1: OK\r\nLine 2: ไทย");
    }
}
