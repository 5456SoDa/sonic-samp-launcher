use crate::win_ro;
use encoding_rs::{UTF_8, WINDOWS_874};
use serde::Serialize;
use std::fs::{self, File};
use std::io::{Read, Seek, SeekFrom};
use std::path::{Path, PathBuf};
use std::sync::OnceLock;
use std::time::UNIX_EPOCH;
use windows_sys::Win32::Media::Audio::{PlaySoundW, SND_ASYNC, SND_MEMORY, SND_NODEFAULT};

#[derive(Debug, Clone, Serialize)]
pub struct ChatlogResult {
    pub exists: bool,
    pub path: String,
    pub file_size_bytes: u64,
    pub total_lines: usize,
    pub matched_count: usize,
    pub truncated: bool,
    pub lines: Vec<String>,
}

#[derive(Debug, Clone, Serialize)]
pub struct GalleryItem {
    pub path: String,
    pub name: String,
    pub size_bytes: u64,
    pub modified_epoch: u64,
}

#[derive(Debug, Clone, Serialize)]
pub struct GalleryScanResult {
    pub directory: String,
    pub exists: bool,
    pub total_found: usize,
    pub items: Vec<GalleryItem>,
}

pub fn default_chatlog_path() -> Option<PathBuf> {
    win_ro::gta_user_files_dir().map(|uf| uf.join("SAMP").join("chatlog.txt"))
}

pub fn default_gallery_dir() -> Option<PathBuf> {
    let uf = win_ro::gta_user_files_dir()?;
    let samp_screens = uf.join("SAMP").join("screens");
    if samp_screens.is_dir() {
        return Some(samp_screens);
    }
    let alt = uf.join("Screenshots");
    if alt.is_dir() {
        return Some(alt);
    }
    let gal = uf.join("Gallery");
    if gal.is_dir() {
        return Some(gal);
    }
    Some(samp_screens)
}

fn decode_chatlog_bytes(bytes: &[u8]) -> String {
    let (utf8_cow, had_errors) = UTF_8.decode_without_bom_handling(bytes);
    if !had_errors {
        utf8_cow.into_owned()
    } else {
        let (cp874_cow, _, _) = WINDOWS_874.decode(bytes);
        cp874_cow.into_owned()
    }
}

pub fn read_chatlog_file(
    explicit_path: Option<&Path>,
    max_bytes: usize,
    max_lines: usize,
    search_query: &str,
) -> Result<ChatlogResult, String> {
    let resolved = match explicit_path {
        Some(p) => p.to_path_buf(),
        None => default_chatlog_path().unwrap_or_else(|| PathBuf::from("chatlog.txt")),
    };
    let path_str = resolved.to_string_lossy().to_string();

    if !resolved.exists() {
        return Ok(ChatlogResult {
            exists: false,
            path: path_str,
            file_size_bytes: 0,
            total_lines: 0,
            matched_count: 0,
            truncated: false,
            lines: Vec::new(),
        });
    }

    let mut file = File::open(&resolved)
        .map_err(|e| format!("เปิด chatlog.txt ไม่ได้: {}", e))?;
    let file_len = file.metadata().map(|m| m.len()).unwrap_or(0);

    let cap_bytes = max_bytes.clamp(4096, 8 * 1024 * 1024) as u64;
    let seeked = file_len > cap_bytes;
    let to_read = if seeked { cap_bytes } else { file_len } as usize;
    if seeked {
        file.seek(SeekFrom::End(-(cap_bytes as i64)))
            .map_err(|e| format!("Seek chatlog ไม่ได้: {}", e))?;
    }

    let mut raw = vec![0u8; to_read];
    let mut read_total = 0usize;
    while read_total < to_read {
        let n = file
            .read(&mut raw[read_total..])
            .map_err(|e| format!("อ่าน chatlog ไม่ได้: {}", e))?;
        if n == 0 {
            break;
        }
        read_total += n;
    }
    raw.truncate(read_total);

    let mut start = 0usize;
    let mut truncated = seeked;
    if !seeked && raw.starts_with(&[0xEF, 0xBB, 0xBF]) {
        start = 3;
    } else if seeked {
        if let Some(nl) = raw.iter().position(|&b| b == b'\n') {
            if nl + 1 < raw.len() {
                start = nl + 1;
            }
        }
    }

    let text = decode_chatlog_bytes(&raw[start..]);
    let all_lines: Vec<String> = text.lines().map(|l| l.to_string()).collect();
    let total_lines = all_lines.len();

    let q_lower = search_query.trim().to_lowercase();
    let mut filtered: Vec<String> = if q_lower.is_empty() {
        all_lines
    } else {
        all_lines
            .into_iter()
            .filter(|l| l.to_lowercase().contains(&q_lower))
            .collect()
    };
    let matched_count = filtered.len();

    let cap_lines = max_lines.clamp(50, 20000);
    if filtered.len() > cap_lines {
        truncated = true;
        let drop_n = filtered.len() - cap_lines;
        filtered.drain(0..drop_n);
    }

    Ok(ChatlogResult {
        exists: true,
        path: path_str,
        file_size_bytes: file_len,
        total_lines,
        matched_count,
        truncated,
        lines: filtered,
    })
}

