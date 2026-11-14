mod assets;
mod audio;
mod backup;
mod crash;
mod db;
mod discord;
mod dwm;
mod gpu;
mod health;
mod inject;
mod modloader_cfg;
mod net_shield;
mod ping_spark;
mod protocol;
mod public_list;
mod query;
mod rcon;
mod resources;
mod settings;
mod tools_io;
mod watchdog;
mod win_ro;

use query::ServerTarget;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::panic::{catch_unwind, AssertUnwindSafe};
use std::path::Path;
use std::ptr::null_mut;

unsafe fn read_cstr(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        return None;
    }
    Some(CStr::from_ptr(ptr).to_string_lossy().into_owned())
}

fn into_cstr(mut s: String) -> *mut c_char {
    if s.as_bytes().contains(&0) {
        s = s.replace('\0', "");
    }
    match CString::new(s) {
        Ok(cs) => cs.into_raw(),
        Err(_) => null_mut(),
    }
}

fn guard_ptr<F: FnOnce() -> *mut c_char>(f: F) -> *mut c_char {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or(null_mut())
}

fn guard_u64<F: FnOnce() -> u64>(f: F) -> u64 {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or(0)
}

#[no_mangle]
pub extern "C" fn core_version() -> *mut c_char {
    guard_ptr(|| into_cstr(format!("samp_core {}", env!("CARGO_PKG_VERSION"))))
}

#[no_mangle]
pub extern "C" fn core_free_str(ptr: *mut c_char) {
    if !ptr.is_null() {
        let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
            drop(CString::from_raw(ptr));
        }));
    }
}

