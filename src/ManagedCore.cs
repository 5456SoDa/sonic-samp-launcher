using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

internal class ManagedCore : ICore
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct STARTUPINFO
	{
		public uint cb;
		public string lpReserved;
		public string lpDesktop;
		public string lpTitle;
		public uint dwX;
		public uint dwY;
		public uint dwXSize;
		public uint dwYSize;
		public uint dwXCountChars;
		public uint dwYCountChars;
		public uint dwFillAttribute;
		public uint dwFlags;
		public ushort wShowWindow;
		public ushort cbReserved2;
		public IntPtr lpReserved2;
		public IntPtr hStdInput;
		public IntPtr hStdOutput;
		public IntPtr hStdError;
	}

	private struct PROCESS_INFORMATION
	{
		public IntPtr hProcess;
		public IntPtr hThread;
		public uint dwProcessId;
		public uint dwThreadId;
	}

	private const uint CREATE_SUSPENDED = 4u;
	private const uint MEM_COMMIT = 4096u;
	private const uint MEM_RESERVE = 8192u;
	private const uint MEM_RELEASE = 0x8000u;
	private const uint PAGE_READWRITE = 4u;
	private const uint WAIT_OBJECT_0 = 0u;
	private const uint WAIT_TIMEOUT = 258u;
	private const uint PROCESS_ALL_ACCESS = 2035711u;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inh, uint flags, IntPtr env, string cwd, ref STARTUPINFO si, out PROCESS_INFORMATION pi);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr VirtualAllocEx(IntPtr hProc, IntPtr addr, IntPtr size, uint type, uint protect);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool VirtualFreeEx(IntPtr hProc, IntPtr addr, IntPtr size, uint freeType);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool WriteProcessMemory(IntPtr hProc, IntPtr baseAddr, byte[] buf, IntPtr size, out IntPtr written);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr GetModuleHandle(string name);

	[DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
	private static extern IntPtr GetProcAddress(IntPtr hModule, string proc);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern IntPtr CreateRemoteThread(IntPtr hProc, IntPtr attr, IntPtr stack, IntPtr start, IntPtr param, uint flags, out IntPtr tid);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern uint WaitForSingleObject(IntPtr handle, uint ms);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

	[DllImport("kernel32.dll")]
	private static extern uint ResumeThread(IntPtr hThread);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	public string EngineName
	{
		get { return "C# Managed (Fallback)"; }
	}

	public bool SupportsBatchScan
	{
		get { return false; }
	}

	public ulong StartScan(List<ServerEntry> targets, int concurrency)
	{
		return 0;
	}

	public ulong StartPublicScan(string cachePath, int concurrency)
	{
		return 0;
	}

	public ScanPollResult PollScan(ulong handle, int maxItems)
	{
		return new ScanPollResult { Done = true };
	}

	public void CancelScan(ulong handle)
	{
	}

	private static byte[] BuildQuery(string ip, int port, byte op)
	{
		IPAddress addr;
		byte[] ipBytes;
		if (IPAddress.TryParse(ip, out addr) && addr.AddressFamily == AddressFamily.InterNetwork)
		{
			ipBytes = addr.GetAddressBytes();
		}
		else
		{
			string[] array = ip.Split('.');
			ipBytes = new byte[4]
			{
				byte.Parse(array[0]),
				byte.Parse(array[1]),
				byte.Parse(array[2]),
				byte.Parse(array[3])
			};
		}
		return new byte[11]
		{
			83, 65, 77, 80,
			ipBytes[0], ipBytes[1], ipBytes[2], ipBytes[3],
			(byte)(port & 0xFF),
			(byte)((port >> 8) & 0xFF),
			op
		};
	}

	private static int LE16(byte[] b, int off)
	{
		return b[off] | (b[off + 1] << 8);
	}

	private static int LE32(byte[] b, int off)
	{
		return b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24);
	}

	private static string ReadStr(byte[] b, ref int off)
	{
		if (off + 4 > b.Length)
		{
			return "";
		}
		int num = LE32(b, off);
		off += 4;
		if (num < 0 || off + num > b.Length)
		{
			num = 0;
		}
		string s = Encoding.GetEncoding(874).GetString(b, off, num);
		off += num;
		return s;
	}

	public string ResolveHost(string host)
	{
		IPAddress address;
		if (IPAddress.TryParse(host, out address) && address.AddressFamily == AddressFamily.InterNetwork)
		{
			return host;
		}
		try
		{
			IPAddress[] hostAddresses = Dns.GetHostAddresses(host);
			foreach (IPAddress iPAddress in hostAddresses)
			{
				if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
				{
					return iPAddress.ToString();
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private byte[] QueryRaw(string resolvedIp, int port, byte op)
	{
		try
		{
			if (resolvedIp == null)
			{
				return null;
			}
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.ReceiveTimeout = 2500;
				socket.SendTimeout = 2500;
				socket.Connect(new IPEndPoint(IPAddress.Parse(resolvedIp), port));
				socket.Send(BuildQuery(resolvedIp, port, op));
				byte[] array = new byte[65536];
				int num = socket.Receive(array);
				if (num > 11 && array[10] == op)
				{
					byte[] array2 = new byte[num];
					Array.Copy(array, array2, num);
					return array2;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public QueryResult QueryServer(string host, int port)
	{
		QueryResult queryResult = new QueryResult();
		try
		{
			string text = ResolveHost(host);
			if (text == null)
			{
				return queryResult;
			}
			byte[] buffer = BuildQuery(text, port, 105);
			Stopwatch stopwatch = Stopwatch.StartNew();
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.ReceiveTimeout = 1500;
				socket.SendTimeout = 1500;
				socket.Connect(new IPEndPoint(IPAddress.Parse(text), port));
				socket.Send(buffer);
				byte[] array = new byte[640];
				int num = socket.Receive(array);
				stopwatch.Stop();
				if (num > 20 && array[10] == 105)
				{
					queryResult.Online = true;
					queryResult.Ping = (int)stopwatch.ElapsedMilliseconds;
					queryResult.Players = LE16(array, 12);
					queryResult.Max = LE16(array, 14);
					int off = 16;
					queryResult.Name = ReadStr(array, ref off);
					queryResult.Mode = ReadStr(array, ref off);
				}
			}
			if (queryResult.Online)
			{
				try
				{
					byte[] array2 = QueryRaw(text, port, 114);
					if (array2 != null && array2.Length > 13)
					{
						int num2 = LE16(array2, 11);
						int num3 = 13;
						Encoding encoding = Encoding.GetEncoding(874);
						for (int i = 0; i < num2; i++)
						{
							if (num3 >= array2.Length)
							{
								break;
							}
							int num4 = array2[num3];
							num3++;
							if (num3 + num4 < array2.Length)
							{
								string key = encoding.GetString(array2, num3, num4);
								num3 += num4;
								if (num3 < array2.Length)
								{
									int num5 = array2[num3];
									num3++;
									if (num3 + num5 <= array2.Length)
									{
										string val = encoding.GetString(array2, num3, num5);
										num3 += num5;
										if (key == "version" && val.Length > 0 && !val.StartsWith("0.3"))
										{
											queryResult.IsOpenMp = true;
										}
										continue;
									}
									break;
								}
								break;
							}
							break;
						}
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
			queryResult.Online = false;
		}
		return queryResult;
	}

	public List<string[]> QueryPlayers(string host, int port)
	{
		string resolvedIp = ResolveHost(host);
		if (resolvedIp == null)
		{
			return null;
		}
		List<string[]> list = new List<string[]>();

		byte[] arrayD = QueryRaw(resolvedIp, port, 100);
		if (arrayD != null && arrayD.Length > 13)
		{
			int count = LE16(arrayD, 11);
			int off = 13;
			Encoding encoding = Encoding.GetEncoding(874);
			for (int i = 0; i < count; i++)
			{
				if (off + 2 > arrayD.Length) break;
				int pId = arrayD[off++];
				int nameLen = arrayD[off++];
				if (off + nameLen + 8 > arrayD.Length) break;
				string pName = encoding.GetString(arrayD, off, nameLen);
				off += nameLen;
				int pScore = LE32(arrayD, off);
				off += 4;
				int pPing = LE32(arrayD, off);
				off += 4;
				list.Add(new string[4]
				{
					pId.ToString(),
					pName,
					pScore.ToString(),
					pPing.ToString()
				});
			}
			if (list.Count > 0)
			{
				return list;
			}
		}

		byte[] array = QueryRaw(resolvedIp, port, 99);
		if (array == null || array.Length < 13)
		{
			return null;
		}
		int num = LE16(array, 11);
		int num2 = 13;
		Encoding encodingC = Encoding.GetEncoding(874);
		for (int j = 0; j < num; j++)
		{
			if (num2 + 5 > array.Length)
			{
				break;
			}
			int num3 = array[num2];
			num2++;
			if (num2 + num3 + 4 > array.Length)
			{
				break;
			}
			string pName2 = encodingC.GetString(array, num2, num3);
			num2 += num3;
			int pScore2 = LE32(array, num2);
			num2 += 4;
			list.Add(new string[4]
			{
				"-",
				pName2,
				pScore2.ToString(),
				"?"
			});
		}
		return list;
	}

	public List<string[]> QueryRules(string host, int port)
	{
		string resolvedIp = ResolveHost(host);
		if (resolvedIp == null)
		{
			return null;
		}
		List<string[]> list = new List<string[]>();
		byte[] array = QueryRaw(resolvedIp, port, 114);
		if (array == null || array.Length < 13)
		{
			return null;
		}
		int num = LE16(array, 11);
		int num2 = 13;
		Encoding encoding = Encoding.GetEncoding(874);
		for (int i = 0; i < num; i++)
		{
			if (num2 + 2 > array.Length)
			{
				break;
			}
			int num3 = array[num2];
			num2++;
			if (num2 + num3 >= array.Length)
			{
				break;
			}
			string key = encoding.GetString(array, num2, num3);
			num2 += num3;
			if (num2 >= array.Length)
			{
				break;
			}
			int num4 = array[num2];
			num2++;
			if (num2 + num4 > array.Length)
			{
				break;
			}
			string val = encoding.GetString(array, num2, num4);
			num2 += num4;
			list.Add(new string[2] { key, val });
		}
		return list;
	}

	public string RconSend(string host, int port, string pass, string cmd)
	{
		try
		{
			string text = ResolveHost(host);
			if (text == null)
			{
				return "(แก้ hostname ไม่สำเร็จ)";
			}
			byte[] ipBytes = IPAddress.Parse(text).GetAddressBytes();
			byte[] buffer;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write((byte)83);
					binaryWriter.Write((byte)65);
					binaryWriter.Write((byte)77);
					binaryWriter.Write((byte)80);
					binaryWriter.Write(ipBytes[0]);
					binaryWriter.Write(ipBytes[1]);
					binaryWriter.Write(ipBytes[2]);
					binaryWriter.Write(ipBytes[3]);
					binaryWriter.Write((ushort)port);
					binaryWriter.Write((byte)120);
					byte[] bytes = Encoding.ASCII.GetBytes(pass ?? "");
					binaryWriter.Write((ushort)bytes.Length);
					binaryWriter.Write(bytes);
					byte[] bytes2 = Encoding.GetEncoding(874).GetBytes(cmd);
					binaryWriter.Write((ushort)bytes2.Length);
					binaryWriter.Write(bytes2);
					buffer = memoryStream.ToArray();
				}
			}
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.ReceiveTimeout = 2500;
				socket.SendTimeout = 2500;
				socket.Connect(new IPEndPoint(IPAddress.Parse(text), port));
				socket.Send(buffer);
				StringBuilder stringBuilder = new StringBuilder();
				byte[] array2 = new byte[4096];
				int num = 0;
				while (num < 8)
				{
					num++;
					try
					{
						int num2 = socket.Receive(array2);
						if (num2 > 12 && array2[11] == 120)
						{
							int num3 = array2[12] | (array2[13] << 8);
							if (num3 > num2 - 14)
							{
								num3 = num2 - 14;
							}
							if (num3 > 0)
							{
								stringBuilder.AppendLine(Encoding.GetEncoding(874).GetString(array2, 14, num3));
							}
						}
					}
					catch
					{
						break;
					}
					if (stringBuilder.Length > 8000)
					{
						break;
					}
				}
				return stringBuilder.ToString().TrimEnd();
			}
		}
		catch (Exception ex)
		{
			return "ERR: " + ex.Message;
		}
	}

	public void LaunchAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass)
	{
		string resolvedIp = ResolveHost(ip);
		if (resolvedIp == null)
		{
			resolvedIp = ip;
		}
		string cmdLine = "\"" + gtaPath + "\" -c -n " + nick + " -h " + resolvedIp + " -p " + port;
		if (!string.IsNullOrEmpty(pass))
		{
			cmdLine += " -z " + pass;
		}
		STARTUPINFO si = default(STARTUPINFO);
		si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
		PROCESS_INFORMATION pi;
		if (!CreateProcess(gtaPath, new StringBuilder(cmdLine), IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, workDir, ref si, out pi))
		{
			throw new Exception("เปิด gta_sa.exe ไม่ได้ (Win32 error " + Marshal.GetLastWin32Error() + ")");
		}
		try
		{
			InjectDll(pi.dwProcessId, dllPath);
		}
		catch
		{
			try { TerminateProcess(pi.hProcess, 1u); } catch { }
			CloseHandle(pi.hThread);
			CloseHandle(pi.hProcess);
			throw;
		}
		ResumeThread(pi.hThread);
		CloseHandle(pi.hThread);
		CloseHandle(pi.hProcess);
	}

	private void InjectDll(uint pid, string dllPath)
	{
		IntPtr hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
		if (hProc == IntPtr.Zero)
		{
			throw new Exception("OpenProcess ล้มเหลว (error " + Marshal.GetLastWin32Error() + ")");
		}
		IntPtr remoteBuf = IntPtr.Zero;
		try
		{
			byte[] bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
			remoteBuf = VirtualAllocEx(hProc, IntPtr.Zero, (IntPtr)bytes.Length, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
			if (remoteBuf == IntPtr.Zero)
			{
				throw new Exception("VirtualAllocEx ล้มเหลว (error " + Marshal.GetLastWin32Error() + ")");
			}
			IntPtr written;
			if (!WriteProcessMemory(hProc, remoteBuf, bytes, (IntPtr)bytes.Length, out written))
			{
				throw new Exception("WriteProcessMemory ล้มเหลว (error " + Marshal.GetLastWin32Error() + ")");
			}
			IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
			if (procAddress == IntPtr.Zero)
			{
				throw new Exception("หา LoadLibraryW ไม่เจอ");
			}
			IntPtr tid;
			IntPtr hThread = CreateRemoteThread(hProc, IntPtr.Zero, IntPtr.Zero, procAddress, remoteBuf, 0u, out tid);
			if (hThread == IntPtr.Zero)
			{
				throw new Exception("CreateRemoteThread ล้มเหลว (error " + Marshal.GetLastWin32Error() + " — antivirus อาจบล็อก)");
			}
			try
			{
				uint wait = WaitForSingleObject(hThread, 15000u);
				if (wait == WAIT_TIMEOUT)
				{
					throw new Exception("LoadLibrary ไม่ตอบสนอง (timeout)");
				}
				if (wait != WAIT_OBJECT_0)
				{
					throw new Exception("WaitForSingleObject ล้มเหลว (code " + wait + ")");
				}
				uint exitCode;
				if (!GetExitCodeThread(hThread, out exitCode) || exitCode == 0u)
				{
					throw new Exception("โหลด samp.dll ไม่สำเร็จ (LoadLibraryW คืนค่า 0 — ไฟล์อาจเสียหายหรือถูก antivirus บล็อก)");
				}
			}
			finally
			{
				CloseHandle(hThread);
			}
			if (remoteBuf != IntPtr.Zero)
			{
				VirtualFreeEx(hProc, remoteBuf, IntPtr.Zero, MEM_RELEASE);
				remoteBuf = IntPtr.Zero;
			}
		}
		finally
		{
			if (remoteBuf != IntPtr.Zero)
			{
				VirtualFreeEx(hProc, remoteBuf, IntPtr.Zero, MEM_RELEASE);
			}
			CloseHandle(hProc);
		}
	}
}
