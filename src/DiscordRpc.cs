using System;

internal static class DiscordRpc
{
	private static string _clientId = "1088484931343753236";
	private static bool _enabled = true;

	public static bool Enabled
	{
		get { return _enabled; }
		set
		{
			_enabled = value;
			RustCore.DiscordSetEnabled(_enabled);
		}
	}

	public static string ClientId
	{
		get { return _clientId; }
		set
		{
			_clientId = value ?? "";
			if (!string.IsNullOrEmpty(_clientId))
			{
				RustCore.DiscordInit(_clientId);
			}
		}
	}

	public static void Initialize(string clientId)
	{
		if (!string.IsNullOrEmpty(clientId))
		{
			_clientId = clientId;
		}
		RustCore.DiscordInit(_clientId);
		RustCore.DiscordSetEnabled(_enabled);
	}

	public static void Shutdown()
	{
		RustCore.DiscordShutdown();
	}

	public static void UpdateLauncherPresence()
	{
		if (!_enabled) return;
		RustCore.DiscordPresenceLauncher();
	}

	public static void UpdateGamePresence(string serverName, string ip, string port, int players, int max)
	{
		if (!_enabled) return;
		RustCore.DiscordPresenceGame(serverName ?? "", ip ?? "", port ?? "", players, max);
	}

	public static void ClearPresence()
	{
		RustCore.DiscordClear();
	}
}
