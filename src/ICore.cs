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
