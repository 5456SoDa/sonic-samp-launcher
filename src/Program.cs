using System;
using System.IO;
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

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

	private const uint WM_COPYDATA = 0x004A;
	public const uint WM_USER_RESTORE = 0x8001;
	private const int SW_RESTORE = 9;

	private static void LogAndShowFatal(Exception ex, bool fromUnhandled)
	{
		try
		{
			string logPath = Path.Combine(Application.StartupPath, "launcher_error.log");
			string msg = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}{2}{3}{2}{2}",
				DateTime.Now,
				fromUnhandled ? "FATAL: " : "UI_ERROR: ",
				Environment.NewLine,
				ex != null ? ex.ToString() : "(unknown error)");
			File.AppendAllText(logPath, msg, Encoding.UTF8);
		}
		catch
		{
		}
		try
		{
			MessageBox.Show(
				"เกิดข้อผิดพลาดที่ไม่คาดคิดใน Launcher:\r\n" + (ex != null ? ex.Message : "Unknown") + "\r\n\r\nรายละเอียดถูกบันทึกไว้ที่ launcher_error.log แล้ว",
				"Sonic SAMP Launcher — ข้อผิดพลาด",
				MessageBoxButtons.OK,
				MessageBoxIcon.Error);
		}
		catch
		{
		}
	}

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
		using (Mutex obj = new Mutex(true, "SampLauncher_SingleInstance", out createdNew))
		{
			if (!createdNew)
			{
				IntPtr hWnd = FindWindow(null, "SA-MP Launcher");
				if (hWnd != IntPtr.Zero)
				{
					SendMessage(hWnd, WM_USER_RESTORE, IntPtr.Zero, IntPtr.Zero);
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
				using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Classes\\samp"))
				{
					if (registryKey != null)
					{
						registryKey.SetValue("", "URL:SAMP Protocol");
						registryKey.SetValue("URL Protocol", "");
						using (RegistryKey registryKey2 = registryKey.CreateSubKey("shell\\open\\command"))
						{
							if (registryKey2 != null)
							{
								registryKey2.SetValue("", "\"" + Application.ExecutablePath + "\" \"%1\"");
							}
						}
					}
				}
			}
			catch
			{
			}
			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
			{
				LogAndShowFatal(e.Exception, false);
			};
			AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
			{
				LogAndShowFatal(e.ExceptionObject as Exception, true);
			};
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Application.Run(new LauncherForm(text));
			}
			catch (CoreLoadException cex)
			{
				MessageBox.Show(cex.Message, "Sonic SAMP Launcher — ไม่พบ Rust Engine", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			catch (Exception ex)
			{
				LogAndShowFatal(ex, true);
			}
			GC.KeepAlive(obj);
		}
	}
}
