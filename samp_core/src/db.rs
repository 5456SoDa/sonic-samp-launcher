use rusqlite::{params, Connection};
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::fs;
use std::path::Path;
use std::sync::{Mutex, OnceLock};
use std::time::{SystemTime, UNIX_EPOCH};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
pub struct SavedServerEntry {
    pub ip: String,
    pub port: String,
    #[serde(default)]
    pub name: String,
    #[serde(default)]
    pub last_joined_at: i64,
    #[serde(default)]
    pub join_count: i64,
}

fn global_db() -> &'static Mutex<Option<Connection>> {
    static DB: OnceLock<Mutex<Option<Connection>>> = OnceLock::new();
    DB.get_or_init(|| Mutex::new(None))
}

pub fn normalize_server_key(ip: &str, port: &str) -> String {
    format!("{}|{}", ip.trim().to_ascii_lowercase(), port.trim())
}

fn now_epoch_secs() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0)
}

fn init_schema(conn: &Connection) -> Result<(), String> {
    conn.execute_batch(
        "PRAGMA busy_timeout = 3000;
         PRAGMA journal_mode = WAL;
         PRAGMA synchronous = NORMAL;
         CREATE TABLE IF NOT EXISTS playtime (
             server_key TEXT PRIMARY KEY,
             total_seconds INTEGER NOT NULL DEFAULT 0
         );
         CREATE TABLE IF NOT EXISTS server_stats (
             id INTEGER PRIMARY KEY AUTOINCREMENT,
             ip TEXT NOT NULL,
             port TEXT NOT NULL,
             recorded_at TEXT NOT NULL,
             players INTEGER NOT NULL
         );
         CREATE UNIQUE INDEX IF NOT EXISTS idx_server_stats_unique
             ON server_stats(ip, port, recorded_at, players);
         CREATE INDEX IF NOT EXISTS idx_server_stats_lookup
             ON server_stats(ip, port, id);
         CREATE TABLE IF NOT EXISTS favorites (
             server_key TEXT PRIMARY KEY,
             ip TEXT NOT NULL,
             port TEXT NOT NULL,
             name TEXT NOT NULL DEFAULT '',
             sort_order INTEGER NOT NULL DEFAULT 0,
             created_at INTEGER NOT NULL DEFAULT 0
         );
         CREATE TABLE IF NOT EXISTS history (
             server_key TEXT PRIMARY KEY,
             ip TEXT NOT NULL,
             port TEXT NOT NULL,
             name TEXT NOT NULL DEFAULT '',
             last_joined_at INTEGER NOT NULL DEFAULT 0,
             join_count INTEGER NOT NULL DEFAULT 1
         );
         CREATE INDEX IF NOT EXISTS idx_history_recent
             ON history(last_joined_at DESC);
         CREATE TABLE IF NOT EXISTS meta (
             key TEXT PRIMARY KEY,
             value TEXT NOT NULL
         );",
    )
    .map_err(|e| format!("DB schema error: {}", e))
}

/// Converts a Buddhist-era timestamp prefix (year >= 2400) to Gregorian.
pub fn normalize_timestamp(stamp: &str) -> String {
    let trimmed = stamp.trim();
    if trimmed.len() >= 5 && trimmed.as_bytes()[4] == b'-' {
        if let Ok(year) = trimmed[0..4].parse::<i32>() {
            if year >= 2400 {
                return format!("{:04}{}", year - 543, &trimmed[4..]);
            }
        }
    }
    trimmed.to_string()
}

fn is_meta_flag(conn: &Connection, key: &str) -> bool {
    conn.query_row(
        "SELECT value FROM meta WHERE key = ?1",
        params![key],
        |row| row.get::<_, String>(0),
    )
    .map(|v| v == "1")
    .unwrap_or(false)
}

