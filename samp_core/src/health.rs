use serde::Serialize;
use sha2::{Digest, Sha256};
use std::collections::{HashMap, HashSet};
use std::fs::{self, File};
use std::io::Read;
use std::path::{Path, PathBuf};

#[derive(Debug, Clone, Serialize)]
pub struct HealthReport {
    pub gta_status: String,
    pub gta_msg: String,
    pub samp_status: String,
    pub samp_msg: String,
    pub mods_status: String,
    pub mods_msg: String,
    pub env_status: String,
    pub env_msg: String,
    pub gfx_status: String,
    pub gfx_msg: String,
    pub duplicate_asis: Vec<String>,
    pub readonly_files: Vec<String>,
    pub in_program_files: bool,
    pub missing_dx9: bool,
    pub fixable_count: usize,
}

#[derive(Debug, Clone, Serialize)]
pub struct AutoFixResult {
    pub fixed_readonly: usize,
    pub disabled_duplicates: Vec<String>,
    pub remaining_warnings: Vec<String>,
}

pub fn sha256_bytes(data: &[u8]) -> String {
    let mut hasher = Sha256::new();
    hasher.update(data);
    let digest = hasher.finalize();
    let mut hex = String::with_capacity(digest.len() * 2);
    for b in digest {
        use std::fmt::Write;
        let _ = write!(&mut hex, "{:02x}", b);
    }
    hex
}

pub fn sha256_file(path: &Path) -> std::io::Result<String> {
    let mut file = File::open(path)?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 65536];
    loop {
        let n = file.read(&mut buf)?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    let digest = hasher.finalize();
    let mut hex = String::with_capacity(digest.len() * 2);
    for b in digest {
        use std::fmt::Write;
        let _ = write!(&mut hex, "{:02x}", b);
    }
    Ok(hex)
}

fn collect_files_limited(dir: &Path, max_depth: usize, out: &mut Vec<PathBuf>) {
    if max_depth == 0 || !dir.is_dir() {
        return;
    }
    let Ok(entries) = fs::read_dir(dir) else {
        return;
    };
    let mut sorted: Vec<PathBuf> = entries.flatten().map(|e| e.path()).collect();
    sorted.sort();
    for p in sorted {
        if p.is_dir() {
            if max_depth > 1 {
                collect_files_limited(&p, max_depth - 1, out);
            }
        } else {
            out.push(p);
        }
    }
}

fn rel_str(root: &Path, full: &Path) -> String {
    full.strip_prefix(root)
        .unwrap_or(full)
        .to_string_lossy()
        .replace('/', "\\")
}

fn detect_dx9_missing() -> bool {
    let windir = std::env::var_os("WINDIR")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\Windows"));
    let candidates = [
        windir.join("SysWOW64").join("d3dx9_43.dll"),
        windir.join("System32").join("d3dx9_43.dll"),
        windir.join("SysWOW64").join("d3dx9_42.dll"),
        windir.join("System32").join("d3dx9_42.dll"),
    ];
    !candidates.iter().any(|p| p.exists())
}

fn find_duplicate_asis(game_dir: &Path) -> Vec<String> {
    let mut scan_roots = vec![
        (game_dir.to_path_buf(), 1usize),
        (game_dir.join("scripts"), 2usize),
        (game_dir.join("CLEO"), 2usize),
        (game_dir.join("modloader"), 3usize),
    ];
    let mut all_files = Vec::new();
    for (dir, depth) in scan_roots.drain(..) {
        collect_files_limited(&dir, depth, &mut all_files);
    }

    let mut seen: HashMap<String, String> = HashMap::new();
    let mut duplicates = Vec::new();
    let mut seen_rel = HashSet::new();
    for path in all_files {
        let Some(fname) = path.file_name().and_then(|n| n.to_str()) else {
            continue;
        };
        let lower = fname.to_ascii_lowercase();
        if !lower.ends_with(".asi") {
            continue;
        }
        let rel = rel_str(game_dir, &path);
        if !seen_rel.insert(rel.to_ascii_lowercase()) {
            continue;
        }
        if seen.contains_key(&lower) {
            duplicates.push(rel);
        } else {
            seen.insert(lower, rel);
        }
    }
    duplicates
}

