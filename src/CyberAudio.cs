using System;
using System.IO;
using System.Media;

internal static class CyberAudio
{
	private static bool _enabled = true;
	private static SoundPlayer _playerClick;
	private static SoundPlayer _playerAlert;
	private static SoundPlayer _playerLaunch;
	private static bool _initialized = false;
	private static readonly object _lock = new object();

	public static bool Enabled
	{
		get { return _enabled; }
		set { _enabled = value; }
	}

	public static void Init()
	{
		if (_initialized) return;
		lock (_lock)
		{
			if (_initialized) return;
			try
			{
				byte[] clickWav = GenerateCyberClick();
				_playerClick = new SoundPlayer(new MemoryStream(clickWav));
				_playerClick.Load();

				byte[] alertWav = GenerateCyberAlert();
				_playerAlert = new SoundPlayer(new MemoryStream(alertWav));
				_playerAlert.Load();

				byte[] launchWav = GenerateCyberLaunch();
				_playerLaunch = new SoundPlayer(new MemoryStream(launchWav));
				_playerLaunch.Load();

				_initialized = true;
			}
			catch
			{
				_initialized = false;
			}
		}
	}

	public static void PlayClick()
	{
		if (!_enabled) return;
		try
		{
			if (!_initialized) Init();
			if (_playerClick != null)
			{
				_playerClick.Play();
			}
		}
		catch
		{
		}
	}

	public static void PlayAlert()
	{
		if (!_enabled) return;
		try
		{
			if (!_initialized) Init();
			if (_playerAlert != null)
			{
				_playerAlert.Play();
			}
		}
		catch
		{
		}
	}

	public static void PlayLaunch()
	{
		if (!_enabled) return;
		try
		{
			if (!_initialized) Init();
			if (_playerLaunch != null)
			{
				_playerLaunch.Play();
			}
		}
		catch
		{
		}
	}

	private static byte[] CreateWavHeader(int sampleRate, short numChannels, short bitsPerSample, int numSamples)
	{
		int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
		short blockAlign = (short)(numChannels * (bitsPerSample / 8));
		int dataSize = numSamples * blockAlign;

		byte[] header = new byte[44];
		using (MemoryStream ms = new MemoryStream(header))
		using (BinaryWriter bw = new BinaryWriter(ms))
		{
			bw.Write(new char[] { 'R', 'I', 'F', 'F' });
			bw.Write(36 + dataSize);
			bw.Write(new char[] { 'W', 'A', 'V', 'E' });
			bw.Write(new char[] { 'f', 'm', 't', ' ' });
			bw.Write(16); // Subchunk1Size (16 for PCM)
			bw.Write((short)1); // AudioFormat (1 for PCM)
			bw.Write(numChannels);
			bw.Write(sampleRate);
			bw.Write(byteRate);
			bw.Write(blockAlign);
			bw.Write(bitsPerSample);
			bw.Write(new char[] { 'd', 'a', 't', 'a' });
			bw.Write(dataSize);
		}
		return header;
	}

	private static byte[] GenerateCyberClick()
	{
		int sampleRate = 44100;
		float duration = 0.045f; // 45ms
		int numSamples = (int)(sampleRate * duration);

		byte[] header = CreateWavHeader(sampleRate, 1, 16, numSamples);
		byte[] raw = new byte[header.Length + numSamples * 2];
		Buffer.BlockCopy(header, 0, raw, 0, header.Length);

		double phase = 0.0;
		int offset = header.Length;
		for (int i = 0; i < numSamples; i++)
		{
			double t = (double)i / sampleRate;
			double normT = t / duration;
			double freq = 1600.0 - (1100.0 * normT);
			phase += 2.0 * Math.PI * freq / sampleRate;
			double envelope = Math.Exp(-6.0 * normT);
			double val = (0.85 * Math.Sin(phase) + 0.15 * Math.Sin(2.0 * phase)) * envelope * 0.35;
			short sampleVal = (short)Math.Max(-32768.0, Math.Min(32767.0, val * 32767.0));
			raw[offset++] = (byte)(sampleVal & 0xFF);
			raw[offset++] = (byte)((sampleVal >> 8) & 0xFF);
		}
		return raw;
	}

	private static byte[] GenerateCyberAlert()
	{
		int sampleRate = 44100;
		float duration = 0.32f; // 320ms
		int numSamples = (int)(sampleRate * duration);

		byte[] header = CreateWavHeader(sampleRate, 1, 16, numSamples);
		byte[] raw = new byte[header.Length + numSamples * 2];
		Buffer.BlockCopy(header, 0, raw, 0, header.Length);

		double phase1 = 0.0;
		double phase2 = 0.0;
		int offset = header.Length;
		for (int i = 0; i < numSamples; i++)
		{
			double t = (double)i / sampleRate;
			double val;
			if (t < 0.12)
			{
				double freq = 987.77;
				double normT = t / 0.12;
				phase1 += 2.0 * Math.PI * freq / sampleRate;
				double env = Math.Exp(-3.5 * normT);
				val = Math.Sin(phase1) * env * 0.40;
			}
			else
			{
				double freq = 1318.51;
				double normT = (t - 0.12) / 0.20;
				phase2 += 2.0 * Math.PI * freq / sampleRate;
				double env = Math.Exp(-4.0 * normT);
				val = Math.Sin(phase2) * env * 0.45;
			}
			short sampleVal = (short)Math.Max(-32768.0, Math.Min(32767.0, val * 32767.0));
			raw[offset++] = (byte)(sampleVal & 0xFF);
			raw[offset++] = (byte)((sampleVal >> 8) & 0xFF);
		}
		return raw;
	}

	private static byte[] GenerateCyberLaunch()
	{
		int sampleRate = 44100;
		float duration = 0.13f; // 130ms
		int numSamples = (int)(sampleRate * duration);

		byte[] header = CreateWavHeader(sampleRate, 1, 16, numSamples);
		byte[] raw = new byte[header.Length + numSamples * 2];
		Buffer.BlockCopy(header, 0, raw, 0, header.Length);

		double phase = 0.0;
		int offset = header.Length;
		for (int i = 0; i < numSamples; i++)
		{
			double t = (double)i / sampleRate;
			double normT = t / duration;
			double freq = 450.0 + (1350.0 * normT);
			phase += 2.0 * Math.PI * freq / sampleRate;
			double envelope = Math.Sin(Math.PI * Math.Min(1.0, normT * 1.2)) * (1.0 - normT * 0.7);
			double val = Math.Sin(phase) * envelope * 0.40;
			short sampleVal = (short)Math.Max(-32768.0, Math.Min(32767.0, val * 32767.0));
			raw[offset++] = (byte)(sampleVal & 0xFF);
			raw[offset++] = (byte)((sampleVal >> 8) & 0xFF);
		}
		return raw;
	}
}