pub fn migrate_legacy_files(
    conn: &mut Connection,
    playtime_txt: Option<&Path>,
    stats_txt: Option<&Path>,
) -> Result<(), String> {
    if is_meta_flag(conn, "migrated_v1") {
        return Ok(());
    }

    let tx = conn
        .transaction()
        .map_err(|e| format!("DB transaction error: {}", e))?;

    if let Some(pt_path) = playtime_txt {
        if pt_path.exists() {
            if let Ok(raw) = fs::read_to_string(pt_path) {
                let mut stmt = tx
                    .prepare(
                        "INSERT INTO playtime (server_key, total_seconds) VALUES (?1, ?2)
                         ON CONFLICT(server_key) DO UPDATE SET
                         total_seconds = MAX(playtime.total_seconds, excluded.total_seconds)",
                    )
                    .map_err(|e| e.to_string())?;
                for line in raw.lines() {
                    let s = line.trim().trim_start_matches('\u{feff}');
                    if s.is_empty() {
                        continue;
                    }
                    let parts: Vec<&str> = s.split('|').collect();
                    // Require at least 3 fields (ip|port|seconds); skip corrupted 2-part lines.
                    if parts.len() >= 3 {
                        if let Ok(secs) = parts[2].trim().parse::<i64>() {
                            if secs > 0 {
                                let key = normalize_server_key(parts[0], parts[1]);
                                stmt.execute(params![key, secs])
                                    .map_err(|e| e.to_string())?;
                            }
                        }
                    }
                }
            }
        }
    }

    if let Some(st_path) = stats_txt {
        if st_path.exists() {
            if let Ok(raw) = fs::read_to_string(st_path) {
                let mut stmt = tx
                    .prepare(
                        "INSERT OR IGNORE INTO server_stats (ip, port, recorded_at, players)
                         VALUES (?1, ?2, ?3, ?4)",
                    )
                    .map_err(|e| e.to_string())?;
                for line in raw.lines() {
                    let s = line.trim().trim_start_matches('\u{feff}');
                    if s.is_empty() {
                        continue;
                    }
                    let parts: Vec<&str> = s.split('|').collect();
                    if parts.len() >= 4 {
                        if let Ok(players) = parts[3].trim().parse::<i32>() {
                            let ip = parts[0].trim().to_ascii_lowercase();
                            let port = parts[1].trim();
                            let stamp = normalize_timestamp(parts[2]);
                            if !ip.is_empty() && !port.is_empty() && !stamp.is_empty() {
                                stmt.execute(params![ip, port, stamp, players])
                                    .map_err(|e| e.to_string())?;
                            }
                        }
                    }
                }
            }
        }
    }

    tx.execute(
        "INSERT OR REPLACE INTO meta (key, value) VALUES ('migrated_v1', '1')",
        [],
    )
    .map_err(|e| e.to_string())?;
    tx.commit().map_err(|e| format!("DB commit error: {}", e))?;
    Ok(())
}

pub fn migrate_v2_casefold(conn: &mut Connection) -> Result<(), String> {
    if is_meta_flag(conn, "migrated_v2_casefold") {
        return Ok(());
    }

    let mut folded: HashMap<String, i64> = HashMap::new();
    let mut needs_rewrite = false;
    {
        let mut stmt = conn
            .prepare("SELECT server_key, total_seconds FROM playtime")
            .map_err(|e| e.to_string())?;
        let rows = stmt
            .query_map([], |r| Ok((r.get::<_, String>(0)?, r.get::<_, i64>(1)?)))
            .map_err(|e| e.to_string())?;
        for item in rows {
            let (k, secs) = item.map_err(|e| e.to_string())?;
            let norm = if let Some((ip, port)) = k.split_once('|') {
                normalize_server_key(ip, port)
            } else {
                k.trim().to_ascii_lowercase()
            };
            if norm != k {
                needs_rewrite = true;
            }
            let e = folded.entry(norm).or_insert(0);
            *e = e.saturating_add(secs);
        }
    }

    let tx = conn
        .transaction()
        .map_err(|e| format!("DB casefold tx error: {}", e))?;
    if needs_rewrite {
        tx.execute("DELETE FROM playtime", [])
            .map_err(|e| e.to_string())?;
        let mut ins = tx
            .prepare("INSERT INTO playtime (server_key, total_seconds) VALUES (?1, ?2)")
            .map_err(|e| e.to_string())?;
        for (k, secs) in folded {
            ins.execute(params![k, secs]).map_err(|e| e.to_string())?;
        }
    }
    tx.execute(
        "INSERT OR REPLACE INTO meta (key, value) VALUES ('migrated_v2_casefold', '1')",
        [],
    )
    .map_err(|e| e.to_string())?;
    tx.commit().map_err(|e| e.to_string())
}