fn find_readonly_configs(game_dir: &Path) -> Vec<String> {
    let mut candidates = Vec::new();
    collect_files_limited(game_dir, 1, &mut candidates);
    collect_files_limited(&game_dir.join("scripts"), 2, &mut candidates);
    collect_files_limited(&game_dir.join("CLEO"), 2, &mut candidates);

    let mut readonly = Vec::new();
    for path in candidates {
        let Some(ext) = path.extension().and_then(|e| e.to_str()) else {
            continue;
        };
        let lower = ext.to_ascii_lowercase();
        if !matches!(lower.as_str(), "ini" | "cfg" | "set" | "dat") {
            continue;
        }
        if let Ok(meta) = fs::metadata(&path) {
            if meta.permissions().readonly() {
                readonly.push(rel_str(game_dir, &path));
            }
        }
    }
    readonly
}

pub fn check_health(game_dir: &Path, skygfx_preset: &str) -> HealthReport {
    // 0. gta_sa.exe + core game data/gta3.img
    let gta_exe = game_dir.join("gta_sa.exe");
    let gta3_img = game_dir.join("models").join("gta3.img");
    let gta_dat = game_dir.join("data").join("gta.dat");

    let (gta_status, gta_msg) = if !gta_exe.exists() {
        ("bad".into(), "✖ ไม่พบ gta_sa.exe ในโฟลเดอร์เกม".into())
    } else if !gta_dat.exists() || !gta3_img.exists() {
        (
            "warn".into(),
            "⚠ พบ gta_sa.exe แต่ไฟล์หลัก data/gta.dat หรือ models/gta3.img ไม่ครบ".into(),
        )
    } else {
        let len = fs::metadata(&gta_exe).map(|m| m.len()).unwrap_or(0);
        let hash_ok = sha256_file(&gta_exe)
            .map(|h| {
                h == "f01a00ce950fa40ca1ed59df0e789848c6edcf6405456274965885d0929343ac"
            })
            .unwrap_or(false);
        if hash_ok || len == 14_383_616 || len == 14_083_072 {
            let tag = if hash_ok { "SHA-256 ✓" } else { "1.0 US" };
            (
                "ok".into(),
                format!("✓ gta_sa.exe 1.0 US ({}) + gta3.img สมบูรณ์", tag),
            )
        } else {
            (
                "warn".into(),
                format!("⚠ gta_sa.exe ขนาด {} bytes — ไม่ใช่ 1.0 US มาตรฐาน SA-MP", len),
            )
        }
    };

    // 1. samp.dll version & DX9 runtime
    let samp_dll = game_dir.join("samp.dll");
    let missing_dx9 = detect_dx9_missing();
    let (samp_status, samp_msg) = if !samp_dll.exists() {
        ("bad".into(), "✖ ไม่พบ samp.dll — ต้องติดตั้ง SA-MP ในโฟลเดอร์เกม".into())
    } else {
        let slen = fs::metadata(&samp_dll).map(|m| m.len()).unwrap_or(0);
        let ver = match slen {
            2_199_552 => "0.3.7 R1",
            2_244_608 => "0.3.7 R2",
            2_285_568 => "0.3.7 R3",
            2_297_856 => "0.3.7 R4",
            2_306_048 => "0.3.7 R5",
            2_428_928 => "0.3DL R1",
            _ => "0.3.7",
        };
        if missing_dx9 {
            (
                "warn".into(),
                format!("⚠ samp.dll ({}) พร้อม แต่ไม่พบ DirectX 9 (d3dx9_43.dll)", ver),
            )
        } else {
            (
                "ok".into(),
                format!("✓ พบ samp.dll ({}) + DirectX 9 พร้อมใช้งาน", ver),
            )
        }
    };

    // 2. ASI Loader, ModLoader, CLEO & duplicate .asi check
    let has_asi = game_dir.join("dinput8.dll").exists() || game_dir.join("vorbisFile.dll").exists();
    let has_ml = game_dir.join("modloader.asi").exists();
    let has_cleo = game_dir.join("CLEO.asi").exists();
    let duplicate_asis = find_duplicate_asis(game_dir);

    let (mods_status, mods_msg) = if !duplicate_asis.is_empty() {
        (
            "warn".into(),
            format!(
                "⚠ พบไฟล์ .asi ซ้ำซ้อน {} ไฟล์ (กด 'แก้ไขอัตโนมัติ' ได้)",
                duplicate_asis.len()
            ),
        )
    } else if has_asi && has_ml && has_cleo {
        ("ok".into(), "✓ ASI Loader + ModLoader + CLEO 4.4 พร้อม (ไม่พบ .asi ซ้ำ)".into())
    } else if has_asi && has_ml {
        ("ok".into(), "✓ ASI Loader + ModLoader ทำงาน (ไม่พบ .asi ซ้ำ)".into())
    } else if has_asi {
        ("ok".into(), "✓ ASI Loader พร้อมทำงาน".into())
    } else {
        ("warn".into(), "⚠ ไม่มี ASI Loader (dinput8.dll) — ม็อดจะไม่ทำงาน".into())
    };

    // 3. FramerateVigilante, read-only files & Program Files warning
    let has_fv = game_dir.join("scripts").join("FramerateVigilante.asi").exists()
        || game_dir.join("FramerateVigilante.asi").exists();
    let readonly_files = find_readonly_configs(game_dir);
    let in_program_files = game_dir
        .to_string_lossy()
        .to_ascii_lowercase()
        .contains(r"\program files");

    let (env_status, env_msg) = if !readonly_files.is_empty() {
        (
            "warn".into(),
            format!(
                "⚠ พบไฟล์ตั้งค่า Read-Only {} ไฟล์ — อาจเซฟค่า/แครชได้",
                readonly_files.len()
            ),
        )
    } else if in_program_files {
        (
            "warn".into(),
            "⚠ เกมอยู่ใน Program Files — อาจติดสิทธิ์ UAC ตอนลงม็อด/บันทึกค่า".into(),
        )
    } else if has_fv {
        ("ok".into(), "✓ FramerateVigilante พร้อม & สิทธิ์เขียนไฟล์ปกติ".into())
    } else {
        ("warn".into(), "⚠ ไม่พบ FramerateVigilante — เล่นเกิน 30 FPS อาจเจอบั๊กฟิสิกส์".into())
    };

    // 4. SkyGfx & overall status
    let has_sg = game_dir.join("skygfx.ini").exists() || game_dir.join("skygfx1.ini").exists();
    let fixable_count = duplicate_asis.len() + readonly_files.len();
    let (gfx_status, gfx_msg) = if fixable_count > 0 {
        (
            "warn".into(),
            format!("⚡ พบปัญหาที่ซ่อมอัตโนมัติได้ {} รายการ กดปุ่ม 'แก้ไขอัตโนมัติ'", fixable_count),
        )
    } else if has_sg {
        (
            "ok".into(),
            format!("✓ SkyGfx ทำงาน (พรีเซ็ต: {})", skygfx_preset.to_uppercase()),
        )
    } else {
        ("info".into(), "– SkyGfx ปิดการทำงาน (ใช้กราฟิกต้นฉบับ)".into())
    };

    HealthReport {
        gta_status,
        gta_msg,
        samp_status,
        samp_msg,
        mods_status,
        mods_msg,
        env_status,
        env_msg,
        gfx_status,
        gfx_msg,
        duplicate_asis,
        readonly_files,
        in_program_files,
        missing_dx9,
        fixable_count,
    }
}

