using System;

internal static class CyberAudio
{
	private static bool _enabled = true;

	public static bool Enabled
	{
		get { return _enabled; }
		set { _enabled = value; }
	}

	public static void Init()
	{
	}

	public static void PlayClick()
	{
		if (!_enabled) return;
		RustCore.AudioCue("click");
	}

	public static void PlayAlert()
	{
		if (!_enabled) return;
		RustCore.AudioCue("alert");
	}

	public static void PlayLaunch()
	{
		if (!_enabled) return;
		RustCore.AudioCue("launch");
	}
}
