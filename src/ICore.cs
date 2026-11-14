using System;
using System.Collections.Generic;
using System.Drawing;

internal class ScanUpdateItem
{
	public string Ip = "";
	public int Port;
	public bool Online;
	public string Name = "";
	public int Players = -1;
	public int Max;
	public int Ping = -1;
	public string Mode = "";
	public string Language = "";
	public bool IsOpenMp;
	public bool Locked;
}

internal class ScanPollResult
{
	public bool Done;
	public bool FromCache;
	public List<ServerEntry> List;
	public List<ScanUpdateItem> Updates = new List<ScanUpdateItem>();
	public string Error;
}

internal class HealthCheckReport
{
	public string GtaStatus = "info";
	public string GtaMsg = "";
	public string SampStatus = "info";
	public string SampMsg = "";
	public string ModsStatus = "info";
	public string ModsMsg = "";
	public string EnvStatus = "info";
	public string EnvMsg = "";
	public string GfxStatus = "info";
	public string GfxMsg = "";
	public List<string> DuplicateAsis = new List<string>();
	public List<string> ReadonlyFiles = new List<string>();
	public bool InProgramFiles;
	public bool MissingDx9;
	public bool NeedsLaa;
	public int FixableCount;
}

internal class SnapshotInfo
{
	public string Id = "";
	public string Name = "";
	public string CreatedAt = "";
	public int FileCount;
	public long TotalRawBytes;
	public long TotalCompressedBytes;
	public int SkippedLargeCount;
	public List<string> SkippedLargeFiles = new List<string>();
}

internal class AssetIssue
{
	public string Severity = "warn";
	public string Kind = "";
	public string RelPath = "";
	public string Message = "";
}

internal class AssetScanReport
{
	public int ScannedFiles;
	public int DffCount;
	public int TxdCount;
	public int ImgCount;
	public int ColCount;
	public int IfpCount;
	public int TextureCount;
	public int ErrorCount;
	public int WarnCount;
	public int DuplicateCount;
	public bool TruncatedIssues;
	public List<AssetIssue> Issues = new List<AssetIssue>();
}

internal class ModLoaderFolder
{
	public string Name = "";
	public bool Enabled = true;
	public int FileCount;
	public long TotalBytes;
	public int Priority = 50;
}

internal class ModLoaderState
{
	public bool Installed;
	public string Profile = "Default";
	public bool IgnoreAll;
	public List<ModLoaderFolder> Folders = new List<ModLoaderFolder>();
}

internal class CrashInfo
{
	public string Timestamp = "";
	public string ExceptionCode = "";
	public string ExceptionName = "";
	public string FaultAddress = "";
	public string ModuleName = "";
	public string ModuleOffset = "";
	public string ThaiExplanation = "";
	public string Suggestion = "";
	public long WorkingSetMb;
	public long VirtualMemMb;
	public List<string> LoadedModules = new List<string>();
}

internal class TelemetryData
{
	public int Pid;
	public long WorkingSetMb;
	public long PeakWorkingSetMb;
	public long PrivateBytesMb;
	public long VirtualMemMb;
	public string Timestamp = "";
	public long SystemFreeRamMb;
	public string ActiveMic = "";
	public bool NetShieldActive;
	public bool AudioShieldActive;
	public bool GpuShieldActive;
	public bool BorderlessActive;
	public int TunedUdpSockets;
}

internal class MicDeviceInfo
{
	public int Id;
	public string Name = "";
	public bool IsVirtual;
	public bool Recommended;
}

internal class PrewarmReport
{
	public string Host = "";
	public string ResolvedIp = "";
	public int GamePort;
	public int VoicePort;
	public int DnsMs;
	public int GamePingMs = -1;
	public bool VoiceRouteWarmed;
	public string Summary = "";
}

internal class ShieldDiagnostics
{
	public bool Enable = true;
	public bool Borderless;
	public bool CatchCrash = true;
	public bool HighPrecisionTimer = true;
	public bool Telemetry = true;
	public bool NetShield = true;
	public bool AudioShield = true;
	public string PreferredMic = "";
	public bool GpuBoost = true;
	public bool ForceImmediatePresent = true;
	public string ResourceMode = "auto";
	public string CpuAffinity = "auto";

