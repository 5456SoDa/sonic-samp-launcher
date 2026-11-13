use serde::Serialize;
use std::collections::{HashMap, HashSet};
use std::fs;
use std::path::Path;

#[derive(Debug, Clone, Serialize)]
pub struct ModLoaderFolder {
    pub name: String,
    pub enabled: bool,
    pub file_count: usize,
    pub total_bytes: u64,
    pub priority: u32,
}

#[derive(Debug, Clone, Serialize)]
pub struct ModLoaderState {
    pub installed: bool,
    pub profile: String,
    pub ignore_all: bool,
    pub folders: Vec<ModLoaderFolder>,
}

const DEFAULT_INI: &str = ";\r\n\
;           Mod Loader Folder Config File\r\n\
;\r\n\
[Folder.Config]\r\n\
Profile = Default                 ; Profile to be used to configure mods\r\n\
PriorityLimit = 100               ; Max allowed value in Profiles.*.Priority (minimum is 1)\r\n\
\r\n\
[Profiles.Default.Config]\r\n\
IgnoreAllMods  = false            ; Ignores all mods. This essentially disables mods loading.\r\n\
ExcludeAllMods = false            ; Excludes all mods from being loaded except the ones at IncludeMods list\r\n\
\r\n\
[Profiles.Default.Priority]\r\n\
\r\n\
[Profiles.Default.IgnoreFiles]\r\n\
\r\n\
[Profiles.Default.IgnoreMods]\r\n\
_ignore\r\n\
\r\n\
[Profiles.Default.IncludeMods]\r\n\
\r\n\
[Profiles.Default.ExclusiveMods]\r\n";

fn count_dir_stats(dir: &Path, depth: usize, files: &mut usize, bytes: &mut u64) {
    if depth == 0 || !dir.is_dir() {
        return;
    }
    let Ok(rd) = fs::read_dir(dir) else {
        return;
    };
    for e in rd.flatten() {
        let p = e.path();
        if p.is_dir() {
            count_dir_stats(&p, depth - 1, files, bytes);
        } else {
            let name = p
                .file_name()
                .and_then(|n| n.to_str())
                .map(|s| s.to_ascii_lowercase())
                .unwrap_or_default();
            if name != "readme.txt" && name != "thumbs.db" {
                *files += 1;
                *bytes += fs::metadata(&p).map(|m| m.len()).unwrap_or(0);
            }
        }
    }
}

fn read_ini_source(game_dir: &Path) -> String {
    let mod_dir = game_dir.join("modloader");
    let ini_path = mod_dir.join("modloader.ini");
    if let Ok(s) = fs::read_to_string(&ini_path) {
        return s;
    }
    let tmpl = mod_dir.join(".data").join("modloader.ini.0");
    if let Ok(s) = fs::read_to_string(&tmpl) {
        return s;
    }
    DEFAULT_INI.to_string()
}

fn strip_comment(line: &str) -> &str {
    match line.find(';') {
        Some(idx) => line[..idx].trim(),
        None => line.trim(),
    }
}

fn parse_ini_state(content: &str) -> (String, bool, HashSet<String>, HashMap<String, u32>) {
    let mut profile = "Default".to_string();
    let mut cur_sec = String::new();

    for raw in content.lines() {
        let clean = strip_comment(raw);
        if clean.starts_with('[') && clean.ends_with(']') && clean.len() >= 2 {
            cur_sec = clean[1..clean.len() - 1].trim().to_string();
            continue;
        }
        if cur_sec.eq_ignore_ascii_case("Folder.Config") {
            if let Some(eq) = clean.find('=') {
                if clean[..eq].trim().eq_ignore_ascii_case("Profile") {
                    let v = clean[eq + 1..].trim();
                    if !v.is_empty() {
                        profile = v.to_string();
                    }
                }
            }
        }
    }

    let cfg_sec = format!("Profiles.{}.Config", profile);
    let ign_sec = format!("Profiles.{}.IgnoreMods", profile);
    let prio_sec = format!("Profiles.{}.Priority", profile);
    let mut ignore_all = false;
    let mut ignored = HashSet::new();
    let mut priorities = HashMap::new();
    cur_sec.clear();

    for raw in content.lines() {
        let clean = strip_comment(raw);
        if clean.is_empty() {
            continue;
        }
        if clean.starts_with('[') && clean.ends_with(']') && clean.len() >= 2 {
            cur_sec = clean[1..clean.len() - 1].trim().to_string();
            continue;
        }
        if cur_sec.eq_ignore_ascii_case(&cfg_sec) {
            if let Some(eq) = clean.find('=') {
                let k = clean[..eq].trim();
                let v = clean[eq + 1..].trim();
                if k.eq_ignore_ascii_case("IgnoreAllMods") || k.eq_ignore_ascii_case("IgnoreAllFiles") {
                    ignore_all = v.eq_ignore_ascii_case("true") || v == "1";
                }
            }
        } else if cur_sec.eq_ignore_ascii_case(&ign_sec) {
            ignored.insert(clean.to_ascii_lowercase());
        } else if cur_sec.eq_ignore_ascii_case(&prio_sec) {
            if let Some(eq) = clean.find('=') {
                let k = clean[..eq].trim().to_ascii_lowercase();
                if let Ok(p) = clean[eq + 1..].trim().parse::<u32>() {
                    if !k.is_empty() {
                        priorities.insert(k, p.clamp(1, 100));
                    }
                }
            }
        }
    }

    (profile, ignore_all, ignored, priorities)
}

