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
		// Phase 2 will move RCON to Rust; delegate for now
		return fallback.RconSend(host, port, pass, cmd);
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
