using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;

internal class RustCore : ICore
{
	private readonly ManagedCore fallback = new ManagedCore();
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
	private static extern IntPtr core_guard_status(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_crash_clear(byte[] gameDir);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	[return: MarshalAs(UnmanagedType.U1)]
	private static extern bool core_ui_apply_dwm(IntPtr hwnd);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_ping_spark_start(byte[] ip, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr core_ping_spark_get(byte[] ip, ushort port);

	[DllImport("samp_core.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void core_ping_spark_stop();

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
			JavaScriptSerializer js = new JavaScriptSerializer();
			Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
			if (d == null)
			{
				return qr;
			}
			qr.Online = GetBool(d, "online");
			qr.Name = GetStr(d, "name");
			qr.Players = GetInt(d, "players", -1);
			qr.Max = GetInt(d, "max", 0);
			qr.Ping = GetInt(d, "ping", -1);
			qr.Mode = GetStr(d, "mode");
			qr.Language = GetStr(d, "language");
			qr.IsOpenMp = GetBool(d, "is_open_mp");
		}
		catch
		{
		}
		return qr;
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
			fallback.InitStorage(dbPath, playtimeTxtPath, statsTxtPath);
		}
	}

	public Dictionary<string, long> LoadAllPlaytime()
	{
		Dictionary<string, long> map = new Dictionary<string, long>();
		string json = TakeOwnedStr(core_db_get_all_playtime());
		if (string.IsNullOrEmpty(json))
		{
			return fallback.LoadAllPlaytime();
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
		if (total < 0)
		{
			return fallback.AddPlaytime(serverKey, deltaSeconds);
		}
		return total;
	}

	public void RecordStat(string ip, string port, string recordedAt, int players)
	{
		int res = core_db_record_stat(ToUtf8(ip), ToUtf8(port), ToUtf8(recordedAt), players);
		if (res < 0)
		{
			fallback.RecordStat(ip, port, recordedAt, players);
		}
	}

	public List<int> GetStats(string ip, string port, int limit)
	{
		string json = TakeOwnedStr(core_db_get_stats(ToUtf8(ip), ToUtf8(port), (uint)(limit > 0 ? limit : 500)));
		if (string.IsNullOrEmpty(json))
		{
			return fallback.GetStats(ip, port, limit);
		}
		List<int> list = new List<int>();
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
		string json = TakeOwnedStr(core_health_check(ToUtf8(gameDir), ToUtf8(skyGfxPreset ?? "ps2")));
		if (string.IsNullOrEmpty(json))
		{
			return fallback.CheckHealth(gameDir, skyGfxPreset);
		}
		try
		{
			JavaScriptSerializer js = new JavaScriptSerializer();
			Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
			if (d == null)
			{
				return fallback.CheckHealth(gameDir, skyGfxPreset);
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
			r.FixableCount = GetInt(d, "fixable_count", 0);
			return r;
		}
		catch
		{
			return fallback.CheckHealth(gameDir, skyGfxPreset);
		}
	}

	public string AutoFixHealth(string gameDir)
	{
		string json = TakeOwnedStr(core_health_autofix(ToUtf8(gameDir)));
		if (string.IsNullOrEmpty(json))
		{
			return fallback.AutoFixHealth(gameDir);
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
		List<string> dis = GetStrList(data, "disabled_duplicates");
		List<string> rem = GetStrList(data, "remaining_warnings");
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("ซ่อมแซมอัตโนมัติเรียบร้อย:");
		sb.AppendLine("• ปลดล็อกไฟล์ Read-Only: " + ro + " ไฟล์");
		sb.AppendLine("• ปิด .asi ที่ซ้ำซ้อน (.disabled): " + dis.Count + " ไฟล์");
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
			return fallback.CreateSnapshot(gameDir, name);
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
		report.ImgCount = GetInt(d, "img_count", 0);
		report.TextureCount = GetInt(d, "texture_count", 0);
		report.ErrorCount = GetInt(d, "error_count", 0);
		report.WarnCount = GetInt(d, "warn_count", 0);
		report.DuplicateCount = GetInt(d, "duplicate_count", 0);

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
			st.Crash = ci;
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
		catch
		{
			return null;
		}
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