#[no_mangle]
pub extern "C" fn core_resolve_host(host: *const c_char) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::resolve_host(&h) {
            Some(ip) => into_cstr(ip.to_string()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_server(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        let qr = query::query_server_sync(&h, port);
        match serde_json::to_string(&qr) {
            Ok(json) => into_cstr(json),
            Err(_) => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_players(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::query_players_sync(&h, port) {
            Some(pl) => serde_json::to_string(&pl).map(into_cstr).unwrap_or(null_mut()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_query_rules(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| {
        let Some(h) = (unsafe { read_cstr(host) }) else {
            return null_mut();
        };
        match query::query_rules_sync(&h, port) {
            Some(rl) => serde_json::to_string(&rl).map(into_cstr).unwrap_or(null_mut()),
            None => null_mut(),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_launch_and_inject(
    gta_path: *const c_char,
    dll_path: *const c_char,
    work_dir: *const c_char,
    nick: *const c_char,
    ip: *const c_char,
    port: *const c_char,
    pass: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(gta), Some(dll), Some(cwd), Some(n), Some(i), Some(p)) = (
            read_cstr(gta_path),
            read_cstr(dll_path),
            read_cstr(work_dir),
            read_cstr(nick),
            read_cstr(ip),
            read_cstr(port),
        ) else {
            return into_cstr("ข้อมูล parameter ไม่ครบ".to_string());
        };
        let pw = read_cstr(pass).unwrap_or_default();
        match inject::launch_and_inject(&gta, &dll, &cwd, &n, &i, &p, &pw) {
            Ok(_pid) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_scan_start(servers_json: *const c_char, concurrency: u32) -> u64 {
    guard_u64(|| {
        let Some(raw) = (unsafe { read_cstr(servers_json) }) else {
            return 0;
        };
        let Ok(targets) = serde_json::from_str::<Vec<ServerTarget>>(&raw) else {
            return 0;
        };
        let conc = if concurrency == 0 { 64 } else { concurrency as usize };
        query::start_batch_scan(targets, conc)
    })
}

#[no_mangle]
pub extern "C" fn core_public_scan_start(cache_path: *const c_char, concurrency: u32) -> u64 {
    guard_u64(|| {
        let cp = unsafe { read_cstr(cache_path) }.filter(|s| !s.is_empty());
        let conc = if concurrency == 0 { 64 } else { concurrency as usize };
        public_list::start_public_scan(cp, conc)
    })
}

#[no_mangle]
pub extern "C" fn core_scan_poll(handle: u64, max_items: u32) -> *mut c_char {
    guard_ptr(|| {
        let max_n = if max_items == 0 { 64 } else { max_items as usize };
        let batch = query::poll_scan(handle, max_n);
        serde_json::to_string(&batch)
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_scan_cancel(handle: u64) {
    let _ = catch_unwind(AssertUnwindSafe(|| {
        query::remove_scan(handle);
    }));
}

#[no_mangle]
pub extern "C" fn core_rcon_send(
    host: *const c_char,
    port: u16,
    pass: *const c_char,
    cmd: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(h) = read_cstr(host) else {
            return into_cstr("ข้อมูล host ไม่ครบ".into());
        };
        let p = read_cstr(pass).unwrap_or_default();
        let c = read_cstr(cmd).unwrap_or_default();
        into_cstr(rcon::rcon_send_sync(&h, port, &p, &c))
    })
}

#[no_mangle]
pub extern "C" fn core_db_init(
    db_path: *const c_char,
    playtime_txt_path: *const c_char,
    stats_txt_path: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(dbp) = read_cstr(db_path).filter(|s| !s.is_empty()) else {
            return into_cstr("db_path ว่าง".into());
        };
        let pt_opt = read_cstr(playtime_txt_path).filter(|s| !s.is_empty());
        let st_opt = read_cstr(stats_txt_path).filter(|s| !s.is_empty());
        match db::init_db(
            Path::new(&dbp),
            pt_opt.as_deref().map(Path::new),
            st_opt.as_deref().map(Path::new),
        ) {
            Ok(()) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_db_add_playtime(server_key: *const c_char, delta_seconds: i64) -> i64 {
    catch_unwind(AssertUnwindSafe(|| unsafe {
        let Some(k) = read_cstr(server_key).filter(|s| !s.is_empty()) else {
            return -1;
        };
        db::add_playtime(&k, delta_seconds).unwrap_or(-1)
    }))
    .unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn core_db_get_all_playtime() -> *mut c_char {
    guard_ptr(|| match db::get_all_playtime() {
        Ok(map) => serde_json::to_string(&map)
            .map(into_cstr)
            .unwrap_or(null_mut()),
        Err(_) => null_mut(),
    })
}

#[no_mangle]
pub extern "C" fn core_db_record_stat(
    ip: *const c_char,
    port: *const c_char,
    recorded_at: *const c_char,
    players: i32,
) -> i32 {
    catch_unwind(AssertUnwindSafe(|| unsafe {
        let (Some(i), Some(p), Some(t)) = (read_cstr(ip), read_cstr(port), read_cstr(recorded_at))
        else {
            return -1;
        };
        match db::record_stat(&i, &p, &t, players) {
            Ok(true) => 1,
            Ok(false) => 0,
            Err(_) => -1,
        }
    }))
    .unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn core_db_get_stats(
    ip: *const c_char,
    port: *const c_char,
    limit: u32,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(i), Some(p)) = (read_cstr(ip), read_cstr(port)) else {
            return null_mut();
        };
        let cap = if limit == 0 { 500 } else { limit as usize };
        match db::get_stats(&i, &p, cap) {
            Ok(list) => serde_json::to_string(&list)
                .map(into_cstr)
                .unwrap_or(null_mut()),
            Err(_) => null_mut(),
        }
    })
}

fn json_ok_or_err<T: serde::Serialize>(res: Result<T, String>) -> *mut c_char {
    match res {
        Ok(val) => serde_json::to_string(&serde_json::json!({ "ok": true, "data": val }))
            .map(into_cstr)
            .unwrap_or(null_mut()),
        Err(e) => serde_json::to_string(&serde_json::json!({ "ok": false, "error": e }))
            .map(into_cstr)
            .unwrap_or(null_mut()),
    }
}

#[no_mangle]
pub extern "C" fn core_health_check(
    game_dir: *const c_char,
    skygfx_preset: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let preset = read_cstr(skygfx_preset).unwrap_or_else(|| "ps2".into());
        let report = health::check_health(Path::new(&gd), &preset);
        serde_json::to_string(&report)
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_health_autofix(game_dir: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบโฟลเดอร์เกม\"}".into());
        };
        json_ok_or_err(health::auto_fix_health(Path::new(&gd)))
    })
}

#[no_mangle]
pub extern "C" fn core_snapshot_create(
    game_dir: *const c_char,
    name: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบโฟลเดอร์เกม\"}".into());
        };
        let nm = read_cstr(name).unwrap_or_default();
        json_ok_or_err(backup::create_snapshot(Path::new(&gd), &nm))
    })
}

#[no_mangle]
pub extern "C" fn core_snapshot_list(game_dir: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("[]".into());
        };
        match backup::list_snapshots(Path::new(&gd)) {
            Ok(list) => serde_json::to_string(&list)
                .map(into_cstr)
                .unwrap_or(null_mut()),
            Err(_) => into_cstr("[]".into()),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_snapshot_restore(
    game_dir: *const c_char,
    snapshot_id: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(gd), Some(id)) = (
            read_cstr(game_dir).filter(|s| !s.is_empty()),
            read_cstr(snapshot_id).filter(|s| !s.is_empty()),
        ) else {
            return into_cstr("{\"ok\":false,\"error\":\"ข้อมูลไม่ครบ\"}".into());
        };
        json_ok_or_err(backup::restore_snapshot(Path::new(&gd), &id))
    })
}

#[no_mangle]
pub extern "C" fn core_snapshot_delete(
    game_dir: *const c_char,
    snapshot_id: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(gd), Some(id)) = (
            read_cstr(game_dir).filter(|s| !s.is_empty()),
            read_cstr(snapshot_id).filter(|s| !s.is_empty()),
        ) else {
            return into_cstr("ข้อมูลไม่ครบ".into());
        };
        match backup::delete_snapshot(Path::new(&gd), &id) {
            Ok(()) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_db_close() {
    let _ = catch_unwind(AssertUnwindSafe(|| {
        ping_spark::stop_ping();
        query::stop_slot_watch();
        watchdog::stop_watchdog();
        discord::shutdown();
        db::close_db();
    }));
}

#[no_mangle]
pub extern "C" fn core_assets_scan(game_dir: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let rep = assets::scan_game_assets(Path::new(&gd));
        serde_json::to_string(&rep)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_modloader_get(game_dir: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        match modloader_cfg::list_modloader_state(Path::new(&gd)) {
            Ok(st) => serde_json::to_string(&st)
                .ok()
                .map(into_cstr)
                .unwrap_or(null_mut()),
            Err(e) => into_cstr(serde_json::json!({ "error": e }).to_string()),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_modloader_save(
    game_dir: *const c_char,
    ignore_all: bool,
    disabled_json: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("ไม่ระบุโฟลเดอร์เกม".into());
        };
        let dis_vec: Vec<String> = read_cstr(disabled_json)
            .and_then(|s| serde_json::from_str(&s).ok())
            .unwrap_or_default();
        match modloader_cfg::save_modloader_state(Path::new(&gd), ignore_all, &dis_vec) {
            Ok(()) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_guard_status(game_dir: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let st = crash::get_guard_status(Path::new(&gd));
        serde_json::to_string(&st)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_crash_clear(game_dir: *const c_char) {
    let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
        if let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) {
            crash::clear_latest_crash(Path::new(&gd));
        }
    }));
}

#[no_mangle]
pub extern "C" fn core_ui_apply_dwm(hwnd: usize) -> bool {
    catch_unwind(AssertUnwindSafe(|| dwm::apply_dwm(hwnd))).unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn core_ui_set_accent(hwnd: usize, r: u8, g: u8, b: u8) -> bool {
    catch_unwind(AssertUnwindSafe(|| dwm::set_dwm_accent(hwnd, r, g, b))).unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn core_ui_style_listview(hwnd: usize, header_bg_rgb: u32, accent_rgb: u32) -> bool {
    catch_unwind(AssertUnwindSafe(|| {
        dwm::style_listview(hwnd, header_bg_rgb, accent_rgb)
    }))
    .unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn core_ping_spark_start(ip: *const c_char, port: u16) {
    let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
        if let Some(host) = read_cstr(ip).filter(|s| !s.is_empty()) {
            ping_spark::start_ping(&host, port);
        } else {
            ping_spark::stop_ping();
        }
    }));
}

#[no_mangle]
pub extern "C" fn core_ping_spark_get(ip: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(host) = read_cstr(ip).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let rep = ping_spark::get_spark_report(&host, port);
        serde_json::to_string(&rep)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_ping_spark_stop() {
    let _ = catch_unwind(AssertUnwindSafe(ping_spark::stop_ping));
}

#[no_mangle]
pub extern "C" fn core_gfx_apply_skygfx(
    game_dir: *const c_char,
    preset: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบโฟลเดอร์เกม\"}".into());
        };
        let p = read_cstr(preset).unwrap_or_else(|| "ps2".into());
        json_ok_or_err(health::apply_skygfx_preset(Path::new(&gd), &p))
    })
}

#[no_mangle]
pub extern "C" fn core_gfx_apply_fps(game_dir: *const c_char, fps: u32) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบโฟลเดอร์เกม\"}".into());
        };
        json_ok_or_err(health::apply_fps_limit(Path::new(&gd), fps))
    })
}

