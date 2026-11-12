use serde::Serialize;
use std::collections::HashMap;
use std::fs::{self, File};
use std::io::Read;
use std::path::Path;

#[derive(Debug, Clone, Serialize)]
pub struct AssetIssue {
    pub severity: String, // "error" | "warn"
    pub kind: String,     // "dff" | "txd" | "img" | "dup"
    pub rel_path: String,
    pub message: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct AssetScanReport {
    pub scanned_files: usize,
    pub dff_count: usize,
    pub txd_count: usize,
    pub img_count: usize,
    pub texture_count: usize,
    pub error_count: usize,
    pub warn_count: usize,
    pub duplicate_count: usize,
    pub issues: Vec<AssetIssue>,
}

fn le_u16(b: &[u8], off: usize) -> Option<u16> {
    b.get(off..off + 2).map(|s| u16::from_le_bytes([s[0], s[1]]))
}

fn le_u32(b: &[u8], off: usize) -> Option<u32> {
    b.get(off..off + 4)
        .map(|s| u32::from_le_bytes([s[0], s[1], s[2], s[3]]))
}

fn cstr_trim(b: &[u8]) -> String {
    let end = b.iter().position(|&c| c == 0).unwrap_or(b.len());
    String::from_utf8_lossy(&b[..end]).trim().to_string()
}

pub fn validate_dff_bytes(data: &[u8]) -> Result<(), String> {
    if data.len() < 12 {
        return Err("ไฟล์ DFF เล็กเกินไป (< 12 bytes)".into());
    }
    let chunk_type = le_u32(data, 0).unwrap_or(0);
    let chunk_len = le_u32(data, 4).unwrap_or(0) as usize;
    if chunk_type != 0x0010 {
        return Err(format!(
            "Header ไม่ใช่ RenderWare Clump (พบ 0x{:04X} แทน 0x0010)",
            chunk_type
        ));
    }
    if 12 + chunk_len > data.len() {
        return Err(format!(
            "ไฟล์ DFF ไม่สมบูรณ์/ถูกตัดขาด (ต้องการ {} bytes แต่ไฟล์มี {} bytes)",
            12 + chunk_len,
            data.len()
        ));
    }
    Ok(())
}

pub struct TxdInspectResult {
    pub texture_count: usize,
    pub warnings: Vec<String>,
}

pub fn validate_txd_bytes(data: &[u8]) -> Result<TxdInspectResult, String> {
    if data.len() < 12 {
        return Err("ไฟล์ TXD เล็กเกินไป (< 12 bytes)".into());
    }
    let chunk_type = le_u32(data, 0).unwrap_or(0);
    let chunk_len = le_u32(data, 4).unwrap_or(0) as usize;
    if chunk_type != 0x0016 {
        return Err(format!(
            "Header ไม่ใช่ RenderWare TXD (พบ 0x{:04X} แทน 0x0016)",
            chunk_type
        ));
    }
    let end = 12 + chunk_len;
    if end > data.len() {
        return Err(format!(
            "ไฟล์ TXD ไม่สมบูรณ์/ถูกตัดขาด (ต้องการ {} bytes แต่ไฟล์มี {} bytes)",
            end,
            data.len()
        ));
    }

    let mut pos = 12usize;
    let mut parsed_tex = 0usize;
    let mut warnings = Vec::new();

    while pos + 12 <= end {
        let ctype = le_u32(data, pos).unwrap_or(0);
        let clen = le_u32(data, pos + 4).unwrap_or(0) as usize;
        let cend = pos + 12 + clen;
        if cend > end {
            return Err(format!(
                "Chunk ย่อยภายใน TXD เสียหาย (0x{:04X} เกินขอบเขตไฟล์)",
                ctype
            ));
        }
        if ctype == 0x0015 {
            // rwID_TEXTURENATIVE: first inner chunk is usually 0x0001 (STRUCT)
            parsed_tex += 1;
            let mut inner = pos + 12;
            while inner + 12 <= cend {
                let itype = le_u32(data, inner).unwrap_or(0);
                let ilen = le_u32(data, inner + 4).unwrap_or(0) as usize;
                let iend = inner + 12 + ilen;
                if iend > cend {
                    break;
                }
                if itype == 0x0001 && ilen >= 88 {
                    let s = &data[inner + 12..iend];
                    let platform = le_u32(s, 0).unwrap_or(0);
                    if platform == 8 || platform == 9 {
                        let tex_name = cstr_trim(&s[8..40]);
                        let d3d_or_alpha = le_u32(s, 76).unwrap_or(0);
                        let width = le_u16(s, 80).unwrap_or(0);
                        let height = le_u16(s, 82).unwrap_or(0);
                        let comp = s[87];
                        let is_dxt = (d3d_or_alpha == 0x31545844
                            || d3d_or_alpha == 0x33545844
                            || d3d_or_alpha == 0x35545844)
                            || (platform == 8 && comp != 0)
                            || (platform == 9 && (comp & 0x08) != 0);

                        if width >= 2048 || height >= 2048 {
                            warnings.push(format!(
                                "เท็กซ์เจอร์ '{}' ขนาด {}x{} ใหญ่มาก (เสี่ยงกิน RAM/VRAM จนเกมเด้ง)",
                                if tex_name.is_empty() { "?" } else { &tex_name },
                                width,
                                height
                            ));
                        } else if !is_dxt && (width >= 1024 || height >= 1024) {
                            warnings.push(format!(
                                "เท็กซ์เจอร์ '{}' ขนาด {}x{} ไม่ได้บีบอัด DXT (กินแรมสูง)",
                                if tex_name.is_empty() { "?" } else { &tex_name },
                                width,
                                height
                            ));
                        }
                    }
                    break;
                }
                inner = iend;
            }
        }
        pos = cend;
    }

    Ok(TxdInspectResult {
        texture_count: parsed_tex,
        warnings,
    })
}

pub fn validate_img_file(path: &Path) -> Result<usize, String> {
    let mut f = File::open(path).map_err(|e| format!("เปิดไฟล์ไม่ได้: {}", e))?;
    let file_len = f.metadata().map(|m| m.len()).unwrap_or(0);
    if file_len < 8 {
        return Err("ไฟล์ IMG เล็กเกินไป (< 8 bytes)".into());
    }
    let mut hdr = [0u8; 8];
    f.read_exact(&mut hdr)
        .map_err(|_| "อ่าน Header IMG ไม่สำเร็จ".to_string())?;
    if &hdr[0..4] != b"VER2" {
        return Err("ไม่ใช่ IMG VER2 (GTA San Andreas standard)".into());
    }
    let entry_count = u32::from_le_bytes([hdr[4], hdr[5], hdr[6], hdr[7]]) as usize;
    if entry_count > 200_000 {
        return Err(format!("จำนวน Entry ใน IMG ผิดปกติ ({})", entry_count));
    }
    let dir_bytes = (entry_count as u64) * 32;
    if 8 + dir_bytes > file_len {
        return Err("ตารางสารบัญ IMG ถูกตัดขาดไม่สมบูรณ์".into());
    }
    let mut dir = vec![0u8; dir_bytes as usize];
    f.read_exact(&mut dir)
        .map_err(|_| "อ่านสารบัญ IMG ไม่สำเร็จ".to_string())?;
    for i in 0..entry_count {
        let e = &dir[i * 32..(i + 1) * 32];
        let off_sec = u32::from_le_bytes([e[0], e[1], e[2], e[3]]) as u64;
        let sz_sec = u16::from_le_bytes([e[4], e[5]]) as u64;
        let end_byte = (off_sec + sz_sec) * 2048;
        if end_byte > file_len {
            let name = cstr_trim(&e[8..32]);
            return Err(format!(
                "Entry '{}' ชี้เกินขนาดไฟล์ IMG (ไฟล์เสียหาย/โหลดไม่ครบ)",
                name
            ));
        }
    }
    Ok(entry_count)
}

fn should_skip_dup_check(lower_name: &str) -> bool {
    lower_name == "thumbs.db"
        || lower_name.starts_with("readme")
        || lower_name.ends_with(".txt")
        || lower_name.ends_with(".md")
        || lower_name.ends_with(".ini")
        || lower_name.ends_with(".cfg")
        || lower_name.ends_with(".log")
}

fn collect_mod_files(
    root: &Path,
    dir: &Path,
    depth: usize,
    in_modloader: bool,
    out: &mut Vec<(std::path::PathBuf, bool)>,
) {
    if depth == 0 || !dir.is_dir() {
        return;
    }
    let Ok(rd) = fs::read_dir(dir) else {
        return;
    };
    let mut paths: Vec<std::path::PathBuf> = rd.flatten().map(|e| e.path()).collect();
    paths.sort();
    for p in paths {
        let name = p
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or_default();
        if in_modloader && (name.starts_with('.') || name.eq_ignore_ascii_case("_ignore")) {
            continue;
        }
        if p.is_dir() {
            collect_mod_files(root, &p, depth - 1, in_modloader, out);
        } else {
            out.push((p, in_modloader));
        }
    }
}

pub fn scan_game_assets(game_dir: &Path) -> AssetScanReport {
    let mut files = Vec::new();
    collect_mod_files(game_dir, &game_dir.join("modloader"), 6, true, &mut files);
    collect_mod_files(game_dir, &game_dir.join("SAMP"), 2, false, &mut files);

    let mut report = AssetScanReport {
        scanned_files: 0,
        dff_count: 0,
        txd_count: 0,
        img_count: 0,
        texture_count: 0,
        error_count: 0,
        warn_count: 0,
        duplicate_count: 0,
        issues: Vec::new(),
    };

    let mut modloader_names: HashMap<String, Vec<String>> = HashMap::new();

    for (path, is_modloader) in files {
        let rel = path
            .strip_prefix(game_dir)
            .unwrap_or(&path)
            .to_string_lossy()
            .replace('/', "\\");
        let file_name = path
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or_default()
            .to_ascii_lowercase();
        let ext = path
            .extension()
            .and_then(|e| e.to_str())
            .map(|s| s.to_ascii_lowercase())
            .unwrap_or_default();

        if is_modloader && !should_skip_dup_check(&file_name) {
            modloader_names.entry(file_name).or_default().push(rel.clone());
        }

        match ext.as_str() {
            "dff" => {
                report.scanned_files += 1;
                report.dff_count += 1;
                if let Ok(bytes) = fs::read(&path) {
                    if let Err(msg) = validate_dff_bytes(&bytes) {
                        report.error_count += 1;
                        if report.issues.len() < 150 {
                            report.issues.push(AssetIssue {
                                severity: "error".into(),
                                kind: "dff".into(),
                                rel_path: rel,
                                message: msg,
                            });
                        }
                    }
                }
            }
            "txd" => {
                report.scanned_files += 1;
                report.txd_count += 1;
                if let Ok(bytes) = fs::read(&path) {
                    match validate_txd_bytes(&bytes) {
                        Ok(res) => {
                            report.texture_count += res.texture_count;
                            for w in res.warnings {
                                report.warn_count += 1;
                                if report.issues.len() < 150 {
                                    report.issues.push(AssetIssue {
                                        severity: "warn".into(),
                                        kind: "txd".into(),
                                        rel_path: rel.clone(),
                                        message: w,
                                    });
                                }
                            }
                        }
                        Err(msg) => {
                            report.error_count += 1;
                            if report.issues.len() < 150 {
                                report.issues.push(AssetIssue {
                                    severity: "error".into(),
                                    kind: "txd".into(),
                                    rel_path: rel,
                                    message: msg,
                                });
                            }
                        }
                    }
                }
            }
            "img" => {
                report.scanned_files += 1;
                report.img_count += 1;
                if let Err(msg) = validate_img_file(&path) {
                    report.error_count += 1;
                    if report.issues.len() < 150 {
                        report.issues.push(AssetIssue {
                            severity: "error".into(),
                            kind: "img".into(),
                            rel_path: rel,
                            message: msg,
                        });
                    }
                }
            }
            _ => {}
        }
    }

    let mut dup_keys: Vec<String> = modloader_names
        .iter()
        .filter(|(_, v)| v.len() > 1)
        .map(|(k, _)| k.clone())
        .collect();
    dup_keys.sort();
    for k in dup_keys {
        if let Some(paths) = modloader_names.get(&k) {
            report.duplicate_count += 1;
            report.warn_count += 1;
            if report.issues.len() < 150 {
                report.issues.push(AssetIssue {
                    severity: "warn".into(),
                    kind: "dup".into(),
                    rel_path: k,
                    message: format!("ไฟล์ชื่อซ้ำทับกัน {} จุด: {}", paths.len(), paths.join(" <-> ")),
                });
            }
        }
    }

    report
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn rw_hdr(ctype: u32, clen: u32) -> [u8; 12] {
        let mut out = [0u8; 12];
        out[0..4].copy_from_slice(&ctype.to_le_bytes());
        out[4..8].copy_from_slice(&clen.to_le_bytes());
        out[8..12].copy_from_slice(&0x1803FFFFu32.to_le_bytes());
        out
    }

    #[test]
    fn test_validate_dff_valid_and_broken() {
        let mut ok_dff = Vec::new();
        ok_dff.extend_from_slice(&rw_hdr(0x0010, 4));
        ok_dff.extend_from_slice(&[1, 2, 3, 4]);
        assert!(validate_dff_bytes(&ok_dff).is_ok());

        let mut bad_hdr = ok_dff.clone();
        bad_hdr[0] = 0x99;
        assert!(validate_dff_bytes(&bad_hdr).is_err());

        let mut truncated = Vec::new();
        truncated.extend_from_slice(&rw_hdr(0x0010, 100));
        truncated.extend_from_slice(&[0; 10]);
        assert!(validate_dff_bytes(&truncated).is_err());
    }

    #[test]
    fn test_validate_txd_detects_oversized_and_broken() {
        // Build TXD -> TEXTURENATIVE -> STRUCT (88+ bytes)
        let mut nat_struct = vec![0u8; 92];
        nat_struct[0..4].copy_from_slice(&9u32.to_le_bytes()); // D3D9
        nat_struct[8..15].copy_from_slice(b"bigroad");
        nat_struct[76..80].copy_from_slice(&0x31545844u32.to_le_bytes()); // DXT1
        nat_struct[80..82].copy_from_slice(&4096u16.to_le_bytes());
        nat_struct[82..84].copy_from_slice(&4096u16.to_le_bytes());

        let mut tex_nat = Vec::new();
        tex_nat.extend_from_slice(&rw_hdr(0x0001, nat_struct.len() as u32));
        tex_nat.extend_from_slice(&nat_struct);

        let mut txd_body = Vec::new();
        txd_body.extend_from_slice(&rw_hdr(0x0001, 4));
        txd_body.extend_from_slice(&[1, 0, 0, 0]);
        txd_body.extend_from_slice(&rw_hdr(0x0015, tex_nat.len() as u32));
        txd_body.extend_from_slice(&tex_nat);

        let mut txd = Vec::new();
        txd.extend_from_slice(&rw_hdr(0x0016, txd_body.len() as u32));
        txd.extend_from_slice(&txd_body);

        let res = validate_txd_bytes(&txd).unwrap();
        assert_eq!(res.texture_count, 1);
        assert_eq!(res.warnings.len(), 1);
        assert!(res.warnings[0].contains("4096x4096"));

        let broken = &txd[..txd.len() - 10];
        assert!(validate_txd_bytes(broken).is_err());
    }

    #[test]
    fn test_scan_game_assets_detects_img_and_duplicates() {
        let nanos = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_assets_{}", nanos));
        fs::create_dir_all(dir.join("modloader").join("PackA")).unwrap();
        fs::create_dir_all(dir.join("modloader").join("PackB")).unwrap();

        let mut dff = Vec::new();
        dff.extend_from_slice(&rw_hdr(0x0010, 4));
        dff.extend_from_slice(&[0; 4]);
        fs::write(dir.join("modloader").join("PackA").join("infernus.dff"), &dff).unwrap();
        fs::write(dir.join("modloader").join("PackB").join("infernus.dff"), &dff).unwrap();

        // Broken IMG VER2 pointing past EOF
        let mut bad_img = Vec::new();
        bad_img.extend_from_slice(b"VER2");
        bad_img.extend_from_slice(&1u32.to_le_bytes());
        let mut entry = [0u8; 32];
        entry[0..4].copy_from_slice(&99u32.to_le_bytes());
        entry[4..6].copy_from_slice(&10u16.to_le_bytes());
        entry[8..16].copy_from_slice(b"test.dff");
        bad_img.extend_from_slice(&entry);
        fs::write(dir.join("modloader").join("PackA").join("broken.img"), &bad_img).unwrap();

        let rep = scan_game_assets(&dir);
        assert_eq!(rep.dff_count, 2);
        assert_eq!(rep.img_count, 1);
        assert_eq!(rep.duplicate_count, 1);
        assert_eq!(rep.error_count, 1);
        assert_eq!(rep.warn_count, 1);

        let _ = fs::remove_dir_all(&dir);
    }
}
