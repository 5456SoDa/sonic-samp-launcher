using System;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Program
{
	[StructLayout(LayoutKind.Sequential)]
	public struct COPYDATASTRUCT
	{
		public IntPtr dwData;
		public int cbData;
		public IntPtr lpData;
	}

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

	private const uint WM_COPYDATA = 0x004A;
	private const int SW_RESTORE = 9;

	[STAThread]
	private static void Main()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		string text = null;
		if (commandLineArgs.Length >= 2 && commandLineArgs[1].ToLowerInvariant().StartsWith("samp://"))
		{
			text = commandLineArgs[1];
		}
		bool createdNew;
		Mutex obj = new Mutex(true, "SampLauncher_SingleInstance", out createdNew);
		if (!createdNew)
		{
			IntPtr hWnd = FindWindow(null, "SA-MP Launcher");
			if (hWnd != IntPtr.Zero)
			{
				ShowWindow(hWnd, SW_RESTORE);
				SetForegroundWindow(hWnd);
				if (text != null)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(text + "\0");
					IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
					try
					{
						Marshal.Copy(bytes, 0, ptr, bytes.Length);
						COPYDATASTRUCT cds = new COPYDATASTRUCT
						{
							dwData = (IntPtr)0x53414D50, // "SAMP"
							cbData = bytes.Length,
							lpData = ptr
						};
						SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
					}
					finally
					{
						Marshal.FreeHGlobal(ptr);
					}
				}
			}
			return;
		}
		try
		{
			ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls;
		}
		catch
		{
			try
			{
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
			}
			catch
			{
			}
		}
		try
		{
			RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\samp");
			registryKey.SetValue("", "URL:SAMP Protocol");
			registryKey.SetValue("URL Protocol", "");
			RegistryKey registryKey2 = registryKey.CreateSubKey("shell\\open\\command");
			registryKey2.SetValue("", "\"" + Application.ExecutablePath + "\" \"%1\"");
			registryKey2.Close();
			registryKey.Close();
		}
		catch
		{
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run(new LauncherForm(text));
		GC.KeepAlive(obj);
	}
}