#[no_mangle]
pub extern "C" fn core_modloader_set_priority(
    game_dir: *const c_char,
    folder_name: *const c_char,
    priority: u32,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let (Some(gd), Some(fnm)) = (
            read_cstr(game_dir).filter(|s| !s.is_empty()),
            read_cstr(folder_name).filter(|s| !s.is_empty()),
        ) else {
            return into_cstr("ข้อมูลไม่ครบ".into());
        };
        match modloader_cfg::set_folder_priority(Path::new(&gd), &fnm, priority) {
            Ok(()) => null_mut(),
            Err(e) => into_cstr(e),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_shield_diagnostics(
    game_dir: *const c_char,
    _host: *const c_char,
    _port: u16,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let gpath = Path::new(&gd);
        let settings = resources::read_sonic_guard_settings(gpath);
        let mode = resources::ResourceMode::from_str_lossy(&settings.resource_mode);
        let net = net_shield::inspect_net_diagnostics(gpath);
        let aud = audio::inspect_audio_diagnostics(gpath);
        let gpu_info = gpu::inspect_gpu_status(gpath);
        let res_info = resources::query_system_memory(mode);

        let payload = serde_json::json!({
            "settings": settings,
            "net": net,
            "audio": aud,
            "gpu": gpu_info,
            "resources": res_info,
        });
        serde_json::to_string(&payload)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_shield_save_ini(
    game_dir: *const c_char,
    updates_json: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบโฟลเดอร์เกม\"}".into());
        };
        let raw_json = read_cstr(updates_json).unwrap_or_default();
        let map: std::collections::HashMap<String, String> = match serde_json::from_str(&raw_json) {
            Ok(m) => m,
            Err(e) => return json_ok_or_err::<()>(Err(format!("ข้อมูล JSON ไม่ถูกต้อง: {}", e))),
        };
        let gpath = Path::new(&gd);
        let res = resources::save_sonic_guard_settings(gpath, &map).map(|_| {
            if map.keys().any(|k| k.eq_ignore_ascii_case("ResourceMode")) {
                let _ = health::apply_adaptive_memory_budget(gpath);
            }
            "บันทึกการตั้งค่า SonicGuard สำเร็จ"
        });
        json_ok_or_err(res)
    })
}

