use crate::health::sha256_bytes;
use serde::{Deserialize, Serialize};
use std::collections::HashSet;
use std::fs::{self, File};
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::time::{SystemTime, UNIX_EPOCH};
use windows_sys::Win32::Foundation::SYSTEMTIME;
use windows_sys::Win32::System::SystemInformation::GetLocalTime;

const MAX_MODLOADER_FILE_BYTES: u64 = 32 * 1024 * 1024;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SnapshotFileEntry {
    pub rel_path: String,
    pub size: u64,
    pub sha256: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SnapshotManifest {
    pub id: String,
    pub name: String,
    pub created_at: String,
    pub total_raw_bytes: u64,
    pub total_compressed_bytes: u64,
    pub files: Vec<SnapshotFileEntry>,
    #[serde(default)]
    pub skipped_large_count: usize,
    #[serde(default)]
    pub skipped_large_files: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct SnapshotSummary {
    pub id: String,
    pub name: String,
    pub created_at: String,
    pub file_count: usize,
    pub total_raw_bytes: u64,
    pub total_compressed_bytes: u64,
    pub skipped_large_count: usize,
    pub skipped_large_files: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct RestoreSummary {
    pub id: String,
    pub name: String,
    pub restored_files: usize,
    pub pre_restore_id: String,
}

fn backup_root(game_dir: &Path) -> PathBuf {
    game_dir.join("_mod_backup")
}

fn blobs_dir(game_dir: &Path) -> PathBuf {
    backup_root(game_dir).join("blobs")
}

fn snapshots_dir(game_dir: &Path) -> PathBuf {
    backup_root(game_dir).join("snapshots")
}

fn local_timestamp_pair() -> (String, String) {
    let mut st: SYSTEMTIME = unsafe { std::mem::zeroed() };
    unsafe { GetLocalTime(&mut st) };
    let year = if st.wYear >= 2400 {
        st.wYear - 543
    } else {
        st.wYear
    };
    let ms = st.wMilliseconds;
    let id = format!(
        "{:04}{:02}{:02}_{:02}{:02}{:02}_{:03}",
        year, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, ms
    );
    let label = format!(
        "{:04}-{:02}-{:02} {:02}:{:02}:{:02}",
        year, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond
    );
    (id, label)
}

fn should_backup_root(name: &str) -> bool {
    let lower = name.to_ascii_lowercase();
    lower.ends_with(".ini")
        || lower.ends_with(".asi")
        || lower.ends_with(".cleo")
        || lower == "samp.cfg"
}

fn should_backup_scripts_or_cleo(name: &str) -> bool {
    let lower = name.to_ascii_lowercase();
    lower.ends_with(".ini")
        || lower.ends_with(".asi")
        || lower.ends_with(".cleo")
        || lower.ends_with(".cs")
        || lower.ends_with(".cm")
}

struct CollectedTargets {
    files: Vec<PathBuf>,
    skipped_large_count: usize,
    skipped_large_files: Vec<String>,
}

fn walk_collect(
    game_dir: &Path,
    dir: &Path,
    max_depth: usize,
    allow_zero: bool,
    eligible: &dyn Fn(&Path) -> bool,
    out: &mut CollectedTargets,
) {
    if max_depth == 0 || !dir.is_dir() {
        return;
    }
    let Ok(entries) = fs::read_dir(dir) else {
        return;
    };
    let mut paths: Vec<PathBuf> = entries.flatten().map(|e| e.path()).collect();
    paths.sort();
    for p in paths {
        if p.is_dir() {
            if max_depth > 1 {
                walk_collect(game_dir, &p, max_depth - 1, allow_zero, eligible, out);
            }
        } else if eligible(&p) {
            if let Ok(meta) = fs::metadata(&p) {
                let sz = meta.len();
                if sz > MAX_MODLOADER_FILE_BYTES {
                    out.skipped_large_count += 1;
                    if out.skipped_large_files.len() < 10 {
                        let rel = p
                            .strip_prefix(game_dir)
                            .unwrap_or(&p)
                            .to_string_lossy()
                            .replace('/', "\\");
                        out.skipped_large_files.push(rel);
                    }
                } else if sz > 0 || allow_zero {
                    out.files.push(p);
                }
            }
        }
    }
}

fn collect_snapshot_targets(game_dir: &Path) -> CollectedTargets {
    let mut out = CollectedTargets {
        files: Vec::new(),
        skipped_large_count: 0,
        skipped_large_files: Vec::new(),
    };
    walk_collect(
        game_dir,
        game_dir,
        1,
        true,
        &|p| {
            p.file_name()
                .and_then(|n| n.to_str())
                .map(should_backup_root)
                .unwrap_or(false)
        },
        &mut out,
    );
    walk_collect(
        game_dir,
        &game_dir.join("scripts"),
        3,
        true,
        &|p| {
            p.file_name()
                .and_then(|n| n.to_str())
                .map(should_backup_scripts_or_cleo)
                .unwrap_or(false)
        },
        &mut out,
    );
    walk_collect(
        game_dir,
        &game_dir.join("CLEO"),
        3,
        true,
        &|p| {
            p.file_name()
                .and_then(|n| n.to_str())
                .map(should_backup_scripts_or_cleo)
                .unwrap_or(false)
        },
        &mut out,
    );
    walk_collect(
        game_dir,
        &game_dir.join("modloader"),
        6,
        false,
        &|p| {
            let lower = p
                .file_name()
                .and_then(|n| n.to_str())
                .map(|s| s.to_ascii_lowercase())
                .unwrap_or_default();
            !lower.ends_with(".log") && !lower.ends_with(".tmp")
        },
        &mut out,
    );
    out
}

pub fn create_snapshot(game_dir: &Path, name: &str) -> Result<SnapshotSummary, String> {
    if !game_dir.is_dir() {
        return Err("ไม่พบโฟลเดอร์เกม".into());
    }
    let collected = collect_snapshot_targets(game_dir);
    if collected.files.is_empty() {
        return Err("ไม่พบไฟล์ .ini / .asi / .cleo / modloader สำหรับสำรอง".into());
    }

    let blobs = blobs_dir(game_dir);
    let snaps = snapshots_dir(game_dir);
    fs::create_dir_all(&blobs).map_err(|e| format!("สร้าง blobs dir ไม่สำเร็จ: {}", e))?;
    fs::create_dir_all(&snaps).map_err(|e| format!("สร้าง snapshots dir ไม่สำเร็จ: {}", e))?;

    let (mut id, created_at) = local_timestamp_pair();
    if snaps.join(format!("{}.json", id)).exists() {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .map(|d| d.subsec_nanos() % 1000)
            .unwrap_or(0);
        id = format!("{}_{:03}", id, nanos);
    }

    let display_name = if name.trim().is_empty() {
        format!("Snapshot {}", created_at)
    } else {
        name.trim().to_string()
    };

    let mut entries = Vec::with_capacity(collected.files.len());
    let mut total_raw = 0u64;
    let mut total_comp = 0u64;

    for full in collected.files {
        let Ok(raw) = fs::read(&full) else {
            continue;
        };
        let hash = sha256_bytes(&raw);
        let blob_path = blobs.join(format!("{}.zst", hash));
        let comp_len = if blob_path.exists() {
            fs::metadata(&blob_path).map(|m| m.len()).unwrap_or(0)
        } else {
            let compressed = zstd::stream::encode_all(raw.as_slice(), 3)
                .map_err(|e| format!("Zstd encode error: {}", e))?;
            let tmp_blob = blobs.join(format!("{}.zst.tmp", hash));
            fs::write(&tmp_blob, &compressed).map_err(|e| format!("เขียน blob ไม่สำเร็จ: {}", e))?;
            let _ = fs::rename(&tmp_blob, &blob_path);
            compressed.len() as u64
        };

        total_raw += raw.len() as u64;
        total_comp += comp_len;

        let rel_path = full
            .strip_prefix(game_dir)
            .unwrap_or(&full)
            .to_string_lossy()
            .replace('/', "\\");
        entries.push(SnapshotFileEntry {
            rel_path,
            size: raw.len() as u64,
            sha256: hash,
        });
    }

    let file_count = entries.len();
    let manifest = SnapshotManifest {
        id: id.clone(),
        name: display_name.clone(),
        created_at: created_at.clone(),
        total_raw_bytes: total_raw,
        total_compressed_bytes: total_comp,
        files: entries,
        skipped_large_count: collected.skipped_large_count,
        skipped_large_files: collected.skipped_large_files.clone(),
    };

    let manifest_json =
        serde_json::to_string_pretty(&manifest).map_err(|e| format!("JSON error: {}", e))?;
    let manifest_path = snaps.join(format!("{}.json", id));
    fs::write(&manifest_path, manifest_json)
        .map_err(|e| format!("บันทึก snapshot manifest ไม่สำเร็จ: {}", e))?;

    Ok(SnapshotSummary {
        id,
        name: display_name,
        created_at,
        file_count,
        total_raw_bytes: total_raw,
        total_compressed_bytes: total_comp,
        skipped_large_count: collected.skipped_large_count,
        skipped_large_files: collected.skipped_large_files,
    })
}

pub fn list_snapshots(game_dir: &Path) -> Result<Vec<SnapshotSummary>, String> {
    let snaps = snapshots_dir(game_dir);
    if !snaps.exists() {
        return Ok(Vec::new());
    }
    let mut out = Vec::new();
    let rd = fs::read_dir(&snaps).map_err(|e| format!("อ่านโฟลเดอร์ snapshots ไม่สำเร็จ: {}", e))?;
    for entry in rd.flatten() {
        let p = entry.path();
        if p.extension().and_then(|e| e.to_str()) != Some("json") {
            continue;
        }
        let Ok(raw) = fs::read_to_string(&p) else {
            continue;
        };
        if let Ok(m) = serde_json::from_str::<SnapshotManifest>(&raw) {
            out.push(SnapshotSummary {
                id: m.id,
                name: m.name,
                created_at: m.created_at,
                file_count: m.files.len(),
                total_raw_bytes: m.total_raw_bytes,
                total_compressed_bytes: m.total_compressed_bytes,
                skipped_large_count: m.skipped_large_count,
                skipped_large_files: m.skipped_large_files,
            });
        }
    }
    out.sort_by(|a, b| b.id.cmp(&a.id));
    Ok(out)
}

pub fn restore_snapshot(game_dir: &Path, snapshot_id: &str) -> Result<RestoreSummary, String> {
    let clean_id = Path::new(snapshot_id)
        .file_name()
        .and_then(|n| n.to_str())
        .ok_or_else(|| "รหัส snapshot ไม่ถูกต้อง".to_string())?;
    let manifest_path = snapshots_dir(game_dir).join(format!("{}.json", clean_id));
    let raw_json = fs::read_to_string(&manifest_path)
        .map_err(|e| format!("ไม่พบ snapshot '{}': {}", clean_id, e))?;
    let manifest: SnapshotManifest =
        serde_json::from_str(&raw_json).map_err(|e| format!("Snapshot เสียหาย: {}", e))?;

    // Auto-backup current game state before applying restore (unless no target files exist)
    let pre_restore_id = match create_snapshot(
        game_dir,
        &format!("Auto Pre-Restore ({})", manifest.name),
    ) {
        Ok(s) => s.id,
        Err(_) => String::new(),
    };

    let blobs = blobs_dir(game_dir);
    let mut restored = 0usize;

    for item in &manifest.files {
        // Guard against path traversal in manifests
        let rel = Path::new(&item.rel_path);
        if rel.is_absolute()
            || rel
                .components()
                .any(|c| matches!(c, std::path::Component::ParentDir))
        {
            continue;
        }
        let blob_path = blobs.join(format!("{}.zst", item.sha256));
        let Ok(mut f) = File::open(&blob_path) else {
            continue;
        };
        let mut comp_bytes = Vec::new();
        if f.read_to_end(&mut comp_bytes).is_err() {
            continue;
        }
        let Ok(plain) = zstd::stream::decode_all(comp_bytes.as_slice()) else {
            continue;
        };
        if sha256_bytes(&plain) != item.sha256 {
            return Err(format!("Checksum ไม่ตรงสำหรับไฟล์ {}", item.rel_path));
        }

        let dest = game_dir.join(rel);
        if let Some(parent) = dest.parent() {
            let _ = fs::create_dir_all(parent);
        }
        if let Ok(meta) = fs::metadata(&dest) {
            let mut perms = meta.permissions();
            if perms.readonly() {
                perms.set_readonly(false);
                let _ = fs::set_permissions(&dest, perms);
            }
        }
        let mut out_f =
            File::create(&dest).map_err(|e| format!("เขียนไฟล์ {} ไม่สำเร็จ: {}", item.rel_path, e))?;
        out_f
            .write_all(&plain)
            .map_err(|e| format!("เขียนไฟล์ {} ไม่สำเร็จ: {}", item.rel_path, e))?;
        restored += 1;
    }

    Ok(RestoreSummary {
        id: manifest.id,
        name: manifest.name,
        restored_files: restored,
        pre_restore_id,
    })
}

pub fn delete_snapshot(game_dir: &Path, snapshot_id: &str) -> Result<(), String> {
    let clean_id = Path::new(snapshot_id)
        .file_name()
        .and_then(|n| n.to_str())
        .ok_or_else(|| "รหัส snapshot ไม่ถูกต้อง".to_string())?;
    let snaps = snapshots_dir(game_dir);
    let target = snaps.join(format!("{}.json", clean_id));
    if !target.exists() {
        return Err("ไม่พบ snapshot ที่ต้องการลบ".into());
    }
    fs::remove_file(&target).map_err(|e| format!("ลบ snapshot ไม่สำเร็จ: {}", e))?;

    // Garbage-collect unreferenced blobs
    let mut keep_hashes = HashSet::new();
    if let Ok(rd) = fs::read_dir(&snaps) {
        for e in rd.flatten() {
            let p = e.path();
            if p.extension().and_then(|x| x.to_str()) == Some("json") {
                if let Ok(raw) = fs::read_to_string(&p) {
                    if let Ok(m) = serde_json::from_str::<SnapshotManifest>(&raw) {
                        for f in m.files {
                            keep_hashes.insert(f.sha256);
                        }
                    }
                }
            }
        }
    }
    if let Ok(rd) = fs::read_dir(blobs_dir(game_dir)) {
        for e in rd.flatten() {
            let p = e.path();
            if let Some(stem) = p.file_stem().and_then(|s| s.to_str()) {
                if !keep_hashes.contains(stem) {
                    let _ = fs::remove_file(&p);
                }
            }
        }
    }
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_game(tag: &str) -> PathBuf {
        let n = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_bkp_{}_{}", tag, n));
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn test_snapshot_dedup_compress_restore_and_gc() {
        let dir = temp_game("snap");
        fs::create_dir_all(dir.join("scripts")).unwrap();
        fs::create_dir_all(dir.join("modloader").join("my_mod")).unwrap();

        fs::write(dir.join("skygfx.ini"), b"[SkyGfx]\nmode=ps2\n").unwrap();
        fs::write(dir.join("scripts").join("FPS.asi"), vec![0xAA; 2048]).unwrap();
        fs::write(dir.join("modloader").join("my_mod").join("readme.txt"), b"hello").unwrap();

        let s1 = create_snapshot(&dir, "Before edit").unwrap();
        assert_eq!(s1.file_count, 3);
        assert!(s1.total_compressed_bytes < s1.total_raw_bytes);

        // Second snapshot with identical content reuses the same 3 blob files
        let s2 = create_snapshot(&dir, "Second").unwrap();
        let blob_count = fs::read_dir(blobs_dir(&dir)).unwrap().count();
        assert_eq!(blob_count, 3);
        assert_ne!(s1.id, s2.id);

        // Mutate files and delete readme, then restore s1 (auto pre-restore backup is created)
        fs::write(dir.join("skygfx.ini"), b"BROKEN").unwrap();
        fs::remove_file(dir.join("modloader").join("my_mod").join("readme.txt")).unwrap();
        let res = restore_snapshot(&dir, &s1.id).unwrap();
        assert_eq!(res.restored_files, 3);
        assert!(!res.pre_restore_id.is_empty());
        assert_eq!(
            fs::read_to_string(dir.join("skygfx.ini")).unwrap(),
            "[SkyGfx]\nmode=ps2\n"
        );
        assert_eq!(
            fs::read_to_string(dir.join("modloader").join("my_mod").join("readme.txt")).unwrap(),
            "hello"
        );

        let list = list_snapshots(&dir).unwrap();
        assert_eq!(list.len(), 3);

        // Deleting s1 & pre keeps blobs still referenced by s2; deleting s2 collects all blobs
        delete_snapshot(&dir, &s1.id).unwrap();
        delete_snapshot(&dir, &res.pre_restore_id).unwrap();
        assert_eq!(fs::read_dir(blobs_dir(&dir)).unwrap().count(), 3);
        delete_snapshot(&dir, &s2.id).unwrap();
        assert_eq!(fs::read_dir(blobs_dir(&dir)).unwrap().count(), 0);

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_snapshot_reports_skipped_large_files() {
        let dir = temp_game("large");
        fs::create_dir_all(dir.join("modloader").join("hd")).unwrap();
        fs::write(dir.join("skygfx.ini"), b"ok").unwrap();
        let big = dir.join("modloader").join("hd").join("huge.txd");
        let f = File::create(&big).unwrap();
        f.set_len(MAX_MODLOADER_FILE_BYTES + 1024).unwrap();
        drop(f);

        let snap = create_snapshot(&dir, "With huge").unwrap();
        assert_eq!(snap.file_count, 1);
        assert_eq!(snap.skipped_large_count, 1);
        assert_eq!(snap.skipped_large_files, vec!["modloader\\hd\\huge.txd"]);

        let _ = fs::remove_dir_all(&dir);
    }
}