pub fn init_db(
    db_path: &Path,
    playtime_txt: Option<&Path>,
    stats_txt: Option<&Path>,
) -> Result<(), String> {
    if let Some(parent) = db_path.parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent).map_err(|e| format!("Create DB dir failed: {}", e))?;
        }
    }
    let mut conn = Connection::open(db_path).map_err(|e| format!("Open SQLite error: {}", e))?;
    init_schema(&conn)?;
    migrate_legacy_files(&mut conn, playtime_txt, stats_txt)?;
    migrate_v2_casefold(&mut conn)?;
    let mut guard = global_db().lock().map_err(|_| "DB mutex poisoned".to_string())?;
    *guard = Some(conn);
    Ok(())
}

pub fn is_initialized() -> bool {
    global_db()
        .lock()
        .map(|g| g.is_some())
        .unwrap_or(false)
}

fn with_conn<F, R>(f: F) -> Result<R, String>
where
    F: FnOnce(&mut Connection) -> Result<R, String>,
{
    let mut guard = global_db().lock().map_err(|_| "DB mutex poisoned".to_string())?;
    let Some(ref mut conn) = *guard else {
        return Err("Database not initialized".into());
    };
    f(conn)
}

pub fn add_playtime_on(conn: &Connection, server_key: &str, delta_seconds: i64) -> Result<i64, String> {
    let norm = if let Some((ip, port)) = server_key.split_once('|') {
        normalize_server_key(ip, port)
    } else {
        server_key.trim().to_ascii_lowercase()
    };
    if delta_seconds <= 0 {
        let cur: i64 = conn
            .query_row(
                "SELECT total_seconds FROM playtime WHERE server_key = ?1",
                params![norm],
                |r| r.get(0),
            )
            .unwrap_or(0);
        return Ok(cur);
    }
    conn.execute(
        "INSERT INTO playtime (server_key, total_seconds) VALUES (?1, ?2)
         ON CONFLICT(server_key) DO UPDATE SET
         total_seconds = playtime.total_seconds + excluded.total_seconds",
        params![norm, delta_seconds],
    )
    .map_err(|e| e.to_string())?;
    conn.query_row(
        "SELECT total_seconds FROM playtime WHERE server_key = ?1",
        params![norm],
        |r| r.get(0),
    )
    .map_err(|e| e.to_string())
}

pub fn get_all_playtime_on(conn: &Connection) -> Result<HashMap<String, i64>, String> {
    let mut stmt = conn
        .prepare("SELECT server_key, total_seconds FROM playtime")
        .map_err(|e| e.to_string())?;
    let rows = stmt
        .query_map([], |r| Ok((r.get::<_, String>(0)?, r.get::<_, i64>(1)?)))
        .map_err(|e| e.to_string())?;
    let mut map = HashMap::new();
    for item in rows {
        let (k, secs) = item.map_err(|e| e.to_string())?;
        let norm = if let Some((ip, port)) = k.split_once('|') {
            normalize_server_key(ip, port)
        } else {
            k.to_ascii_lowercase()
        };
        let entry = map.entry(norm).or_insert(0);
        *entry = (*entry).max(secs);
    }
    Ok(map)
}