#[no_mangle]
pub extern "C" fn core_audio_list_mics() -> *mut c_char {
    guard_ptr(|| {
        let mics = audio::list_capture_devices();
        serde_json::to_string(&mics)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_prewarm_server(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(h) = read_cstr(host).filter(|s| !s.is_empty()) else {
            return null_mut();
        };
        let rep = net_shield::prewarm_connection(&h, port, None);
        serde_json::to_string(&rep)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_trim_memory() -> u64 {
    guard_u64(resources::trim_current_process_working_set)
}

fn guard_i32<F: FnOnce() -> i32>(f: F) -> i32 {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or(-1)
}

fn err_cstr(res: Result<(), String>) -> *mut c_char {
    match res {
        Ok(()) => null_mut(),
        Err(e) => into_cstr(e),
    }
}

fn db_server_op<F>(
    ip: *const c_char,
    port: *const c_char,
    name: *const c_char,
    op: F,
) -> *mut c_char
where
    F: FnOnce(&str, &str, &str) -> Result<(), String>,
{
    guard_ptr(|| unsafe {
        let (Some(i), Some(p)) = (
            read_cstr(ip).filter(|s| !s.trim().is_empty()),
            read_cstr(port).filter(|s| !s.trim().is_empty()),
        ) else {
            return into_cstr("ข้อมูล IP/Port ไม่ครบ".into());
        };
        let n = read_cstr(name).unwrap_or_default();
        err_cstr(op(&i, &p, &n))
    })
}

fn db_remove_op<F>(ip: *const c_char, port: *const c_char, op: F) -> i32
where
    F: FnOnce(&str, &str) -> Result<bool, String>,
{
    guard_i32(|| unsafe {
        let (Some(i), Some(p)) = (
            read_cstr(ip).filter(|s| !s.trim().is_empty()),
            read_cstr(port).filter(|s| !s.trim().is_empty()),
        ) else {
            return -1;
        };
        match op(&i, &p) {
            Ok(true) => 1,
            Ok(false) => 0,
            Err(_) => -1,
        }
    })
}

#[no_mangle]
pub extern "C" fn core_settings_load(cfg_path: *const c_char) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(p) = read_cstr(cfg_path).filter(|s| !s.is_empty()) else {
            return into_cstr("{\"ok\":false,\"error\":\"ไม่พบเส้นทางไฟล์ตั้งค่า\"}".into());
        };
        json_ok_or_err(settings::load_launcher_config(Path::new(&p)))
    })
}

#[no_mangle]
pub extern "C" fn core_settings_save(
    cfg_path: *const c_char,
    json: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(p) = read_cstr(cfg_path).filter(|s| !s.is_empty()) else {
            return into_cstr("ไม่พบเส้นทางไฟล์ตั้งค่า".into());
        };
        let raw_json = read_cstr(json).unwrap_or_default();
        let cfg: settings::LauncherConfig = match serde_json::from_str(&raw_json) {
            Ok(c) => c,
            Err(e) => return into_cstr(format!("ข้อมูลตั้งค่า JSON ไม่ถูกต้อง: {}", e)),
        };
        err_cstr(settings::save_launcher_config(Path::new(&p), &cfg))
    })
}