pub fn scan_gallery_dir(explicit_dir: Option<&Path>, max_items: usize) -> GalleryScanResult {
    let dir = match explicit_dir {
        Some(d) => d.to_path_buf(),
        None => default_gallery_dir().unwrap_or_default(),
    };
    let dir_str = dir.to_string_lossy().to_string();
    if dir.as_os_str().is_empty() || !dir.is_dir() {
        return GalleryScanResult {
            directory: dir_str,
            exists: false,
            total_found: 0,
            items: Vec::new(),
        };
    }

    let mut items: Vec<(u64, GalleryItem)> = Vec::new();
    if let Ok(rd) = fs::read_dir(&dir) {
        for entry in rd.flatten() {
            let p = entry.path();
            if !p.is_file() {
                continue;
            }
            let ext = p
                .extension()
                .and_then(|e| e.to_str())
                .map(|s| s.to_ascii_lowercase())
                .unwrap_or_default();
            if !matches!(ext.as_str(), "png" | "jpg" | "jpeg" | "bmp") {
                continue;
            }
            let Ok(meta) = entry.metadata() else {
                continue;
            };
            let mtime_epoch = meta
                .modified()
                .ok()
                .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
                .map(|d| d.as_secs())
                .unwrap_or(0);
            let mtime_nanos = meta
                .modified()
                .ok()
                .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
                .map(|d| d.as_nanos() as u64)
                .unwrap_or(0);
            let name = p
                .file_name()
                .and_then(|n| n.to_str())
                .unwrap_or_default()
                .to_string();
            items.push((
                mtime_nanos,
                GalleryItem {
                    path: p.to_string_lossy().to_string(),
                    name,
                    size_bytes: meta.len(),
                    modified_epoch: mtime_epoch,
                },
            ));
        }
    }

    items.sort_by(|a, b| b.0.cmp(&a.0));
    let total_found = items.len();
    let cap = max_items.clamp(1, 500);
    let out: Vec<GalleryItem> = items.into_iter().take(cap).map(|(_, it)| it).collect();

    GalleryScanResult {
        directory: dir_str,
        exists: true,
        total_found,
        items: out,
    }
}

struct WavBank {
    click: Vec<u8>,
    alert: Vec<u8>,
    launch: Vec<u8>,
}

fn build_wav(num_samples: usize, mut sample_fn: impl FnMut(usize, usize, f64) -> f64) -> Vec<u8> {
    let sample_rate: u32 = 44100;
    let data_size = (num_samples * 2) as u32;
    let mut raw = Vec::with_capacity(44 + data_size as usize);

    raw.extend_from_slice(b"RIFF");
    raw.extend_from_slice(&(36u32 + data_size).to_le_bytes());
    raw.extend_from_slice(b"WAVE");
    raw.extend_from_slice(b"fmt ");
    raw.extend_from_slice(&16u32.to_le_bytes());
    raw.extend_from_slice(&1u16.to_le_bytes()); // PCM
    raw.extend_from_slice(&1u16.to_le_bytes()); // Mono
    raw.extend_from_slice(&sample_rate.to_le_bytes());
    raw.extend_from_slice(&(sample_rate * 2).to_le_bytes());
    raw.extend_from_slice(&2u16.to_le_bytes());
    raw.extend_from_slice(&16u16.to_le_bytes());
    raw.extend_from_slice(b"data");
    raw.extend_from_slice(&data_size.to_le_bytes());

    for i in 0..num_samples {
        let val = sample_fn(i, num_samples, sample_rate as f64);
        let sample = (val * 32767.0).clamp(-32768.0, 32767.0) as i16;
        raw.extend_from_slice(&sample.to_le_bytes());
    }
    raw
}

