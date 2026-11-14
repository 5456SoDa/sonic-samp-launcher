use crate::db::{self, SavedServerEntry};
use crate::health;
use crate::win_ro;
use serde::{Deserialize, Serialize};
use std::fs::{self, File};
use std::io::Write;
use std::path::Path;
use std::ptr::{null, null_mut};
use windows_sys::Win32::Foundation::LocalFree;
use windows_sys::Win32::Security::Cryptography::{
    CryptProtectData, CryptUnprotectData, CRYPTPROTECT_UI_FORBIDDEN, CRYPT_INTEGER_BLOB,
};
use windows_sys::Win32::Storage::FileSystem::{
    MoveFileExW, MOVEFILE_REPLACE_EXISTING, MOVEFILE_WRITE_THROUGH,
};

const DPAPI_PREFIX: &str = "dpapi:";
const DPAPI_ENTROPY: &[u8] = b"SonicSampLauncher_v2_Secret";

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct LauncherConfig {
    pub nick: String,
    pub accent: String,
    pub game_path: String,
    pub auto_connect: bool,
    pub close_after_launch: bool,
    pub last_ip: String,
    pub last_port: String,
    pub rcon_ip: String,
    pub rcon_port: String,
    pub rcon_pass: String,
    pub sort_col: i32,
    pub sort_desc: bool,
    pub skygfx_preset: String,
    pub fps_limit: i32,
    pub auto_join_slot: bool,
    pub sound_fx: bool,
    pub discord_rpc: bool,
    pub discord_app_id: String,
    pub no_pause_alt_tab: bool,
    pub nick_profiles: Vec<String>,
}

impl Default for LauncherConfig {
    fn default() -> Self {
        Self {
            nick: String::new(),
            accent: "138,43,226".to_string(),
            game_path: String::new(),
            auto_connect: false,
            close_after_launch: false,
            last_ip: "127.0.0.1".to_string(),
            last_port: "7777".to_string(),
            rcon_ip: String::new(),
            rcon_port: String::new(),
            rcon_pass: String::new(),
            sort_col: 0,
            sort_desc: false,
            skygfx_preset: "ps2".to_string(),
            fps_limit: 0,
            auto_join_slot: false,
            sound_fx: true,
            discord_rpc: true,
            discord_app_id: "1088484931343753236".to_string(),
            no_pause_alt_tab: false,
            nick_profiles: Vec::new(),
        }
    }
}

fn bytes_to_hex(bytes: &[u8]) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut out = String::with_capacity(bytes.len() * 2);
    for &b in bytes {
        out.push(HEX[(b >> 4) as usize] as char);
        out.push(HEX[(b & 0x0F) as usize] as char);
    }
    out
}

fn hex_to_bytes(hex: &str) -> Result<Vec<u8>, String> {
    let trimmed = hex.trim();
    if trimmed.len() % 2 != 0 {
        return Err("Invalid DPAPI hex length".into());
    }
    let mut out = Vec::with_capacity(trimmed.len() / 2);
    let raw = trimmed.as_bytes();
    for pair in raw.chunks_exact(2) {
        let hi = (pair[0] as char)
            .to_digit(16)
            .ok_or_else(|| "Invalid DPAPI hex digit".to_string())?;
        let lo = (pair[1] as char)
            .to_digit(16)
            .ok_or_else(|| "Invalid DPAPI hex digit".to_string())?;
        out.push(((hi << 4) | lo) as u8);
    }
    Ok(out)
}