#[no_mangle]
pub extern "C" fn core_alt_tab_set(game_dir: *const c_char, enable: bool) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(gd) = read_cstr(game_dir).filter(|s| !s.is_empty()) else {
            return into_cstr("ไม่พบโฟลเดอร์เกม".into());
        };
        err_cstr(settings::set_alt_tab_nopause(Path::new(&gd), enable))
    })
}

#[no_mangle]
pub extern "C" fn core_reset_gta_set() -> *mut c_char {
    guard_ptr(|| json_ok_or_err(settings::reset_gta_sa_set()))
}

#[no_mangle]
pub extern "C" fn core_db_fav_list() -> *mut c_char {
    guard_ptr(|| match db::get_favorites() {
        Ok(list) => serde_json::to_string(&list)
            .map(into_cstr)
            .unwrap_or(null_mut()),
        Err(_) => into_cstr("[]".into()),
    })
}

#[no_mangle]
pub extern "C" fn core_db_fav_upsert(
    ip: *const c_char,
    port: *const c_char,
    name: *const c_char,
) -> *mut c_char {
    db_server_op(ip, port, name, db::upsert_favorite)
}

#[no_mangle]
pub extern "C" fn core_db_fav_remove(ip: *const c_char, port: *const c_char) -> i32 {
    db_remove_op(ip, port, db::remove_favorite)
}

#[no_mangle]
pub extern "C" fn core_db_hist_list(limit: u32) -> *mut c_char {
    guard_ptr(|| {
        let cap = if limit == 0 { 30 } else { limit as usize };
        match db::get_history(cap) {
            Ok(list) => serde_json::to_string(&list)
                .map(into_cstr)
                .unwrap_or(null_mut()),
            Err(_) => into_cstr("[]".into()),
        }
    })
}

#[no_mangle]
pub extern "C" fn core_db_hist_record(
    ip: *const c_char,
    port: *const c_char,
    name: *const c_char,
) -> *mut c_char {
    db_server_op(ip, port, name, db::record_history)
}

#[no_mangle]
pub extern "C" fn core_db_hist_remove(ip: *const c_char, port: *const c_char) -> i32 {
    db_remove_op(ip, port, db::remove_history)
}

#[no_mangle]
pub extern "C" fn core_db_server_rename(
    ip: *const c_char,
    port: *const c_char,
    name: *const c_char,
) -> *mut c_char {
    db_server_op(ip, port, name, db::update_server_name)
}

