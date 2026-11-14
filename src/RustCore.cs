using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

internal class RustCore : ICore
{
	private string engineVersion = "Rust (samp_core.dll)";

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_version();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_free_str(IntPtr ptr);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_resolve_host(byte[] host);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_query_server(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_query_players(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_query_rules(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_launch_and_inject(byte[] gtaPath, byte[] dllPath, byte[] workDir, byte[] nick, byte[] ip, byte[] port, byte[] pass);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern ulong core_scan_start(byte[] serversJson, uint concurrency);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern ulong core_public_scan_start(byte[] cachePath, uint concurrency);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_scan_poll(ulong handle, uint maxItems);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_scan_cancel(ulong handle);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_rcon_send(byte[] host, ushort port, byte[] pass, byte[] cmd);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_init(byte[] dbPath, byte[] playtimeTxtPath, byte[] statsTxtPath);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern long core_db_add_playtime(byte[] serverKey, long deltaSeconds);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_get_all_playtime();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_db_record_stat(byte[] ip, byte[] port, byte[] recordedAt, int players);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_get_stats(byte[] ip, byte[] port, uint limit);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_health_check(byte[] gameDir, byte[] skygfxPreset);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_health_autofix(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_snapshot_create(byte[] gameDir, byte[] name);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_snapshot_list(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_snapshot_restore(byte[] gameDir, byte[] snapshotId);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_snapshot_delete(byte[] gameDir, byte[] snapshotId);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_db_close();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_assets_scan(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_modloader_get(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_modloader_save(byte[] gameDir, [MarshalAs(UnmanagedType.U1)] bool ignoreAll, byte[] disabledJson);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_modloader_set_priority(byte[] gameDir, byte[] folderName, uint priority);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_gfx_apply_skygfx(byte[] gameDir, byte[] preset);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_gfx_apply_fps(byte[] gameDir, uint fps);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_guard_status(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_crash_clear(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private static extern bool core_ui_apply_dwm(IntPtr hwnd);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private static extern bool core_ui_set_accent(IntPtr hwnd, byte r, byte g, byte b);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private static extern bool core_ui_style_listview(IntPtr hwnd, uint headerBgRgb, uint accentRgb);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_ping_spark_start(byte[] ip, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_ping_spark_get(byte[] ip, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_ping_spark_stop();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_shield_diagnostics(byte[] gameDir, byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_shield_save_ini(byte[] gameDir, byte[] updatesJson);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_prewarm_server(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern ulong core_trim_memory();

	public string EngineName
	{
		get { return engineVersion; }
	}

	public bool SupportsBatchScan
	{
		get { return true; }
	}

	private static byte[] ToUtf8(string s)
	{
		return Encoding.UTF8.GetBytes((s ?? "") + "\0");
	}

	private static string TakeOwnedStr(IntPtr ptr)
	{
		if (ptr == IntPtr.Zero)
		{
			return null;
		}
		try
		{
			int len = 0;
			while (Marshal.ReadByte(ptr, len) != 0)
			{
				len++;
			}
			if (len == 0)
			{
				return "";
			}
			byte[] buf = new byte[len];
			Marshal.Copy(ptr, buf, 0, len);
			return Encoding.UTF8.GetString(buf);
		}
		finally
		{
			core_free_str(ptr);
		}
	}

	public bool PingSelfTest()
	{
		string ver = TakeOwnedStr(core_version());
		if (string.IsNullOrEmpty(ver))
		{
			return false;
		}
		engineVersion = "Rust (" + ver + ")";
		return true;
	}

	public string ResolveHost(string host)
	{
		return TakeOwnedStr(core_resolve_host(ToUtf8(host)));
	}

	public QueryResult QueryServer(string host, int port)
	{
		QueryResult qr = new QueryResult();
		if (port < 1 || port > 65535)
		{
			return qr;
		}
		string json = TakeOwnedStr(core_query_server(ToUtf8(host), (ushort)port));
		if (string.IsNullOrEmpty(json))
		{
			return qr;
		}
		try
		{
			Dictionary<string, object> d = DeserializeDict(json);
			if (d != null)
			{
				ParseQueryInfoInto(d, qr);
			}
		}
		catch
		{
		}
		return qr;
	}

	private static void ParseQueryInfoInto(Dictionary<string, object> d, QueryResult qr)
	{
		qr.Online = GetBool(d, "online");
		qr.Name = GetStr(d, "name");
		qr.Players = GetInt(d, "players", -1);
		qr.Max = GetInt(d, "max", 0);
		qr.Ping = GetInt(d, "ping", -1);
		qr.Mode = GetStr(d, "mode");
		qr.Language = GetStr(d, "language");
		qr.IsOpenMp = GetBool(d, "is_open_mp");
		qr.Locked = GetBool(d, "password_locked");
	}

	public List<string[]> QueryPlayers(string host, int port)
	{
		if (port < 1 || port > 65535)
		{
			return null;
		}
		string json = TakeOwnedStr(core_query_players(ToUtf8(host), (ushort)port));
		return ParseStringMatrix(json);
	}

	public List<string[]> QueryRules(string host, int port)
	{
		if (port < 1 || port > 65535)
		{
			return null;
		}
		string json = TakeOwnedStr(core_query_rules(ToUtf8(host), (ushort)port));
		return ParseStringMatrix(json);
	}

	public string RconSend(string host, int port, string pass, string cmd)
	{
		if (port < 1 || port > 65535)
		{
			return "(พอร์ตไม่ถูกต้อง)";
		}
		string res = TakeOwnedStr(core_rcon_send(
			ToUtf8(host),
			(ushort)port,
			ToUtf8(pass ?? ""),
			ToUtf8(cmd ?? "")));
		return res ?? "(ไม่มีการตอบกลับ)";
	}

	public void LaunchAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass)
	{
		string err = TakeOwnedStr(core_launch_and_inject(
			ToUtf8(gtaPath),
			ToUtf8(dllPath),
			ToUtf8(workDir),
			ToUtf8(nick),
			ToUtf8(ip),
			ToUtf8(port),
			ToUtf8(pass ?? "")));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public ulong StartScan(List<ServerEntry> targets, int concurrency)
	{
		List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
		foreach (ServerEntry e in targets)
		{
			int p;
			if (int.TryParse(e.Port, out p) && p > 0 && p <= 65535)
			{
				Dictionary<string, object> item = new Dictionary<string, object>();
				item["ip"] = e.Ip;
				item["port"] = p;
				list.Add(item);
			}
		}
		if (list.Count == 0)
		{
			return 0;
		}
		JavaScriptSerializer js = new JavaScriptSerializer();
		string json = js.Serialize(list);
		return core_scan_start(ToUtf8(json), (uint)concurrency);
	}

	public ulong StartPublicScan(string cachePath, int concurrency)
	{
		return core_public_scan_start(ToUtf8(cachePath ?? ""), (uint)concurrency);
	}

	public ScanPollResult PollScan(ulong handle, int maxItems)
	{
		ScanPollResult res = new ScanPollResult();
		string json = TakeOwnedStr(core_scan_poll(handle, (uint)maxItems));
		if (string.IsNullOrEmpty(json))
		{
			res.Done = true;
			return res;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			js.MaxJsonLength = 16 * 1024 * 1024;
			Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
			if (d == null)
			{
				res.Done = true;
				return res;
			}
			res.Done = GetBool(d, "done");
			res.FromCache = GetBool(d, "from_cache");
			object errObj;
			if (d.TryGetValue("error", out errObj) && errObj != null)
			{
				res.Error = errObj as string;
			}
			object listObj;
			if (d.TryGetValue("list", out listObj) && listObj != null)
			{
				IList arr = listObj as IList;
				if (arr != null)
				{
					res.List = new List<ServerEntry>(arr.Count);
					foreach (object o in arr)
					{
						Dictionary<string, object> sd = o as Dictionary<string, object>;
						if (sd != null)
						{
							ServerEntry se = new ServerEntry();
							se.Ip = GetStr(sd, "ip");
							se.Port = GetStr(sd, "port");
							se.Name = GetStr(sd, "name");
							se.Players = GetInt(sd, "players", -1);
							se.Max = GetInt(sd, "max", 0);
							se.Mode = GetStr(sd, "mode");
							se.Locked = GetBool(sd, "locked");
							se.IsOpenMp = GetBool(sd, "is_open_mp");
							se.Language = GetStr(sd, "language");
							se.Fav = false;
							se.Ping = -2;
							res.List.Add(se);
						}
					}
				}
			}
			object updObj;
			if (d.TryGetValue("updates", out updObj) && updObj != null)
			{
				IList arr = updObj as IList;
				if (arr != null)
				{
					foreach (object o in arr)
					{
						Dictionary<string, object> ud = o as Dictionary<string, object>;
						if (ud != null)
						{
							ScanUpdateItem u = new ScanUpdateItem();
							u.Ip = GetStr(ud, "ip");
							u.Port = GetInt(ud, "port", 0);
							u.Online = GetBool(ud, "online");
							u.Name = GetStr(ud, "name");
							u.Players = GetInt(ud, "players", -1);
							u.Max = GetInt(ud, "max", 0);
							u.Ping = GetInt(ud, "ping", -1);
							u.Mode = GetStr(ud, "mode");
							u.Language = GetStr(ud, "language");
							u.IsOpenMp = GetBool(ud, "is_open_mp");
							u.Locked = GetBool(ud, "locked");
							res.Updates.Add(u);
						}
					}
				}
			}
		}
		catch
		{
			res.Done = true;
		}
		return res;
	}

	public void CancelScan(ulong handle)
	{
		if (handle != 0)
		{
			core_scan_cancel(handle);
		}
	}

	public void InitStorage(string dbPath, string playtimeTxtPath, string statsTxtPath)
	{
		string err = TakeOwnedStr(core_db_init(
			ToUtf8(dbPath),
			ToUtf8(playtimeTxtPath ?? ""),
			ToUtf8(statsTxtPath ?? "")));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public Dictionary<string, long> LoadAllPlaytime()
	{
		Dictionary<string, long> map = new Dictionary<string, long>();
		string json = TakeOwnedStr(core_db_get_all_playtime());
		if (string.IsNullOrEmpty(json))
		{
			return map;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
			if (d != null)
			{
				foreach (KeyValuePair<string, object> kv in d)
				{
					long secs;
					try
					{
						secs = Convert.ToInt64(kv.Value);
					}
					catch
					{
						secs = 0L;
					}
					if (secs > 0)
					{
						map[kv.Key] = secs;
					}
				}
			}
		}
		catch
		{
		}
		return map;
	}

	public long AddPlaytime(string serverKey, long deltaSeconds)
	{
		long total = core_db_add_playtime(ToUtf8(serverKey), deltaSeconds);
		return (total < 0) ? 0L : total;
	}

	public void RecordStat(string ip, string port, string recordedAt, int players)
	{
		core_db_record_stat(ToUtf8(ip), ToUtf8(port), ToUtf8(recordedAt), players);
	}

	public List<int> GetStats(string ip, string port, int limit)
	{
		List<int> list = new List<int>();
		string json = TakeOwnedStr(core_db_get_stats(ToUtf8(ip), ToUtf8(port), (uint)(limit > 0 ? limit : 500)));
		if (string.IsNullOrEmpty(json))
		{
			return list;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			IList arr = js.DeserializeObject(json) as IList;
			if (arr != null)
			{
				foreach (object item in arr)
				{
					try
					{
						list.Add(Convert.ToInt32(item));
					}
					catch
					{
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public HealthCheckReport CheckHealth(string gameDir, string skyGfxPreset)
	{
		HealthCheckReport fallbackRep = new HealthCheckReport();
		string json = TakeOwnedStr(core_health_check(ToUtf8(gameDir), ToUtf8(skyGfxPreset ?? "ps2")));
		if (string.IsNullOrEmpty(json))
		{
			return fallbackRep;
		}
		try
		{
			Dictionary<string, object> d = DeserializeDict(json);
			if (d == null)
			{
				return fallbackRep;
			}
			HealthCheckReport r = new HealthCheckReport();
			r.GtaStatus = GetStr(d, "gta_status");
			r.GtaMsg = GetStr(d, "gta_msg");
			r.SampStatus = GetStr(d, "samp_status");
			r.SampMsg = GetStr(d, "samp_msg");
			r.ModsStatus = GetStr(d, "mods_status");
			r.ModsMsg = GetStr(d, "mods_msg");
			r.EnvStatus = GetStr(d, "env_status");
			r.EnvMsg = GetStr(d, "env_msg");
			r.GfxStatus = GetStr(d, "gfx_status");
			r.GfxMsg = GetStr(d, "gfx_msg");
			r.DuplicateAsis = GetStrList(d, "duplicate_asis");
			r.ReadonlyFiles = GetStrList(d, "readonly_files");
			r.InProgramFiles = GetBool(d, "in_program_files");
			r.MissingDx9 = GetBool(d, "missing_dx9");
			r.NeedsLaa = GetBool(d, "needs_laa");
			r.FixableCount = GetInt(d, "fixable_count", 0);
			return r;
		}
		catch
		{
			return fallbackRep;
		}
	}

	public string AutoFixHealth(string gameDir)
	{
		string json = TakeOwnedStr(core_health_autofix(ToUtf8(gameDir)));
		if (string.IsNullOrEmpty(json))
		{
			return "เกิดข้อผิดพลาดในการซ่อมอัตโนมัติ (samp_core.dll)";
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return "เกิดข้อผิดพลาดในการซ่อมอัตโนมัติ";
		}
		if (!GetBool(d, "ok"))
		{
			return GetStr(d, "error");
		}
		Dictionary<string, object> data = GetDict(d, "data");
		if (data == null)
		{
			return "ดำเนินการเสร็จสิ้น";
		}
		int ro = GetInt(data, "fixed_readonly", 0);
		bool laa = GetBool(data, "laa_enabled");
		int optCfg = GetInt(data, "optimized_configs", 0);
		int cleanedLogs = GetInt(data, "cleaned_logs", 0);
		List<string> dis = GetStrList(data, "disabled_duplicates");
		List<string> rem = GetStrList(data, "remaining_warnings");
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("ซ่อมแซมอัตโนมัติเรียบร้อย:");
		sb.AppendLine("• ปลดล็อกไฟล์ Read-Only: " + ro + " ไฟล์");
		sb.AppendLine("• ปิด .asi ที่ซ้ำซ้อน (.disabled): " + dis.Count + " ไฟล์");
		sb.AppendLine("• เปิดใช้งาน 4GB Large Address Aware: " + (laa ? "เปิดแล้ว" : "เปิดอยู่แล้ว / ข้าม"));
		sb.AppendLine("• ปรับจูนไฟล์ตั้งค่ากราฟิก/หน่วยความจำ/FPS: " + optCfg + " ไฟล์");
		sb.AppendLine("• ล้างไฟล์ Log/Backup เก่าที่สะสม: " + cleanedLogs + " ไฟล์");
		if (rem.Count > 0)
		{
			sb.AppendLine();
			sb.AppendLine("หมายเหตุเพิ่มเติม:");
			foreach (string w in rem)
			{
				sb.AppendLine("  - " + w);
			}
		}
		return sb.ToString().TrimEnd();
	}

	public string CreateSnapshot(string gameDir, string name)
	{
		string json = TakeOwnedStr(core_snapshot_create(ToUtf8(gameDir), ToUtf8(name ?? "")));
		if (string.IsNullOrEmpty(json))
		{
			throw new Exception("สร้าง Snapshot ไม่สำเร็จ (samp_core.dll)");
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			throw new Exception("สร้าง Snapshot ไม่สำเร็จ");
		}
		if (!GetBool(d, "ok"))
		{
			throw new Exception(GetStr(d, "error"));
		}
		Dictionary<string, object> s = GetDict(d, "data");
		if (s == null)
		{
			return "สำรอง Snapshot เรียบร้อยแล้ว";
		}
		int files = GetInt(s, "file_count", 0);
		long raw = GetLong(s, "total_raw_bytes", 0);
		long comp = GetLong(s, "total_compressed_bytes", 0);
		int skipped = GetInt(s, "skipped_large_count", 0);
		List<string> skippedList = GetStrList(s, "skipped_large_files");
		string msg = string.Format("สำรอง '{0}' ({1} ไฟล์: {2} KB → {3} KB zstd)",
			GetStr(s, "name"), files, Math.Max(1, raw / 1024), Math.Max(1, comp / 1024));
		if (skipped > 0)
		{
			msg += string.Format("\r\n⚠ ข้ามไฟล์ > 32 MB จำนวน {0} ไฟล์ ({1})",
				skipped, string.Join(", ", skippedList.ToArray()));
		}
		return msg;
	}

	public List<SnapshotInfo> ListSnapshots(string gameDir)
	{
		string json = TakeOwnedStr(core_snapshot_list(ToUtf8(gameDir)));
		List<SnapshotInfo> list = new List<SnapshotInfo>();
		if (string.IsNullOrEmpty(json))
		{
			return list;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			IList arr = js.DeserializeObject(json) as IList;
			if (arr != null)
			{
				foreach (object o in arr)
				{
					Dictionary<string, object> sd = o as Dictionary<string, object>;
					if (sd != null)
					{
						SnapshotInfo info = new SnapshotInfo();
						info.Id = GetStr(sd, "id");
						info.Name = GetStr(sd, "name");
						info.CreatedAt = GetStr(sd, "created_at");
						info.FileCount = GetInt(sd, "file_count", 0);
						info.TotalRawBytes = GetLong(sd, "total_raw_bytes", 0);
						info.TotalCompressedBytes = GetLong(sd, "total_compressed_bytes", 0);
						info.SkippedLargeCount = GetInt(sd, "skipped_large_count", 0);
						info.SkippedLargeFiles = GetStrList(sd, "skipped_large_files");
						list.Add(info);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public string RestoreSnapshot(string gameDir, string snapshotId)
	{
		string json = TakeOwnedStr(core_snapshot_restore(ToUtf8(gameDir), ToUtf8(snapshotId)));
		if (string.IsNullOrEmpty(json))
		{
			throw new Exception("กู้คืน Snapshot ไม่สำเร็จ");
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			throw new Exception("ข้อมูล Snapshot ไม่ถูกต้อง");
		}
		if (!GetBool(d, "ok"))
		{
			throw new Exception(GetStr(d, "error"));
		}
		Dictionary<string, object> r = GetDict(d, "data");
		if (r == null)
		{
			return "กู้คืน Snapshot เรียบร้อยแล้ว";
		}
		string msg = "กู้คืน '" + GetStr(r, "name") + "' (" + GetInt(r, "restored_files", 0) + " ไฟล์) สำเร็จ";
		string preId = GetStr(r, "pre_restore_id");
		if (!string.IsNullOrEmpty(preId))
		{
			msg += " • สำรองสถานะก่อนกู้คืนไว้แล้ว (" + preId + ")";
		}
		return msg;
	}

	public void DeleteSnapshot(string gameDir, string snapshotId)
	{
		string err = TakeOwnedStr(core_snapshot_delete(ToUtf8(gameDir), ToUtf8(snapshotId)));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public AssetScanReport ScanAssets(string gameDir)
	{
		IntPtr ptr = core_assets_scan(ToUtf8(gameDir));
		string json = TakeOwnedStr(ptr);
		AssetScanReport report = new AssetScanReport();
		if (string.IsNullOrEmpty(json))
		{
			return report;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return report;
		}
		report.ScannedFiles = GetInt(d, "scanned_files", 0);
		report.DffCount = GetInt(d, "dff_count", 0);
		report.TxdCount = GetInt(d, "txd_count", 0);
		report.ColCount = GetInt(d, "col_count", 0);
		report.IfpCount = GetInt(d, "ifp_count", 0);
		report.ImgCount = GetInt(d, "img_count", 0);
		report.TextureCount = GetInt(d, "texture_count", 0);
		report.ErrorCount = GetInt(d, "error_count", 0);
		report.WarnCount = GetInt(d, "warn_count", 0);
		report.DuplicateCount = GetInt(d, "duplicate_count", 0);
		report.TruncatedIssues = GetBool(d, "truncated_issues");

		object rawIssues;
		if (d.TryGetValue("issues", out rawIssues))
		{
			IList list = rawIssues as IList;
			if (list != null)
			{
				foreach (object item in list)
				{
					Dictionary<string, object> id = item as Dictionary<string, object>;
					if (id != null)
					{
						AssetIssue issue = new AssetIssue();
						issue.Severity = GetStr(id, "severity");
						issue.Kind = GetStr(id, "kind");
						issue.RelPath = GetStr(id, "rel_path");
						issue.Message = GetStr(id, "message");
						report.Issues.Add(issue);
					}
				}
			}
		}
		return report;
	}

	public ModLoaderState GetModLoaderState(string gameDir)
	{
		IntPtr ptr = core_modloader_get(ToUtf8(gameDir));
		string json = TakeOwnedStr(ptr);
		ModLoaderState state = new ModLoaderState();
		if (string.IsNullOrEmpty(json))
		{
			return state;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return state;
		}
		state.Installed = GetBool(d, "installed");
		state.Profile = GetStr(d, "profile");
		if (string.IsNullOrEmpty(state.Profile)) state.Profile = "Default";
		state.IgnoreAll = GetBool(d, "ignore_all");

		object rawFolders;
		if (d.TryGetValue("folders", out rawFolders))
		{
			IList list = rawFolders as IList;
			if (list != null)
			{
				foreach (object item in list)
				{
					Dictionary<string, object> fd = item as Dictionary<string, object>;
					if (fd != null)
					{
						ModLoaderFolder folder = new ModLoaderFolder();
						folder.Name = GetStr(fd, "name");
						folder.Enabled = GetBool(fd, "enabled");
						folder.FileCount = GetInt(fd, "file_count", 0);
						folder.TotalBytes = GetLong(fd, "total_bytes", 0);
						folder.Priority = GetInt(fd, "priority", 50);
						state.Folders.Add(folder);
					}
				}
			}
		}
		return state;
	}

	public string SaveModLoaderState(string gameDir, bool ignoreAll, List<string> disabledFolders)
	{
		JavaScriptSerializer js = new JavaScriptSerializer();
		string disJson = js.Serialize(disabledFolders ?? new List<string>());
		IntPtr ptr = core_modloader_save(ToUtf8(gameDir), ignoreAll, ToUtf8(disJson));
		string err = TakeOwnedStr(ptr);
		return err;
	}

	public string SetModLoaderPriority(string gameDir, string folderName, int priority)
	{
		uint prio = (uint)Math.Max(1, Math.Min(100, priority));
		IntPtr ptr = core_modloader_set_priority(ToUtf8(gameDir), ToUtf8(folderName ?? ""), prio);
		return TakeOwnedStr(ptr);
	}

	public string ApplySkyGfx(string gameDir, string preset)
	{
		string json = TakeOwnedStr(core_gfx_apply_skygfx(ToUtf8(gameDir), ToUtf8(preset ?? "")));
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			throw new Exception("เกิดข้อผิดพลาดจาก samp_core.dll");
		}
		if (!GetBool(d, "ok"))
		{
			string err = GetStr(d, "error");
			throw new Exception(string.IsNullOrEmpty(err) ? "สลับโทนภาพไม่สำเร็จ" : err);
		}
		return GetStr(d, "data");
	}

	public string ApplyFpsLimit(string gameDir, int fps)
	{
		uint clamped = (uint)Math.Max(0, fps);
		string json = TakeOwnedStr(core_gfx_apply_fps(ToUtf8(gameDir), clamped));
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			throw new Exception("เกิดข้อผิดพลาดจาก samp_core.dll");
		}
		if (!GetBool(d, "ok"))
		{
			string err = GetStr(d, "error");
			throw new Exception(string.IsNullOrEmpty(err) ? "ตั้งค่า FPS ไม่สำเร็จ" : err);
		}
		return GetStr(d, "data");
	}

	public GuardRuntimeStatus GetGuardStatus(string gameDir)
	{
		IntPtr ptr = core_guard_status(ToUtf8(gameDir));
		string json = TakeOwnedStr(ptr);
		GuardRuntimeStatus st = new GuardRuntimeStatus();
		if (string.IsNullOrEmpty(json))
		{
			return st;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return st;
		}
		st.HasCrash = GetBool(d, "has_crash");

		Dictionary<string, object> cd = GetDict(d, "crash");
		if (cd != null)
		{
			st.Crash = ParseCrashInfo(cd);
		}

		Dictionary<string, object> td = GetDict(d, "telemetry");
		if (td != null)
		{
			TelemetryData t = new TelemetryData();
			t.Pid = GetInt(td, "pid", 0);
			t.WorkingSetMb = GetLong(td, "working_set_mb", 0);
			t.PeakWorkingSetMb = GetLong(td, "peak_working_set_mb", 0);
			t.PrivateBytesMb = GetLong(td, "private_bytes_mb", 0);
			t.VirtualMemMb = GetLong(td, "virtual_mem_mb", 0);
			t.Timestamp = GetStr(td, "timestamp");
			t.SystemFreeRamMb = GetLong(td, "system_free_ram_mb", 0);
			t.ActiveMic = GetStr(td, "active_mic");
			t.NetShieldActive = GetBool(td, "net_shield_active");
			t.AudioShieldActive = GetBool(td, "audio_shield_active");
			t.GpuShieldActive = GetBool(td, "gpu_shield_active");
			t.BorderlessActive = GetBool(td, "borderless_active");
			t.TunedUdpSockets = GetInt(td, "tuned_udp_sockets", 0);
			st.Telemetry = t;
		}

		return st;
	}

	public void ClearLatestCrash(string gameDir)
	{
		try
		{
			core_crash_clear(ToUtf8(gameDir));
		}
		catch
		{
		}
	}

	public bool ApplyDwm(IntPtr hWnd)
	{
		try
		{
			return core_ui_apply_dwm(hWnd);
		}
		catch
		{
			return false;
		}
	}

	private static uint PackRgb(Color c)
	{
		return (uint)((c.R << 16) | (c.G << 8) | c.B);
	}

	public bool SetDwmAccent(IntPtr hWnd, Color accent)
	{
		try
		{
			return core_ui_set_accent(hWnd, accent.R, accent.G, accent.B);
		}
		catch
		{
			return false;
		}
	}

	public bool StyleListView(IntPtr lvHwnd, Color headerBg, Color accent)
	{
		try
		{
			return core_ui_style_listview(lvHwnd, PackRgb(headerBg), PackRgb(accent));
		}
		catch
		{
			return false;
		}
	}

	public void StartPingSpark(string ip, int port)
	{
		try
		{
			if (string.IsNullOrEmpty(ip) || port <= 0 || port > 65535)
			{
				core_ping_spark_stop();
				return;
			}
			core_ping_spark_start(ToUtf8(ip), (ushort)port);
		}
		catch
		{
		}
	}

	public PingSparkReport GetPingSpark(string ip, int port)
	{
		PingSparkReport rep = new PingSparkReport();
		rep.Host = ip ?? "";
		rep.Port = port;
		if (string.IsNullOrEmpty(ip) || port <= 0 || port > 65535)
		{
			return rep;
		}
		try
		{
			IntPtr ptr = core_ping_spark_get(ToUtf8(ip), (ushort)port);
			string json = TakeOwnedStr(ptr);
			if (string.IsNullOrEmpty(json))
			{
				return rep;
			}
			Dictionary<string, object> d = DeserializeDict(json);
			if (d == null)
			{
				return rep;
			}
			rep.Latest = GetInt(d, "latest", -1);
			rep.Min = GetInt(d, "min", 0);
			rep.Max = GetInt(d, "max", 0);
			rep.Avg = GetInt(d, "avg", 0);
			rep.Jitter = GetDouble(d, "jitter", 0.0);
			rep.Loss = GetDouble(d, "loss", 0.0);
			object rawHist;
			if (d.TryGetValue("history", out rawHist))
			{
				IList list = rawHist as IList;
				if (list != null)
				{
					foreach (object item in list)
					{
						if (item != null)
						{
							try { rep.History.Add(Convert.ToInt32(item)); } catch { }
						}
					}
				}
			}
		}
		catch
		{
		}
		return rep;
	}

	public void StopPingSpark()
	{
		try
		{
			core_ping_spark_stop();
		}
		catch
		{
		}
	}

	public ShieldDiagnostics GetShieldDiagnostics(string gameDir, string host, int port)
	{
		ShieldDiagnostics res = new ShieldDiagnostics();
		try
		{
			ushort p = (port > 0 && port <= 65535) ? (ushort)port : (ushort)7777;
			string json = TakeOwnedStr(core_shield_diagnostics(ToUtf8(gameDir ?? ""), ToUtf8(host ?? ""), p));
			if (string.IsNullOrEmpty(json))
			{
				return res;
			}
			Dictionary<string, object> root = DeserializeDict(json);
			if (root == null)
			{
				return res;
			}

			Dictionary<string, object> st = GetDict(root, "settings");
			if (st != null)
			{
				res.Enable = GetBool(st, "enable");
				res.Borderless = GetBool(st, "borderless");
				res.CatchCrash = GetBool(st, "catch_crash");
				res.HighPrecisionTimer = GetBool(st, "high_precision_timer");
				res.Telemetry = GetBool(st, "telemetry");
				res.NetShield = GetBool(st, "net_shield");
				res.AudioShield = GetBool(st, "audio_shield");
				res.PreferredMic = GetStr(st, "preferred_mic");
				res.GpuBoost = GetBool(st, "gpu_boost");
				res.ForceImmediatePresent = GetBool(st, "force_immediate_present");
				string rm = GetStr(st, "resource_mode");
				if (!string.IsNullOrEmpty(rm)) res.ResourceMode = rm;
				string ca = GetStr(st, "cpu_affinity");
				if (!string.IsNullOrEmpty(ca)) res.CpuAffinity = ca;
			}

			Dictionary<string, object> net = GetDict(root, "net");
			if (net != null)
			{
				Dictionary<string, object> fw = GetDict(net, "firewall");
				if (fw != null)
				{
					res.FirewallChecked = GetBool(fw, "checked");
					res.FirewallGtaAllowed = GetBool(fw, "gta_sa_allowed");
					res.FirewallGtaBlocked = GetBool(fw, "gta_sa_blocked");
					res.FirewallSummary = GetStr(fw, "summary");
				}
				res.AltTabNoPause = GetBool(net, "alt_tab_nopause");
				res.AltTabNote = GetStr(net, "alt_tab_note");
				res.LastVoiceServer = GetStr(net, "last_voice_server");
				res.LastVoicePort = GetInt(net, "last_voice_port", 0);
			}

			Dictionary<string, object> aud = GetDict(root, "audio");
			if (aud != null)
			{
				res.MicConsentAllowed = GetBool(aud, "mic_consent_allowed");
				res.AudioRecommendation = GetStr(aud, "recommendation");
				object rawDevs;
				if (aud.TryGetValue("devices", out rawDevs))
				{
					IList list = rawDevs as IList;
					if (list != null)
					{
						foreach (object item in list)
						{
							Dictionary<string, object> dm = item as Dictionary<string, object>;
							if (dm != null)
							{
								MicDeviceInfo mi = new MicDeviceInfo();
								mi.Id = GetInt(dm, "id", 0);
								mi.Name = GetStr(dm, "name");
								mi.IsVirtual = GetBool(dm, "is_virtual");
								mi.Recommended = GetBool(dm, "recommended");
								res.Mics.Add(mi);
							}
						}
					}
				}
				Dictionary<string, object> sv = GetDict(aud, "svlog");
				if (sv != null)
				{
					res.SvlogPauseDisconnect = GetBool(sv, "had_pause_disconnect");
					res.SvlogErrors = GetStrList(sv, "recent_errors");
				}
			}

			Dictionary<string, object> gp = GetDict(root, "gpu");
			if (gp != null)
			{
				res.IsHybridGpu = GetBool(gp, "is_hybrid");
				res.IgpuName = GetStr(gp, "igpu_name");
				res.DgpuName = GetStr(gp, "dgpu_name");
				res.WindowsGpuPref = GetStr(gp, "windows_pref_for_gta");
				res.GpuSummary = GetStr(gp, "status_summary");
			}

			Dictionary<string, object> rs = GetDict(root, "resources");
			if (rs != null)
			{
				res.TotalRamMb = GetLong(rs, "total_phys_mb", 0);
				res.AvailRamMb = GetLong(rs, "avail_phys_mb", 0);
				res.MemoryLoadPct = GetInt(rs, "memory_load_pct", 0);
				res.LogicalCores = GetInt(rs, "logical_cores", 0);
				res.RecommendedStreamMb = GetInt(rs, "recommended_stream_mb", 512);
				res.RecommendedLimitMb = GetInt(rs, "recommended_limit_mb", 512);
				res.LauncherWorkingSetMb = GetLong(rs, "launcher_working_set_mb", 0);
			}
		}
		catch
		{
		}
		return res;
	}

	public string SaveShieldIni(string gameDir, Dictionary<string, string> updates)
	{
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			string payload = js.Serialize(updates ?? new Dictionary<string, string>());
			string json = TakeOwnedStr(core_shield_save_ini(ToUtf8(gameDir ?? ""), ToUtf8(payload)));
			if (!string.IsNullOrEmpty(json))
			{
				Dictionary<string, object> d = DeserializeDict(json);
				if (d != null && GetBool(d, "ok"))
				{
					return null;
				}
				if (d != null)
				{
					return GetStr(d, "error");
				}
			}
			return "บันทึก SonicGuard.ini ไม่สำเร็จ";
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}

	public PrewarmReport PrewarmServer(string host, int port)
	{
		PrewarmReport rep = new PrewarmReport();
		try
		{
			if (string.IsNullOrEmpty(host) || port <= 0 || port > 65535)
			{
				rep.Summary = "IP หรือ Port ไม่ถูกต้อง";
				return rep;
			}
			string json = TakeOwnedStr(core_prewarm_server(ToUtf8(host), (ushort)port));
			if (!string.IsNullOrEmpty(json))
			{
				Dictionary<string, object> d = DeserializeDict(json);
				if (d != null)
				{
					rep.Host = GetStr(d, "host");
					rep.ResolvedIp = GetStr(d, "resolved_ip");
					rep.GamePort = GetInt(d, "game_port", port);
					rep.VoicePort = GetInt(d, "voice_port", 0);
					rep.DnsMs = GetInt(d, "dns_ms", 0);
					rep.GamePingMs = GetInt(d, "game_ping_ms", -1);
					rep.VoiceRouteWarmed = GetBool(d, "voice_route_warmed");
					rep.Summary = GetStr(d, "summary");
				}
			}
		}
		catch
		{
		}
		return rep;
	}

	public long TrimLauncherMemory()
	{
		try
		{
			return (long)core_trim_memory();
		}
		catch
		{
			return 0L;
		}
	}

	private static double GetDouble(Dictionary<string, object> d, string key, double def)
	{
		object v;
		if (d.TryGetValue(key, out v) && v != null)
		{
			try { return Convert.ToDouble(v); } catch { }
		}
		return def;
	}

	public void CloseDb()
	{
		try
		{
			core_db_close();
		}
		catch
		{
		}
	}

	public LauncherSettingsDto LoadSettings(string cfgPath)
	{
		LauncherSettingsDto cfg = new LauncherSettingsDto();
		string json = TakeOwnedStr(core_settings_load(ToUtf8(cfgPath ?? "")));
		if (string.IsNullOrEmpty(json))
		{
			return cfg;
		}
		Dictionary<string, object> env = DeserializeDict(json);
		if (env == null || !GetBool(env, "ok"))
		{
			return cfg;
		}
		Dictionary<string, object> d = GetDict(env, "data");
		if (d == null)
		{
			return cfg;
		}
		cfg.Nick = GetStr(d, "nick");
		string acc = GetStr(d, "accent");
		if (!string.IsNullOrEmpty(acc)) cfg.Accent = acc;
		cfg.GamePath = GetStr(d, "game_path");
		cfg.AutoConnect = GetBool(d, "auto_connect");
		cfg.CloseAfterLaunch = GetBool(d, "close_after_launch");
		string lip = GetStr(d, "last_ip");
		if (!string.IsNullOrEmpty(lip)) cfg.LastIp = lip;
		string lpt = GetStr(d, "last_port");
		if (!string.IsNullOrEmpty(lpt)) cfg.LastPort = lpt;
		cfg.RconIp = GetStr(d, "rcon_ip");
		cfg.RconPort = GetStr(d, "rcon_port");
		cfg.RconPass = GetStr(d, "rcon_pass");
		cfg.SortCol = GetInt(d, "sort_col", 0);
		cfg.SortDesc = GetBool(d, "sort_desc");
		string sg = GetStr(d, "skygfx_preset");
		if (!string.IsNullOrEmpty(sg)) cfg.SkyGfxPreset = sg;
		cfg.FpsLimit = GetInt(d, "fps_limit", 0);
		cfg.AutoJoinSlot = GetBool(d, "auto_join_slot");
		if (d.ContainsKey("sound_fx")) cfg.SoundFx = GetBool(d, "sound_fx");
		if (d.ContainsKey("discord_rpc")) cfg.DiscordRpc = GetBool(d, "discord_rpc");
		cfg.DiscordAppId = GetStr(d, "discord_app_id");
		cfg.NoPauseAltTab = GetBool(d, "no_pause_alt_tab");
		cfg.NickProfiles = GetStrList(d, "nick_profiles");
		return cfg;
	}

	public string SaveSettings(string cfgPath, LauncherSettingsDto cfg)
	{
		if (cfg == null)
		{
			return "ไม่มีข้อมูลการตั้งค่า";
		}
		try
		{
			Dictionary<string, object> m = new Dictionary<string, object>();
			m["nick"] = cfg.Nick ?? "";
			m["accent"] = cfg.Accent ?? "138,43,226";
			m["game_path"] = cfg.GamePath ?? "";
			m["auto_connect"] = cfg.AutoConnect;
			m["close_after_launch"] = cfg.CloseAfterLaunch;
			m["last_ip"] = cfg.LastIp ?? "127.0.0.1";
			m["last_port"] = cfg.LastPort ?? "7777";
			m["rcon_ip"] = cfg.RconIp ?? "";
			m["rcon_port"] = cfg.RconPort ?? "";
			m["rcon_pass"] = cfg.RconPass ?? "";
			m["sort_col"] = cfg.SortCol;
			m["sort_desc"] = cfg.SortDesc;
			m["skygfx_preset"] = cfg.SkyGfxPreset ?? "ps2";
			m["fps_limit"] = cfg.FpsLimit;
			m["auto_join_slot"] = cfg.AutoJoinSlot;
			m["sound_fx"] = cfg.SoundFx;
			m["discord_rpc"] = cfg.DiscordRpc;
			m["discord_app_id"] = cfg.DiscordAppId ?? "";
			m["no_pause_alt_tab"] = cfg.NoPauseAltTab;
			m["nick_profiles"] = cfg.NickProfiles ?? new List<string>();
			JavaScriptSerializer js = new JavaScriptSerializer();
			string json = js.Serialize(m);
			return TakeOwnedStr(core_settings_save(ToUtf8(cfgPath ?? ""), ToUtf8(json)));
		}
		catch (Exception ex)
		{
			return ex.Message;
		}
	}

	public string SetAltTabNoPause(string gameDir, bool enable)
	{
		return TakeOwnedStr(core_alt_tab_set(ToUtf8(gameDir ?? ""), enable));
	}

	public bool ResetGtaSaSet()
	{
		string json = TakeOwnedStr(core_reset_gta_set());
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			throw new Exception("รีเซ็ต gta_sa.set ไม่สำเร็จ");
		}
		if (!GetBool(d, "ok"))
		{
			string err = GetStr(d, "error");
			throw new Exception(string.IsNullOrEmpty(err) ? "รีเซ็ต gta_sa.set ไม่สำเร็จ" : err);
		}
		return GetBool(d, "data");
	}

	private static List<SavedServerDto> ParseSavedServerList(string json)
	{
		List<SavedServerDto> list = new List<SavedServerDto>();
		if (string.IsNullOrEmpty(json))
		{
			return list;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			js.MaxJsonLength = 16 * 1024 * 1024;
			IList arr = js.DeserializeObject(json) as IList;
			if (arr != null)
			{
				foreach (object obj in arr)
				{
					Dictionary<string, object> d = obj as Dictionary<string, object>;
					if (d != null)
					{
						SavedServerDto item = new SavedServerDto();
						item.Ip = GetStr(d, "ip");
						item.Port = GetStr(d, "port");
						item.Name = GetStr(d, "name");
						item.LastJoinedAt = GetLong(d, "last_joined_at", 0);
						item.JoinCount = GetLong(d, "join_count", 0);
						list.Add(item);
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}

	public List<SavedServerDto> ListFavorites()
	{
		return ParseSavedServerList(TakeOwnedStr(core_db_fav_list()));
	}

	public void UpsertFavorite(string ip, string port, string name)
	{
		string err = TakeOwnedStr(core_db_fav_upsert(ToUtf8(ip ?? ""), ToUtf8(port ?? ""), ToUtf8(name ?? "")));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public bool RemoveFavorite(string ip, string port)
	{
		return core_db_fav_remove(ToUtf8(ip ?? ""), ToUtf8(port ?? "")) > 0;
	}

	public List<SavedServerDto> ListHistory(int limit)
	{
		uint cap = (limit > 0) ? (uint)limit : 30u;
		return ParseSavedServerList(TakeOwnedStr(core_db_hist_list(cap)));
	}

	public void RecordHistory(string ip, string port, string name)
	{
		string err = TakeOwnedStr(core_db_hist_record(ToUtf8(ip ?? ""), ToUtf8(port ?? ""), ToUtf8(name ?? "")));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public bool RemoveHistory(string ip, string port)
	{
		return core_db_hist_remove(ToUtf8(ip ?? ""), ToUtf8(port ?? "")) > 0;
	}

	public void RenameSavedServer(string ip, string port, string name)
	{
		string err = TakeOwnedStr(core_db_server_rename(ToUtf8(ip ?? ""), ToUtf8(port ?? ""), ToUtf8(name ?? "")));
		if (!string.IsNullOrEmpty(err))
		{
			throw new Exception(err);
		}
	}

	public WatchdogStatusDto PollWatchdog()
	{
		WatchdogStatusDto st = new WatchdogStatusDto();
		string json = TakeOwnedStr(core_watchdog_poll());
		if (string.IsNullOrEmpty(json))
		{
			return st;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return st;
		}
		st.Active = GetBool(d, "active");
		st.Running = GetBool(d, "running");
		st.Exited = GetBool(d, "exited");
		st.Crashed = GetBool(d, "crashed");
		long pid = GetLong(d, "pid", 0);
		st.Pid = (pid >= 0) ? (uint)pid : 0u;
		st.ServerKey = GetStr(d, "server_key");
		st.SessionSeconds = GetLong(d, "session_seconds", 0);
		st.TotalPlaytime = GetLong(d, "total_playtime", 0);
		long ec = GetLong(d, "exit_code", 0);
		st.ExitCode = (ec >= 0) ? (uint)ec : 0u;
		st.Crash = ParseCrashInfo(GetDict(d, "crash"));
		return st;
	}

	public void StopWatchdog()
	{
		try { core_watchdog_stop(); } catch { }
	}

	public bool IsGameRunning()
	{
		try { return core_is_game_running() == 1; } catch { return false; }
	}

	public ServerDetailsDto QueryServerDetails(string host, int port)
	{
		ServerDetailsDto res = new ServerDetailsDto();
		if (port < 1 || port > 65535 || string.IsNullOrEmpty(host))
		{
			return res;
		}
		string json = TakeOwnedStr(core_query_server_details(ToUtf8(host), (ushort)port));
		if (string.IsNullOrEmpty(json))
		{
			return res;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return res;
		}
		Dictionary<string, object> info = GetDict(d, "info");
		if (info != null)
		{
			ParseQueryInfoInto(info, res.Info);
		}
		object rawPlayers;
		if (d.TryGetValue("players", out rawPlayers) && rawPlayers != null)
		{
			res.Players = ParseStringMatrixList(rawPlayers as IList);
		}
		object rawRules;
		if (d.TryGetValue("rules", out rawRules) && rawRules != null)
		{
			res.Rules = ParseStringMatrixList(rawRules as IList);
		}
		return res;
	}

	public bool StartSlotWatch(string host, int port)
	{
		if (port < 1 || port > 65535 || string.IsNullOrEmpty(host))
		{
			return false;
		}
		try { return core_slot_watch_start(ToUtf8(host), (ushort)port) == 1; } catch { return false; }
	}

	public void StopSlotWatch()
	{
		try { core_slot_watch_stop(); } catch { }
	}

	public SlotWatchStatusDto PollSlotWatch()
	{
		SlotWatchStatusDto st = new SlotWatchStatusDto();
		string json = TakeOwnedStr(core_slot_watch_poll());
		if (string.IsNullOrEmpty(json))
		{
			return st;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return st;
		}
		st.Watching = GetBool(d, "watching");
		st.FoundSlot = GetBool(d, "found_slot");
		st.Ip = GetStr(d, "ip");
		st.Port = GetInt(d, "port", 0);
		st.Name = GetStr(d, "name");
		st.Players = GetInt(d, "players", 0);
		st.Max = GetInt(d, "max", 0);
		st.Ping = GetInt(d, "ping", -1);
		return st;
	}

	public ChatlogResultDto ReadChatlog(string path, int maxBytes, int maxLines, string query)
	{
		ChatlogResultDto res = new ChatlogResultDto();
		uint mb = (maxBytes > 0) ? (uint)maxBytes : 0u;
		uint ml = (maxLines > 0) ? (uint)maxLines : 0u;
		string json = TakeOwnedStr(core_chatlog_read(ToUtf8(path ?? ""), mb, ml, ToUtf8(query ?? "")));
		if (string.IsNullOrEmpty(json))
		{
			return res;
		}
		Dictionary<string, object> env = DeserializeDict(json);
		if (env == null || !GetBool(env, "ok"))
		{
			return res;
		}
		Dictionary<string, object> d = GetDict(env, "data");
		if (d == null)
		{
			return res;
		}
		res.Exists = GetBool(d, "exists");
		res.Path = GetStr(d, "path");
		res.FileSizeBytes = GetLong(d, "file_size_bytes", 0);
		res.TotalLines = GetInt(d, "total_lines", 0);
		res.MatchedCount = GetInt(d, "matched_count", 0);
		res.Truncated = GetBool(d, "truncated");
		res.Lines = GetStrList(d, "lines");
		return res;
	}

	public GalleryScanResultDto ScanGallery(string dir, int maxItems)
	{
		GalleryScanResultDto res = new GalleryScanResultDto();
		uint cap = (maxItems > 0) ? (uint)maxItems : 240u;
		string json = TakeOwnedStr(core_gallery_scan(ToUtf8(dir ?? ""), cap));
		if (string.IsNullOrEmpty(json))
		{
			return res;
		}
		Dictionary<string, object> d = DeserializeDict(json);
		if (d == null)
		{
			return res;
		}
		res.Directory = GetStr(d, "directory");
		res.Exists = GetBool(d, "exists");
		res.TotalFound = GetInt(d, "total_found", 0);
		object rawItems;
		if (d.TryGetValue("items", out rawItems) && rawItems != null)
		{
			IList arr = rawItems as IList;
			if (arr != null)
			{
				foreach (object obj in arr)
				{
					Dictionary<string, object> id = obj as Dictionary<string, object>;
					if (id != null)
					{
						GalleryItemDto it = new GalleryItemDto();
						it.Path = GetStr(id, "path");
						it.Name = GetStr(id, "name");
						it.SizeBytes = GetLong(id, "size_bytes", 0);
						it.ModifiedEpoch = GetLong(id, "modified_epoch", 0);
						res.Items.Add(it);
					}
				}
			}
		}
		return res;
	}

	internal static void DiscordInit(string clientId)
	{
		try { core_discord_init(ToUtf8(clientId ?? "")); } catch { }
	}

	internal static void DiscordSetEnabled(bool enabled)
	{
		try { core_discord_set_enabled(enabled); } catch { }
	}

	internal static void DiscordPresenceLauncher()
	{
		try { core_discord_presence_launcher(); } catch { }
	}

	internal static void DiscordPresenceGame(string serverName, string ip, string port, int players, int max)
	{
		try { core_discord_presence_game(ToUtf8(serverName ?? ""), ToUtf8(ip ?? ""), ToUtf8(port ?? ""), players, max); } catch { }
	}

	internal static void DiscordClear()
	{
		try { core_discord_clear(); } catch { }
	}

	internal static void DiscordShutdown()
	{
		try { core_discord_shutdown(); } catch { }
	}

	internal static void AudioCue(string kind)
	{
		try { core_audio_cue(ToUtf8(kind ?? "")); } catch { }
	}

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_settings_load(byte[] cfgPath);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_settings_save(byte[] cfgPath, byte[] json);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_alt_tab_set(byte[] gameDir, [MarshalAs(UnmanagedType.U1)] bool enable);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_reset_gta_set();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_fav_list();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_fav_upsert(byte[] ip, byte[] port, byte[] name);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_db_fav_remove(byte[] ip, byte[] port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_hist_list(uint limit);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_hist_record(byte[] ip, byte[] port, byte[] name);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_db_hist_remove(byte[] ip, byte[] port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_db_server_rename(byte[] ip, byte[] port, byte[] name);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_watchdog_poll();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_watchdog_stop();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_is_game_running();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_query_server_details(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_slot_watch_start(byte[] host, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_slot_watch_stop();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_slot_watch_poll();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_init(byte[] clientId);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_set_enabled([MarshalAs(UnmanagedType.U1)] bool enabled);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_presence_launcher();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_presence_game(byte[] serverName, byte[] ip, byte[] port, int players, int max);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_clear();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_discord_shutdown();

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_chatlog_read(byte[] path, uint maxBytes, uint maxLines, byte[] query);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_gallery_scan(byte[] dir, uint maxItems);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern int core_audio_cue(byte[] kind);

	private static Dictionary<string, object> DeserializeDict(string json)
	{
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			js.MaxJsonLength = 16 * 1024 * 1024;
			return js.Deserialize<Dictionary<string, object>>(json);
		}
		catch
		{
			return null;
		}
	}

	private static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key)
	{
		object v;
		if (d != null && d.TryGetValue(key, out v))
		{
			return v as Dictionary<string, object>;
		}
		return null;
	}

	private static List<string> GetStrList(Dictionary<string, object> d, string key)
	{
		List<string> list = new List<string>();
		object v;
		if (d != null && d.TryGetValue(key, out v))
		{
			IList arr = v as IList;
			if (arr != null)
			{
				foreach (object item in arr)
				{
					if (item != null)
					{
						list.Add(item.ToString());
					}
				}
			}
		}
		return list;
	}

	private static long GetLong(Dictionary<string, object> d, string key, long def)
	{
		object v;
		if (d.TryGetValue(key, out v) && v != null)
		{
			try { return Convert.ToInt64(v); } catch { }
		}
		return def;
	}

	private static List<string[]> ParseStringMatrix(string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			IList rows = js.DeserializeObject(json) as IList;
			return ParseStringMatrixList(rows);
		}
		catch
		{
			return null;
		}
	}

	private static List<string[]> ParseStringMatrixList(IList rows)
	{
		if (rows == null)
		{
			return null;
		}
		List<string[]> result = new List<string[]>(rows.Count);
		foreach (object rowObj in rows)
		{
			IList cols = rowObj as IList;
			if (cols != null)
			{
				string[] arr = new string[cols.Count];
				for (int i = 0; i < cols.Count; i++)
				{
					arr[i] = (cols[i] != null) ? cols[i].ToString() : "";
				}
				result.Add(arr);
			}
		}
		return result;
	}

	private static CrashInfo ParseCrashInfo(Dictionary<string, object> cd)
	{
		if (cd == null)
		{
			return null;
		}
		CrashInfo ci = new CrashInfo();
		ci.Timestamp = GetStr(cd, "timestamp");
		ci.ExceptionCode = GetStr(cd, "exception_code");
		ci.ExceptionName = GetStr(cd, "exception_name");
		ci.FaultAddress = GetStr(cd, "fault_address");
		ci.ModuleName = GetStr(cd, "module_name");
		ci.ModuleOffset = GetStr(cd, "module_offset");
		ci.ThaiExplanation = GetStr(cd, "thai_explanation");
		ci.Suggestion = GetStr(cd, "suggestion");
		ci.WorkingSetMb = GetLong(cd, "working_set_mb", 0);
		ci.VirtualMemMb = GetLong(cd, "virtual_mem_mb", 0);
		ci.LoadedModules = GetStrList(cd, "loaded_modules");
		return ci;
	}

	private static string GetStr(Dictionary<string, object> d, string key)
	{
		object v;
		if (d.TryGetValue(key, out v) && v != null)
		{
			return v as string ?? v.ToString();
		}
		return "";
	}

	private static int GetInt(Dictionary<string, object> d, string key, int def)
	{
		object v;
		if (d.TryGetValue(key, out v) && v != null)
		{
			try { return Convert.ToInt32(v); } catch { }
		}
		return def;
	}

	private static bool GetBool(Dictionary<string, object> d, string key)
	{
		object v;
		if (d.TryGetValue(key, out v) && v != null)
		{
			try { return Convert.ToBoolean(v); } catch { }
		}
		return false;
	}
}
