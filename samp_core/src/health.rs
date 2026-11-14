use serde::Serialize;
use sha2::{Digest, Sha256};
use std::collections::{HashMap, HashSet};
use std::fs::{self, File, OpenOptions};
use std::io::{Read, Seek, SeekFrom, Write};
use std::path::{Path, PathBuf};
use std::sync::{Mutex, OnceLock};
use std::time::UNIX_EPOCH;

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
    pub needs_laa: bool,
    pub fixable_count: usize,
}

#[derive(Debug, Clone, Serialize)]
pub struct AutoFixResult {
    pub fixed_readonly: usize,
    pub disabled_duplicates: Vec<String>,
    pub remaining_warnings: Vec<String>,
    pub laa_enabled: bool,
    pub optimized_configs: usize,
    pub cleaned_logs: usize,
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

fn sha256_file_cached(path: &Path) -> std::io::Result<String> {
    static CACHE: OnceLock<Mutex<HashMap<String, (u64, u128, String)>>> = OnceLock::new();
    let meta = fs::metadata(path)?;
    let len = meta.len();
    let mtime_nanos = meta
        .modified()
        .ok()
        .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    let key = path.to_string_lossy().to_ascii_lowercase();
    let store = CACHE.get_or_init(|| Mutex::new(HashMap::new()));
    if mtime_nanos > 0 {
        if let Ok(guard) = store.lock() {
            if let Some((clen, cmtime, chex)) = guard.get(&key) {
                if *clen == len && *cmtime == mtime_nanos {
                    return Ok(chex.clone());
                }
            }
        }
    }
    let hex = sha256_file(path)?;
    if mtime_nanos > 0 {
        if let Ok(mut guard) = store.lock() {
            if guard.len() >= 8 {
                guard.clear();
            }
            guard.insert(key, (len, mtime_nanos, hex.clone()));
        }
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
    let (ml_ignore_all, ml_ignored) = crate::modloader_cfg::ignored_mod_folders(game_dir);
    let mut scan_roots = vec![
        (game_dir.to_path_buf(), 1usize),
        (game_dir.join("scripts"), 2usize),
        (game_dir.join("CLEO"), 2usize),
    ];
    let ml_dir = game_dir.join("modloader");
    if !ml_ignore_all && ml_dir.is_dir() {
        if let Ok(rd) = fs::read_dir(&ml_dir) {
            let mut dirs: Vec<PathBuf> = rd.flatten().map(|e| e.path()).collect();
            dirs.sort();
            for p in dirs {
                if !p.is_dir() {
                    continue;
                }
                let name = p
                    .file_name()
                    .and_then(|n| n.to_str())
                    .map(|s| s.to_ascii_lowercase())
                    .unwrap_or_default();
                if name.starts_with('.') || name == "_ignore" || ml_ignored.contains(&name) {
                    continue;
                }
                scan_roots.push((p, 2usize));
            }
        }
    }
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
        let hash_ok = sha256_file_cached(&gta_exe)
            .map(|h| {
                h == "f01a00ce950fa40ca1ed59df0e789848c6edcf6405456274965885d0929343ac"
            })
            .unwrap_or(false);
        if hash_ok || len == 14_383_616 || len == 14_083_072 {
            let tag = if hash_ok { "SHA-256 ✓" } else { "1.0 US" };
            let laa_suffix = match check_pe_laa(&gta_exe) {
                Some(true) => " • 4GB LAA ✓",
                Some(false) => " (ยังไม่เปิด 4GB LAA)",
                None => "",
            };
            (
                "ok".into(),
                format!(
                    "✓ gta_sa.exe 1.0 US ({}) + gta3.img สมบูรณ์{}",
                    tag, laa_suffix
                ),
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
    let sky_active = game_dir.join("skygfx.ini").exists()
        && (game_dir.join("skygfx.asi").exists()
            || game_dir.join("scripts").join("skygfx.asi").exists());
    let needs_laa = matches!(check_pe_laa(&gta_exe), Some(false));
    let fixable_count = duplicate_asis.len() + readonly_files.len() + usize::from(needs_laa);
    let (gfx_status, gfx_msg) = if fixable_count > 0 {
        (
            "warn".into(),
            format!("⚡ พบปัญหาที่ซ่อมอัตโนมัติได้ {} รายการ กดปุ่ม 'แก้ไขอัตโนมัติ'", fixable_count),
        )
    } else if sky_active {
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
        needs_laa,
        fixable_count,
    }
}

const IMAGE_FILE_LARGE_ADDRESS_AWARE: u16 = 0x0020;
const IMAGE_NT_OPTIONAL_HDR32_MAGIC: u16 = 0x010B;

/// Inspects a PE32 binary's COFF Characteristics for IMAGE_FILE_LARGE_ADDRESS_AWARE.
/// Returns `None` if the file does not exist or is not a valid 32-bit PE32 header.
pub fn check_pe_laa(exe_path: &Path) -> Option<bool> {
    let mut f = File::open(exe_path).ok()?;
    let mut hdr = [0u8; 4096];
    let n = f.read(&mut hdr).ok()?;
    if n < 0x40 || &hdr[0..2] != b"MZ" {
        return None;
    }
    let e_lfanew = u32::from_le_bytes([hdr[0x3C], hdr[0x3D], hdr[0x3E], hdr[0x3F]]) as usize;
    let end = e_lfanew.checked_add(26)?;
    if end > n || &hdr[e_lfanew..e_lfanew + 4] != b"PE\0\0" {
        return None;
    }
    let opt_magic = u16::from_le_bytes([hdr[e_lfanew + 24], hdr[e_lfanew + 25]]);
    if opt_magic != IMAGE_NT_OPTIONAL_HDR32_MAGIC {
        return None;
    }
    let chars = u16::from_le_bytes([hdr[e_lfanew + 22], hdr[e_lfanew + 23]]);
    Some((chars & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0)
}

/// Sets the IMAGE_FILE_LARGE_ADDRESS_AWARE (4GB virtual memory) bit on a 32-bit PE header.
/// Returns `Ok(true)` if modified, `Ok(false)` if already enabled or not a PE32 file.
pub fn enable_pe_laa(exe_path: &Path) -> Result<bool, String> {
    let Some(enabled) = check_pe_laa(exe_path) else {
        return Ok(false);
    };
    if enabled {
        return Ok(false);
    }
    if let Ok(meta) = fs::metadata(exe_path) {
        let mut perms = meta.permissions();
        if perms.readonly() {
            perms.set_readonly(false);
            let _ = fs::set_permissions(exe_path, perms);
        }
    }
    let mut f = OpenOptions::new()
        .read(true)
        .write(true)
        .open(exe_path)
        .map_err(|e| format!("ไม่สามารถเปิด {} เพื่อเปิด 4GB LAA: {}", exe_path.display(), e))?;
    let mut dos = [0u8; 64];
    f.read_exact(&mut dos)
        .map_err(|e| format!("อ่าน DOS header ล้มเหลว: {}", e))?;
    let e_lfanew = u32::from_le_bytes([dos[0x3C], dos[0x3D], dos[0x3E], dos[0x3F]]) as u64;
    let chars_off = e_lfanew
        .checked_add(22)
        .ok_or_else(|| "PE header offset ไม่ถูกต้อง".to_string())?;
    f.seek(SeekFrom::Start(chars_off))
        .map_err(|e| format!("Seek PE Characteristics ล้มเหลว: {}", e))?;
    let mut cbuf = [0u8; 2];
    f.read_exact(&mut cbuf)
        .map_err(|e| format!("อ่าน PE Characteristics ล้มเหลว: {}", e))?;
    let updated = u16::from_le_bytes(cbuf) | IMAGE_FILE_LARGE_ADDRESS_AWARE;
    f.seek(SeekFrom::Start(chars_off))
        .map_err(|e| format!("Seek PE write ล้มเหลว: {}", e))?;
    f.write_all(&updated.to_le_bytes())
        .map_err(|e| format!("บันทึก PE Characteristics ล้มเหลว: {}", e))?;
    Ok(true)
}

/// Updates INI keys case-insensitively while preserving sections, blank lines, and comments.
/// When `target_section` is `Some(sec)`, only keys inside `[sec]` are updated and missing keys
/// are inserted at the end of `[sec]` (or created under a new `[sec]` section).
pub(crate) fn update_ini_keys(
    path: &Path,
    updates: &[(&str, &str)],
    spaced_equals: bool,
    target_section: Option<&str>,
) -> Result<bool, String> {
    if !path.exists() {
        return Ok(false);
    }
    if let Ok(meta) = fs::metadata(path) {
        let mut perms = meta.permissions();
        if perms.readonly() {
            perms.set_readonly(false);
            let _ = fs::set_permissions(path, perms);
        }
    }
    let raw = fs::read_to_string(path).map_err(|e| format!("อ่าน {} ล้มเหลว: {}", path.display(), e))?;
    let has_bom = raw.starts_with('\u{feff}');
    let clean_raw = raw.strip_prefix('\u{feff}').unwrap_or(&raw);
    let line_sep = if clean_raw.contains("\r\n") { "\r\n" } else { "\n" };
    let mut out_lines: Vec<String> = Vec::new();
    let mut applied: HashSet<String> = HashSet::new();
    let mut changed = has_bom;
    let mut current_section: Option<String> = None;
    let mut target_sec_insert_idx: Option<usize> = None;

    for orig in clean_raw.lines() {
        let trimmed = orig.trim();
        if trimmed.starts_with('[') && trimmed.ends_with(']') && trimmed.len() >= 2 {
            let sec_name = trimmed[1..trimmed.len() - 1].trim().to_string();
            current_section = Some(sec_name.clone());
            out_lines.push(orig.to_string());
            if let Some(ts) = target_section {
                if sec_name.eq_ignore_ascii_case(ts) {
                    target_sec_insert_idx = Some(out_lines.len());
                }
            }
            continue;
        }
        let in_scope = match target_section {
            Some(ts) => current_section
                .as_deref()
                .map_or(false, |s| s.eq_ignore_ascii_case(ts)),
            None => true,
        };
        if trimmed.is_empty() || trimmed.starts_with(';') || trimmed.starts_with('#') || !in_scope {
            out_lines.push(orig.to_string());
            if in_scope && !trimmed.is_empty() {
                target_sec_insert_idx = Some(out_lines.len());
            }
            continue;
        }
        if let Some(eq_idx) = orig.find('=') {
            let key_raw = orig[..eq_idx].trim();
            if let Some((_, new_val)) = updates.iter().find(|(k, _)| k.eq_ignore_ascii_case(key_raw)) {
                let after_eq = &orig[eq_idx + 1..];
                let comment_suffix = after_eq
                    .find(';')
                    .map(|ci| format!("  {}", after_eq[ci..].trim()))
                    .unwrap_or_default();
                let sep = if spaced_equals { " = " } else { "=" };
                let replacement = format!("{}{}{}{}", key_raw, sep, new_val, comment_suffix);
                if replacement != orig {
                    changed = true;
                }
                out_lines.push(replacement);
                applied.insert(key_raw.to_ascii_lowercase());
                if target_section.is_some() {
                    target_sec_insert_idx = Some(out_lines.len());
                }
                continue;
            }
        }
        out_lines.push(orig.to_string());
        if target_section.is_some() {
            target_sec_insert_idx = Some(out_lines.len());
        }
    }

    let missing: Vec<(&str, &str)> = updates
        .iter()
        .copied()
        .filter(|(k, _)| !applied.contains(&k.to_ascii_lowercase()))
        .collect();
    if !missing.is_empty() {
        let sep = if spaced_equals { " = " } else { "=" };
        if let Some(ts) = target_section {
            if let Some(mut idx) = target_sec_insert_idx {
                for (k, v) in missing {
                    out_lines.insert(idx, format!("{}{}{}", k, sep, v));
                    idx += 1;
                    changed = true;
                }
            } else {
                if !out_lines.is_empty() && !out_lines.last().map_or(true, |l| l.trim().is_empty()) {
                    out_lines.push(String::new());
                }
                out_lines.push(format!("[{}]", ts));
                for (k, v) in missing {
                    out_lines.push(format!("{}{}{}", k, sep, v));
                }
                changed = true;
            }
        } else {
            for (k, v) in missing {
                out_lines.push(format!("{}{}{}", k, sep, v));
                changed = true;
            }
        }
    }

    if changed {
        let body = format!("{}{}", out_lines.join(line_sep), line_sep);
        fs::write(path, body).map_err(|e| format!("เขียน {} ล้มเหลว: {}", path.display(), e))?;
    }
    Ok(changed)
}

fn sanitize_stream_ini(path: &Path, target_kb: u32) -> Result<bool, String> {
    if !path.exists() {
        return Ok(false);
    }
    let raw = fs::read_to_string(path).map_err(|e| format!("อ่าน stream.ini ล้มเหลว: {}", e))?;
    let line_sep = if raw.contains("\r\n") { "\r\n" } else { "\n" };
    let target_str = target_kb.to_string();
    let mut out: Vec<String> = Vec::new();
    let mut changed = false;
    let mut saw_mem = false;
    let mut saw_dev = false;

    for orig in raw.lines() {
        let mut parts = orig.split_whitespace();
        if let (Some(k), Some(v)) = (parts.next(), parts.next()) {
            if k.eq_ignore_ascii_case("memory") {
                saw_mem = true;
                if v != target_str {
                    out.push(format!("memory\t\t{}", target_str));
                    changed = true;
                    continue;
                }
            } else if k.eq_ignore_ascii_case("devkit_memory") {
                saw_dev = true;
                if v != target_str {
                    out.push(format!("devkit_memory\t{}", target_str));
                    changed = true;
                    continue;
                }
            }
        }
        out.push(orig.to_string());
    }
    if !saw_mem {
        out.push(format!("memory\t\t{}", target_str));
        changed = true;
    }
    if !saw_dev {
        out.push(format!("devkit_memory\t{}", target_str));
        changed = true;
    }
    if changed {
        let body = format!("{}{}", out.join(line_sep), line_sep);
        fs::write(path, body).map_err(|e| format!("เขียน stream.ini ล้มเหลว: {}", e))?;
    }
    Ok(changed)
}

/// Applies stream.ini and III.VC.SA.LimitAdjuster.ini memory budgets matching real available RAM
/// and the user's ResourceMode setting in SonicGuard.ini.
pub fn apply_adaptive_memory_budget_for_avail(
    game_dir: &Path,
    avail_phys_mb: u64,
    mode: crate::resources::ResourceMode,
) -> usize {
    let (target_kb, target_mb) =
        crate::resources::compute_adaptive_stream_budget_kb(avail_phys_mb, mode);
    let mut count = 0usize;

    let limit_ini = game_dir.join("III.VC.SA.LimitAdjuster.ini");
    if limit_ini.exists() {
        if let Ok(raw) = fs::read_to_string(&limit_ini) {
            let needs_update = raw.lines().any(|line| {
                let t = line.trim();
                if t.is_empty() || t.starts_with(';') || t.starts_with('#') {
                    return false;
                }
                if let Some((k, v)) = t.split_once('=') {
                    if k.trim().eq_ignore_ascii_case("MemoryAvailable") {
                        let vt = v.split(';').next().unwrap_or(v).trim();
                        return vt.eq_ignore_ascii_case("50%")
                            || vt.parse::<u32>().map(|n| n != target_mb).unwrap_or(false);
                    }
                }
                false
            });
            if needs_update {
                let mb_str = target_mb.to_string();
                if update_ini_keys(&limit_ini, &[("MemoryAvailable", &mb_str)], true, None)
                    .unwrap_or(false)
                {
                    count += 1;
                }
            }
        }
    }

    if sanitize_stream_ini(&game_dir.join("stream.ini"), target_kb).unwrap_or(false) {
        count += 1;
    }
    count
}

pub fn apply_adaptive_memory_budget(game_dir: &Path) -> usize {
    let sg = crate::resources::read_sonic_guard_settings(game_dir);
    let mode = crate::resources::ResourceMode::from_str_lossy(&sg.resource_mode);
    let sys = crate::resources::query_system_memory(mode);
    apply_adaptive_memory_budget_for_avail(game_dir, sys.avail_phys_mb, mode)
}

fn toggle_skygfx_asi(game_dir: &Path, enable: bool) -> Result<(), String> {
    let candidates = [
        (game_dir.join("skygfx.asi"), game_dir.join("skygfx.asi.disabled")),
        (
            game_dir.join("scripts").join("skygfx.asi"),
            game_dir.join("scripts").join("skygfx.asi.disabled"),
        ),
    ];
    for (asi, dis) in candidates {
        if enable {
            if !asi.exists() && dis.exists() {
                fs::rename(&dis, &asi)
                    .map_err(|e| format!("ไม่สามารถเปิด {}: {}", asi.display(), e))?;
            }
        } else if asi.exists() {
            if dis.exists() {
                let _ = fs::remove_file(&dis);
            }
            fs::rename(&asi, &dis)
                .map_err(|e| format!("ไม่สามารถปิด {}: {}", asi.display(), e))?;
        }
    }
    Ok(())
}

fn sanitize_skygfx_templates(game_dir: &Path) -> Result<usize, String> {
    let mut fixed = 0usize;
    let has_texdb = game_dir.join("models").join("texdb").is_dir();

    let sg1 = game_dir.join("skygfx1.ini");
    if sg1.exists()
        && update_ini_keys(
            &sg1,
            &[("keySwitch", "0"), ("colorFilter", "PS2")],
            false,
            None,
        )?
    {
        fixed += 1;
    }
    let sg2 = game_dir.join("skygfx2.ini");
    if sg2.exists() && update_ini_keys(&sg2, &[("keySwitch", "0")], false, None)? {
        fixed += 1;
    }
    let sg3 = game_dir.join("skygfx3.ini");
    if sg3.exists() {
        let mut updates = vec![("keySwitch", "0")];
        if !has_texdb {
            updates.push(("detailMaps", "0"));
        }
        if update_ini_keys(&sg3, &updates, false, None)? {
            fixed += 1;
        }
    }
    Ok(fixed)
}

/// Applies SkyGfx graphics preset cleanly, including toggling skygfx.asi when turning Off
/// and sanitizing colorFilter/detailMaps/keySwitch to prevent visual glitches and F10 conflicts.
pub fn apply_skygfx_preset(game_dir: &Path, preset: &str) -> Result<String, String> {
    let mode = preset.trim().to_ascii_lowercase();
    let target = game_dir.join("skygfx.ini");

    if mode == "off" {
        let had_ini = target.exists();
        if had_ini {
            fs::remove_file(&target).map_err(|e| format!("ลบ skygfx.ini ไม่สำเร็จ: {}", e))?;
        }
        toggle_skygfx_asi(game_dir, false)?;
        if had_ini {
            return Ok("ปิดใช้งาน SkyGfx และปิดปลั๊กอิน skygfx.asi แล้ว".into());
        }
        return Ok("SkyGfx ปิดใช้งานอยู่แล้ว".into());
    }

    let (src_name, label, filter_val) = match mode.as_str() {
        "ps2" => ("skygfx1.ini", "PS2 Classic", "PS2"),
        "pc" => ("skygfx2.ini", "PC Original", "PC"),
        "mobile" => ("skygfx3.ini", "Mobile", "Mobile"),
        _ => return Err(format!("ไม่รู้จัก SkyGfx preset '{}'", preset)),
    };

    let src = game_dir.join(src_name);
    if !src.exists() {
        return Err(format!("ไม่พบไฟล์ {} ในโฟลเดอร์เกม:\r\n{}", src_name, game_dir.display()));
    }

    toggle_skygfx_asi(game_dir, true)?;
    let _ = sanitize_skygfx_templates(game_dir);
    fs::copy(&src, &target).map_err(|e| format!("คัดลอก {} ไม่สำเร็จ: {}", src_name, e))?;

    let has_texdb = game_dir.join("models").join("texdb").is_dir();
    let mut enforce = vec![("keySwitch", "0"), ("colorFilter", filter_val)];
    if mode == "mobile" && !has_texdb {
        enforce.push(("detailMaps", "0"));
    }
    let _ = update_ini_keys(&target, &enforce, false, None);

    Ok(format!("สลับโทนภาพเป็น {} เรียบร้อย (Rust Engine)", label))
}

/// Applies FPS limit cleanly across FPSUnlock.ini (numeric 1/0 + safe PhysicsScale) and
/// FramerateVigilante.ini, capping Unlimited ("Max") at 240 FPS so GTA SA physics stay stable.
pub fn apply_fps_limit(game_dir: &Path, fps: u32) -> Result<String, String> {
    let scripts_dir = game_dir.join("scripts");
    let unlock_ini = scripts_dir.join("FPSUnlock.ini");
    let fv_ini = scripts_dir.join("FramerateVigilante.ini");
    let has_fv_asi = scripts_dir.join("FramerateVigilante.asi").exists()
        || game_dir.join("FramerateVigilante.asi").exists();

    if !unlock_ini.exists() && !fv_ini.exists() {
        return Err("ไม่พบไฟล์ตั้งค่า FPS ในโฟลเดอร์ scripts (FPSUnlock.ini หรือ FramerateVigilante.ini)".into());
    }

    let effective_fps = if fps == 0 { 240 } else { fps };
    let fps_str = effective_fps.to_string();
    let physics_scale = if has_fv_asi { "1.0" } else { "0.6" };

    if unlock_ini.exists() {
        // FPSUnlock.asi uses GetPrivateProfileIntA -> must write integer 1/0, never "true"
        update_ini_keys(
            &unlock_ini,
            &[
                ("UnlockFPS", "1"),
                ("CustomFPS", &fps_str),
                ("PhysicsScale", physics_scale),
            ],
            true,
            None,
        )?;
    }

    if fv_ini.exists() {
        update_ini_keys(
            &fv_ini,
            &[("FPSlimit", &fps_str), ("VehicleHandlingPatches", "1")],
            false,
            None,
        )?;
    }

    if fps == 0 {
        Ok("ตั้งค่า FPS เป็น Max (เพดานปลอดภัย 240 FPS + กันบั๊กฟิสิกส์) เรียบร้อย".into())
    } else {
        Ok(format!("ตั้งค่า FPS Limit เป็น {} FPS เรียบร้อย", fps))
    }
}

/// Cleans/truncates bloated log files and stale flat .bak files outside Zstd snapshots.
fn clean_bloated_logs_and_backups(game_dir: &Path) -> usize {
    let log_candidates = [
        game_dir.join("scripts").join("FPSUnlock.log"),
        game_dir.join("modloader").join("modloader.log"),
        game_dir.join("samp.log"),
        game_dir.join("cleo.log"),
        game_dir.join("SVulcan.log"),
    ];
    let mut cleaned = 0usize;
    for lp in log_candidates {
        if let Ok(meta) = fs::metadata(&lp) {
            // Clean FPSUnlock.log if over 64 KB or any log over 1 MB
            let limit = if lp.file_name().and_then(|n| n.to_str()) == Some("FPSUnlock.log") {
                64 * 1024
            } else {
                1024 * 1024
            };
            if meta.len() > limit && fs::write(&lp, b"").is_ok() {
                cleaned += 1;
            }
        }
    }
    let backup_dir = game_dir.join("_mod_backup");
    let snapshots_dir = backup_dir.join("snapshots");
    let has_snapshot = snapshots_dir.is_dir()
        && fs::read_dir(&snapshots_dir)
            .ok()
            .map(|rd| {
                rd.flatten().any(|e| {
                    e.path()
                        .extension()
                        .and_then(|x| x.to_str())
                        .map_or(false, |s| s.eq_ignore_ascii_case("json"))
                })
            })
            .unwrap_or(false);
    if has_snapshot {
        if let Ok(rd) = fs::read_dir(&backup_dir) {
            for ent in rd.flatten() {
                let p = ent.path();
                if p.is_file() {
                    if let Some(ext) = p.extension().and_then(|e| e.to_str()) {
                        if ext.eq_ignore_ascii_case("bak") && fs::remove_file(&p).is_ok() {
                            cleaned += 1;
                        }
                    }
                }
            }
        }
    }
    cleaned
}

fn sanitize_game_configs(game_dir: &Path) -> usize {
    let mut count = apply_adaptive_memory_budget(game_dir);

    let unlock_ini = game_dir.join("scripts").join("FPSUnlock.ini");
    if unlock_ini.exists() {
        let has_fv = game_dir.join("scripts").join("FramerateVigilante.asi").exists()
            || game_dir.join("FramerateVigilante.asi").exists();
        let physics_scale = if has_fv { "1.0" } else { "0.6" };
        if update_ini_keys(
            &unlock_ini,
            &[("UnlockFPS", "1"), ("PhysicsScale", physics_scale)],
            true,
            None,
        )
        .unwrap_or(false)
        {
            count += 1;
        }
    }

    count += sanitize_skygfx_templates(game_dir).unwrap_or(0);
    let active_sg = game_dir.join("skygfx.ini");
    if active_sg.exists()
        && update_ini_keys(&active_sg, &[("keySwitch", "0")], false, None).unwrap_or(false)
    {
        count += 1;
    }

    count
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

    let laa_enabled = enable_pe_laa(&game_dir.join("gta_sa.exe")).unwrap_or(false);
    let optimized_configs = sanitize_game_configs(game_dir);
    let cleaned_logs = clean_bloated_logs_and_backups(game_dir);

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
        laa_enabled,
        optimized_configs,
        cleaned_logs,
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

    #[test]
    fn test_pe_laa_skygfx_fps_and_stream_sanitization() {
        let dir = make_temp("gfx");
        fs::create_dir_all(dir.join("scripts")).unwrap();

        // Build minimal PE32 with e_lfanew = 0x80, Characteristics = 0x010F (no 0x0020 LAA),
        // and PE32 OptionalHeader magic = 0x010B
        let mut pe = vec![0u8; 256];
        pe[0] = b'M';
        pe[1] = b'Z';
        pe[0x3C] = 0x80;
        pe[0x80..0x84].copy_from_slice(b"PE\0\0");
        pe[0x80 + 22..0x80 + 24].copy_from_slice(&0x010Fu16.to_le_bytes());
        pe[0x80 + 24..0x80 + 26].copy_from_slice(&0x010Bu16.to_le_bytes());
        let exe = dir.join("gta_sa.exe");
        fs::write(&exe, &pe).unwrap();
        assert_eq!(check_pe_laa(&exe), Some(false));
        assert!(enable_pe_laa(&exe).unwrap());
        assert_eq!(check_pe_laa(&exe), Some(true));

        // Corrupt / non-PE32 magic must return None
        pe[0x80 + 24..0x80 + 26].copy_from_slice(&0x020Bu16.to_le_bytes());
        let bad_pe = dir.join("win64.exe");
        fs::write(&bad_pe, &pe).unwrap();
        assert_eq!(check_pe_laa(&bad_pe), None);

        // SkyGfx presets + asi toggling + keySwitch=0
        fs::write(dir.join("skygfx.asi"), b"asi").unwrap();
        fs::write(dir.join("skygfx1.ini"), "[SkyGfx]\nkeySwitch=0x79\ncolorFilter=Mobile\n").unwrap();
        fs::write(dir.join("skygfx3.ini"), "[SkyGfx]\nkeySwitch=0x79\ndetailMaps=1\n").unwrap();

        apply_skygfx_preset(&dir, "ps2").unwrap();
        let sg = fs::read_to_string(dir.join("skygfx.ini")).unwrap();
        assert!(sg.contains("keySwitch=0"));
        assert!(sg.contains("colorFilter=PS2"));
        assert!(dir.join("skygfx.asi").exists());

        apply_skygfx_preset(&dir, "mobile").unwrap();
        let sg_mob = fs::read_to_string(dir.join("skygfx.ini")).unwrap();
        assert!(sg_mob.contains("detailMaps=0"));

        apply_skygfx_preset(&dir, "off").unwrap();
        assert!(!dir.join("skygfx.ini").exists());
        assert!(!dir.join("skygfx.asi").exists());
        assert!(dir.join("skygfx.asi.disabled").exists());

        // FPSUnlock + FramerateVigilante coordination
        fs::write(dir.join("scripts").join("FramerateVigilante.asi"), b"fv").unwrap();
        fs::write(dir.join("scripts").join("FPSUnlock.ini"), "[Settings]\nUnlockFPS = true\nCustomFPS = 0\nPhysicsScale = 0.6\n").unwrap();
        fs::write(dir.join("scripts").join("FramerateVigilante.ini"), "[Settings]\nFPSlimit=0\nVehicleHandlingPatches=0\n").unwrap();
        apply_fps_limit(&dir, 0).unwrap();
        let fu = fs::read_to_string(dir.join("scripts").join("FPSUnlock.ini")).unwrap();
        let fv = fs::read_to_string(dir.join("scripts").join("FramerateVigilante.ini")).unwrap();
        assert!(fu.contains("UnlockFPS = 1"));
        assert!(fu.contains("CustomFPS = 240"));
        assert!(fu.contains("PhysicsScale = 1.0"));
        assert!(fv.contains("FPSlimit=240"));
        assert!(fv.contains("VehicleHandlingPatches=1"));

        // Memory + stream.ini adaptive and explicit modes
        fs::write(dir.join("stream.ini"), "memory\t\t131072\ndevkit_memory\t131072\n").unwrap();
        fs::write(dir.join("III.VC.SA.LimitAdjuster.ini"), "[SALIMITS]\nMemoryAvailable = 50%\n").unwrap();
        let _ = apply_adaptive_memory_budget_for_avail(&dir, 6000, crate::resources::ResourceMode::Auto);
        let st = fs::read_to_string(dir.join("stream.ini")).unwrap();
        let la = fs::read_to_string(dir.join("III.VC.SA.LimitAdjuster.ini")).unwrap();
        assert!(st.contains("1048576"));
        assert!(la.contains("MemoryAvailable = 1024"));

        let _ = apply_adaptive_memory_budget_for_avail(&dir, 1150, crate::resources::ResourceMode::Auto);
        let st_low = fs::read_to_string(dir.join("stream.ini")).unwrap();
        let la_low = fs::read_to_string(dir.join("III.VC.SA.LimitAdjuster.ini")).unwrap();
        assert!(st_low.contains("524288"));
        assert!(la_low.contains("MemoryAvailable = 512"));

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_ini_section_target_bom_strip_and_ignore_mods_dup() {
        let dir = make_temp("ini_ign");
        fs::create_dir_all(dir.join("scripts")).unwrap();
        fs::create_dir_all(dir.join("modloader").join("IgnoredPack")).unwrap();

        fs::write(dir.join("scripts").join("CoolMod.asi"), b"1").unwrap();
        fs::write(dir.join("modloader").join("IgnoredPack").join("CoolMod.asi"), b"2").unwrap();
        fs::write(
            dir.join("modloader").join("modloader.ini"),
            "[Folder.Config]\r\nProfile = Default\r\n[Profiles.Default.IgnoreMods]\r\nIgnoredPack\r\n",
        )
        .unwrap();
        assert!(find_duplicate_asis(&dir).is_empty());

        let ini = dir.join("multi.ini");
        fs::write(
            &ini,
            "\u{feff}[OTHER]\r\nMode=keep\r\n[MAIN]\r\nMode=old\r\n",
        )
        .unwrap();
        assert!(update_ini_keys(&ini, &[("Mode", "new"), ("Extra", "1")], false, Some("MAIN")).unwrap());
        let after = fs::read_to_string(&ini).unwrap();
        assert!(!after.starts_with('\u{feff}'));
        assert!(after.contains("[OTHER]\r\nMode=keep"));
        assert!(after.contains("[MAIN]\r\nMode=new\r\nExtra=1"));

        let _ = fs::remove_dir_all(&dir);
    }
}