#[no_mangle]
pub extern "C" fn core_watchdog_poll() -> *mut c_char {
    guard_ptr(|| {
        let st = watchdog::poll_watchdog();
        serde_json::to_string(&st)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_watchdog_stop() {
    let _ = catch_unwind(AssertUnwindSafe(watchdog::stop_watchdog));
}

#[no_mangle]
pub extern "C" fn core_is_game_running() -> i32 {
    guard_i32(|| if watchdog::is_gta_sa_running() { 1 } else { 0 })
}

#[no_mangle]
pub extern "C" fn core_query_server_details(host: *const c_char, port: u16) -> *mut c_char {
    guard_ptr(|| unsafe {
        let Some(h) = read_cstr(host).filter(|s| !s.trim().is_empty()) else {
            return null_mut();
        };
        let res = query::query_server_details_sync(&h, port);
        serde_json::to_string(&res)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_slot_watch_start(host: *const c_char, port: u16) -> i32 {
    guard_i32(|| unsafe {
        let Some(h) = read_cstr(host).filter(|s| !s.trim().is_empty()) else {
            return 0;
        };
        if query::start_slot_watch(&h, port) {
            1
        } else {
            0
        }
    })
}

#[no_mangle]
pub extern "C" fn core_slot_watch_stop() {
    let _ = catch_unwind(AssertUnwindSafe(query::stop_slot_watch));
}

#[no_mangle]
pub extern "C" fn core_slot_watch_poll() -> *mut c_char {
    guard_ptr(|| {
        let st = query::poll_slot_watch();
        serde_json::to_string(&st)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_discord_init(client_id: *const c_char) {
    let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
        let cid = read_cstr(client_id).unwrap_or_default();
        discord::init(&cid);
    }));
}

#[no_mangle]
pub extern "C" fn core_discord_set_enabled(enabled: bool) {
    let _ = catch_unwind(AssertUnwindSafe(|| {
        discord::set_enabled(enabled);
    }));
}

#[no_mangle]
pub extern "C" fn core_discord_presence_launcher() {
    let _ = catch_unwind(AssertUnwindSafe(discord::update_launcher_presence));
}

#[no_mangle]
pub extern "C" fn core_discord_presence_game(
    server_name: *const c_char,
    ip: *const c_char,
    port: *const c_char,
    players: i32,
    max: i32,
) {
    let _ = catch_unwind(AssertUnwindSafe(|| unsafe {
        let nm = read_cstr(server_name).unwrap_or_default();
        let i = read_cstr(ip).unwrap_or_default();
        let p = read_cstr(port).unwrap_or_default();
        discord::update_game_presence(&nm, &i, &p, players, max);
    }));
}

#[no_mangle]
pub extern "C" fn core_discord_clear() {
    let _ = catch_unwind(AssertUnwindSafe(discord::clear_presence));
}

#[no_mangle]
pub extern "C" fn core_discord_shutdown() {
    let _ = catch_unwind(AssertUnwindSafe(discord::shutdown));
}

#[no_mangle]
pub extern "C" fn core_chatlog_read(
    path: *const c_char,
    max_bytes: u32,
    max_lines: u32,
    query: *const c_char,
) -> *mut c_char {
    guard_ptr(|| unsafe {
        let p_opt = read_cstr(path).filter(|s| !s.trim().is_empty());
        let q = read_cstr(query).unwrap_or_default();
        json_ok_or_err(tools_io::read_chatlog_file(
            p_opt.as_deref().map(Path::new),
            max_bytes as usize,
            max_lines as usize,
            &q,
        ))
    })
}

#[no_mangle]
pub extern "C" fn core_gallery_scan(dir: *const c_char, max_items: u32) -> *mut c_char {
    guard_ptr(|| unsafe {
        let d_opt = read_cstr(dir).filter(|s| !s.trim().is_empty());
        let cap = if max_items == 0 { 240 } else { max_items as usize };
        let res = tools_io::scan_gallery_dir(d_opt.as_deref().map(Path::new), cap);
        serde_json::to_string(&res)
            .ok()
            .map(into_cstr)
            .unwrap_or(null_mut())
    })
}

#[no_mangle]
pub extern "C" fn core_audio_cue(kind: *const c_char) -> i32 {
    guard_i32(|| unsafe {
        let k = read_cstr(kind).unwrap_or_default();
        if tools_io::play_audio_cue(&k) {
            1
        } else {
            0
        }
    })
}
