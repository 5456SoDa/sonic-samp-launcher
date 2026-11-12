using System.Collections.Generic;

internal interface ICore
{
	string EngineName { get; }

	string ResolveHost(string host);

	QueryResult QueryServer(string host, int port);

	List<string[]> QueryPlayers(string host, int port);

	List<string[]> QueryRules(string host, int port);

	string RconSend(string host, int port, string pass, string cmd);

	void LaunchAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass);
}

internal static class CoreFactory
{
	public static ICore Create()
	{
		return new ManagedCore();
	}
}