pub fn record_stat_on(
    conn: &Connection,
    ip: &str,
    port: &str,
    recorded_at: &str,
    players: i32,
) -> Result<bool, String> {
    let norm_ip = ip.trim().to_ascii_lowercase();
    let norm_ts = normalize_timestamp(recorded_at);
    let changed = conn
        .execute(
            "INSERT OR IGNORE INTO server_stats (ip, port, recorded_at, players)
             VALUES (?1, ?2, ?3, ?4)",
            params![norm_ip, port.trim(), norm_ts, players],
        )
        .map_err(|e| e.to_string())?;
    Ok(changed > 0)
}

pub fn get_stats_on(conn: &Connection, ip: &str, port: &str, limit: usize) -> Result<Vec<i32>, String> {
    let cap = limit.clamp(1, 5000) as i64;
    let mut stmt = conn
        .prepare(
            "SELECT players FROM (
                 SELECT id, players FROM server_stats
                 WHERE LOWER(ip) = LOWER(?1) AND port = ?2
                 ORDER BY id DESC
                 LIMIT ?3
             ) ORDER BY id ASC",
        )
        .map_err(|e| e.to_string())?;
    let rows = stmt
        .query_map(params![ip.trim(), port.trim(), cap], |r| r.get::<_, i32>(0))
        .map_err(|e| e.to_string())?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r.map_err(|e| e.to_string())?);
    }
    Ok(out)
}

pub fn get_favorites_on(conn: &Connection) -> Result<Vec<SavedServerEntry>, String> {
    let mut stmt = conn
        .prepare(
            "SELECT ip, port, name, created_at FROM favorites
             ORDER BY sort_order ASC, rowid ASC",
        )
        .map_err(|e| e.to_string())?;
    let rows = stmt
        .query_map([], |r| {
            Ok(SavedServerEntry {
                ip: r.get(0)?,
                port: r.get(1)?,
                name: r.get(2)?,
                last_joined_at: r.get(3)?,
                join_count: 0,
            })
        })
        .map_err(|e| e.to_string())?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r.map_err(|e| e.to_string())?);
    }
    Ok(out)
}

pub fn upsert_favorite_on(
    conn: &Connection,
    ip: &str,
    port: &str,
    name: &str,
) -> Result<(), String> {
    let clean_ip = ip.trim();
    let clean_port = port.trim();
    if clean_ip.is_empty() || clean_port.is_empty() {
        return Err("IP หรือ Port ว่าง".into());
    }
    let key = normalize_server_key(clean_ip, clean_port);
    let next_order: i64 = conn
        .query_row(
            "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM favorites",
            [],
            |r| r.get(0),
        )
        .unwrap_or(0);
    conn.execute(
        "INSERT INTO favorites (server_key, ip, port, name, sort_order, created_at)
         VALUES (?1, ?2, ?3, ?4, ?5, ?6)
         ON CONFLICT(server_key) DO UPDATE SET
             ip = excluded.ip,
             port = excluded.port,
             name = CASE WHEN excluded.name <> '' THEN excluded.name ELSE favorites.name END",
        params![
            key,
            clean_ip,
            clean_port,
            name.trim(),
            next_order,
            now_epoch_secs()
        ],
    )
    .map_err(|e| e.to_string())?;
    Ok(())
}

pub fn remove_favorite_on(conn: &Connection, ip: &str, port: &str) -> Result<bool, String> {
    let key = normalize_server_key(ip, port);
    let n = conn
        .execute("DELETE FROM favorites WHERE server_key = ?1", params![key])
        .map_err(|e| e.to_string())?;
    Ok(n > 0)
}

pub fn get_history_on(conn: &Connection, limit: usize) -> Result<Vec<SavedServerEntry>, String> {
    let cap = limit.clamp(1, 500) as i64;
    let mut stmt = conn
        .prepare(
            "SELECT ip, port, name, last_joined_at, join_count FROM history
             ORDER BY last_joined_at DESC, rowid DESC
             LIMIT ?1",
        )
        .map_err(|e| e.to_string())?;
    let rows = stmt
        .query_map(params![cap], |r| {
            Ok(SavedServerEntry {
                ip: r.get(0)?,
                port: r.get(1)?,
                name: r.get(2)?,
                last_joined_at: r.get(3)?,
                join_count: r.get(4)?,
            })
        })
        .map_err(|e| e.to_string())?;
    let mut out = Vec::new();
    for r in rows {
        out.push(r.map_err(|e| e.to_string())?);
    }
    Ok(out)
}