	public bool FirewallChecked;
	public bool FirewallGtaAllowed;
	public bool FirewallGtaBlocked;
	public string FirewallSummary = "";
	public bool AltTabNoPause;
	public string AltTabNote = "";
	public string LastVoiceServer = "";
	public int LastVoicePort;

	public List<MicDeviceInfo> Mics = new List<MicDeviceInfo>();
	public bool MicConsentAllowed = true;
	public string AudioRecommendation = "";
	public bool SvlogPauseDisconnect;
	public List<string> SvlogErrors = new List<string>();

	public bool IsHybridGpu;
	public string IgpuName = "";
	public string DgpuName = "";
	public string WindowsGpuPref = "";
	public string GpuSummary = "";

	public long TotalRamMb;
	public long AvailRamMb;
	public int MemoryLoadPct;
	public int LogicalCores;
	public int RecommendedStreamMb;
	public int RecommendedLimitMb;
	public long LauncherWorkingSetMb;
}

internal class GuardRuntimeStatus
{
	public bool HasCrash;
	public CrashInfo Crash;
	public TelemetryData Telemetry;
}

internal class PingSparkReport
{
	public string Host = "";
	public int Port;
	public List<int> History = new List<int>();
	public int Latest = -1;
	public int Min;
	public int Max;
	public int Avg;
	public double Jitter;
	public double Loss;
}

internal interface ICore
{
	string EngineName { get; }

	bool SupportsBatchScan { get; }

	string ResolveHost(string host);

	QueryResult QueryServer(string host, int port);

	List<string[]> QueryPlayers(string host, int port);

	List<string[]> QueryRules(string host, int port);

	string RconSend(string host, int port, string pass, string cmd);

	void LaunchAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass);

	ulong StartScan(List<ServerEntry> targets, int concurrency);

	ulong StartPublicScan(string cachePath, int concurrency);

	ScanPollResult PollScan(ulong handle, int maxItems);

	void CancelScan(ulong handle);

	void InitStorage(string dbPath, string playtimeTxtPath, string statsTxtPath);

	Dictionary<string, long> LoadAllPlaytime();

	long AddPlaytime(string serverKey, long deltaSeconds);

	void RecordStat(string ip, string port, string recordedAt, int players);

	List<int> GetStats(string ip, string port, int limit);

	HealthCheckReport CheckHealth(string gameDir, string skyGfxPreset);

	string AutoFixHealth(string gameDir);

	string CreateSnapshot(string gameDir, string name);

	List<SnapshotInfo> ListSnapshots(string gameDir);

	string RestoreSnapshot(string gameDir, string snapshotId);

	void DeleteSnapshot(string gameDir, string snapshotId);

	AssetScanReport ScanAssets(string gameDir);

	ModLoaderState GetModLoaderState(string gameDir);

	string SaveModLoaderState(string gameDir, bool ignoreAll, List<string> disabledFolders);

	string SetModLoaderPriority(string gameDir, string folderName, int priority);

	string ApplySkyGfx(string gameDir, string preset);

	string ApplyFpsLimit(string gameDir, int fps);

	GuardRuntimeStatus GetGuardStatus(string gameDir);

	void ClearLatestCrash(string gameDir);

	bool ApplyDwm(IntPtr hWnd);

	bool SetDwmAccent(IntPtr hWnd, Color accent);

	bool StyleListView(IntPtr lvHwnd, Color headerBg, Color accent);

	void StartPingSpark(string ip, int port);

	PingSparkReport GetPingSpark(string ip, int port);

	void StopPingSpark();

	ShieldDiagnostics GetShieldDiagnostics(string gameDir, string host, int port);

	string SaveShieldIni(string gameDir, Dictionary<string, string> updates);

	PrewarmReport PrewarmServer(string host, int port);

	long TrimLauncherMemory();

	void CloseDb();

	LauncherSettingsDto LoadSettings(string cfgPath);

	string SaveSettings(string cfgPath, LauncherSettingsDto cfg);

	string SetAltTabNoPause(string gameDir, bool enable);

	bool ResetGtaSaSet();

	List<SavedServerDto> ListFavorites();

	void UpsertFavorite(string ip, string port, string name);

	bool RemoveFavorite(string ip, string port);

	List<SavedServerDto> ListHistory(int limit);

	void RecordHistory(string ip, string port, string name);

	bool RemoveHistory(string ip, string port);

	void RenameSavedServer(string ip, string port, string name);

	WatchdogStatusDto PollWatchdog();

	void StopWatchdog();

	bool IsGameRunning();

	ServerDetailsDto QueryServerDetails(string host, int port);

	bool StartSlotWatch(string host, int port);

	void StopSlotWatch();

	SlotWatchStatusDto PollSlotWatch();

	ChatlogResultDto ReadChatlog(string path, int maxBytes, int maxLines, string query);

	GalleryScanResultDto ScanGallery(string dir, int maxItems);
}

