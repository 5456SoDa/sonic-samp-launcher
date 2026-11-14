use crate::win_ro::{self, HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};
use serde::Serialize;
use std::collections::HashSet;
use std::fs;
use std::path::Path;

const MMDEV_CAPTURE_KEY: &str =
    r"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";
const PROP_DEVICE_DESC: &str = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
const PROP_IFACE_NAME: &str = "{b3f8fa53-0004-438e-9003-51a46e139bfc},6";
const MIC_CONSENT_SUBKEY: &str =
    r"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

#[derive(Debug, Clone, Serialize)]
pub struct MicDeviceInfo {
    pub id: u32,
    pub name: String,
    pub is_virtual: bool,
    pub recommended: bool,
}

#[derive(Debug, Clone, Serialize)]
pub struct SampVoiceLogSummary {
    pub exists: bool,
    pub svconfig_exists: bool,
    pub last_voice_server: String,
    pub last_voice_port: u16,
    pub detected_mics: Vec<String>,
    pub had_pause_disconnect: bool,
    pub recent_errors: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct AudioDiagnostics {
    pub devices: Vec<MicDeviceInfo>,
    pub mic_consent_allowed: bool,
    pub svlog: SampVoiceLogSummary,
    pub recommendation: String,
}

#[repr(C)]
#[allow(non_snake_case)]
struct WaveInCapsW {
    w_mid: u16,
    w_pid: u16,
    v_driver_version: u32,
    sz_pname: [u16; 32],
    dw_formats: u32,
    w_channels: u16,
    w_reserved1: u16,
}

#[link(name = "winmm")]
extern "system" {
    fn waveInGetNumDevs() -> u32;
    fn waveInGetDevCapsW(u_device_id: usize, pwic: *mut WaveInCapsW, cbwic: u32) -> u32;
}

pub fn classify_virtual_mic(name: &str) -> bool {
    let low = name.to_ascii_lowercase();
    [
        "virtual",
        "audiorelay",
        "voicemod",
        "vb-audio",
        "vb-cable",
        "cable output",
        "fxsound",
        "droidcam",
        "iriun",
        "nvidia broadcast",
        "voicemeeter",
        "stereo mix",
    ]
    .iter()
    .any(|kw| low.contains(kw))
}

/// Enumerates active capture devices purely READ-ONLY without altering Windows default mic.
/// Reads full friendly names from MMDevices registry (WOW64 64-bit view), with WinMM fallback.
pub fn list_capture_devices() -> Vec<MicDeviceInfo> {
    let mut devices = Vec::new();
    let mut seen: HashSet<String> = HashSet::new();

    for sub in win_ro::reg_enum_subkeys(HKEY_LOCAL_MACHINE, MMDEV_CAPTURE_KEY, true) {
        let dev_key = format!(r"{}\{}", MMDEV_CAPTURE_KEY, sub);
        let state = win_ro::reg_read_u32(HKEY_LOCAL_MACHINE, &dev_key, "DeviceState", true).unwrap_or(0);
        if state != 1 {
            // 1 == DEVICE_STATE_ACTIVE
            continue;
        }
        let props_key = format!(r"{}\Properties", dev_key);
        let desc = win_ro::reg_read_string(HKEY_LOCAL_MACHINE, &props_key, PROP_DEVICE_DESC, true);
        let iface = win_ro::reg_read_string(HKEY_LOCAL_MACHINE, &props_key, PROP_IFACE_NAME, true);

        let full_name = match (desc, iface) {
            (Some(d), Some(i)) if !d.is_empty() && !i.is_empty() => format!("{} ({})", d, i),
            (Some(d), _) if !d.is_empty() => d,
            (_, Some(i)) if !i.is_empty() => i,
            _ => continue,
        };

        let key_norm = full_name.to_ascii_lowercase();
        if seen.insert(key_norm) {
            let is_virtual = classify_virtual_mic(&full_name);
            let id = devices.len() as u32;
            devices.push(MicDeviceInfo {
                id,
                name: full_name,
                is_virtual,
                recommended: !is_virtual,
            });
        }
    }

    if devices.is_empty() {
        let count = unsafe { waveInGetNumDevs() };
        for i in 0..count {
            let mut caps: WaveInCapsW = unsafe { std::mem::zeroed() };
            let rc = unsafe {
                waveInGetDevCapsW(i as usize, &mut caps, std::mem::size_of::<WaveInCapsW>() as u32)
            };
            if rc == 0 {
                let name = win_ro::utf16_slice_to_string(&caps.sz_pname);
                if !name.trim().is_empty() && seen.insert(name.to_ascii_lowercase()) {
                    let is_virtual = classify_virtual_mic(&name);
                    devices.push(MicDeviceInfo {
                        id: i,
                        name,
                        is_virtual,
                        recommended: !is_virtual,
                    });
                }
            }
        }
    }

    devices
}

pub fn parse_svlog_text(raw: &str, exists: bool, svconfig_exists: bool) -> SampVoiceLogSummary {
    let mut last_voice_server = String::new();
    let mut last_voice_port: u16 = 0;
    let mut detected_mics: Vec<String> = Vec::new();
    let mut had_pause_disconnect = false;
    let mut recent_errors: Vec<String> = Vec::new();
    let mut saw_pause = false;

    for line in raw.lines() {
        let t = line.trim();
        if let Some(pos) = t.find("connecting to voiceserver '") {
            let rest = &t[pos + "connecting to voiceserver '".len()..];
            if let Some(endq) = rest.find('\'') {
                let hostport = &rest[..endq];
                if let Some((h, p)) = hostport.rsplit_once(':') {
                    last_voice_server = h.to_string();
                    last_voice_port = p.parse::<u16>().unwrap_or(0);
                }
            }
        }
        if t.contains("[sv:dbg:record:init] : device detect") && t.contains("loopback(0)") {
            if let Some(ns) = t.find("name(") {
                let after = &t[ns + 5..];
                if let Some(te) = after.find(") type(") {
                    let mic = after[..te].to_string();
                    if !detected_mics.contains(&mic) {
                        detected_mics.push(mic);
                    }
                }
            }
        }
        if t.contains("[sv:dbg:plugin] : game paused") {
            saw_pause = true;
        }
        if saw_pause
            && (t.contains("channel recording stoped") || t.contains("raknet disconnected"))
        {
            had_pause_disconnect = true;
        }
        if t.contains(": [sv:err:") || t.contains(": [err:") {
            if let Some(idx) = t.rfind("] : ") {
                let err_msg = t[idx + 4..].trim().to_string();
                if !err_msg.is_empty() && !recent_errors.contains(&err_msg) {
                    recent_errors.push(err_msg);
                }
            }
        }
    }

    if recent_errors.len() > 6 {
        let start = recent_errors.len() - 6;
        recent_errors = recent_errors.split_off(start);
    }

    SampVoiceLogSummary {
        exists,
        svconfig_exists,
        last_voice_server,
        last_voice_port,
        detected_mics,
        had_pause_disconnect,
        recent_errors,
    }
}

pub fn read_svlog_summary() -> SampVoiceLogSummary {
    let Some(uf) = win_ro::gta_user_files_dir() else {
        return parse_svlog_text("", false, false);
    };
    let sv_dir = uf.join("sampvoice");
    let svlog_path = sv_dir.join("svlog.txt");
    let svcfg_exists = sv_dir.join("svconfig.bin").exists();
    match fs::read_to_string(&svlog_path) {
        Ok(raw) => parse_svlog_text(&raw, true, svcfg_exists),
        Err(_) => parse_svlog_text("", svlog_path.exists(), svcfg_exists),
    }
}

fn check_mic_privacy_allowed() -> bool {
    let main_val = win_ro::reg_read_string(HKEY_CURRENT_USER, MIC_CONSENT_SUBKEY, "Value", false);
    let nonpkg_val = win_ro::reg_read_string(
        HKEY_CURRENT_USER,
        &format!(r"{}\NonPackaged", MIC_CONSENT_SUBKEY),
        "Value",
        false,
    );
    let ok_main = main_val
        .as_deref()
        .map(|s| !s.eq_ignore_ascii_case("Deny"))
        .unwrap_or(true);
    let ok_nonpkg = nonpkg_val
        .as_deref()
        .map(|s| !s.eq_ignore_ascii_case("Deny"))
        .unwrap_or(true);
    ok_main && ok_nonpkg
}

pub fn inspect_audio_diagnostics(_game_dir: &Path) -> AudioDiagnostics {
    let devices = list_capture_devices();
    let mic_consent_allowed = check_mic_privacy_allowed();
    let svlog = read_svlog_summary();

    let hw_count = devices.iter().filter(|d| !d.is_virtual).count();
    let virt_count = devices.iter().filter(|d| d.is_virtual).count();

    let recommendation = if !mic_consent_allowed {
        "Windows Microphone Privacy ตั้งเป็น Deny — กรุณาเปิดสิทธิ์ไมค์ใน Windows Settings เอง".into()
    } else if devices.is_empty() {
        "ไม่พบอุปกรณ์ไมโครโฟนที่เปิดใช้งานในเครื่อง".into()
    } else if hw_count > 0 && virt_count > 0 {
        "พบทั้งไมค์จริงและ Virtual Mic ในเครื่อง — แนะนำเลือกไมค์จริงในเมนู PreferredMic ของ Launcher เพื่อให้ SonicGuard ล็อกให้ในเกมอัตโนมัติ (ไม่กระทบ Windows Settings)".into()
    } else if svlog.had_pause_disconnect {
        "svlog เคยบันทึกรอบที่เกม Pause แล้วไมค์/เสียงหลุด — แนะนำเปิด Borderless หรือ AllowAltTabbingWithoutPausing".into()
    } else {
        "ระบบเสียงและไมโครโฟนพร้อมใช้งาน".into()
    };

    AudioDiagnostics {
        devices,
        mic_consent_allowed,
        svlog,
        recommendation,
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_virtual_mic_classifier_and_svlog_parser() {
        assert!(!classify_virtual_mic("Microphone Array (Realtek(R) Audio)"));
        assert!(classify_virtual_mic("Virtual Mic (Virtual Mic for AudioRelay)"));

        let sample = "\
[20:58:15] : [sv:err:plugin] : failed to load configs
[20:58:35] : [sv:dbg:network:serverInfo] : connecting to voiceserver '82.26.104.142:9392'...
[20:58:38] : [sv:dbg:record:init] : device detect [ id(0) enabled(1) loopback(0) name(Microphone Array (Realtek(R) Audio)) type(0x5000000) ]
[20:58:38] : [sv:dbg:record:init] : device detect [ id(2) enabled(1) loopback(1) name(Speakers (Realtek(R) Audio)) type(0x2000000) ]
[20:59:36] : [sv:dbg:plugin] : game paused
[20:59:38] : [sv:dbg:record:stoprecording] : channel recording stoped
";
        let sum = parse_svlog_text(sample, true, false);
        assert_eq!(sum.last_voice_server, "82.26.104.142");
        assert_eq!(sum.last_voice_port, 9392);
        assert_eq!(sum.detected_mics, vec!["Microphone Array (Realtek(R) Audio)"]);
        assert!(sum.had_pause_disconnect);
        assert!(!sum.svconfig_exists);
        assert_eq!(sum.recent_errors, vec!["failed to load configs"]);
    }
}