pub fn record_history_on(
    conn: &Connection,
    ip: &str,
    port: &str,
    name: &str,
) -> Result<(), String> {
    let clean_ip = ip.trim();
    let clean_port = port.trim();
    if clean_ip.is_empty() || clean_port.is_empty() {
        return Err("IP หรือ Port ว่าง".into());
    }
    let key = normalize_server_key(clean_ip, clean_port);
    conn.execute(
        "INSERT INTO history (server_key, ip, port, name, last_joined_at, join_count)
         VALUES (?1, ?2, ?3, ?4, ?5, 1)
         ON CONFLICT(server_key) DO UPDATE SET
             ip = excluded.ip,
             port = excluded.port,
             name = CASE WHEN excluded.name <> '' THEN excluded.name ELSE history.name END,
             last_joined_at = excluded.last_joined_at,
             join_count = history.join_count + 1",
        params![key, clean_ip, clean_port, name.trim(), now_epoch_secs()],
    )
    .map_err(|e| e.to_string())?;

    // Keep most recent 150 history rows
    conn.execute(
        "DELETE FROM history WHERE server_key NOT IN (
             SELECT server_key FROM history ORDER BY last_joined_at DESC, rowid DESC LIMIT 150
         )",
        [],
    )
    .map_err(|e| e.to_string())?;
    Ok(())
}

pub fn remove_history_on(conn: &Connection, ip: &str, port: &str) -> Result<bool, String> {
    let key = normalize_server_key(ip, port);
    let n = conn
        .execute("DELETE FROM history WHERE server_key = ?1", params![key])
        .map_err(|e| e.to_string())?;
    Ok(n > 0)
}

pub fn update_server_name_on(
    conn: &Connection,
    ip: &str,
    port: &str,
    name: &str,
) -> Result<(), String> {
    let clean_name = name.trim();
    if clean_name.is_empty() {
        return Ok(());
    }
    let key = normalize_server_key(ip, port);
    conn.execute(
        "UPDATE favorites SET name = ?2 WHERE server_key = ?1 AND name <> ?2",
        params![key, clean_name],
    )
    .map_err(|e| e.to_string())?;
    conn.execute(
        "UPDATE history SET name = ?2 WHERE server_key = ?1 AND name <> ?2",
        params![key, clean_name],
    )
    .map_err(|e| e.to_string())?;
    Ok(())
}

pub fn migrate_cfg_servers_on(
    conn: &mut Connection,
    favs: &[SavedServerEntry],
    hist: &[SavedServerEntry],
) -> Result<bool, String> {
    if is_meta_flag(conn, "migrated_cfg_servers_v1") {
        return Ok(false);
    }
    let tx = conn.transaction().map_err(|e| e.to_string())?;
    let base_ts = now_epoch_secs();
    for (idx, f) in favs.iter().enumerate() {
        if f.ip.trim().is_empty() || f.port.trim().is_empty() {
            continue;
        }
        let key = normalize_server_key(&f.ip, &f.port);
        tx.execute(
            "INSERT OR IGNORE INTO favorites (server_key, ip, port, name, sort_order, created_at)
             VALUES (?1, ?2, ?3, ?4, ?5, ?6)",
            params![
                key,
                f.ip.trim(),
                f.port.trim(),
                f.name.trim(),
                idx as i64,
                base_ts
            ],
        )
        .map_err(|e| e.to_string())?;
    }
    for (idx, h) in hist.iter().enumerate() {
        if h.ip.trim().is_empty() || h.port.trim().is_empty() {
            continue;
        }
        let key = normalize_server_key(&h.ip, &h.port);
        let ts = (base_ts - idx as i64).max(1);
        tx.execute(
            "INSERT OR IGNORE INTO history (server_key, ip, port, name, last_joined_at, join_count)
             VALUES (?1, ?2, ?3, ?4, ?5, 1)",
            params![key, h.ip.trim(), h.port.trim(), h.name.trim(), ts],
        )
        .map_err(|e| e.to_string())?;
    }
    tx.execute(
        "INSERT OR REPLACE INTO meta (key, value) VALUES ('migrated_cfg_servers_v1', '1')",
        [],
    )
    .map_err(|e| e.to_string())?;
    tx.commit().map_err(|e| e.to_string())?;
    Ok(true)
}