pub fn auto_fix_health(game_dir: &Path) -> Result<AutoFixResult, String> {
    if !game_dir.exists() {
        return Err("ไม่พบโฟลเดอร์เกม".into());
    }
    let report = check_health(game_dir, "ps2");

    let mut fixed_readonly = 0usize;
    for rel in &report.readonly_files {
        let full = game_dir.join(rel);
        if let Ok(meta) = fs::metadata(&full) {
            let mut perms = meta.permissions();
            if perms.readonly() {
                perms.set_readonly(false);
                if fs::set_permissions(&full, perms).is_ok() {
                    fixed_readonly += 1;
                }
            }
        }
    }

    let mut disabled_duplicates = Vec::new();
    for rel in &report.duplicate_asis {
        let src = game_dir.join(rel);
        if !src.exists() {
            continue;
        }
        let mut target = PathBuf::from(format!("{}.disabled", src.to_string_lossy()));
        let mut idx = 1usize;
        while target.exists() {
            target = PathBuf::from(format!("{}.dup{}.disabled", src.to_string_lossy(), idx));
            idx += 1;
        }
        if fs::rename(&src, &target).is_ok() {
            disabled_duplicates.push(rel.clone());
        }
    }

    let mut remaining_warnings = Vec::new();
    if report.in_program_files {
        remaining_warnings.push("โฟลเดอร์เกมอยู่ใน Program Files (แนะนำย้ายออกหากติดสิทธิ์เขียนไฟล์)".into());
    }
    if report.missing_dx9 {
        remaining_warnings.push("ไม่พบ DirectX 9 End-User Runtime (d3dx9_43.dll)".into());
    }

    Ok(AutoFixResult {
        fixed_readonly,
        disabled_duplicates,
        remaining_warnings,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn make_temp(tag: &str) -> PathBuf {
        let n = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_health_{}_{}", tag, n));
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn test_health_detects_and_fixes_duplicates_and_readonly() {
        let dir = make_temp("fix");
        fs::create_dir_all(dir.join("scripts")).unwrap();
        fs::create_dir_all(dir.join("modloader").join("pack")).unwrap();
        fs::create_dir_all(dir.join("models")).unwrap();
        fs::create_dir_all(dir.join("data")).unwrap();

        fs::write(dir.join("gta_sa.exe"), b"dummy").unwrap();
        fs::write(dir.join("models").join("gta3.img"), b"img").unwrap();
        fs::write(dir.join("data").join("gta.dat"), b"dat").unwrap();
        fs::write(dir.join("samp.dll"), b"dll").unwrap();
        fs::write(dir.join("scripts").join("DemoMod.asi"), b"1").unwrap();
        fs::write(dir.join("modloader").join("pack").join("DemoMod.asi"), b"2").unwrap();

        let cfg = dir.join("skygfx.ini");
        fs::write(&cfg, b"[SkyGfx]\n").unwrap();
        let mut perms = fs::metadata(&cfg).unwrap().permissions();
        perms.set_readonly(true);
        fs::set_permissions(&cfg, perms).unwrap();

        let r = check_health(&dir, "ps2");
        assert_eq!(r.duplicate_asis.len(), 1);
        assert_eq!(r.readonly_files, vec!["skygfx.ini"]);
        assert_eq!(r.fixable_count, 2);

        let fix = auto_fix_health(&dir).unwrap();
        assert_eq!(fix.fixed_readonly, 1);
        assert_eq!(fix.disabled_duplicates.len(), 1);
        assert!(dir
            .join("modloader")
            .join("pack")
            .join("DemoMod.asi.disabled")
            .exists());
        assert!(!fs::metadata(&cfg).unwrap().permissions().readonly());

        let r2 = check_health(&dir, "ps2");
        assert_eq!(r2.fixable_count, 0);

        let _ = fs::remove_dir_all(&dir);
    }
}