pub fn list_modloader_state(game_dir: &Path) -> Result<ModLoaderState, String> {
    let mod_dir = game_dir.join("modloader");
    if !mod_dir.is_dir() {
        return Ok(ModLoaderState {
            installed: false,
            profile: "Default".into(),
            ignore_all: false,
            folders: Vec::new(),
        });
    }

    let ini_text = read_ini_source(game_dir);
    let (profile, ignore_all, ignored, priorities) = parse_ini_state(&ini_text);

    let mut folders = Vec::new();
    let rd = fs::read_dir(&mod_dir).map_err(|e| format!("อ่าน modloader ไม่สำเร็จ: {}", e))?;
    for e in rd.flatten() {
        let p = e.path();
        if !p.is_dir() {
            continue;
        }
        let name = p
            .file_name()
            .and_then(|n| n.to_str())
            .unwrap_or_default()
            .to_string();
        if name.starts_with('.') || name.eq_ignore_ascii_case("_ignore") {
            continue;
        }
        let mut fc = 0usize;
        let mut tb = 0u64;
        count_dir_stats(&p, 6, &mut fc, &mut tb);
        let key = name.to_ascii_lowercase();
        let enabled = !ignored.contains(&key);
        let priority = priorities.get(&key).copied().unwrap_or(50);
        folders.push(ModLoaderFolder {
            name,
            enabled,
            file_count: fc,
            total_bytes: tb,
            priority,
        });
    }
    folders.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));

    Ok(ModLoaderState {
        installed: true,
        profile,
        ignore_all,
        folders,
    })
}

pub fn set_folder_priority(game_dir: &Path, folder_name: &str, priority: u32) -> Result<(), String> {
    let mod_dir = game_dir.join("modloader");
    if !mod_dir.is_dir() {
        return Err("ไม่พบโฟลเดอร์ modloader ในโฟลเดอร์เกม".into());
    }
    let clean_name = folder_name.trim();
    if clean_name.is_empty() {
        return Err("ไม่ได้ระบุชื่อโฟลเดอร์ม็อด".into());
    }
    let prio = priority.clamp(1, 100);

    let raw = read_ini_source(game_dir);
    let (profile, _, _, _) = parse_ini_state(&raw);
    let prio_sec = format!("Profiles.{}.Priority", profile);

    let mut out_lines: Vec<String> = Vec::new();
    let mut cur_sec = String::new();
    let mut has_prio_sec = false;
    let mut wrote_entry = false;

    for orig in raw.lines() {
        let clean = strip_comment(orig);
        if clean.starts_with('[') && clean.ends_with(']') && clean.len() >= 2 {
            if cur_sec.eq_ignore_ascii_case(&prio_sec) && !wrote_entry {
                out_lines.push(format!("{} = {}", clean_name, prio));
                wrote_entry = true;
            }
            cur_sec = clean[1..clean.len() - 1].trim().to_string();
            if cur_sec.eq_ignore_ascii_case(&prio_sec) {
                has_prio_sec = true;
            }
            out_lines.push(orig.to_string());
            continue;
        }
        if cur_sec.eq_ignore_ascii_case(&prio_sec) {
            if let Some(eq) = clean.find('=') {
                if clean[..eq].trim().eq_ignore_ascii_case(clean_name) {
                    out_lines.push(format!("{} = {}", clean_name, prio));
                    wrote_entry = true;
                    continue;
                }
            }
        }
        out_lines.push(orig.to_string());
    }

    if cur_sec.eq_ignore_ascii_case(&prio_sec) && !wrote_entry {
        out_lines.push(format!("{} = {}", clean_name, prio));
        wrote_entry = true;
    }
    if !has_prio_sec || !wrote_entry {
        out_lines.push(String::new());
        out_lines.push(format!("[{}]", prio_sec));
        out_lines.push(format!("{} = {}", clean_name, prio));
    }

    let ini_path = mod_dir.join("modloader.ini");
    let final_text = out_lines.join("\r\n") + "\r\n";
    fs::write(&ini_path, final_text)
        .map_err(|e| format!("บันทึก priority ใน modloader.ini ไม่สำเร็จ: {}", e))?;
    Ok(())
}