pub fn add_playtime(server_key: &str, delta_seconds: i64) -> Result<i64, String> {
    with_conn(|c| add_playtime_on(c, server_key, delta_seconds))
}

pub fn get_all_playtime() -> Result<HashMap<String, i64>, String> {
    with_conn(|c| get_all_playtime_on(c))
}

pub fn record_stat(ip: &str, port: &str, recorded_at: &str, players: i32) -> Result<bool, String> {
    with_conn(|c| record_stat_on(c, ip, port, recorded_at, players))
}

pub fn get_stats(ip: &str, port: &str, limit: usize) -> Result<Vec<i32>, String> {
    with_conn(|c| get_stats_on(c, ip, port, limit))
}

pub fn get_favorites() -> Result<Vec<SavedServerEntry>, String> {
    with_conn(|c| get_favorites_on(c))
}

pub fn upsert_favorite(ip: &str, port: &str, name: &str) -> Result<(), String> {
    with_conn(|c| upsert_favorite_on(c, ip, port, name))
}

pub fn remove_favorite(ip: &str, port: &str) -> Result<bool, String> {
    with_conn(|c| remove_favorite_on(c, ip, port))
}

pub fn get_history(limit: usize) -> Result<Vec<SavedServerEntry>, String> {
    with_conn(|c| get_history_on(c, limit))
}

pub fn record_history(ip: &str, port: &str, name: &str) -> Result<(), String> {
    with_conn(|c| record_history_on(c, ip, port, name))
}

pub fn remove_history(ip: &str, port: &str) -> Result<bool, String> {
    with_conn(|c| remove_history_on(c, ip, port))
}

pub fn update_server_name(ip: &str, port: &str, name: &str) -> Result<(), String> {
    with_conn(|c| update_server_name_on(c, ip, port, name))
}

pub fn migrate_cfg_servers(
    favs: &[SavedServerEntry],
    hist: &[SavedServerEntry],
) -> Result<bool, String> {
    with_conn(|c| migrate_cfg_servers_on(c, favs, hist))
}

