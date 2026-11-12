using System;
using System.Collections.Generic;

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
	public bool IsOpenMp;
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
	public int TextureCount;
	public int ErrorCount;
	public int WarnCount;
	public int DuplicateCount;
	public List<AssetIssue> Issues = new List<AssetIssue>();
}

internal class ModLoaderFolder
{
	public string Name = "";
	public bool Enabled = true;
	public int FileCount;
	public long TotalBytes;
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

	GuardRuntimeStatus GetGuardStatus(string gameDir);

	void ClearLatestCrash(string gameDir);

	bool ApplyDwm(IntPtr hWnd);

	void StartPingSpark(string ip, int port);

	PingSparkReport GetPingSpark(string ip, int port);

	void StopPingSpark();

	void CloseDb();
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
		}
		catch
		{
		}
		return new ManagedCore();
	}
}