pub fn save_modloader_state(
    game_dir: &Path,
    ignore_all: bool,
    disabled_mods: &[String],
) -> Result<(), String> {
    let mod_dir = game_dir.join("modloader");
    if !mod_dir.is_dir() {
        return Err("ไม่พบโฟลเดอร์ modloader ในโฟลเดอร์เกม".into());
    }

    let raw = read_ini_source(game_dir);
    let (profile, _, _, _) = parse_ini_state(&raw);
    let cfg_sec = format!("Profiles.{}.Config", profile);
    let ign_sec = format!("Profiles.{}.IgnoreMods", profile);

    let mut out_lines: Vec<String> = Vec::new();
    let mut cur_sec = String::new();
    let mut wrote_ignore_all = false;
    let mut has_cfg_sec = false;
    let mut has_ign_sec = false;
    let mut skipping_ign_entries = false;

    for orig in raw.lines() {
        let clean = strip_comment(orig);
        if clean.starts_with('[') && clean.ends_with(']') && clean.len() >= 2 {
            if cur_sec.eq_ignore_ascii_case(&cfg_sec) && !wrote_ignore_all {
                out_lines.push(format!("IgnoreAllMods  = {}", ignore_all));
                wrote_ignore_all = true;
            }
            cur_sec = clean[1..clean.len() - 1].trim().to_string();
            skipping_ign_entries = false;
            out_lines.push(orig.to_string());

            if cur_sec.eq_ignore_ascii_case(&cfg_sec) {
                has_cfg_sec = true;
            } else if cur_sec.eq_ignore_ascii_case(&ign_sec) {
                has_ign_sec = true;
                out_lines.push("_ignore".to_string());
                for m in disabled_mods {
                    let t = m.trim();
                    if !t.is_empty() && !t.eq_ignore_ascii_case("_ignore") {
                        out_lines.push(t.to_string());
                    }
                }
                skipping_ign_entries = true;
            }
            continue;
        }

        if skipping_ign_entries {
            // Preserve comments inside IgnoreMods section, drop old mod entries
            if orig.trim_start().starts_with(';') {
                out_lines.push(orig.to_string());
            }
            continue;
        }

        if cur_sec.eq_ignore_ascii_case(&cfg_sec) {
            if let Some(eq) = clean.find('=') {
                if clean[..eq].trim().eq_ignore_ascii_case("IgnoreAllMods") {
                    out_lines.push(format!("IgnoreAllMods  = {}", ignore_all));
                    wrote_ignore_all = true;
                    continue;
                }
            }
        }

        out_lines.push(orig.to_string());
    }

    if cur_sec.eq_ignore_ascii_case(&cfg_sec) && !wrote_ignore_all {
        out_lines.push(format!("IgnoreAllMods  = {}", ignore_all));
        wrote_ignore_all = true;
    }
    if !has_cfg_sec {
        out_lines.push(String::new());
        out_lines.push(format!("[{}]", cfg_sec));
        out_lines.push(format!("IgnoreAllMods  = {}", ignore_all));
    } else if !wrote_ignore_all {
        // Already appended above if at EOF; if mid-file without key, append safe section tail
        out_lines.push(String::new());
        out_lines.push(format!("[{}]", cfg_sec));
        out_lines.push(format!("IgnoreAllMods  = {}", ignore_all));
    }
    if !has_ign_sec {
        out_lines.push(String::new());
        out_lines.push(format!("[{}]", ign_sec));
        out_lines.push("_ignore".to_string());
        for m in disabled_mods {
            let t = m.trim();
            if !t.is_empty() && !t.eq_ignore_ascii_case("_ignore") {
                out_lines.push(t.to_string());
            }
        }
    }

    let ini_path = mod_dir.join("modloader.ini");
    let final_text = out_lines.join("\r\n") + "\r\n";
    fs::write(&ini_path, final_text)
        .map_err(|e| format!("บันทึก modloader.ini ไม่สำเร็จ: {}", e))?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn test_modloader_roundtrip_preserves_other_sections() {
        let nanos = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_ml_{}", nanos));
        fs::create_dir_all(dir.join("modloader").join("Maps")).unwrap();
        fs::create_dir_all(dir.join("modloader").join("Vehicles")).unwrap();
        fs::write(dir.join("modloader").join("Vehicles").join("car.dff"), b"1234").unwrap();

        let s0 = list_modloader_state(&dir).unwrap();
        assert!(s0.installed);
        assert!(!s0.ignore_all);
        assert_eq!(s0.folders.len(), 2);
        assert!(s0.folders.iter().all(|f| f.enabled));

        save_modloader_state(&dir, true, &["Vehicles".to_string()]).unwrap();
        let s1 = list_modloader_state(&dir).unwrap();
        assert!(s1.ignore_all);
        let veh = s1.folders.iter().find(|f| f.name == "Vehicles").unwrap();
        let maps = s1.folders.iter().find(|f| f.name == "Maps").unwrap();
        assert!(!veh.enabled);
        assert!(maps.enabled);
        assert_eq!(veh.file_count, 1);
        assert_eq!(veh.priority, 50);

        set_folder_priority(&dir, "Vehicles", 85).unwrap();
        let s2 = list_modloader_state(&dir).unwrap();
        let veh2 = s2.folders.iter().find(|f| f.name == "Vehicles").unwrap();
        assert_eq!(veh2.priority, 85);

        let _ = fs::remove_dir_all(&dir);
    }
}