fn synth_wav_bank() -> WavBank {
    let mut click_phase = 0.0f64;
    let click = build_wav((44100.0 * 0.045) as usize, |i, n, sr| {
        let norm_t = i as f64 / n as f64;
        let freq = 1600.0 - 1100.0 * norm_t;
        click_phase += 2.0 * std::f64::consts::PI * freq / sr;
        let env = (-6.0 * norm_t).exp();
        (0.85 * click_phase.sin() + 0.15 * (2.0 * click_phase).sin()) * env * 0.35
    });

    let mut p1 = 0.0f64;
    let mut p2 = 0.0f64;
    let alert = build_wav((44100.0 * 0.32) as usize, |i, _, sr| {
        let t = i as f64 / sr;
        if t < 0.12 {
            p1 += 2.0 * std::f64::consts::PI * 987.77 / sr;
            p1.sin() * (-3.5 * (t / 0.12)).exp() * 0.40
        } else {
            p2 += 2.0 * std::f64::consts::PI * 1318.51 / sr;
            p2.sin() * (-4.0 * ((t - 0.12) / 0.20)).exp() * 0.45
        }
    });

    let mut launch_phase = 0.0f64;
    let launch = build_wav((44100.0 * 0.13) as usize, |i, n, sr| {
        let norm_t = i as f64 / n as f64;
        let freq = 450.0 + 1350.0 * norm_t;
        launch_phase += 2.0 * std::f64::consts::PI * freq / sr;
        let env = (std::f64::consts::PI * (norm_t * 1.2).min(1.0)).sin() * (1.0 - norm_t * 0.7);
        launch_phase.sin() * env * 0.40
    });

    WavBank {
        click,
        alert,
        launch,
    }
}

fn wav_bank() -> &'static WavBank {
    static BANK: OnceLock<WavBank> = OnceLock::new();
    BANK.get_or_init(synth_wav_bank)
}

pub fn play_audio_cue(kind: &str) -> bool {
    let bank = wav_bank();
    let wav: &[u8] = match kind.trim().to_ascii_lowercase().as_str() {
        "click" => &bank.click,
        "alert" => &bank.alert,
        "launch" => &bank.launch,
        _ => return false,
    };
    let ok = unsafe {
        PlaySoundW(
            wav.as_ptr() as *const u16,
            std::ptr::null_mut(),
            SND_MEMORY | SND_ASYNC | SND_NODEFAULT,
        )
    };
    ok != 0
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    fn temp_dir(tag: &str) -> PathBuf {
        let nanos = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let dir = std::env::temp_dir().join(format!("samp_tools_{}_{}", tag, nanos));
        fs::create_dir_all(&dir).unwrap();
        dir
    }

    #[test]
    fn test_chatlog_utf8_and_cp874_tail_and_search() {
        let dir = temp_dir("chat");
        let utf8_file = dir.join("chat_utf8.txt");
        fs::write(
            &utf8_file,
            "\u{feff}[10:00:00] สวัสดี\r\n[10:00:05] Admin: ประกาศ\r\n[10:00:09] Bye\r\n",
        )
        .unwrap();

        let res = read_chatlog_file(Some(&utf8_file), 1024 * 1024, 100, "ประกาศ").unwrap();
        assert!(res.exists);
        assert_eq!(res.total_lines, 3);
        assert_eq!(res.matched_count, 1);
        assert_eq!(res.lines, vec!["[10:00:05] Admin: ประกาศ"]);

        let cp874_file = dir.join("chat_874.txt");
        let (encoded, _, _) = WINDOWS_874.encode("[11:00:00] ทดสอบภาษาไทย\r\n");
        fs::write(&cp874_file, encoded.as_ref()).unwrap();
        let res2 = read_chatlog_file(Some(&cp874_file), 1024 * 1024, 100, "").unwrap();
        assert_eq!(res2.lines, vec!["[11:00:00] ทดสอบภาษาไทย"]);

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_gallery_scanner_filters_and_sorts_newest() {
        let dir = temp_dir("gal");
        fs::write(dir.join("ignore.txt"), b"txt").unwrap();
        fs::write(dir.join("a.png"), b"png1").unwrap();
        std::thread::sleep(std::time::Duration::from_millis(20));
        fs::write(dir.join("b.jpg"), b"jpg2").unwrap();

        let res = scan_gallery_dir(Some(&dir), 150);
        assert!(res.exists);
        assert_eq!(res.total_found, 2);
        assert_eq!(res.items[0].name, "b.jpg");
        assert_eq!(res.items[1].name, "a.png");

        let _ = fs::remove_dir_all(&dir);
    }

    #[test]
    fn test_wav_pcm_headers_valid() {
        let b = wav_bank();
        assert_eq!(&b.click[0..4], b"RIFF");
        assert_eq!(&b.click[8..12], b"WAVE");
        assert_eq!(&b.alert[0..4], b"RIFF");
        assert_eq!(&b.launch[0..4], b"RIFF");
        assert!(b.click.len() > 44 && b.alert.len() > 44 && b.launch.len() > 44);
    }
}
