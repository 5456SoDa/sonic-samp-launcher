using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

internal static class DiscordRpc
{
	private static string _clientId = "1088484931343753236";
	private static bool _enabled = true;
	private static NamedPipeClientStream _pipe;
	private static Thread _workerThread;
	private static bool _running = false;
	private static readonly object _lock = new object();
	private static Dictionary<string, object> _currentActivity = null;
	private static bool _activityDirty = false;
	private static bool _isConnected = false;

	public static bool Enabled
	{
		get { return _enabled; }
		set
		{
			_enabled = value;
			if (!_enabled)
			{
				ClearPresence();
			}
			else
			{
				_activityDirty = true;
			}
		}
	}

	public static string ClientId
	{
		get { return _clientId; }
		set { _clientId = value; }
	}

	public static bool IsConnected
	{
		get { return _isConnected; }
	}

	public static void Initialize(string clientId)
	{
		if (!string.IsNullOrEmpty(clientId))
		{
			_clientId = clientId;
		}

		if (_running) return;

		_running = true;
		_workerThread = new Thread(WorkerLoop)
		{
			IsBackground = true,
			Name = "DiscordRpcWorker"
		};
		_workerThread.Start();
	}

	public static void Shutdown()
	{
		_running = false;
		Disconnect();
	}

	public static void UpdateLauncherPresence()
	{
		if (!_enabled) return;

		Dictionary<string, object> activity = new Dictionary<string, object>();
		activity["details"] = "อยู่ในเมนูหลัก (Browsing Servers)";
		activity["state"] = "SONIC CLIENT v2.3";

		Dictionary<string, object> assets = new Dictionary<string, object>();
		assets["large_image"] = "gtasa";
		assets["large_text"] = "GTA San Andreas - Sonic Client";
		assets["small_image"] = "online";
		assets["small_text"] = "Online";
		activity["assets"] = assets;

		lock (_lock)
		{
			_currentActivity = activity;
			_activityDirty = true;
		}
	}

	public static void UpdateGamePresence(string serverName, string ip, string port, int players, int max)
	{
		if (!_enabled) return;

		Dictionary<string, object> activity = new Dictionary<string, object>();
		string sName = string.IsNullOrEmpty(serverName) ? "GTA San Andreas Multiplayer" : serverName;
		if (sName.Length > 120) sName = sName.Substring(0, 117) + "...";
		activity["details"] = "เล่นอยู่ใน " + sName;

		if (players >= 0 && max > 0)
		{
			activity["state"] = string.Format("ผู้เล่น: {0}/{1} คน ({2}:{3})", players, max, ip, port);
		}
		else
		{
			activity["state"] = string.Format("เซิร์ฟเวอร์: {0}:{1}", ip, port);
		}

		Dictionary<string, object> timestamps = new Dictionary<string, object>();
		long epochSeconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
		timestamps["start"] = epochSeconds;
		activity["timestamps"] = timestamps;

		Dictionary<string, object> assets = new Dictionary<string, object>();
		assets["large_image"] = "gtasa";
		assets["large_text"] = sName;
		assets["small_image"] = "play";
		assets["small_text"] = "In-Game";
		activity["assets"] = assets;

		lock (_lock)
		{
			_currentActivity = activity;
			_activityDirty = true;
		}
	}

	public static void ClearPresence()
	{
		lock (_lock)
		{
			_currentActivity = null;
			_activityDirty = true;
		}
	}

	private static void WorkerLoop()
	{
		while (_running)
		{
			try
			{
				if (!_enabled)
				{
					if (_isConnected)
					{
						Disconnect();
					}
					Thread.Sleep(1000);
					continue;
				}

				if (!_isConnected)
				{
					if (!TryConnect())
					{
						Thread.Sleep(15000); // Retry every 15s if Discord is closed
						continue;
					}
				}

				bool needSend = false;
				Dictionary<string, object> actCopy = null;

				lock (_lock)
				{
					if (_activityDirty)
					{
						needSend = true;
						_activityDirty = false;
						actCopy = _currentActivity;
					}
				}

				if (needSend)
				{
					SendActivity(actCopy);
				}

				Thread.Sleep(1000);
			}
			catch
			{
				Disconnect();
				Thread.Sleep(10000);
			}
		}
	}

	private static bool TryConnect()
	{
		for (int i = 0; i < 10; i++)
		{
			try
			{
				_pipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
				_pipe.Connect(150);

				// Send Handshake: Opcode 0
				Dictionary<string, object> handshake = new Dictionary<string, object>();
				handshake["v"] = 1;
				handshake["client_id"] = _clientId;

				JavaScriptSerializer js = new JavaScriptSerializer();
				string json = js.Serialize(handshake);
				WriteFrame(0, json);

				// Read response frame
				ReadFrame();

				_isConnected = true;
				_activityDirty = true; // Trigger refresh of current activity
				return true;
			}
			catch
			{
				Disconnect();
			}
		}
		return false;
	}

	private static void Disconnect()
	{
		_isConnected = false;
		if (_pipe != null)
		{
			try
			{
				_pipe.Close();
				_pipe.Dispose();
			}
			catch
			{
			}
			_pipe = null;
		}
	}

	private static void SendActivity(Dictionary<string, object> activity)
	{
		if (_pipe == null || !_pipe.IsConnected)
		{
			_isConnected = false;
			return;
		}

		Dictionary<string, object> root = new Dictionary<string, object>();
		root["cmd"] = "SET_ACTIVITY";

		Dictionary<string, object> args = new Dictionary<string, object>();
		args["pid"] = Process.GetCurrentProcess().Id;
		args["activity"] = activity;

		root["args"] = args;
		root["nonce"] = Guid.NewGuid().ToString("N");

		JavaScriptSerializer js = new JavaScriptSerializer();
		string json = js.Serialize(root);
		WriteFrame(1, json);
	}

	private static void WriteFrame(int opcode, string json)
	{
		byte[] payload = Encoding.UTF8.GetBytes(json);
		byte[] header = new byte[8];

		// Opcode (4 bytes LE)
		header[0] = (byte)(opcode & 0xFF);
		header[1] = (byte)((opcode >> 8) & 0xFF);
		header[2] = (byte)((opcode >> 16) & 0xFF);
		header[3] = (byte)((opcode >> 24) & 0xFF);

		// Length (4 bytes LE)
		int len = payload.Length;
		header[4] = (byte)(len & 0xFF);
		header[5] = (byte)((len >> 8) & 0xFF);
		header[6] = (byte)((len >> 16) & 0xFF);
		header[7] = (byte)((len >> 24) & 0xFF);

		_pipe.Write(header, 0, 8);
		_pipe.Write(payload, 0, payload.Length);
		_pipe.Flush();
	}

	private static void ReadFrame()
	{
		if (_pipe == null || !_pipe.IsConnected) return;

		byte[] header = new byte[8];
		int read = 0;
		while (read < 8)
		{
			int r = _pipe.Read(header, read, 8 - read);
			if (r <= 0) return;
			read += r;
		}

		int len = header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24);
		if (len > 0 && len < 65536)
		{
			byte[] buf = new byte[len];
			int totalRead = 0;
			while (totalRead < len)
			{
				int r = _pipe.Read(buf, totalRead, len - totalRead);
				if (r <= 0) break;
				totalRead += r;
			}
		}
	}
}