pub fn close_db() {
    if let Ok(mut g) = global_db().lock() {
        if let Some(conn) = g.take() {
            let _ = conn.execute_batch("PRAGMA wal_checkpoint(TRUNCATE);");
            drop(conn);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_dir(name: &str) -> std::path::PathBuf {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_db_{}_{}", name, nanos));
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn test_migrate_playtime_and_stats_cleans_data() {
        let dir = temp_dir("mig");
        let pt_path = dir.join("playtime.txt");
        let st_path = dir.join("server_stats.txt");
        let db_path = dir.join("launcher.db");

        // Corrupted 2-part rows should be ignored; valid rows imported
        fs::write(
            &pt_path,
            "\u{feff}GTA-IRZ.COM|7777\r\n127.0.0.1|5456\r\n1.2.3.4|7777|3600\r\n1.2.3.4|7777|1200\r\n5.6.7.8|7777|0\r\n",
        )
        .unwrap();

        // Duplicate entries and Buddhist year 2569 -> 2026
        fs::write(
            &st_path,
            "1.2.3.4|7777|2569-11-12 20:00|40\r\n1.2.3.4|7777|2569-11-12 20:00|40\r\n1.2.3.4|7777|2026-11-12 20:05|55\r\n",
        )
        .unwrap();

        let mut conn = Connection::open(&db_path).unwrap();
        init_schema(&conn).unwrap();
        migrate_legacy_files(&mut conn, Some(&pt_path), Some(&st_path)).unwrap();

        let pt = get_all_playtime_on(&conn).unwrap();
        assert_eq!(pt.len(), 1);
        assert_eq!(pt.get("1.2.3.4|7777"), Some(&3600));

        let st = get_stats_on(&conn, "1.2.3.4", "7777", 100).unwrap();
        assert_eq!(st, vec![40, 55]);

        // Re-running migration is a no-op
        add_playtime_on(&conn, "1.2.3.4|7777", 30).unwrap();
        migrate_legacy_files(&mut conn, Some(&pt_path), Some(&st_path)).unwrap();
        let pt2 = get_all_playtime_on(&conn).unwrap();
        assert_eq!(pt2.get("1.2.3.4|7777"), Some(&3630));

        drop(conn);
        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_record_stat_chronological_and_dedup() {
        let conn = Connection::open_in_memory().unwrap();
        init_schema(&conn).unwrap();
        assert!(record_stat_on(&conn, "9.9.9.9", "7777", "2569-01-01 10:00", 10).unwrap());
        assert!(!record_stat_on(&conn, "9.9.9.9", "7777", "2026-01-01 10:00", 10).unwrap());
        assert!(record_stat_on(&conn, "9.9.9.9", "7777", "2026-01-01 10:05", 25).unwrap());
        assert!(record_stat_on(&conn, "9.9.9.9", "7777", "2026-01-01 10:10", 30).unwrap());

        let all = get_stats_on(&conn, "9.9.9.9", "7777", 500).unwrap();
        assert_eq!(all, vec![10, 25, 30]);

        let last2 = get_stats_on(&conn, "9.9.9.9", "7777", 2).unwrap();
        assert_eq!(last2, vec![25, 30]);
    }

    #[test]
    fn test_favorites_history_case_normalization_and_migration() {
        let mut conn = Connection::open_in_memory().unwrap();
        init_schema(&conn).unwrap();

        let favs = vec![SavedServerEntry {
            ip: "GTA-IRZ.COM".into(),
            port: "7777".into(),
            name: "IRZ".into(),
            last_joined_at: 0,
            join_count: 0,
        }];
        let hist = vec![SavedServerEntry {
            ip: "127.0.0.1".into(),
            port: "7777".into(),
            name: "Local".into(),
            last_joined_at: 0,
            join_count: 0,
        }];
        assert!(migrate_cfg_servers_on(&mut conn, &favs, &hist).unwrap());
        assert!(!migrate_cfg_servers_on(&mut conn, &favs, &hist).unwrap());

        // Case-insensitive upsert merges rather than duplicating
        upsert_favorite_on(&conn, "gta-irz.com", "7777", "GTA IRZ Updated").unwrap();
        let fl = get_favorites_on(&conn).unwrap();
        assert_eq!(fl.len(), 1);
        assert_eq!(fl[0].name, "GTA IRZ Updated");

        record_history_on(&conn, "127.0.0.1", "7777", "Local New").unwrap();
        let hl = get_history_on(&conn, 50).unwrap();
        assert_eq!(hl.len(), 1);
        assert_eq!(hl[0].join_count, 2);
        assert_eq!(hl[0].name, "Local New");

        assert!(remove_favorite_on(&conn, "GTA-IRZ.COM", "7777").unwrap());
        assert!(get_favorites_on(&conn).unwrap().is_empty());
    }

    #[test]
    fn test_playtime_case_folding_v2() {
        let mut conn = Connection::open_in_memory().unwrap();
        init_schema(&conn).unwrap();
        conn.execute(
            "INSERT INTO playtime (server_key, total_seconds) VALUES ('GTA-IRZ.COM|7777', 120), ('gta-irz.com|7777', 80)",
            [],
        )
        .unwrap();
        migrate_v2_casefold(&mut conn).unwrap();
        let pt = get_all_playtime_on(&conn).unwrap();
        assert_eq!(pt.len(), 1);
        assert_eq!(pt.get("gta-irz.com|7777"), Some(&200));
    }
}
