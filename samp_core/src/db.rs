use rusqlite::{params, Connection};
use std::collections::HashMap;
use std::fs;
use std::path::Path;
use std::sync::{Mutex, OnceLock};

fn global_db() -> &'static Mutex<Option<Connection>> {
    static DB: OnceLock<Mutex<Option<Connection>>> = OnceLock::new();
    DB.get_or_init(|| Mutex::new(None))
}

fn init_schema(conn: &Connection) -> Result<(), String> {
    conn.execute_batch(
        "PRAGMA journal_mode = WAL;
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

fn is_migrated(conn: &Connection) -> bool {
    conn.query_row(
        "SELECT value FROM meta WHERE key = 'migrated_v1'",
        [],
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
    if is_migrated(conn) {
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
                                let key = format!("{}|{}", parts[0].trim(), parts[1].trim());
                                let _ = stmt.execute(params![key, secs]);
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
                            let ip = parts[0].trim();
                            let port = parts[1].trim();
                            let stamp = normalize_timestamp(parts[2]);
                            if !ip.is_empty() && !port.is_empty() && !stamp.is_empty() {
                                let _ = stmt.execute(params![ip, port, stamp, players]);
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

pub fn init_db(
    db_path: &Path,
    playtime_txt: Option<&Path>,
    stats_txt: Option<&Path>,
) -> Result<(), String> {
    if let Some(parent) = db_path.parent() {
        if !parent.as_os_str().is_empty() {
            let _ = fs::create_dir_all(parent);
        }
    }
    let mut conn = Connection::open(db_path).map_err(|e| format!("Open SQLite error: {}", e))?;
    init_schema(&conn)?;
    migrate_legacy_files(&mut conn, playtime_txt, stats_txt)?;
    let mut guard = global_db().lock().map_err(|_| "DB mutex poisoned".to_string())?;
    *guard = Some(conn);
    Ok(())
}

fn with_conn<F, R>(f: F) -> Result<R, String>
where
    F: FnOnce(&Connection) -> Result<R, String>,
{
    let guard = global_db().lock().map_err(|_| "DB mutex poisoned".to_string())?;
    let Some(ref conn) = *guard else {
        return Err("Database not initialized".into());
    };
    f(conn)
}

pub fn add_playtime_on(conn: &Connection, server_key: &str, delta_seconds: i64) -> Result<i64, String> {
    if delta_seconds <= 0 {
        let cur: i64 = conn
            .query_row(
                "SELECT total_seconds FROM playtime WHERE server_key = ?1",
                params![server_key],
                |r| r.get(0),
            )
            .unwrap_or(0);
        return Ok(cur);
    }
    conn.execute(
        "INSERT INTO playtime (server_key, total_seconds) VALUES (?1, ?2)
         ON CONFLICT(server_key) DO UPDATE SET
         total_seconds = playtime.total_seconds + excluded.total_seconds",
        params![server_key, delta_seconds],
    )
    .map_err(|e| e.to_string())?;
    conn.query_row(
        "SELECT total_seconds FROM playtime WHERE server_key = ?1",
        params![server_key],
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
    for item in rows.flatten() {
        map.insert(item.0, item.1);
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
    let norm = normalize_timestamp(recorded_at);
    let changed = conn
        .execute(
            "INSERT OR IGNORE INTO server_stats (ip, port, recorded_at, players)
             VALUES (?1, ?2, ?3, ?4)",
            params![ip, port, norm, players],
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
                 WHERE ip = ?1 AND port = ?2
                 ORDER BY id DESC
                 LIMIT ?3
             ) ORDER BY id ASC",
        )
        .map_err(|e| e.to_string())?;
    let rows = stmt
        .query_map(params![ip, port, cap], |r| r.get::<_, i32>(0))
        .map_err(|e| e.to_string())?;
    Ok(rows.flatten().collect())
}

pub fn add_playtime(server_key: &str, delta_seconds: i64) -> Result<i64, String> {
    with_conn(|c| add_playtime_on(c, server_key, delta_seconds))
}

pub fn get_all_playtime() -> Result<HashMap<String, i64>, String> {
    with_conn(get_all_playtime_on)
}

pub fn record_stat(ip: &str, port: &str, recorded_at: &str, players: i32) -> Result<bool, String> {
    with_conn(|c| record_stat_on(c, ip, port, recorded_at, players))
}

pub fn get_stats(ip: &str, port: &str, limit: usize) -> Result<Vec<i32>, String> {
    with_conn(|c| get_stats_on(c, ip, port, limit))
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
    use std::time::{SystemTime, UNIX_EPOCH};

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
}
