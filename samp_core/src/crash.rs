use serde::{Deserialize, Serialize};
use std::fs;
use std::path::Path;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct CrashInfo {
    #[serde(default)]
    pub timestamp: String,
    #[serde(default)]
    pub exception_code: String,
    #[serde(default)]
    pub exception_name: String,
    #[serde(default)]
    pub fault_address: String,
    #[serde(default)]
    pub module_name: String,
    #[serde(default)]
    pub module_offset: String,
    #[serde(default)]
    pub thai_explanation: String,
    #[serde(default)]
    pub suggestion: String,
    #[serde(default)]
    pub working_set_mb: u64,
    #[serde(default)]
    pub virtual_mem_mb: u64,
    #[serde(default)]
    pub loaded_modules: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct TelemetryData {
    #[serde(default)]
    pub pid: u32,
    #[serde(default)]
    pub working_set_mb: u64,
    #[serde(default)]
    pub peak_working_set_mb: u64,
    #[serde(default)]
    pub private_bytes_mb: u64,
    #[serde(default)]
    pub virtual_mem_mb: u64,
    #[serde(default)]
    pub timestamp: String,
}

#[derive(Debug, Clone, Serialize)]
pub struct GuardRuntimeStatus {
    pub has_crash: bool,
    pub crash: Option<CrashInfo>,
    pub telemetry: Option<TelemetryData>,
}

pub fn get_guard_status(game_dir: &Path) -> GuardRuntimeStatus {
    let bkp = game_dir.join("_mod_backup");
    let crash = fs::read_to_string(bkp.join("crash_latest.json"))
        .ok()
        .and_then(|raw| serde_json::from_str::<CrashInfo>(&raw).ok());
    let telemetry = fs::read_to_string(bkp.join("game_telemetry.json"))
        .ok()
        .and_then(|raw| serde_json::from_str::<TelemetryData>(&raw).ok());
    GuardRuntimeStatus {
        has_crash: crash.is_some(),
        crash,
        telemetry,
    }
}

pub fn clear_latest_crash(game_dir: &Path) {
    let p = game_dir.join("_mod_backup").join("crash_latest.json");
    if p.exists() {
        let _ = fs::remove_file(&p);
    }
}