internal class CoreLoadException : Exception
{
	public CoreLoadException(string message, Exception inner)
		: base(message, inner)
	{
	}
}

internal class LauncherSettingsDto
{
	public string Nick = "";
	public string Accent = "138,43,226";
	public string GamePath = "";
	public bool AutoConnect;
	public bool CloseAfterLaunch;
	public string LastIp = "127.0.0.1";
	public string LastPort = "7777";
	public string RconIp = "";
	public string RconPort = "";
	public string RconPass = "";
	public int SortCol;
	public bool SortDesc;
	public string SkyGfxPreset = "ps2";
	public int FpsLimit;
	public bool AutoJoinSlot;
	public bool SoundFx = true;
	public bool DiscordRpc = true;
	public string DiscordAppId = "";
	public bool NoPauseAltTab;
	public List<string> NickProfiles = new List<string>();
}

internal class SavedServerDto
{
	public string Ip = "";
	public string Port = "";
	public string Name = "";
	public long LastJoinedAt;
	public long JoinCount;
}

internal class ServerDetailsDto
{
	public QueryResult Info = new QueryResult();
	public List<string[]> Players;
	public List<string[]> Rules;
}

internal class SlotWatchStatusDto
{
	public bool Watching;
	public bool FoundSlot;
	public string Ip = "";
	public int Port;
	public string Name = "";
	public int Players;
	public int Max;
	public int Ping = -1;
}

internal class WatchdogStatusDto
{
	public bool Active;
	public bool Running;
	public bool Exited;
	public bool Crashed;
	public uint Pid;
	public string ServerKey = "";
	public long SessionSeconds;
	public long TotalPlaytime;
	public uint ExitCode;
	public CrashInfo Crash;
}

internal class ChatlogResultDto
{
	public bool Exists;
	public string Path = "";
	public long FileSizeBytes;
	public int TotalLines;
	public int MatchedCount;
	public bool Truncated;
	public List<string> Lines = new List<string>();
}

internal class GalleryItemDto
{
	public string Path = "";
	public string Name = "";
	public long SizeBytes;
	public long ModifiedEpoch;
}

internal class GalleryScanResultDto
{
	public string Directory = "";
	public bool Exists;
	public int TotalFound;
	public List<GalleryItemDto> Items = new List<GalleryItemDto>();
}

internal static class CoreFactory
{
	public static ICore Create()
	{
		try
		{
			RustCore rc = new RustCore();
			if (rc.PingSelfTest())
			{
				return rc;
			}
			throw new CoreLoadException("ไม่พบหรือโหลด samp_core.dll (x86) ไม่สำเร็จ กรุณาตรวจสอบไฟล์ข้างตัว Launcher", null);
		}
		catch (CoreLoadException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new CoreLoadException("ไม่สามารถเริ่มต้น Rust Engine (samp_core.dll): " + ex.Message, ex);
		}
	}
}