pub fn dpapi_encrypt(plaintext: &str) -> Result<String, String> {
    if plaintext.is_empty() {
        return Ok(String::new());
    }
    let mut in_bytes = plaintext.as_bytes().to_vec();
    let mut ent_bytes = DPAPI_ENTROPY.to_vec();
    let blob_in = CRYPT_INTEGER_BLOB {
        cbData: in_bytes.len() as u32,
        pbData: in_bytes.as_mut_ptr(),
    };
    let blob_ent = CRYPT_INTEGER_BLOB {
        cbData: ent_bytes.len() as u32,
        pbData: ent_bytes.as_mut_ptr(),
    };
    let mut blob_out = CRYPT_INTEGER_BLOB {
        cbData: 0,
        pbData: null_mut(),
    };

    let ok = unsafe {
        CryptProtectData(
            &blob_in,
            null(),
            &blob_ent,
            null(),
            null(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut blob_out,
        )
    };
    if ok == 0 || blob_out.pbData.is_null() {
        return Err("CryptProtectData failed".into());
    }

    let hex = unsafe {
        let slice = std::slice::from_raw_parts(blob_out.pbData, blob_out.cbData as usize);
        let h = bytes_to_hex(slice);
        LocalFree(blob_out.pbData as _);
        h
    };
    Ok(format!("{}{}", DPAPI_PREFIX, hex))
}

pub fn dpapi_decrypt(cipher: &str) -> Result<String, String> {
    let Some(hex_part) = cipher.strip_prefix(DPAPI_PREFIX) else {
        return Ok(cipher.to_string());
    };
    let mut in_bytes = hex_to_bytes(hex_part)?;
    if in_bytes.is_empty() {
        return Ok(String::new());
    }
    let mut ent_bytes = DPAPI_ENTROPY.to_vec();
    let blob_in = CRYPT_INTEGER_BLOB {
        cbData: in_bytes.len() as u32,
        pbData: in_bytes.as_mut_ptr(),
    };
    let blob_ent = CRYPT_INTEGER_BLOB {
        cbData: ent_bytes.len() as u32,
        pbData: ent_bytes.as_mut_ptr(),
    };
    let mut blob_out = CRYPT_INTEGER_BLOB {
        cbData: 0,
        pbData: null_mut(),
    };

    let ok = unsafe {
        CryptUnprotectData(
            &blob_in,
            null_mut(),
            &blob_ent,
            null(),
            null(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut blob_out,
        )
    };
    if ok == 0 || blob_out.pbData.is_null() {
        return Err("CryptUnprotectData failed".into());
    }

    let plain = unsafe {
        let slice = std::slice::from_raw_parts(blob_out.pbData, blob_out.cbData as usize);
        let s = String::from_utf8(slice.to_vec())
            .map_err(|e| format!("DPAPI UTF-8 decode error: {}", e));
        LocalFree(blob_out.pbData as _);
        s
    }?;
    Ok(plain)
}

pub fn write_file_atomic(path: &Path, bytes: &[u8]) -> Result<(), String> {
    if let Some(parent) = path.parent() {
        if !parent.as_os_str().is_empty() && !parent.exists() {
            fs::create_dir_all(parent)
                .map_err(|e| format!("สร้างโฟลเดอร์ไม่สำเร็จ: {}", e))?;
        }
    }
    let file_name = path
        .file_name()
        .and_then(|n| n.to_str())
        .unwrap_or("config.cfg");
    let tmp_path = path.with_file_name(format!("{}.tmp.{}", file_name, std::process::id()));
    {
        let mut f = File::create(&tmp_path)
            .map_err(|e| format!("สร้างไฟล์ชั่วคราวไม่สำเร็จ: {}", e))?;
        f.write_all(bytes)
            .map_err(|e| format!("เขียนข้อมูลไฟล์ชั่วคราวไม่สำเร็จ: {}", e))?;
        let _ = f.sync_data();
    }

    let src_w = win_ro::to_wide(&tmp_path.to_string_lossy());
    let dst_w = win_ro::to_wide(&path.to_string_lossy());
    let ok = unsafe {
        MoveFileExW(
            src_w.as_ptr(),
            dst_w.as_ptr(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH,
        )
    };
    if ok == 0 {
        let _ = fs::remove_file(&tmp_path);
        return Err(format!("ย้ายไฟล์คอนฟิกเข้าที่จริงไม่สำเร็จ ({})", path.display()));
    }
    Ok(())
}

fn parse_legacy_server_line(val: &str) -> Option<SavedServerEntry> {
    let mut parts = val.splitn(3, '|');
    let ip = parts.next()?.trim();
    let port = parts.next()?.trim();
    if ip.is_empty() || port.is_empty() {
        return None;
    }
    let name = parts.next().map(|s| s.trim()).unwrap_or("").to_string();
    Some(SavedServerEntry {
        ip: ip.to_string(),
        port: port.to_string(),
        name,
        join_count: 1,
        last_joined_at: 0,
    })
}

fn read_alt_tab_nopause_ini(game_dir: &Path) -> Option<bool> {
    let ws_ini = game_dir.join("scripts").join("GTASA.WidescreenFix.ini");
    let raw = fs::read_to_string(&ws_ini).ok()?;
    for line in raw.lines() {
        let t = line.trim().trim_start_matches('\u{feff}');
        if t.starts_with(';') || t.starts_with('#') || t.starts_with('[') {
            continue;
        }
        if let Some((k, v)) = t.split_once('=') {
            if k.trim().eq_ignore_ascii_case("AllowAltTabbingWithoutPausing") {
                let vt = v.split(';').next().unwrap_or(v).trim();
                return Some(vt == "1" || vt.eq_ignore_ascii_case("true"));
            }
        }
    }
    None
}

pub fn load_launcher_config(cfg_path: &Path) -> Result<LauncherConfig, String> {
    let mut cfg = LauncherConfig::default();
    let mut legacy_favs: Vec<SavedServerEntry> = Vec::new();
    let mut legacy_hist: Vec<SavedServerEntry> = Vec::new();
    let mut saw_plaintext_rcon = false;

    if cfg_path.exists() {
        let raw = fs::read_to_string(cfg_path)
            .map_err(|e| format!("อ่านไฟล์ตั้งค่าไม่ได้: {}", e))?;
        for line in raw.lines() {
            let trimmed = line.trim().trim_start_matches('\u{feff}');
            if trimmed.is_empty() {
                continue;
            }
            let Some((k, v)) = trimmed.split_once('=') else {
                continue;
            };
            let key = k.trim();
            let val = v.trim();
            if key.eq_ignore_ascii_case("nick") {
                cfg.nick = val.to_string();
            } else if key.eq_ignore_ascii_case("accent") {
                cfg.accent = val.to_string();
            } else if key.eq_ignore_ascii_case("gamepath") {
                cfg.game_path = val.to_string();
            } else if key.eq_ignore_ascii_case("autoc") {
                cfg.auto_connect = val == "1";
            } else if key.eq_ignore_ascii_case("closeafter") {
                cfg.close_after_launch = val == "1";
            } else if key.eq_ignore_ascii_case("last") {
                if let Some((ip, port)) = val.split_once('|') {
                    cfg.last_ip = ip.trim().to_string();
                    cfg.last_port = port.trim().to_string();
                }
            } else if key.eq_ignore_ascii_case("rconip") {
                cfg.rcon_ip = val.to_string();
            } else if key.eq_ignore_ascii_case("rconport") {
                cfg.rcon_port = val.to_string();
            } else if key.eq_ignore_ascii_case("rconpass") {
                if val.starts_with(DPAPI_PREFIX) {
                    cfg.rcon_pass = dpapi_decrypt(val).unwrap_or_default();
                } else if !val.is_empty() {
                    cfg.rcon_pass = val.to_string();
                    saw_plaintext_rcon = true;
                }
            } else if key.eq_ignore_ascii_case("sortcol") {
                if let Ok(sc) = val.parse::<i32>() {
                    if (0..=4).contains(&sc) {
                        cfg.sort_col = sc;
                    }
                }
            } else if key.eq_ignore_ascii_case("sortdesc") {
                cfg.sort_desc = val == "1";
            } else if key.eq_ignore_ascii_case("srv") {
                if let Some(entry) = parse_legacy_server_line(val) {
                    legacy_favs.push(entry);
                }
            } else if key.eq_ignore_ascii_case("hist") {
                if let Some(entry) = parse_legacy_server_line(val) {
                    legacy_hist.push(entry);
                }
            } else if key.eq_ignore_ascii_case("nick_prof") {
                if !val.is_empty() && !cfg.nick_profiles.contains(&val.to_string()) {
                    cfg.nick_profiles.push(val.to_string());
                }
            } else if key.eq_ignore_ascii_case("skygfx_preset") {
                if !val.is_empty() {
                    cfg.skygfx_preset = val.to_ascii_lowercase();
                }
            } else if key.eq_ignore_ascii_case("fps_limit") {
                if let Ok(fps) = val.parse::<i32>() {
                    cfg.fps_limit = fps.max(0);
                }
            } else if key.eq_ignore_ascii_case("autoslot") {
                cfg.auto_join_slot = val == "1";
            } else if key.eq_ignore_ascii_case("sound_fx") {
                cfg.sound_fx = val == "1";
            } else if key.eq_ignore_ascii_case("discord_rpc") {
                cfg.discord_rpc = val == "1";
            } else if key.eq_ignore_ascii_case("discord_app_id") {
                if !val.is_empty() {
                    cfg.discord_app_id = val.to_string();
                }
            } else if key.eq_ignore_ascii_case("nopause_alttab") {
                cfg.no_pause_alt_tab = val == "1";
            }
        }
    }

    let mut migrated_servers = false;
    if db::is_initialized() && (!legacy_favs.is_empty() || !legacy_hist.is_empty()) {
        if db::migrate_cfg_servers(&legacy_favs, &legacy_hist).is_ok() {
            migrated_servers = true;
        }
    }

    let effective_game_dir = if !cfg.game_path.trim().is_empty() && Path::new(&cfg.game_path).is_dir()
    {
        Some(Path::new(&cfg.game_path).to_path_buf())
    } else {
        cfg_path.parent().map(|p| p.to_path_buf())
    };
    if let Some(gd) = effective_game_dir {
        if let Some(ini_val) = read_alt_tab_nopause_ini(&gd) {
            cfg.no_pause_alt_tab = ini_val;
        }
    }

    if cfg_path.exists() && (saw_plaintext_rcon || migrated_servers) {
        let _ = save_launcher_config(cfg_path, &cfg);
    }

    Ok(cfg)
}

pub fn save_launcher_config(cfg_path: &Path, cfg: &LauncherConfig) -> Result<(), String> {
    let mut out = String::with_capacity(512);
    let clean_line = |s: &str| s.replace(['\r', '\n'], "").trim().to_string();

    let nick = clean_line(&cfg.nick);
    if !nick.is_empty() {
        out.push_str(&format!("nick={}\r\n", nick));
    }
    let accent = clean_line(&cfg.accent);
    if !accent.is_empty() {
        out.push_str(&format!("accent={}\r\n", accent));
    }
    let game_path = clean_line(&cfg.game_path);
    if !game_path.is_empty() {
        out.push_str(&format!("gamepath={}\r\n", game_path));
    }
    out.push_str(&format!(
        "autoc={}\r\n",
        if cfg.auto_connect { "1" } else { "0" }
    ));
    out.push_str(&format!(
        "closeafter={}\r\n",
        if cfg.close_after_launch { "1" } else { "0" }
    ));

    let last_ip = clean_line(&cfg.last_ip);
    let last_port = clean_line(&cfg.last_port);
    if !last_ip.is_empty() && !last_port.is_empty() {
        out.push_str(&format!("last={}|{}\r\n", last_ip, last_port));
    }

    let rcon_ip = clean_line(&cfg.rcon_ip);
    let rcon_port = clean_line(&cfg.rcon_port);
    if !rcon_ip.is_empty() {
        out.push_str(&format!("rconip={}\r\n", rcon_ip));
    }
    if !rcon_port.is_empty() {
        out.push_str(&format!("rconport={}\r\n", rcon_port));
    }
    if !cfg.rcon_pass.is_empty() {
        let enc = dpapi_encrypt(&cfg.rcon_pass)?;
        out.push_str(&format!("rconpass={}\r\n", enc));
    }

    out.push_str(&format!("sortcol={}\r\n", cfg.sort_col.clamp(0, 4)));
    out.push_str(&format!(
        "sortdesc={}\r\n",
        if cfg.sort_desc { "1" } else { "0" }
    ));
    out.push_str(&format!("skygfx_preset={}\r\n", clean_line(&cfg.skygfx_preset)));
    out.push_str(&format!("fps_limit={}\r\n", cfg.fps_limit.max(0)));
    out.push_str(&format!(
        "autoslot={}\r\n",
        if cfg.auto_join_slot { "1" } else { "0" }
    ));
    out.push_str(&format!(
        "sound_fx={}\r\n",
        if cfg.sound_fx { "1" } else { "0" }
    ));
    out.push_str(&format!(
        "discord_rpc={}\r\n",
        if cfg.discord_rpc { "1" } else { "0" }
    ));
    let app_id = clean_line(&cfg.discord_app_id);
    if !app_id.is_empty() {
        out.push_str(&format!("discord_app_id={}\r\n", app_id));
    }
    out.push_str(&format!(
        "nopause_alttab={}\r\n",
        if cfg.no_pause_alt_tab { "1" } else { "0" }
    ));
    for prof in &cfg.nick_profiles {
        let p = clean_line(prof);
        if !p.is_empty() {
            out.push_str(&format!("nick_prof={}\r\n", p));
        }
    }

    write_file_atomic(cfg_path, out.as_bytes())
}

pub fn set_alt_tab_nopause(game_dir: &Path, enabled: bool) -> Result<(), String> {
    let ini_path = game_dir.join("scripts").join("GTASA.WidescreenFix.ini");
    if !ini_path.exists() {
        return Err("ไม่พบไฟล์ scripts\\GTASA.WidescreenFix.ini ในโฟลเดอร์เกม".into());
    }
    let val = if enabled { "1" } else { "0" };
    health::update_ini_keys(
        &ini_path,
        &[("AllowAltTabbingWithoutPausing", val)],
        true,
        Some("MAIN"),
    )?;
    Ok(())
}

pub fn reset_gta_sa_set() -> Result<bool, String> {
    let Some(uf) = win_ro::gta_user_files_dir() else {
        return Ok(false);
    };
    let set_path = uf.join("gta_sa.set");
    if !set_path.exists() {
        return Ok(false);
    }
    fs::remove_file(&set_path).map_err(|e| format!("ลบ gta_sa.set ไม่สำเร็จ: {}", e))?;
    Ok(true)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn temp_dir(tag: &str) -> std::path::PathBuf {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_cfg_{}_{}", tag, nanos));
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn test_dpapi_encrypt_decrypt_roundtrip() {
        let secret = "RconPass!ไทย#123";
        let enc = dpapi_encrypt(secret).expect("dpapi encrypt");
        assert!(enc.starts_with("dpapi:"));
        assert!(!enc.contains("RconPass"));
        let dec = dpapi_decrypt(&enc).expect("dpapi decrypt");
        assert_eq!(dec, secret);
    }

    #[test]
    fn test_config_atomic_save_load_and_plaintext_migration() {
        let dir = temp_dir("mig");
        let cfg_path = dir.join("samp_launcher.cfg");
        fs::write(
            &cfg_path,
            "\u{feff}nick=SoDa_TH\r\nrconpass=legacy_plain_pw\r\nfps_limit=144\r\n",
        )
        .unwrap();

        let loaded = load_launcher_config(&cfg_path).expect("load cfg");
        assert_eq!(loaded.nick, "SoDa_TH");
        assert_eq!(loaded.rcon_pass, "legacy_plain_pw");
        assert_eq!(loaded.fps_limit, 144);

        let disk_after = fs::read(&cfg_path).expect("read rewritten cfg");
        assert!(!disk_after.starts_with(&[0xEF, 0xBB, 0xBF]));
        let disk_text = String::from_utf8(disk_after).unwrap();
        assert!(disk_text.contains("rconpass=dpapi:"));
        assert!(!disk_text.contains("legacy_plain_pw"));

        let reloaded = load_launcher_config(&cfg_path).expect("reload dpapi cfg");
        assert_eq!(reloaded.rcon_pass, "legacy_plain_pw");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_set_alt_tab_nopause_writes_nobom_and_fails_when_missing() {
        let dir = temp_dir("alttab");
        assert!(set_alt_tab_nopause(&dir, true).is_err());

        fs::create_dir_all(dir.join("scripts")).unwrap();
        let ini = dir.join("scripts").join("GTASA.WidescreenFix.ini");
        fs::write(&ini, "[MAIN]\r\nAllowAltTabbingWithoutPausing = 0\r\n").unwrap();

        set_alt_tab_nopause(&dir, true).expect("update alt tab");
        let bytes = fs::read(&ini).unwrap();
        assert!(!bytes.starts_with(&[0xEF, 0xBB, 0xBF]));
        let text = String::from_utf8(bytes).unwrap();
        assert!(text.contains("AllowAltTabbingWithoutPausing = 1"));

        let _ = fs::remove_dir_all(&dir);
    }
}
