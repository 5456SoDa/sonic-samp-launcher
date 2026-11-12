using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

internal class LauncherForm : Form
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

	private const uint PAGE_READWRITE = 4u;

	private const uint WAIT_TIMEOUT = 258u;

	private const uint PROCESS_ALL_ACCESS = 2035711u;

	private Color colBg = Color.FromArgb(11, 14, 20);

	private Color colSide = Color.FromArgb(14, 19, 30);

	private Color colCard = Color.FromArgb(19, 27, 43);

	private Color colField = Color.FromArgb(16, 22, 36);

	private Color colFieldHover = Color.FromArgb(25, 36, 58);

	private Color colAccent = Color.FromArgb(51, 204, 255);

	private Color colAccentHi = Color.FromArgb(115, 225, 255);

	private Color colText = Color.FromArgb(255, 255, 255);

	private Color colSub = Color.FromArgb(140, 165, 195);

	private Color colSel = Color.FromArgb(28, 45, 72);

	private Color colOk = Color.FromArgb(102, 255, 0);

	private Color colYellow = Color.FromArgb(255, 235, 0);

	private Color colYellowHi = Color.FromArgb(255, 250, 80);

	private Font fTitle;

	private Font fSub;

	private Font fField;

	private Font fButton;

	private Font fSmall;

	private Font fLogo;

	private Font fLogoSub;

	private Font fNav;

	private Font fStat;

	private Font fBadge;

	private RoundedBtn btnNavPlay;

	private RoundedBtn btnNavSrv;

	private RoundedBtn btnNavSet;

	private RoundedBtn btnNavChat;
	private Panel pnlChat;
	private RichTextBox txtChatLog;
	private TextBox txtChatSearch;
	private Label lblChatInfo;
	private List<string> chatLines = new List<string>();
	private Panel pnlPlay;

	private Panel pnlSrv;

	private Panel pnlSet;

	private TextBox txtNick;

	private TextBox txtIp;

	private TextBox txtPort;

	private TextBox txtPath;

	private Label lblHeroName;

	private Label lblHeroIp;

	private Label lblHeroMode;

	private Label lblHeroPing;

	private RingGauge ring;

	private Panel chipPanel;

	private ToggleSwitch swAuto;

	private ToggleSwitch swClose;

	private ToggleSwitch swSoundFx;

	private ToggleSwitch swDiscordRpc;

	private bool soundFxEnabled = true;

	private bool discordRpcEnabled = true;

	private string discordClientId = "1088484931343753236";

	private Panel navIndicator;

	private Bitmap bgCache;

	private RoundedBtn btnF1;

	private RoundedBtn btnF2;

	private RoundedBtn btnF3;

	private bool fHideFull;

	private bool fHideEmpty;

	private bool fHideLocked;

	private System.Windows.Forms.Timer pubAuto;

	private Dictionary<string, long> playtime = new Dictionary<string, long>();

	private string playtimeFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playtime.txt");

	private System.Threading.Timer ptTimer;

	private string ptServer;

	private readonly object ptLock = new object();

	private bool connecting;

	private RoundedBtn btnConnect;

	private RoundedBtn btnRefreshAll;

	private ListView lstSrv;

	private Label lblStatus;

	private List<ServerEntry> favs = new List<ServerEntry>();

	private List<ServerEntry> hist = new List<ServerEntry>();

	private List<ServerEntry> pub = new List<ServerEntry>();

	private string viewMode = "mine";

	private bool fetchingPub;

	private RoundedBtn btnMine;

	private RoundedBtn btnPub;

	private Label emptyLbl;

	private RoundedBtn btnNavGal;

	private RoundedBtn btnNavRcon;

	private Panel pnlGal;

	private Panel pnlRcon;

	private FlowLayoutPanel galFlow;

	private Label galCount;

	private bool galLoaded;

	private int galGen;

	private TextBox txtRconIp;

	private TextBox txtRconPort;

	private TextBox txtRconPass;

	private TextBox txtRconCmd;

	private TextBox txtRconOut;

	private ServerEntry watchEntry;

	private System.Threading.Timer watchTimer;

	private ToolStripMenuItem miWatch;

	private SolidBrush brCard;

	private SolidBrush brSel;

	private SolidBrush brHeader;

	private SolidBrush brAccentBar;

	private SolidBrush brDotOff;

	private SolidBrush brPingG;

	private SolidBrush brPingY;

	private SolidBrush brPingR;

	private SolidBrush brPillFav;

	private SolidBrush brPillPub;

	private SolidBrush brPillHist;

	private Pen penHeaderLine;

	private Dictionary<string, int> pillWidthCache = new Dictionary<string, int>();

	private TextBox txtSearch;

	private string filterText = "";

	private int sortCol = -1;

	private bool sortDesc;

	private Label[] lblHealth;

	private string bootUrl;

	private string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samp_launcher.cfg");

	private string statsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_stats.txt");

	private Random rnd = new Random();

	private bool querying;

	private string playQueryKey = "";

	private bool cfgReady;

	private List<System.Windows.Forms.Timer> activeTimers = new List<System.Windows.Forms.Timer>();

	private List<string> nickProfiles = new List<string>();
	private string activeSkyGfx = "ps2";
	private int activeFpsLimit = 144;
	private bool autoJoinSlot = false;
	private ToggleSwitch swAutoSlot;
	private RoundedBtn btnSgPs2;
	private RoundedBtn btnSgPc;
	private RoundedBtn btnSgMob;
	private RoundedBtn btnSgOff;
	private RoundedBtn btnFps60;
	private RoundedBtn btnFps90;
	private RoundedBtn btnFps120;
	private RoundedBtn btnFps144;
	private RoundedBtn btnFpsMax;
	private TextBox txtServerPass;
	private ToggleSwitch swNoPauseAltTab;
	private bool noPauseAltTabEnabled = false;
	private RoundedBtn btnWatch;
	private List<string> currentGalleryPaths = new List<string>();

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

	[DllImport("kernel32.dll")]
	private static extern uint ResumeThread(IntPtr hThread);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

	public LauncherForm()
		: this(null)
	{
	}

	public LauncherForm(string bootUrlArg)
	{
		bootUrl = bootUrlArg;
		Text = "SA-MP Launcher";
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterScreen;
		base.Size = new Size(900, 580);
		BackColor = colBg;
		DoubleBuffered = true;
		base.KeyPreview = true;
		base.Opacity = 0.0;
		InitBrushes();
		fTitle = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold);
		fSub = new Font("Segoe UI", 9f);
		fField = new Font("Segoe UI", 10.5f);
		fButton = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
		fSmall = new Font("Segoe UI", 9f);
		fLogo = new Font("Segoe UI", 19f, FontStyle.Bold);
		fLogoSub = new Font("Segoe UI", 8.5f);
		fNav = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
		fStat = new Font("Segoe UI Semibold", 10f);
		fBadge = new Font("Segoe UI", 8.5f, FontStyle.Bold);
		BuildUi();
		UpdateRegion();
		LoadConfig();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		UpdateRegion();
		RenderBg();
		Invalidate();
	}

	private void UpdateRegion()
	{
		base.Region = new Region(RoundRect(new Rectangle(0, 0, base.Width, base.Height), 16));
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		if (bgCache != null)
		{
			graphics.DrawImage(bgCache, 0, 0);
		}
		using (Pen pen = new Pen(Color.FromArgb(60, colAccent), 1f))
		{
			graphics.DrawPath(pen, RoundRect(new Rectangle(0, 0, base.Width - 1, base.Height - 1), 16));
		}
	}

	private void RenderBg()
	{
		if (base.Width >= 50 && base.Height >= 50 && fLogo != null && fLogoSub != null)
		{
			if (bgCache != null)
			{
				bgCache.Dispose();
			}
			Bitmap image = new Bitmap(base.Width, base.Height);
			Graphics graphics = Graphics.FromImage(image);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;

			// Cyber Obsidian Gradient Background
			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, base.Width, base.Height), Color.FromArgb(11, 14, 20), Color.FromArgb(16, 23, 36), LinearGradientMode.Vertical))
			{
				graphics.FillRectangle(brush, 0, 0, base.Width, base.Height);
			}

			// Bottom status bar background
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(9, 12, 17)))
			{
				graphics.FillRectangle(brush2, 0, base.Height - 50, base.Width, 50);
			}

			// Top-right Sonic Blue ambient glow
			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(24, colAccent)))
			{
				graphics.FillEllipse(brush3, base.Width - 320, -160, 440, 440);
			}
			// Bottom-left subtle glow
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(14, colAccent)))
			{
				graphics.FillEllipse(brush4, -120, base.Height - 220, 360, 360);
			}

			// Cyber Tech Grid Lines in top-right
			using (Pen penGrid = new Pen(Color.FromArgb(18, colAccent), 1f))
			{
				for (int i = 0; i < 7; i++)
				{
					int offset = i * 32;
					graphics.DrawLine(penGrid, base.Width - 260 + offset, 0, base.Width, 260 - offset);
				}
			}

			// Branding: SONIC (Blue) + SAMP (White) + LAUNCHER (Cyan)
			string s1 = "SONIC ";
			string s2 = "SAMP ";
			string s3 = "LAUNCHER";
			SizeF sz1 = graphics.MeasureString(s1, fLogo);
			SizeF sz2 = graphics.MeasureString(s2, fLogo);
			SizeF sz3 = graphics.MeasureString(s3, fLogo);

			graphics.DrawString(s1, fLogo, new SolidBrush(colAccent), 200f, 16f);
			graphics.DrawString(s2, fLogo, new SolidBrush(colText), 200f + sz1.Width, 16f);
			graphics.DrawString(s3, fLogo, new SolidBrush(colAccentHi), 200f + sz1.Width + sz2.Width, 16f);

			// Modern Cyber Badge: "CYBER EDITION" in Cyber Yellow
			Rectangle rBadge = new Rectangle((int)(200f + sz1.Width + sz2.Width + sz3.Width + 12f), 22, 114, 22);
			using (SolidBrush bBadge = new SolidBrush(Color.FromArgb(30, colYellow)))
			using (Pen pBadge = new Pen(colYellow, 1f))
			{
				graphics.FillRectangle(bBadge, rBadge);
				graphics.DrawRectangle(pBadge, rBadge);
				graphics.DrawString("CYBER EDITION", fBadge, new SolidBrush(colYellow), rBadge.X + 8, rBadge.Y + 3);
			}

			graphics.DrawString("HIGH-PERFORMANCE MULTIPLAYER CLIENT  •  SA-MP 0.3.7 R2", fLogoSub, new SolidBrush(colSub), 202f, 56f);

			// Sonic Blue 2px Glow Divider Line
			using (Pen pen = new Pen(Color.FromArgb(140, colAccent), 2f))
			{
				graphics.DrawLine(pen, 200, 86, base.Width - 26, 86);
			}

			graphics.Dispose();
			bgCache = image;
		}
	}

	private void DrawPalm(Graphics g, Color c, int baseX, int baseY, int h, bool flip)
	{
		// Retired in Cyber-Sonic Edition
	}


	private void BuildUi()
	{
		base.MouseDown += DragWindow;
		RoundedBtn roundedBtn = MakeWinBtn("–");
		roundedBtn.Location = new Point(base.Width - 84, 14);
		roundedBtn.Click += delegate
		{
			base.WindowState = FormWindowState.Minimized;
		};
		RoundedBtn roundedBtn2 = MakeWinBtn("×");
		roundedBtn2.Location = new Point(base.Width - 46, 14);
		roundedBtn2.TextColor = Color.FromArgb(255, 99, 99);
		roundedBtn2.Click += delegate
		{
			Close();
		};
		Panel panel = new Panel();
		panel.Location = new Point(0, 87);
		panel.Size = new Size(190, base.Height - 87 - 36);
		panel.BackColor = colSide;
		EnableDoubleBuffer(panel);
		base.Controls.Add(panel);
		btnNavPlay = MakeNav("   ▶  เล\u0e48นเกม", 14);
		btnNavSrv = MakeNav("   ☰  เซ\u0e34ร\u0e4cเวอร\u0e4c", 56);
		btnNavChat = MakeNav("   📜  บ\u0e31นท\u0e36กแชท", 98);
		btnNavGal = MakeNav("   🖼  แกลเลอร\u0e35", 140);
		btnNavSet = MakeNav("   ⚙  ต\u0e31\u0e49งค\u0e48า", 182);
		btnNavRcon = MakeNav("   🛠  RCON", 224);
		btnNavPlay.Click += delegate
		{
			ShowPage(0);
		};
		btnNavSrv.Click += delegate
		{
			ShowPage(1);
		};
		btnNavChat.Click += delegate
		{
			ShowPage(2);
		};
		btnNavGal.Click += delegate
		{
			ShowPage(3);
		};
		btnNavSet.Click += delegate
		{
			ShowPage(4);
		};
		btnNavRcon.Click += delegate
		{
			ShowPage(5);
		};
		panel.Controls.Add(btnNavPlay);
		panel.Controls.Add(btnNavSrv);
		panel.Controls.Add(btnNavChat);
		panel.Controls.Add(btnNavGal);
		panel.Controls.Add(btnNavSet);
		panel.Controls.Add(btnNavRcon);
		navIndicator = new Panel();
		navIndicator.Size = new Size(4, 26);
		navIndicator.Location = new Point(0, 20);
		navIndicator.BackColor = colAccent;
		panel.Controls.Add(navIndicator);
		navIndicator.BringToFront();
		Label label = new Label();
		label.Text = "SONIC CLIENT v2.3";
		label.Font = fSmall;
		label.ForeColor = colSub;
		label.BackColor = Color.Transparent;
		label.Location = new Point(18, panel.Height - 34);
		label.AutoSize = true;
		panel.Controls.Add(label);
		pnlPlay = MakePage();
		pnlSrv = MakePage();
		pnlChat = MakePage();
		pnlGal = MakePage();
		pnlSet = MakePage();
		pnlRcon = MakePage();
		EnableDoubleBuffer(pnlPlay);
		EnableDoubleBuffer(pnlSrv);
		EnableDoubleBuffer(pnlChat);
		EnableDoubleBuffer(pnlGal);
		EnableDoubleBuffer(pnlSet);
		EnableDoubleBuffer(pnlRcon);
		BuildPlayPage();
		BuildSrvPage();
		BuildChatPage();
		BuildGalPage();
		BuildSetPage();
		BuildRconPage();
		lblStatus = new Label();
		lblStatus.Text = "พร\u0e49อมเช\u0e37\u0e48อมต\u0e48อ";
		lblStatus.Font = fSub;
		lblStatus.ForeColor = colSub;
		lblStatus.BackColor = Color.Transparent;
		lblStatus.Location = new Point(206, base.Height - 36);
		lblStatus.AutoSize = true;
		base.Controls.Add(lblStatus);
		Label label2 = new Label();
		label2.Text = "SA-MP 0.3.7 R2 • CUSTOM LAUNCHER";
		label2.Font = fLogoSub;
		label2.ForeColor = colSub;
		label2.BackColor = Color.Transparent;
		label2.AutoSize = false;
		label2.Size = new Size(TextRenderer.MeasureText(label2.Text, label2.Font).Width + 4, 18);
		label2.Left = base.Width - label2.Width - 28;
		label2.Top = base.Height - 36;
		base.Controls.Add(label2);
		ShowPage(0);
		CyberAudio.Enabled = soundFxEnabled;
		CyberAudio.Init();
		DiscordRpc.Enabled = discordRpcEnabled;
		DiscordRpc.Initialize(discordClientId);
		DiscordRpc.UpdateLauncherPresence();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		DiscordRpc.Shutdown();
		base.OnFormClosing(e);
	}

	private Panel MakePage()
	{
		Panel panel = new TransPanel();
		panel.Location = new Point(206, 98);
		panel.Size = new Size(668, 436);
		panel.MouseDown += DragWindow;
		base.Controls.Add(panel);
		return panel;
	}

	private RoundedBtn MakeNav(string text, int y)
	{
		RoundedBtn roundedBtn = new RoundedBtn(text, colSide, colField, colSub, 10, fNav);
		roundedBtn.AlignLeft = true;
		roundedBtn.Location = new Point(14, y);
		roundedBtn.Size = new Size(162, 40);
		return roundedBtn;
	}

	private void InitBrushes()
	{
		brCard = new SolidBrush(colCard);
		brSel = new SolidBrush(colSel);
		brHeader = new SolidBrush(colSide);
		brAccentBar = new SolidBrush(colAccent);
		penHeaderLine = new Pen(Color.FromArgb(120, colAccent));
		brDotOff = new SolidBrush(Color.FromArgb(80, 95, 120));
		brPingG = new SolidBrush(colOk);
		brPingY = new SolidBrush(colYellow);
		brPingR = new SolidBrush(Color.FromArgb(255, 70, 90));
		brPillFav = new SolidBrush(Color.FromArgb(50, colAccent));
		brPillPub = new SolidBrush(Color.FromArgb(50, colOk));
		brPillHist = new SolidBrush(Color.FromArgb(50, colSub));
	}

	private int PillWidth(string t)
	{
		int value;
		if (!pillWidthCache.TryGetValue(t, out value))
		{
			value = TextRenderer.MeasureText(t, fSub).Width + 18;
			pillWidthCache[t] = value;
		}
		return value;
	}

	private static void EnableDoubleBuffer(Control c)
	{
		typeof(Control).InvokeMember("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty, null, c, new object[1] { true });
	}

	private void ShowPage(int idx)
	{
		Panel panel;
		switch (idx)
		{
		default:
			panel = pnlRcon;
			break;
		case 4:
			panel = pnlSet;
			break;
		case 3:
			panel = pnlGal;
			break;
		case 2:
			panel = pnlChat;
			break;
		case 1:
			panel = pnlSrv;
			break;
		case 0:
			panel = pnlPlay;
			break;
		}
		Panel target = panel;
		Panel[] array = new Panel[6] { pnlPlay, pnlSrv, pnlChat, pnlGal, pnlSet, pnlRcon };
		foreach (Panel panel2 in array)
		{
			if (panel2 != target)
			{
				panel2.Visible = false;
				panel2.Location = new Point(206, 98);
			}
		}
		target.Visible = true;
		StyleNav(btnNavPlay, idx == 0);
		StyleNav(btnNavSrv, idx == 1);
		StyleNav(btnNavChat, idx == 2);
		StyleNav(btnNavGal, idx == 3);
		StyleNav(btnNavSet, idx == 4);
		StyleNav(btnNavRcon, idx == 5);

		RoundedBtn activeBtn = ((idx == 0) ? btnNavPlay : ((idx == 1) ? btnNavSrv : ((idx == 2) ? btnNavChat : ((idx == 3) ? btnNavGal : ((idx == 4) ? btnNavSet : btnNavRcon)))));
		if (activeBtn != null && navIndicator != null)
		{
			navIndicator.Top = activeBtn.Top + 6;
		}

		Point orig = target.Location;
		target.Location = new Point(orig.X + 26, orig.Y);
		Animate(190, delegate(float t)
		{
			if (!target.IsDisposed)
			{
				target.Location = new Point(orig.X + (int)(26f * (1f - t)), orig.Y);
			}
		});
		if (idx == 1 && viewMode == "mine")
		{
			RefreshAllServers();
		}
		if (idx != 1)
		{
			StopPubAuto();
		}
		if (idx == 1 && viewMode == "public")
		{
			StartPubAuto();
		}
		if (idx == 2)
		{
			LoadChatLines();
		}
		if (idx == 3 && !galLoaded)
		{
			galLoaded = true;
			LoadGallery();
		}
		if (idx == 4)
		{
			RunHealthCheck();
		}
	}

	private void Animate(int ms, Action<float> step)
	{
		System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
		t.Interval = 15;
		activeTimers.Add(t);
		Stopwatch swt = Stopwatch.StartNew();
		t.Tick += delegate
		{
			if (base.IsDisposed)
			{
				t.Stop();
				t.Dispose();
				activeTimers.Remove(t);
			}
			else
			{
				float num = Math.Min(1f, (float)swt.ElapsedMilliseconds / (float)ms);
				step(Anim.EaseOut(num));
				if (num >= 1f)
				{
					t.Stop();
					t.Dispose();
					activeTimers.Remove(t);
				}
			}
		};
		t.Start();
	}

	private void StyleNav(RoundedBtn b, bool active)
	{
		if (active)
		{
			b.SetPalette(colSel, colFieldHover);
			b.TextColor = colAccent;
			b.BorderColor = Color.FromArgb(100, colAccent);
		}
		else
		{
			b.SetPalette(colSide, colField);
			b.TextColor = colSub;
			b.BorderColor = Color.Transparent;
		}
	}

	private void BuildPlayPage()
	{
		// 1. Hero Server Card (Y=0, H=140, W=668)
		SmoothPanel smoothPanel = new SmoothPanel(colCard, 12);
		smoothPanel.BorderColor = Color.FromArgb(45, 68, 105);
		smoothPanel.Location = new Point(0, 0);
		smoothPanel.Size = new Size(668, 140);
		pnlPlay.Controls.Add(smoothPanel);

		lblHeroName = new Label();
		lblHeroName.Text = "กรอก IP เพ\u0e37\u0e48อตรวจสถานะเซ\u0e34ร\u0e4cเวอร\u0e4c";
		lblHeroName.Font = new Font("Segoe UI Semibold", 13.5f, FontStyle.Bold);
		lblHeroName.ForeColor = Color.White;
		lblHeroName.BackColor = Color.Transparent;
		lblHeroName.Location = new Point(20, 14);
		lblHeroName.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroName);

		lblHeroIp = new Label();
		lblHeroIp.Text = "—";
		lblHeroIp.Font = new Font("Segoe UI Semibold", 9.5f);
		lblHeroIp.ForeColor = colAccent;
		lblHeroIp.BackColor = Color.Transparent;
		lblHeroIp.Location = new Point(20, 44);
		lblHeroIp.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroIp);

		lblHeroMode = new Label();
		lblHeroMode.Text = "";
		lblHeroMode.Font = new Font("Segoe UI Semibold", 9f);
		lblHeroMode.ForeColor = colSub;
		lblHeroMode.BackColor = Color.Transparent;
		lblHeroMode.Location = new Point(20, 68);
		lblHeroMode.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroMode);

		lblHeroPing = new Label();
		lblHeroPing.Text = "";
		lblHeroPing.Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		lblHeroPing.ForeColor = colOk;
		lblHeroPing.BackColor = Color.Transparent;
		lblHeroPing.Location = new Point(20, 92);
		lblHeroPing.AutoSize = true;
		smoothPanel.Controls.Add(lblHeroPing);

		Label label = new Label();
		label.Text = "* ป\u0e34ด Frame Limiter ในเกม เพ\u0e37\u0e48อเล\u0e48นได\u0e49 144 FPS อย\u0e48างล\u0e37\u0e48นไหล";
		label.Font = fSmall;
		label.ForeColor = colSub;
		label.BackColor = Color.Transparent;
		label.Location = new Point(20, 114);
		label.AutoSize = true;
		smoothPanel.Controls.Add(label);

		ring = new RingGauge();
		ring.Location = new Point(552, 20);
		ring.Size = new Size(100, 100);
		ring.RingColor = colAccent;
		smoothPanel.Controls.Add(ring);

		RoundedBtn roundedBtn = MakeMiniBtn("↻", 508, 48, 36, 36);
		roundedBtn.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
		roundedBtn.BorderColor = colAccent;
		roundedBtn.Click += delegate
		{
			QueryPlayServer();
		};
		smoothPanel.Controls.Add(roundedBtn);

		// 2. Middle: Symmetrical 2-Column Boxes (Y=148, H=124)
		// Left Box: Player Identity (W=326, H=124)
		SmoothPanel pnlNickBox = new SmoothPanel(colCard, 10);
		pnlNickBox.Location = new Point(0, 148);
		pnlNickBox.Size = new Size(326, 124);
		pnlNickBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlNickBox);

		AddLbl(pnlNickBox, "👤  ข\u0e49อม\u0e39ลต\u0e31วละคร (PLAYER IDENTITY)", fSub, 14, 10, colAccent);
		txtNick = AddField(pnlNickBox, "ช\u0e37\u0e48อเล\u0e48น: MyNick_TH", 14, 32, 298);

		RoundedBtn btnProfiles = MakeMiniBtn("👤  โปรไฟล\u0e4cช\u0e37\u0e48อ", 14, 78, 144, 34);
		btnProfiles.BorderColor = colAccent;
		btnProfiles.Click += delegate
		{
			ShowNickProfilesMenu(btnProfiles);
		};
		pnlNickBox.Controls.Add(btnProfiles);

		RoundedBtn btnRand = MakeMiniBtn("🎲  ส\u0e38\u0e48มช\u0e37\u0e48อ", 168, 78, 144, 34);
		btnRand.BorderColor = Color.FromArgb(40, 60, 95);
		btnRand.Click += delegate
		{
			ForceText(txtNick, RandomNick());
		};
		pnlNickBox.Controls.Add(btnRand);

		// Right Box: Target Server (W=326, H=124)
		SmoothPanel pnlSrvBox = new SmoothPanel(colCard, 10);
		pnlSrvBox.Location = new Point(342, 148);
		pnlSrvBox.Size = new Size(326, 124);
		pnlSrvBox.BorderColor = Color.FromArgb(40, 60, 95);
		pnlPlay.Controls.Add(pnlSrvBox);

		AddLbl(pnlSrvBox, "🌐  ปลายทางเซิร์ฟเวอร์ & รหัสผ่าน (SERVER ADDRESS)", fSub, 14, 10, colAccent);
		txtIp = AddField(pnlSrvBox, "127.0.0.1", 14, 32, 130);
		txtPort = AddField(pnlSrvBox, "7777", 150, 32, 66);
		txtServerPass = AddField(pnlSrvBox, "รหัส (ถ้ามี)", 222, 32, 90);
		txtServerPass.GotFocus += delegate
		{
			if (txtServerPass.ForeColor == colText && txtServerPass.Text.Length > 0)
			{
				txtServerPass.UseSystemPasswordChar = true;
			}
		};
		txtServerPass.TextChanged += delegate
		{
			if (txtServerPass.ForeColor == colText && txtServerPass.Text.Length > 0)
			{
				txtServerPass.UseSystemPasswordChar = true;
			}
			else
			{
				txtServerPass.UseSystemPasswordChar = false;
			}
		};
		txtServerPass.LostFocus += delegate
		{
			if (txtServerPass.ForeColor == colSub)
			{
				txtServerPass.UseSystemPasswordChar = false;
			}
		};

		txtIp.TextChanged += delegate
		{
			string val = txtIp.Text.Trim();
			if (val.Contains(":"))
			{
				string[] parts = val.Split(':');
				if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
				{
					ForceText(txtIp, parts[0]);
					ForceText(txtPort, parts[1].TrimEnd('/'));
					QueryPlayServer();
				}
			}
		};

		swAuto = new ToggleSwitch();
		swAuto.Location = new Point(16, 82);
		swAuto.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlSrvBox.Controls.Add(swAuto);
		AddLbl(pnlSrvBox, "เช\u0e37\u0e48อมต\u0e48ออ\u0e31ตโนม\u0e31ต\u0e34เม\u0e37\u0e48อเป\u0e34ด Launcher", fSmall, 68, 85, colSub);

		// 3. Primary Action Button: Full-Width Cyber Yellow (Y=282, H=50, W=668)
		btnConnect = new RoundedBtn("▶   เข\u0e49าส\u0e39\u0e48เกม (LAUNCH GAME)", colYellow, colYellowHi, Color.FromArgb(11, 14, 20), 12, new Font("Segoe UI", 13.5f, FontStyle.Bold));
		btnConnect.BorderColor = Color.FromArgb(255, 245, 120);
		btnConnect.Location = new Point(0, 282);
		btnConnect.Size = new Size(668, 50);
		btnConnect.Click += delegate
		{
			ConnectToServer();
		};
		pnlPlay.Controls.Add(btnConnect);
		base.AcceptButton = btnConnect;

		// 4. Quick Access Server Chips (Y=344, H=42, W=668)
		AddLbl(pnlPlay, "⭐  เซ\u0e34ร\u0e4cเวอร\u0e4cโปรด / เข\u0e49าเร\u0e47ว:", fSub, 0, 344, colSub);
		chipPanel = new TransPanel();
		chipPanel.Location = new Point(0, 368);
		chipPanel.Size = new Size(668, 42);
		pnlPlay.Controls.Add(chipPanel);
	}


	private void ShowNickProfilesMenu(Control anchor)
	{
		ContextMenuStrip cms = new ContextMenuStrip();
		cms.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());
		cms.BackColor = colSide;
		cms.ForeColor = colText;

		ToolStripMenuItem header = new ToolStripMenuItem("--- เล\u0e37อกโปรไฟล\u0e4cช\u0e37\u0e48อผ\u0e39\u0e49เล\u0e48น (RP / DM) ---");
		header.Enabled = false;
		cms.Items.Add(header);

		string curNick = FieldVal(txtNick).Trim();
		if (nickProfiles.Count == 0 && curNick.Length > 0)
		{
			nickProfiles.Add(curNick);
		}

		foreach (string prof in nickProfiles)
		{
			string pName = prof;
			ToolStripMenuItem item = new ToolStripMenuItem("👤  " + pName);
			if (string.Equals(pName, curNick, StringComparison.OrdinalIgnoreCase))
			{
				item.Text = "✓  " + pName;
			}
			item.Click += delegate
			{
				ForceText(txtNick, pName);
				SaveConfig();
				ToastOk("เล\u0e37อกช\u0e37\u0e48อ: " + pName);
			};
			cms.Items.Add(item);
		}

		cms.Items.Add(new ToolStripSeparator());

		ToolStripMenuItem itemAdd = new ToolStripMenuItem("💾  บ\u0e31นท\u0e36กช\u0e37\u0e48อป\u0e31จจ\u0e38บ\u0e31นลงโปรไฟล\u0e4c");
		itemAdd.Click += delegate
		{
			string cur = FieldVal(txtNick).Trim();
			if (cur.Length >= 3 && cur.Length <= 20)
			{
				if (!nickProfiles.Contains(cur))
				{
					nickProfiles.Add(cur);
					SaveConfig();
					ToastOk("บ\u0e31นท\u0e36กช\u0e37\u0e48อ " + cur + " ลงโปรไฟล\u0e4cแล\u0e49ว");
				}
				else
				{
					Toast("ม\u0e35ช\u0e37\u0e48อ " + cur + " ในโปรไฟล\u0e4cแล\u0e49ว");
				}
			}
			else
			{
				ShowErr("กร\u0e38ณากรอกช\u0e37\u0e48อผ\u0e39\u0e49เล\u0e48นให\u0e49ถ\u0e39กต\u0e49อง (3-20 ต\u0e31วอ\u0e31กษร)");
			}
		};
		cms.Items.Add(itemAdd);

		if (nickProfiles.Count > 1)
		{
			ToolStripMenuItem itemClear = new ToolStripMenuItem("🗑  ล\u0e49างโปรไฟล\u0e4cอ\u0e37\u0e48นๆ ออก");
			itemClear.Click += delegate
			{
				string cur = FieldVal(txtNick).Trim();
				nickProfiles.Clear();
				if (cur.Length > 0)
				{
					nickProfiles.Add(cur);
				}
				SaveConfig();
				Toast("ล\u0e49างโปรไฟล\u0e4cเร\u0e35ยบร\u0e49อย");
			};
			cms.Items.Add(itemClear);
		}

		cms.Show(anchor, new Point(0, anchor.Height));
	}

	private void RebuildChips()
	{
		if (chipPanel == null)
		{
			return;
		}
		chipPanel.Controls.Clear();
		List<ServerEntry> list = new List<ServerEntry>();
		list.AddRange(favs);
		list.AddRange(hist);
		int num = 0;
		foreach (ServerEntry item in list)
		{
			if (num < 3)
			{
				ServerEntry ce = item;
				string text = ((ce.Name.Length > 0) ? ce.Name : (ce.Ip + ":" + ce.Port));
				if (text.Length > 24)
				{
					text = text.Substring(0, 23) + "…";
				}
				RoundedBtn roundedBtn = MakeMiniBtn(text, num * 206, 3, 198, 34);
				roundedBtn.Click += delegate
				{
					ForceText(txtIp, ce.Ip);
					ForceText(txtPort, ce.Port);
					ShowPage(0);
					QueryPlayServer();
				};
				chipPanel.Controls.Add(roundedBtn);
				num++;
				continue;
			}
			break;
		}
	}

	private void BuildSrvPage()
	{
		// Top Search & Filter Bar (Y=0, H=42, W=668)
		txtSearch = AddField(pnlSrv, "🔍  ค\u0e49นหาเซ\u0e34ร\u0e4cเวอร\u0e4c (ช\u0e37\u0e48อ / IP)...", 0, 0, 316);
		txtSearch.TextChanged += delegate
		{
			filterText = FieldVal(txtSearch).ToLowerInvariant();
			RebuildList();
		};
		txtSearch.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.KeyCode == Keys.Escape)
			{
				ev.SuppressKeyPress = true;
				ForceText(txtSearch, "");
				filterText = "";
				RebuildList();
			}
		};

		btnMine = MakeMiniBtn("★  ของฉ\u0e31น (0)", 326, 0, 106, 42);
		btnMine.BorderColor = colAccent;
		btnMine.Click += delegate
		{
			ShowView("mine");
		};
		pnlSrv.Controls.Add(btnMine);

		btnPub = MakeMiniBtn("🌐  สาธารณะ", 440, 0, 114, 42);
		btnPub.BorderColor = Color.FromArgb(40, 60, 95);
		btnPub.Click += delegate
		{
			ShowView("public");
		};
		pnlSrv.Controls.Add(btnPub);

		btnRefreshAll = MakeMiniBtn("↻  ร\u0e35เฟรช", 562, 0, 106, 42);
		btnRefreshAll.BorderColor = colAccent;
		btnRefreshAll.Click += delegate
		{
			RefreshCurrentView();
		};
		pnlSrv.Controls.Add(btnRefreshAll);

		// Filter Chips (shown when public mode is selected)
		btnF1 = MakeMiniBtn("ซ\u0e48อนเต\u0e47ม", 0, 48, 104, 26);
		btnF2 = MakeMiniBtn("ซ\u0e48อนคน 0", 110, 48, 104, 26);
		btnF3 = MakeMiniBtn("ซ\u0e48อนม\u0e35รห\u0e31ส", 220, 48, 114, 26);
		btnF1.Click += delegate
		{
			fHideFull = !fHideFull;
			StyleChip(btnF1, fHideFull);
			RebuildList();
		};
		btnF2.Click += delegate
		{
			fHideEmpty = !fHideEmpty;
			StyleChip(btnF2, fHideEmpty);
			RebuildList();
		};
		btnF3.Click += delegate
		{
			fHideLocked = !fHideLocked;
			StyleChip(btnF3, fHideLocked);
			RebuildList();
		};
		StyleChip(btnF1, false);
		StyleChip(btnF2, false);
		StyleChip(btnF3, false);
		btnF1.Visible = false;
		btnF2.Visible = false;
		btnF3.Visible = false;
		pnlSrv.Controls.Add(btnF1);
		pnlSrv.Controls.Add(btnF2);
		pnlSrv.Controls.Add(btnF3);

		// Center Table: ListView (Y=48, H=328, W=668)
		lstSrv = new ListView();
		lstSrv.View = View.Details;
		lstSrv.BorderStyle = BorderStyle.None;
		lstSrv.BackColor = colCard;
		lstSrv.ForeColor = colText;
		lstSrv.Font = fField;
		lstSrv.FullRowSelect = true;
		lstSrv.HideSelection = true;
		lstSrv.ShowItemToolTips = true;
		lstSrv.OwnerDraw = true;
		lstSrv.Location = new Point(0, 48);
		lstSrv.Size = new Size(668, 328);
		lstSrv.SmallImageList = new ImageList();
		lstSrv.SmallImageList.ImageSize = new Size(1, 46);
		lstSrv.Columns.Add("ช\u0e37\u0e48อเซ\u0e34ร\u0e4cเวอร\u0e4c", 354);
		lstSrv.Columns.Add("ผ\u0e39\u0e49เล\u0e48น", 96);
		lstSrv.Columns.Add("Ping", 86);
		lstSrv.Columns.Add("ประเภท", 130);
		lstSrv.DoubleClick += delegate
		{
			UseSelectedEntry(true);
		};
		lstSrv.MouseClick += delegate(object s, MouseEventArgs ev)
		{
			if (ev.Button == MouseButtons.Right)
			{
				ListViewItem itemAt = lstSrv.GetItemAt(ev.X, ev.Y);
				if (itemAt != null)
				{
					itemAt.Selected = true;
					itemAt.Focused = true;
				}
			}
		};
		lstSrv.ColumnClick += delegate(object s, ColumnClickEventArgs ev)
		{
			SortByColumn(ev.Column);
		};
		lstSrv.DrawColumnHeader += LstSrv_DrawColumnHeader;
		lstSrv.DrawItem += LstSrv_DrawItem;
		lstSrv.DrawSubItem += LstSrv_DrawSubItem;
		EnableDoubleBuffer(lstSrv);
		pnlSrv.Controls.Add(lstSrv);

		// Empty label
		emptyLbl = new Label();
		emptyLbl.Text = "ย\u0e31งไม\u0e48ม\u0e35เซ\u0e34ร\u0e4cเวอร\u0e4cในรายการ";
		emptyLbl.Font = fField;
		emptyLbl.ForeColor = colSub;
		emptyLbl.BackColor = colCard;
		emptyLbl.AutoSize = false;
		emptyLbl.Size = new Size(300, 30);
		emptyLbl.TextAlign = ContentAlignment.MiddleCenter;
		emptyLbl.Location = new Point((lstSrv.Width - emptyLbl.Width) / 2, (lstSrv.Height - emptyLbl.Height) / 2);
		emptyLbl.Visible = false;
		lstSrv.Controls.Add(emptyLbl);

		// Bottom Action Bar (Y=382, H=42, W=668)
		RoundedBtn roundedBtn = MakeMiniBtn("+  เพิ่ม", 0, 382, 86, 42);
		roundedBtn.BorderColor = colAccent;
		roundedBtn.Click += delegate
		{
			PromptAddServer();
		};
		pnlSrv.Controls.Add(roundedBtn);

		RoundedBtn roundedBtn2 = MakeMiniBtn("−  ลบ", 92, 382, 76, 42);
		roundedBtn2.BorderColor = Color.FromArgb(40, 60, 95);
		roundedBtn2.Click += delegate
		{
			DeleteEntry();
		};
		pnlSrv.Controls.Add(roundedBtn2);

		RoundedBtn roundedBtn3 = MakeMiniBtn("ℹ  ข้อมูล", 174, 382, 90, 42);
		roundedBtn3.BorderColor = colAccent;
		roundedBtn3.Click += delegate
		{
			ShowServerInfo();
		};
		pnlSrv.Controls.Add(roundedBtn3);

		btnWatch = MakeMiniBtn("🔔  เฝ้าดูที่ว่าง", 270, 382, 126, 42);
		btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
		btnWatch.Click += delegate
		{
			ToggleWatch();
		};
		pnlSrv.Controls.Add(btnWatch);

		RoundedBtn roundedBtn4 = MakeMiniBtn("▷   เล\u0e48นเซ\u0e34ร\u0e4cเวอร\u0e4cน\u0e35\u0e49", 404, 382, 264, 42);
		roundedBtn4.SetPalette(colYellow, colYellowHi);
		roundedBtn4.TextColor = Color.FromArgb(11, 14, 20);
		roundedBtn4.BorderColor = Color.FromArgb(255, 245, 120);
		roundedBtn4.Font = new Font("Segoe UI", 11.5f, FontStyle.Bold);
		roundedBtn4.Click += delegate
		{
			UseSelectedEntry(true);
		};
		pnlSrv.Controls.Add(roundedBtn4);

		// Context Menu
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		contextMenuStrip.BackColor = colCard;
		contextMenuStrip.ForeColor = colText;
		contextMenuStrip.ShowImageMargin = false;
		contextMenuStrip.Font = fSub;
		contextMenuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors());

		ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem("▷   เข\u0e49าเกม");
		toolStripMenuItem.Click += delegate
		{
			UseSelectedEntry(true);
		};
		ToolStripMenuItem toolStripMenuItem2 = new ToolStripMenuItem("ℹ   ข\u0e49อม\u0e39ลเซ\u0e34ร\u0e4cเวอร\u0e4c");
		toolStripMenuItem2.Click += delegate
		{
			ShowServerInfo();
		};
		ToolStripMenuItem toolStripMenuItem3 = new ToolStripMenuItem("★   เพ\u0e34\u0e48มเป\u0e47นรายการโปรด");
		toolStripMenuItem3.Click += delegate
		{
			AddFavorite();
		};
		ToolStripMenuItem toolStripMenuItem4 = new ToolStripMenuItem("⧉   ค\u0e31ดลอก IP");
		toolStripMenuItem4.Click += delegate
		{
			CopySelectedIp();
		};
		miWatch = new ToolStripMenuItem("🔔   เฝ\u0e49าด\u0e39ท\u0e35\u0e48ว\u0e48าง");
		miWatch.Click += delegate
		{
			ToggleWatch();
		};
		ToolStripMenuItem toolStripMenuItem5 = new ToolStripMenuItem("📊   สถ\u0e34ต\u0e34ผ\u0e39\u0e49เล\u0e48น");
		toolStripMenuItem5.Click += delegate
		{
			ShowStats();
		};
		ToolStripMenuItem toolStripMenuItem6 = new ToolStripMenuItem("🗑   ล\u0e49างประว\u0e31ต\u0e34ท\u0e31\u0e49งหมด");
		toolStripMenuItem6.Click += delegate
		{
			ClearHistory();
		};
		ToolStripMenuItem toolStripMenuItem7 = new ToolStripMenuItem("−   ลบ");
		toolStripMenuItem7.Click += delegate
		{
			DeleteEntry();
		};

		contextMenuStrip.Items.AddRange(new ToolStripItem[9]
		{
			toolStripMenuItem,
			toolStripMenuItem2,
			toolStripMenuItem3,
			toolStripMenuItem4,
			miWatch,
			toolStripMenuItem5,
			new ToolStripSeparator(),
			toolStripMenuItem7,
			toolStripMenuItem6
		});
		lstSrv.ContextMenuStrip = contextMenuStrip;
	}


	private void Toast(string msg)
	{
		new MiniToast(msg, colAccent).Show(this);
	}

	private void ToastOk(string msg)
	{
		new MiniToast(msg, colOk).Show(this);
	}

	private void CopySelectedIp()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		try
		{
			Clipboard.SetText(serverEntry.Ip + ":" + serverEntry.Port);
			SetStatus("ค\u0e31ดลอก " + serverEntry.Ip + ":" + serverEntry.Port + " แล\u0e49ว");
			ToastOk("ค\u0e31ดลอก " + serverEntry.Ip + ":" + serverEntry.Port + " แล\u0e49ว");
		}
		catch
		{
		}
	}

	private void LstSrv_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
	{
		e.DrawDefault = false;
		Graphics graphics = e.Graphics;
		graphics.FillRectangle(brHeader, e.Bounds);
		TextRenderer.DrawText(graphics, e.Header.Text, fSub, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		if (e.ColumnIndex == sortCol)
		{
			TextRenderer.DrawText(graphics, sortDesc ? "▼" : "▲", fSmall, new Point(e.Bounds.Right - 16, e.Bounds.Y + (e.Bounds.Height - 12) / 2), colAccentHi);
		}
		graphics.DrawLine(penHeaderLine, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
	}

	private void LstSrv_DrawItem(object sender, DrawListViewItemEventArgs e)
	{
		e.DrawDefault = false;
		Graphics graphics = e.Graphics;
		graphics.FillRectangle(brCard, e.Bounds);
		if ((e.State & ListViewItemStates.Selected) != 0)
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle r = new Rectangle(e.Bounds.X + 3, e.Bounds.Y + 2, e.Bounds.Width - 6, e.Bounds.Height - 4);
			using (GraphicsPath path = RoundRect(r, 8))
			{
				graphics.FillPath(brSel, path);
			}
			graphics.FillRectangle(brAccentBar, r.X, r.Y + 6, 3, r.Height - 12);
		}
	}

	private void LstSrv_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
	{
		e.DrawDefault = false;
		ServerEntry serverEntry = e.Item.Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		Graphics graphics = e.Graphics;
		bool flag = (e.ItemState & ListViewItemStates.Selected) != 0;
		Color foreColor = (flag ? colText : Color.FromArgb(215, 215, 230));
		int num = e.Bounds.Y + e.Bounds.Height / 2;
		if (e.ColumnIndex == 0)
		{
			string text = ((serverEntry.Name.Length > 0) ? serverEntry.Name : (serverEntry.Ip + ":" + serverEntry.Port));
			TextRenderer.DrawText(graphics, text, fField, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 5, e.Bounds.Width - 50, 20), foreColor, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			TextRenderer.DrawText(graphics, serverEntry.Ip + ":" + serverEntry.Port, fSmall, new Rectangle(e.Bounds.X + 14, e.Bounds.Y + 24, e.Bounds.Width - 18, 18), colSub, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			if (serverEntry.IsOpenMp)
			{
				TextRenderer.DrawText(graphics, "open.mp", fSmall, new Rectangle(e.Bounds.Right - 62, e.Bounds.Y + 4, 52, 20), Color.FromArgb(0, 206, 201), TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else if (serverEntry.Locked)
			{
				TextRenderer.DrawText(graphics, "\ud83d\udd12", fSmall, new Rectangle(e.Bounds.Right - 34, e.Bounds.Y + 4, 26, 20), colSub, TextFormatFlags.Right | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		}
		else if (e.ColumnIndex == 1)
		{
			string text2 = ((serverEntry.Ping == -2) ? "-" : ((serverEntry.Players < 0) ? "-" : (serverEntry.Players + "/" + serverEntry.Max)));
			Color foreColor2 = ((serverEntry.Players >= 0 && serverEntry.Max > 0 && serverEntry.Players >= serverEntry.Max) ? brPingY.Color : (flag ? colText : Color.FromArgb(200, 200, 215)));
			TextRenderer.DrawText(graphics, text2, fField, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), foreColor2, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
		else if (e.ColumnIndex == 2)
		{
			if (serverEntry.Ping == -2)
			{
				TextRenderer.DrawText(graphics, "...", fField, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else if (serverEntry.Ping < 0)
			{
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.FillEllipse(brDotOff, e.Bounds.X + 8, num - 4, 9, 9);
				TextRenderer.DrawText(graphics, "ออฟ", fField, new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height), colSub, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
			else
			{
				SolidBrush solidBrush = ((serverEntry.Ping < 80) ? brPingG : ((serverEntry.Ping < 150) ? brPingY : brPingR));
				Color color = solidBrush.Color;
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.FillEllipse(solidBrush, e.Bounds.X + 8, num - 4, 9, 9);
				TextRenderer.DrawText(graphics, serverEntry.Ping + " ms", fField, new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height), color, TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
			}
		}
		else if (e.ColumnIndex == 3)
		{
			string t;
			Color foreColor3;
			SolidBrush brush;
			if (serverEntry.Fav)
			{
				t = "★ โปรด";
				foreColor3 = colAccentHi;
				brush = brPillFav;
			}
			else if (viewMode == "public")
			{
				t = "สาธารณะ";
				foreColor3 = Color.FromArgb(0, 206, 201);
				brush = brPillPub;
			}
			else
			{
				t = "ประว\u0e31ต\u0e34";
				foreColor3 = colSub;
				brush = brPillHist;
			}
			int num2 = PillWidth(t);
			Rectangle rectangle = new Rectangle(e.Bounds.X + 8, num - 11, num2, 22);
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.FillPath(brush, RoundRect(rectangle, 11));
			TextRenderer.DrawText(graphics, t, fSub, rectangle, foreColor3, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter);
		}
	}

	private bool MatchFilter(ServerEntry e, string f)
	{
		if (f.Length > 0 && !(e.Name + " " + e.Ip + ":" + e.Port + " " + e.Mode).ToLowerInvariant().Contains(f))
		{
			return false;
		}
		if (viewMode == "public")
		{
			if (fHideFull && e.Players >= 0 && e.Max > 0 && e.Players >= e.Max)
			{
				return false;
			}
			if (fHideEmpty && e.Players == 0)
			{
				return false;
			}
			if (fHideLocked && e.Locked)
			{
				return false;
			}
		}
		return true;
	}

	private void SortByColumn(int col)
	{
		if (sortCol == col)
		{
			sortDesc = !sortDesc;
		}
		else
		{
			sortCol = col;
			sortDesc = false;
		}
		Comparison<ServerEntry> comparison = delegate(ServerEntry a, ServerEntry b)
		{
			int num;
			if (col == 0)
			{
				string na = (a.Name.Length > 0) ? a.Name : (a.Ip + ":" + a.Port);
				string nb = (b.Name.Length > 0) ? b.Name : (b.Ip + ":" + b.Port);
				num = string.Compare(na, nb, true);
			}
			else if (col == 1)
			{
				int ap = (a.Players < 0) ? -1 : a.Players;
				int bp = (b.Players < 0) ? -1 : b.Players;
				num = bp.CompareTo(ap);
			}
			else if (col == 2)
			{
				int aping = (a.Ping < 0) ? 999999 : a.Ping;
				int bping = (b.Ping < 0) ? 999999 : b.Ping;
				num = aping.CompareTo(bping);
			}
			else if (col == 3)
			{
				num = b.Fav.CompareTo(a.Fav);
				if (num == 0)
				{
					num = string.Compare(a.Ip + ":" + a.Port, b.Ip + ":" + b.Port, true);
				}
			}
			else
			{
				num = string.Compare(a.Ip + ":" + a.Port, b.Ip + ":" + b.Port, true);
			}
			return (!sortDesc) ? num : (-num);
		};
		if (viewMode == "public")
		{
			pub.Sort(comparison);
		}
		else
		{
			favs.Sort(comparison);
			hist.Sort(comparison);
		}
		RebuildList();
	}

	private void StyleChip(RoundedBtn b, bool on)
	{
		Color color = Anim.Lerp(colField, colAccent, 0.45f);
		b.SetPalette(on ? color : colField, colSel);
		b.TextColor = (on ? colText : colSub);
	}

	private void ShowView(string mode)
	{
		viewMode = mode;
		Color color = Anim.Lerp(colField, colAccent, 0.45f);
		btnMine.SetPalette((mode == "mine") ? color : colField, colField);
		btnMine.TextColor = ((mode == "mine") ? colText : colSub);
		btnPub.SetPalette((mode == "public") ? color : colField, colField);
		btnPub.TextColor = ((mode == "public") ? colText : colSub);
		bool flag = viewMode == "public";
		if (btnF1 != null)
		{
			btnF1.Visible = flag;
			btnF2.Visible = flag;
			btnF3.Visible = flag;
		}
		lstSrv.Location = new Point(0, flag ? 80 : 48);
		lstSrv.Size = new Size(668, flag ? 296 : 328);
		if (emptyLbl != null)
		{
			emptyLbl.Location = lstSrv.Location;
			emptyLbl.Size = lstSrv.Size;
		}
		RebuildList();
		if (flag)
		{
			if (pub.Count == 0)
			{
				FetchPublicServers();
			}
			StartPubAuto();
		}
		else
		{
			StopPubAuto();
		}
	}

	private void StartPubAuto()
	{
		if (pubAuto != null)
		{
			return;
		}
		pubAuto = new System.Windows.Forms.Timer();
		pubAuto.Interval = 60000;
		pubAuto.Tick += delegate
		{
			if (base.IsDisposed)
			{
				StopPubAuto();
			}
			else if (viewMode == "public" && pnlSrv.Visible && !fetchingPub)
			{
				FetchPublicServers();
			}
		};
		pubAuto.Start();
	}

	private void StopPubAuto()
	{
		if (pubAuto != null)
		{
			pubAuto.Stop();
			pubAuto.Dispose();
			pubAuto = null;
		}
	}

	private void RefreshCurrentView()
	{
		if (viewMode == "public")
		{
			FetchPublicServers();
		}
		else
		{
			RefreshAllServers();
		}
	}

	private void FetchPublicServers()
	{
		if (fetchingPub)
		{
			return;
		}
		fetchingPub = true;
		SetStatus("กำล\u0e31งด\u0e36งรายช\u0e37\u0e48อเซ\u0e34ร\u0e4cเวอร\u0e4cสาธารณะ...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			List<ServerEntry> list = new List<ServerEntry>();
			try
			{
				string input;
				using (WebClient webClient = new WebClient())
				{
					webClient.Encoding = Encoding.UTF8;
					input = webClient.DownloadString("https://api.open.mp/servers");
				}
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				object[] array = javaScriptSerializer.Deserialize<object[]>(input);
				object[] array2 = array;
				foreach (object obj in array2)
				{
					Dictionary<string, object> dictionary = obj as Dictionary<string, object>;
					if (dictionary != null)
					{
						object value;
						string text = (dictionary.TryGetValue("ip", out value) ? (value as string) : null);
						if (text != null)
						{
							object value2;
							string text2 = (dictionary.TryGetValue("vn", out value2) ? ((value2 as string) ?? "") : "");
							bool isOpenMp = text2.Length > 0 && !text2.StartsWith("0.3");
							int num = text.LastIndexOf(':');
							if (num > 0)
							{
								ServerEntry serverEntry = new ServerEntry
								{
									Ip = text.Substring(0, num),
									Port = text.Substring(num + 1),
									Fav = false,
									Ping = -2,
									IsOpenMp = isOpenMp
								};
								if (dictionary.TryGetValue("hn", out value))
								{
									serverEntry.Name = (value as string) ?? "";
								}
								if (dictionary.TryGetValue("pc", out value) && value != null)
								{
									serverEntry.Players = Convert.ToInt32(value);
								}
								if (dictionary.TryGetValue("pm", out value) && value != null)
								{
									serverEntry.Max = Convert.ToInt32(value);
								}
								if (dictionary.TryGetValue("gm", out value))
								{
									serverEntry.Mode = (value as string) ?? "";
								}
								if (dictionary.TryGetValue("pa", out value) && value != null)
								{
									try
									{
										serverEntry.Locked = Convert.ToBoolean(value);
									}
									catch
									{
									}
								}
								if (dictionary.TryGetValue("la", out value))
								{
									serverEntry.Language = (value as string) ?? "";
								}
								list.Add(serverEntry);
							}
						}
					}
				}
				list.Sort((ServerEntry a, ServerEntry b) => b.Players.CompareTo(a.Players));
				if (list.Count > 100)
				{
					list.RemoveRange(100, list.Count - 100);
				}
			}
			catch
			{
			}
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						fetchingPub = false;
						if (list.Count > 0)
						{
							pub = list;
						}
						if (viewMode == "public")
						{
							RebuildList();
							if (pub.Count == 0)
							{
								SetStatus("ด\u0e36งรายการสาธารณะไม\u0e48สำเร\u0e47จ — ตรวจอ\u0e34นเทอร\u0e4cเน\u0e47ตแล\u0e49วกดร\u0e35เฟรชอ\u0e35กคร\u0e31\u0e49ง");
							}
							else
							{
								SetStatus("เซ\u0e34ร\u0e4cเวอร\u0e4cสาธารณะ " + pub.Count + " เซ\u0e34ร\u0e4cเวอร\u0e4c — กำล\u0e31งตรวจสถานะ...");
							}
						}
						List<ServerEntry> list2 = new List<ServerEntry>(pub);
						foreach (ServerEntry item in list2)
						{
							ServerEntry ee = item;
							int qport;
							if (int.TryParse(ee.Port, out qport))
							{
								ThreadPool.QueueUserWorkItem(delegate
								{
									QueryResult qr = QueryServer(ee.Ip, qport);
									try
									{
										if (!base.IsDisposed)
										{
											BeginInvoke((MethodInvoker)delegate
											{
												ApplyEntryQuery(ee, qr);
											});
										}
									}
									catch
									{
									}
								});
							}
						}
					});
				}
			}
			catch
			{
				fetchingPub = false;
			}
		});
	}

	private void BuildSetPage()
	{
		// 1. Left Card: Graphics, FPS & Theme (X=0, Y=0, W=326, H=258)
		SmoothPanel pnlGfx = new SmoothPanel(colCard, 10);
		pnlGfx.Location = new Point(0, 0);
		pnlGfx.Size = new Size(326, 258);
		pnlGfx.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlGfx);

		AddLbl(pnlGfx, "🎮  ภาพและกราฟ\u0e34ก (GRAPHICS & FPS)", fSub, 14, 10, colAccent);

		AddLbl(pnlGfx, "โทนภาพ SkyGfx:", fSmall, 14, 34, colSub);
		btnSgPs2 = MakeMiniBtn("PS2 Classic", 14, 52, 144, 28);
		btnSgPs2.Click += delegate { ApplySkyGfxPreset("ps2"); };
		pnlGfx.Controls.Add(btnSgPs2);

		btnSgPc = MakeMiniBtn("PC Original", 166, 52, 146, 28);
		btnSgPc.Click += delegate { ApplySkyGfxPreset("pc"); };
		pnlGfx.Controls.Add(btnSgPc);

		btnSgMob = MakeMiniBtn("Mobile", 14, 84, 144, 28);
		btnSgMob.Click += delegate { ApplySkyGfxPreset("mobile"); };
		pnlGfx.Controls.Add(btnSgMob);

		btnSgOff = MakeMiniBtn("ป\u0e34ดใช\u0e49งาน", 166, 84, 146, 28);
		btnSgOff.Click += delegate { ApplySkyGfxPreset("off"); };
		pnlGfx.Controls.Add(btnSgOff);

		AddLbl(pnlGfx, "จำก\u0e31ดเฟรมเรต (FPS Limit):", fSmall, 14, 118, colSub);
		btnFps60 = MakeMiniBtn("60", 14, 138, 54, 28);
		btnFps60.Click += delegate { ApplyFpsLimit(60); };
		pnlGfx.Controls.Add(btnFps60);

		btnFps90 = MakeMiniBtn("90", 74, 138, 54, 28);
		btnFps90.Click += delegate { ApplyFpsLimit(90); };
		pnlGfx.Controls.Add(btnFps90);

		btnFps120 = MakeMiniBtn("120", 134, 138, 54, 28);
		btnFps120.Click += delegate { ApplyFpsLimit(120); };
		pnlGfx.Controls.Add(btnFps120);

		btnFps144 = MakeMiniBtn("144", 194, 138, 54, 28);
		btnFps144.Click += delegate { ApplyFpsLimit(144); };
		pnlGfx.Controls.Add(btnFps144);

		btnFpsMax = MakeMiniBtn("Max", 254, 138, 58, 28);
		btnFpsMax.Click += delegate { ApplyFpsLimit(0); };
		pnlGfx.Controls.Add(btnFpsMax);

		AddLbl(pnlGfx, "ส\u0e35ธ\u0e35มหล\u0e31ก (THEME ACCENT):", fSmall, 14, 174, colSub);
		Color[] array = new Color[6]
		{
			Color.FromArgb(51, 204, 255),
			Color.FromArgb(255, 235, 0),
			Color.FromArgb(102, 255, 0),
			Color.FromArgb(255, 0, 128),
			Color.FromArgb(138, 43, 226),
			Color.FromArgb(0, 230, 255)
		};
		for (int i = 0; i < array.Length; i++)
		{
			Color c = array[i];
			RoundedBtn roundedBtn = new RoundedBtn("", c, ControlPaint.Light(c, 0.2f), c, 6, fSmall);
			roundedBtn.Location = new Point(14 + i * 50, 196);
			roundedBtn.Size = new Size(42, 26);
			roundedBtn.Tag = c;
			roundedBtn.Click += delegate
			{
				ApplyAccent(c);
				SaveConfig();
				ToastOk("เปล\u0e35\u0e48ยนส\u0e35ธ\u0e35มแล\u0e49ว");
			};
			pnlGfx.Controls.Add(roundedBtn);
		}

		// 2. Right Card: Game Path & Mods (X=342, Y=0, W=326, H=258)
		SmoothPanel pnlMods = new SmoothPanel(colCard, 10);
		pnlMods.Location = new Point(342, 0);
		pnlMods.Size = new Size(326, 258);
		pnlMods.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlMods);

		AddLbl(pnlMods, "📁  ไฟล\u0e4cเกมและม\u0e47อด (GAME & MODS)", fSub, 14, 10, colAccent);

		AddLbl(pnlMods, "โฟลเดอร\u0e4cต\u0e34ดต\u0e31\u0e49ง GTA San Andreas:", fSmall, 14, 34, colSub);
		txtPath = AddField(pnlMods, "", 14, 52, 214);
		RoundedBtn btnBrowse = MakeMiniBtn("เล\u0e37อก...", 234, 52, 78, 42);
		btnBrowse.BorderColor = colAccent;
		btnBrowse.Click += delegate
		{
			BrowseGameDir();
		};
		pnlMods.Controls.Add(btnBrowse);

		AddLbl(pnlMods, "ทางล\u0e31ดเป\u0e34ดโฟลเดอร\u0e4c:", fSmall, 14, 106, colSub);
		RoundedBtn btnOpenFolder = MakeMiniBtn("📂  โฟลเดอร\u0e4cเกม", 14, 126, 144, 32);
		btnOpenFolder.BorderColor = Color.FromArgb(40, 60, 95);
		btnOpenFolder.Click += delegate
		{
			try
			{
				Process.Start("explorer.exe", GetGameDir());
			}
			catch
			{
			}
		};
		pnlMods.Controls.Add(btnOpenFolder);

		RoundedBtn btnModFolder = MakeMiniBtn("📦  ModLoader", 166, 126, 146, 32);
		btnModFolder.BorderColor = colAccent;
		btnModFolder.Click += delegate
		{
			try
			{
				string ml = Path.Combine(GetGameDir(), "modloader");
				if (!Directory.Exists(ml))
				{
					Directory.CreateDirectory(ml);
				}
				Process.Start("explorer.exe", ml);
			}
			catch
			{
			}
		};
		pnlMods.Controls.Add(btnModFolder);

		AddLbl(pnlMods, "การสำรองและกู้คืนไฟล์ม็อด:", fSmall, 14, 168, colSub);
		RoundedBtn btnBackup = MakeMiniBtn("💾  สำรองม็อด", 14, 188, 144, 30);
		btnBackup.BorderColor = Color.FromArgb(40, 60, 95);
		btnBackup.Click += delegate
		{
			CreateModBackup();
		};
		pnlMods.Controls.Add(btnBackup);

		RoundedBtn btnRestore = MakeMiniBtn("↺  คืนค่าม็อดเดิม", 166, 188, 146, 30);
		btnRestore.BorderColor = Color.FromArgb(40, 60, 95);
		btnRestore.Click += delegate
		{
			RestoreModBackup();
		};
		pnlMods.Controls.Add(btnRestore);

		RoundedBtn btnFixCrash = MakeMiniBtn("🛠️  แก้เกมเด้ง (รีเซ็ต gta_sa.set)", 14, 222, 298, 28);
		btnFixCrash.BorderColor = Color.FromArgb(243, 156, 18);
		btnFixCrash.Click += delegate
		{
			ResetGameSettings();
		};
		pnlMods.Controls.Add(btnFixCrash);

		// 3. Bottom Card: System Integrity & Health (X=0, Y=266, W=668, H=168)
		SmoothPanel pnlHealth = new SmoothPanel(colCard, 10);
		pnlHealth.Location = new Point(0, 266);
		pnlHealth.Size = new Size(668, 168);
		pnlHealth.BorderColor = Color.FromArgb(40, 60, 95);
		pnlSet.Controls.Add(pnlHealth);

		AddLbl(pnlHealth, "🛡️  ตรวจสอบระบบ & ตัวเลือกการทำงาน (SYSTEM HEALTH & OPTIONS)", fSub, 14, 8, colAccent);

		RoundedBtn roundedBtn3 = MakeMiniBtn("↻  ตรวจอีกครั้ง", 530, 6, 124, 26);
		roundedBtn3.BorderColor = colAccent;
		roundedBtn3.Click += delegate
		{
			RunHealthCheck();
		};
		pnlHealth.Controls.Add(roundedBtn3);

		lblHealth = new Label[5];
		lblHealth[0] = AddLbl(pnlHealth, "กำลังตรวจสอบ gta_sa.exe...", fSmall, 14, 34, colSub);
		lblHealth[1] = AddLbl(pnlHealth, "กำลังตรวจสอบ samp.dll...", fSmall, 14, 52, colSub);
		lblHealth[2] = AddLbl(pnlHealth, "กำลังตรวจสอบ ModLoader & CLEO...", fSmall, 14, 70, colSub);
		lblHealth[3] = AddLbl(pnlHealth, "กำลังตรวจสอบ FramerateVigilante...", fSmall, 340, 34, colSub);
		lblHealth[4] = AddLbl(pnlHealth, "กำลังตรวจสอบ SkyGfx...", fSmall, 340, 52, colSub);

		AddLbl(pnlHealth, "ม็อดหลักที่ทำงาน: FramerateVigilante • ModLoader v0.3.10 • CLEO 4.4 • SkyGfx • SilentPatch • 2DFX", fSmall, 14, 88, colSub);

		swSoundFx = new ToggleSwitch();
		swSoundFx.Location = new Point(14, 108);
		swSoundFx.CheckedChanged += delegate
		{
			soundFxEnabled = swSoundFx.Checked;
			CyberAudio.Enabled = soundFxEnabled;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlHealth.Controls.Add(swSoundFx);
		AddLbl(pnlHealth, "🔊  เสียงไซเบอร์ (Audio FX)", fSmall, 66, 112, colSub);

		RoundedBtn btnTestSound = MakeMiniBtn("▶ ฟังเสียง", 204, 108, 64, 22);
		btnTestSound.BorderColor = colAccent;
		btnTestSound.Click += delegate
		{
			CyberAudio.PlayAlert();
		};
		pnlHealth.Controls.Add(btnTestSound);

		swDiscordRpc = new ToggleSwitch();
		swDiscordRpc.Location = new Point(340, 108);
		swDiscordRpc.CheckedChanged += delegate
		{
			discordRpcEnabled = swDiscordRpc.Checked;
			DiscordRpc.Enabled = discordRpcEnabled;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlHealth.Controls.Add(swDiscordRpc);
		AddLbl(pnlHealth, "🎮  Discord Rich Presence", fSmall, 392, 112, colSub);

		swAutoSlot = new ToggleSwitch();
		swAutoSlot.Location = new Point(14, 138);
		swAutoSlot.CheckedChanged += delegate
		{
			autoJoinSlot = swAutoSlot.Checked;
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlHealth.Controls.Add(swAutoSlot);
		AddLbl(pnlHealth, "⚡  ออโต้จอยเมื่อห้องว่าง", fSmall, 66, 142, colSub);

		swNoPauseAltTab = new ToggleSwitch();
		swNoPauseAltTab.Location = new Point(230, 138);
		swNoPauseAltTab.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				ApplyNoPauseAltTab(swNoPauseAltTab.Checked);
			}
		};
		pnlHealth.Controls.Add(swNoPauseAltTab);
		AddLbl(pnlHealth, "🖥️  ไม่ค้างตอนพับจอ (Alt+Tab)", fSmall, 282, 142, colSub);

		swClose = new ToggleSwitch();
		swClose.Location = new Point(480, 138);
		swClose.CheckedChanged += delegate
		{
			if (cfgReady)
			{
				SaveConfig();
			}
		};
		pnlHealth.Controls.Add(swClose);
		AddLbl(pnlHealth, "ปิดเมื่อเข้าเกม", fSmall, 532, 142, colSub);

		UpdateSkyGfxButtons();
		UpdateFpsButtons();
	}


	private void ApplySkyGfxPreset(string mode)
	{
		activeSkyGfx = mode;
		string g = GetGameDir();
		string target = Path.Combine(g, "skygfx.ini");
		try
		{
			if (mode == "ps2")
			{
				string src = Path.Combine(g, "skygfx1.ini");
				if (File.Exists(src))
				{
					File.Copy(src, target, true);
				}
				ToastOk("สล\u0e31บโทนภาพเป\u0e47น PS2 Classic เร\u0e35ยบร\u0e49อย");
			}
			else if (mode == "pc")
			{
				string src2 = Path.Combine(g, "skygfx2.ini");
				if (File.Exists(src2))
				{
					File.Copy(src2, target, true);
				}
				ToastOk("สล\u0e31บโทนภาพเป\u0e47น PC Enhanced เร\u0e35ยบร\u0e49อย");
			}
			else if (mode == "mobile")
			{
				string src3 = Path.Combine(g, "skygfx3.ini");
				if (File.Exists(src3))
				{
					File.Copy(src3, target, true);
				}
				ToastOk("สล\u0e31บโทนภาพเป\u0e47น Mobile เร\u0e35ยบร\u0e49อย");
			}
			else if (mode == "off")
			{
				if (File.Exists(target))
				{
					File.Delete(target);
				}
				ToastOk("ป\u0e34ดใช\u0e49งาน SkyGfx แล\u0e49ว");
			}
			UpdateSkyGfxButtons();
			SaveConfig();
		}
		catch (Exception ex)
		{
			ShowErr("ไม\u0e48สามารถเปล\u0e35\u0e48ยน SkyGfx preset: " + ex.Message);
		}
	}

	private void UpdateSkyGfxButtons()
	{
		if (btnSgPs2 == null)
		{
			return;
		}
		Color cAct = colAccent;
		Color cActHi = colAccentHi;
		Color cNorm = colField;
		Color cNormHi = colFieldHover;

		btnSgPs2.SetPalette((activeSkyGfx == "ps2") ? cAct : cNorm, (activeSkyGfx == "ps2") ? cActHi : cNormHi);
		btnSgPs2.TextColor = (activeSkyGfx == "ps2") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgPs2.BorderColor = (activeSkyGfx == "ps2") ? Color.FromArgb(180, 240, 255) : Color.FromArgb(35, 52, 80);

		btnSgPc.SetPalette((activeSkyGfx == "pc") ? cAct : cNorm, (activeSkyGfx == "pc") ? cActHi : cNormHi);
		btnSgPc.TextColor = (activeSkyGfx == "pc") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgPc.BorderColor = (activeSkyGfx == "pc") ? Color.FromArgb(180, 240, 255) : Color.FromArgb(35, 52, 80);

		btnSgMob.SetPalette((activeSkyGfx == "mobile") ? cAct : cNorm, (activeSkyGfx == "mobile") ? cActHi : cNormHi);
		btnSgMob.TextColor = (activeSkyGfx == "mobile") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgMob.BorderColor = (activeSkyGfx == "mobile") ? Color.FromArgb(180, 240, 255) : Color.FromArgb(35, 52, 80);

		btnSgOff.SetPalette((activeSkyGfx == "off") ? cAct : cNorm, (activeSkyGfx == "off") ? cActHi : cNormHi);
		btnSgOff.TextColor = (activeSkyGfx == "off") ? Color.FromArgb(11, 14, 20) : colSub;
		btnSgOff.BorderColor = (activeSkyGfx == "off") ? Color.FromArgb(180, 240, 255) : Color.FromArgb(35, 52, 80);
	}

	private void ApplyFpsLimit(int fps)
	{
		activeFpsLimit = fps;
		string gameDir = GetGameDir();
		string text = Path.Combine(gameDir, "scripts", "FPSUnlock.ini");
		string text2 = Path.Combine(gameDir, "scripts", "FramerateVigilante.ini");
		try
		{
			if (File.Exists(text))
			{
				string contents = "[Settings]\r\nUnlockFPS = true\r\nCustomFPS = " + fps + "\r\nPhysicsScale = 0.6\r\n";
				File.WriteAllText(text, contents, Encoding.UTF8);
			}
			if (File.Exists(text2))
			{
				string contents2 = "[Settings]\r\nFPSlimit=" + fps + "\r\nRefreshRate=0\r\nAutoLimitFPS=0\r\nVehicleHandlingPatches=1\r\n";
				File.WriteAllText(text2, contents2, Encoding.UTF8);
			}
			UpdateFpsButtons();
			SaveConfig();
			if (fps == 0)
			{
				ToastOk("ต\u0e31\u0e49งค\u0e48า FPS เป\u0e47น ปลดล\u0e47อกเต\u0e47มท\u0e35\u0e48 (Unlimited)");
			}
			else
			{
				ToastOk("ต\u0e31\u0e49งค\u0e48า FPS Limit เป\u0e47น " + fps + " FPS เร\u0e35ยบร\u0e49อย");
			}
		}
		catch (Exception ex)
		{
			ShowErr("ไม\u0e48สามารถต\u0e31\u0e49งค\u0e48า FPS: " + ex.Message);
		}
	}

	private void UpdateFpsButtons()
	{
		if (btnFps60 == null)
		{
			return;
		}
		Color cAct = colYellow;
		Color cActHi = colYellowHi;
		Color cNorm = colField;
		Color cNormHi = colFieldHover;

		RoundedBtn[] btns = new RoundedBtn[5] { btnFps60, btnFps90, btnFps120, btnFps144, btnFpsMax };
		int[] vals = new int[5] { 60, 90, 120, 144, 0 };

		for (int i = 0; i < btns.Length; i++)
		{
			bool active = (activeFpsLimit == vals[i]);
			btns[i].SetPalette(active ? cAct : cNorm, active ? cActHi : cNormHi);
			btns[i].TextColor = active ? Color.FromArgb(11, 14, 20) : colSub;
			btns[i].BorderColor = active ? Color.FromArgb(255, 245, 120) : Color.FromArgb(35, 52, 80);
		}
	}


	private void RestoreModBackup()
	{
		string g = GetGameDir();
		string b = Path.Combine(g, "_mod_backup");
		if (!Directory.Exists(b))
		{
			ShowErr("ไม่พบโฟลเดอร์ _mod_backup ในโฟลเดอร์เกม");
			return;
		}
		if (MessageBox.Show(this, "คุณต้องการคืนค่าไฟล์จาก _mod_backup กลับไปยังโฟลเดอร์เกมหรือไม่?\r\n(ไฟล์คอนฟิกเดิมจะถูกนำกลับมาใช้งาน)", "ยืนยันการคืนค่าม็อด", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			foreach (string file in Directory.GetFiles(b, "*.*", SearchOption.AllDirectories))
			{
				string rel = file.Substring(b.Length).TrimStart('\\', '/');
				if (rel.EndsWith(".bak"))
				{
					rel = rel.Substring(0, rel.Length - 4);
				}
				string dest = Path.Combine(g, rel);
				string dir = Path.GetDirectoryName(dest);
				if (!Directory.Exists(dir))
				{
					Directory.CreateDirectory(dir);
				}
				File.Copy(file, dest, true);
			}
			ToastOk("คืนค่าม็อดเรียบร้อยแล้ว");
			RunHealthCheck();
		}
		catch (Exception ex)
		{
			ShowErr("เกิดข้อผิดพลาดในการคืนค่าม็อด:\r\n" + ex.Message);
		}
	}

	private void CreateModBackup()
	{
		string g = GetGameDir();
		string b = Path.Combine(g, "_mod_backup");
		try
		{
			if (!Directory.Exists(b))
			{
				Directory.CreateDirectory(b);
			}
			string[] targetFiles = new string[] { "stream.ini", "III.VC.SA.LimitAdjuster.ini", "SALodLights.ini", "skygfx1.ini" };
			foreach (string tf in targetFiles)
			{
				string full = Path.Combine(g, tf);
				if (File.Exists(full))
				{
					File.Copy(full, Path.Combine(b, tf + ".bak"), true);
				}
			}
			ToastOk("สำรองข้อมูลไฟล์ม็อดเรียบร้อยแล้ว");
		}
		catch (Exception ex)
		{
			ShowErr("การสำรองข้อมูลล้มเหลว:\r\n" + ex.Message);
		}
	}

	private void SetHealth(int i, string s, Color c)
	{
		if (lblHealth != null && i >= 0 && i < lblHealth.Length && lblHealth[i] != null && !lblHealth[i].IsDisposed)
		{
			lblHealth[i].Text = s;
			lblHealth[i].ForeColor = c;
		}
	}

	private void RunHealthCheck()
	{
		if (lblHealth == null)
		{
			return;
		}
		for (int i = 0; i < lblHealth.Length; i++)
		{
			SetHealth(i, "กำล\u0e31งตรวจสอบ...", colSub);
		}
		Color cOk = colOk;
		Color cWarn = Color.FromArgb(243, 156, 18);
		Color cBad = Color.FromArgb(255, 99, 99);
		string g = GetGameDir();
		ThreadPool.QueueUserWorkItem(delegate
		{
			// 0. gta_sa.exe
			string gtaExe = Path.Combine(g, "gta_sa.exe");
			string r0;
			Color c0;
			if (!File.Exists(gtaExe))
			{
				r0 = "✖ ไม\u0e48พบ gta_sa.exe ในโฟลเดอร\u0e4cเกม";
				c0 = cBad;
			}
			else
			{
				long length = new FileInfo(gtaExe).Length;
				if (length == 14383616L || length == 14083072L)
				{
					r0 = "✓ gta_sa.exe 1.0 US (HOODLUM) สมบ\u0e39รณ\u0e4c รองร\u0e31บม\u0e47อดท\u0e38กต\u0e31ว";
					c0 = cOk;
				}
				else
				{
					r0 = "⚠ gta_sa.exe ขนาด " + length + " bytes — ไม\u0e48ใช\u0e48 1.0 US มาตรฐาน SA-MP";
					c0 = cWarn;
				}
			}

			// 1. samp.dll
			string sampFile = Path.Combine(g, "samp.dll");
			string r1;
			Color c1;
			if (File.Exists(sampFile))
			{
				long sLen = new FileInfo(sampFile).Length;
				string sVer = ((sLen == 2199552L) ? "0.3.7 R1" : ((sLen == 2244608L) ? "0.3.7 R2" : "0.3.7"));
				r1 = "✓ พบ samp.dll (SA-MP " + sVer + " พร\u0e49อมใช\u0e49งาน)";
				c1 = cOk;
			}
			else
			{
				r1 = "✖ ไม\u0e48พบ samp.dll — ต\u0e49องต\u0e34ดต\u0e31\u0e49ง SA-MP ในโฟลเดอร\u0e4cเกม";
				c1 = cBad;
			}

			// 2. ASI Loader & ModLoader & CLEO
			bool hasAsi = File.Exists(Path.Combine(g, "dinput8.dll"));
			bool hasMl = File.Exists(Path.Combine(g, "modloader.asi"));
			bool hasCleo = File.Exists(Path.Combine(g, "CLEO.asi"));
			string r2;
			Color c2;
			if (hasAsi && hasMl && hasCleo)
			{
				r2 = "✓ ASI Loader + ModLoader (v0.3.10) + CLEO 4.4 พร\u0e49อมลงม\u0e47อด";
				c2 = cOk;
			}
			else if (hasAsi && hasMl)
			{
				r2 = "✓ ASI Loader + ModLoader ทำงาน";
				c2 = cOk;
			}
			else if (hasAsi)
			{
				r2 = "✓ ASI Loader (dinput8.dll) ทำงาน";
				c2 = cOk;
			}
			else
			{
				r2 = "⚠ ไม\u0e48ม\u0e35 ASI Loader (dinput8.dll) — ม\u0e47อดจะไม\u0e48ทำงาน";
				c2 = cWarn;
			}

			// 3. FramerateVigilante & FPS
			bool hasFv = File.Exists(Path.Combine(g, "scripts", "FramerateVigilante.asi"));
			string r3;
			Color c3;
			if (hasFv)
			{
				r3 = "✓ FramerateVigilante ทำงาน (ฟ\u0e34ส\u0e34กส\u0e4c 60-144 FPS ปลอดภ\u0e31ย ไม\u0e48พ\u0e31งบ\u0e31ก)";
				c3 = cOk;
			}
			else
			{
				r3 = "⚠ ไม\u0e48พบ FramerateVigilante — เล\u0e48นเก\u0e34น 30 FPS อาจเจอบ\u0e31กฟ\u0e34ส\u0e34กส\u0e4c";
				c3 = cWarn;
			}

			// 4. SkyGfx preset
			string sgIni = Path.Combine(g, "skygfx.ini");
			string r4;
			Color c4;
			if (File.Exists(sgIni))
			{
				r4 = "✓ SkyGfx ระบบแสงเงาและภาพสะท\u0e49อนรถทำงาน (พร\u0e35เซ\u0e47ต: " + activeSkyGfx.ToUpper() + ")";
				c4 = cOk;
			}
			else
			{
				r4 = "– SkyGfx ป\u0e34ดการทำงาน (ใช\u0e49กราฟ\u0e34กต\u0e49นฉบ\u0e31บ)";
				c4 = colSub;
			}

			BeginInvoke((MethodInvoker)delegate
			{
				SetHealth(0, r0, c0);
				SetHealth(1, r1, c1);
				SetHealth(2, r2, c2);
				SetHealth(3, r3, c3);
				SetHealth(4, r4, c4);
			});
		});
	}


	private void BuildGalPage()
	{
		AddLbl(pnlGal, "แกลเลอร\u0e35สกร\u0e35นช\u0e47อต (F8 ในเกม)", fTitle, 0, 0, colText);
		galCount = AddLbl(pnlGal, "", fSub, 0, 32, colSub);
		RoundedBtn roundedBtn = MakeMiniBtn("↻  ร\u0e35เฟรช", 446, 22, 106, 34);
		roundedBtn.Click += delegate
		{
			LoadGallery();
		};
		RoundedBtn roundedBtn2 = MakeMiniBtn("📂  เปิดโฟลเดอร์", 558, 22, 110, 34);
		roundedBtn2.Click += delegate
		{
			string text = ShotDir();
			if (!Directory.Exists(text))
			{
				try
				{
					Directory.CreateDirectory(text);
				}
				catch { }
			}
			if (Directory.Exists(text))
			{
				Process.Start("explorer.exe", text);
			}
			else
			{
				ShowErr("ไม่สามารถเปิดโฟลเดอร์สกรีนช็อตได้");
			}
		};
		pnlGal.Controls.Add(roundedBtn);
		pnlGal.Controls.Add(roundedBtn2);
		galFlow = new FlowLayoutPanel();
		galFlow.Location = new Point(0, 64);
		galFlow.Size = new Size(668, 364);
		galFlow.BackColor = colCard;
		galFlow.AutoScroll = true;
		galFlow.Padding = new Padding(8);
		EnableDoubleBuffer(galFlow);
		pnlGal.Controls.Add(galFlow);
	}

	private void BuildChatPage()
	{
		txtChatSearch = AddField(pnlChat, "🔍  พ\u0e34มพ\u0e4cคำค\u0e49นหา (ช\u0e37\u0e48อผ\u0e39\u0e49เล\u0e48น, คำส\u0e31\u0e48ง, ประว\u0e31ต\u0e34การซ\u0e37\u0e49อขาย, แชท)...", 0, 0, 370);
		txtChatSearch.TextChanged += delegate
		{
			FilterChatLines();
		};

		RoundedBtn btnReload = MakeMiniBtn("↻  ร\u0e35เฟรช", 380, 0, 90, 42);
		btnReload.BorderColor = colAccent;
		btnReload.Click += delegate
		{
			LoadChatLines();
		};
		pnlChat.Controls.Add(btnReload);

		RoundedBtn btnCopy = MakeMiniBtn("📋  ค\u0e31ดลอก", 478, 0, 96, 42);
		btnCopy.BorderColor = colYellow;
		btnCopy.Click += delegate
		{
			if (txtChatLog != null && !string.IsNullOrEmpty(txtChatLog.Text))
			{
				try
				{
					Clipboard.SetText(txtChatLog.Text);
					ToastOk("ค\u0e31ดลอกบ\u0e31นท\u0e36กแชทแล\u0e49ว");
				}
				catch { }
			}
		};
		pnlChat.Controls.Add(btnCopy);

		RoundedBtn btnOpen = MakeMiniBtn("📂  โฟลเดอร\u0e4c", 582, 0, 86, 42);
		btnOpen.BorderColor = colAccent;
		btnOpen.Click += delegate
		{
			try
			{
				string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP");
				if (Directory.Exists(path))
				{
					Process.Start("explorer.exe", path);
				}
				else
				{
					ShowErr("ย\u0e31งไม\u0e48พบโฟลเดอร\u0e4c SAMP ใน Documents");
				}
			}
			catch { }
		};
		pnlChat.Controls.Add(btnOpen);

		lblChatInfo = AddLbl(pnlChat, "กำล\u0e31งโหลดบ\u0e31นท\u0e36กแชท...", fSmall, 2, 46, colSub);

		SmoothPanel logBox = new SmoothPanel(colField, 10);
		logBox.Location = new Point(0, 68);
		logBox.Size = new Size(668, 360);
		logBox.Padding = new Padding(10);
		logBox.BorderColor = Color.FromArgb(40, 60, 95);

		txtChatLog = new RichTextBox();
		txtChatLog.Dock = DockStyle.Fill;
		txtChatLog.BorderStyle = BorderStyle.None;
		txtChatLog.BackColor = colField;
		txtChatLog.ForeColor = Color.FromArgb(235, 245, 255);
		txtChatLog.Font = new Font("Leelawadee UI", 9.75f);
		txtChatLog.ReadOnly = true;
		txtChatLog.WordWrap = false;
		txtChatLog.ScrollBars = RichTextBoxScrollBars.Both;

		logBox.Controls.Add(txtChatLog);
		pnlChat.Controls.Add(logBox);
	}

	private void LoadChatLines()
	{
		chatLines.Clear();
		string chatlogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GTA San Andreas User Files", "SAMP", "chatlog.txt");
		if (!File.Exists(chatlogPath))
		{
			if (lblChatInfo != null)
			{
				lblChatInfo.Text = "ยังไม่พบไฟล์ chatlog.txt (ไฟล์จะถูกสร้างอัตโนมัติเมื่อเข้าเล่นในเซิร์ฟเวอร์)";
			}
			if (txtChatLog != null)
			{
				txtChatLog.Text = "--- ยังไม่มีประวัติแชท ---\nเข้าเล่นในเซิร์ฟเวอร์ SA-MP เพื่อเริ่มเก็บบันทึกข้อความสนทนา";
			}
			return;
		}

		try
		{
			using (FileStream fs = new FileStream(chatlogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				byte[] fileBytes = new byte[fs.Length];
				fs.Read(fileBytes, 0, fileBytes.Length);

				string content;
				if (fileBytes.Length >= 3 && fileBytes[0] == 0xEF && fileBytes[1] == 0xBB && fileBytes[2] == 0xBF)
				{
					content = Encoding.UTF8.GetString(fileBytes, 3, fileBytes.Length - 3);
				}
				else
				{
					bool isUtf8 = true;
					try
					{
						UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
						content = strictUtf8.GetString(fileBytes);
					}
					catch
					{
						isUtf8 = false;
						content = "";
					}
					if (!isUtf8)
					{
						content = Encoding.GetEncoding(874).GetString(fileBytes);
					}
				}

				using (StringReader sr = new StringReader(content))
				{
					string line;
					while ((line = sr.ReadLine()) != null)
					{
						chatLines.Add(line);
					}
				}
			}
			FilterChatLines();
		}
		catch (Exception ex)
		{
			if (lblChatInfo != null)
			{
				lblChatInfo.Text = "เก\u0e34ดข\u0e49อผ\u0e34ดพลาดในการอ\u0e48านไฟล\u0e4c: " + ex.Message;
			}
		}
	}

	private void FilterChatLines()
	{
		if (txtChatLog == null || lblChatInfo == null)
		{
			return;
		}
		string query = FieldVal(txtChatSearch).Trim();
		StringBuilder sb = new StringBuilder();
		int matchCount = 0;

		for (int i = 0; i < chatLines.Count; i++)
		{
			string line = chatLines[i];
			if (string.IsNullOrEmpty(query) || line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				sb.AppendLine(line);
				matchCount++;
			}
		}

		txtChatLog.Text = sb.ToString();
		if (!string.IsNullOrEmpty(query))
		{
			lblChatInfo.Text = string.Format("ผลการค\u0e49นหา: พบ {0} จากท\u0e31\u0e49งหมด {1} บรรท\u0e31ด", matchCount, chatLines.Count);
		}
		else
		{
			lblChatInfo.Text = string.Format("แสดงประว\u0e31ต\u0e34แชทท\u0e31\u0e49งหมด {0} บรรท\u0e31ด (เล\u0e37\u0e48อนลงเพ\u0e37\u0e48อด\u0e39ข\u0e49อความล\u0e48าส\u0e38ด)", chatLines.Count);
		}

		txtChatLog.SelectionStart = txtChatLog.Text.Length;
		txtChatLog.ScrollToCaret();
	}

	private string ShotDir()
	{
		string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string sampScreens = Path.Combine(myDocs, "GTA San Andreas User Files", "SAMP", "screens");
		if (Directory.Exists(sampScreens))
		{
			return sampScreens;
		}
		string altScreens = Path.Combine(myDocs, "GTA San Andreas User Files", "Screenshots");
		if (Directory.Exists(altScreens))
		{
			return altScreens;
		}
		string gallery = Path.Combine(myDocs, "GTA San Andreas User Files", "Gallery");
		if (Directory.Exists(gallery))
		{
			return gallery;
		}
		return sampScreens;
	}

	private void LoadGallery()
	{
		string path = ShotDir();
		List<Image> list = new List<Image>();
		foreach (Control control in galFlow.Controls)
		{
			PictureBox pictureBox = control as PictureBox;
			if (pictureBox != null && pictureBox.Image != null)
			{
				list.Add(pictureBox.Image);
			}
		}
		galFlow.Controls.Clear();
		foreach (Image item in list)
		{
			try
			{
				item.Dispose();
			}
			catch
			{
			}
		}
		galGen++;
		int gen = galGen;
		int num = 0;
		if (Directory.Exists(path))
		{
			List<FileInfo> list2 = new List<FileInfo>();
			string[] files = Directory.GetFiles(path);
			foreach (string text in files)
			{
				switch (Path.GetExtension(text).ToLowerInvariant())
				{
				case ".png":
				case ".jpg":
				case ".bmp":
					list2.Add(new FileInfo(text));
					break;
				}
			}
			list2.Sort((FileInfo a, FileInfo b) => b.LastWriteTime.CompareTo(a.LastWriteTime));
			if (list2.Count > 150)
			{
				list2.RemoveRange(150, list2.Count - 150);
			}
			currentGalleryPaths.Clear();
			foreach (FileInfo item2 in list2)
			{
				currentGalleryPaths.Add(item2.FullName);
			}
			num = list2.Count;
			foreach (FileInfo item2 in list2)
			{
				string path2 = item2.FullName;
				ThreadPool.QueueUserWorkItem(delegate
				{
					Image thumb = MakeThumb(path2);
					if (thumb == null || base.IsDisposed)
					{
						if (thumb != null)
						{
							thumb.Dispose();
						}
						return;
					}
					try
					{
						BeginInvoke((MethodInvoker)delegate
						{
							if (galFlow.IsDisposed || gen != galGen)
							{
								thumb.Dispose();
							}
							else
							{
								PictureBox pictureBox2 = new PictureBox();
								pictureBox2.Image = thumb;
								pictureBox2.Size = thumb.Size;
								pictureBox2.SizeMode = PictureBoxSizeMode.StretchImage;
								pictureBox2.BackColor = colField;
								pictureBox2.Padding = new Padding(2);
								pictureBox2.Cursor = Cursors.Hand;
								pictureBox2.Click += delegate
								{
									OpenShotViewer(path2);
								};
								galFlow.Controls.Add(pictureBox2);
							}
						});
					}
					catch
					{
						thumb.Dispose();
					}
				});
			}
		}
		if (num == 0)
		{
			Label label = new Label();
			label.Text = "ยังไม่มีสกรีนช็อต — เข้าเกมแล้วกด F8 ถ่ายรูป แล้วรีเฟรชที่นี่";
			label.ForeColor = colSub;
			label.Font = fSub;
			label.AutoSize = true;
			label.Padding = new Padding(16, 20, 8, 8);
			galFlow.Controls.Add(label);
		}
		galCount.Text = num + " รูป";
	}

	private Image MakeThumb(string path)
	{
		try
		{
			using (Image image = Image.FromFile(path))
			{
				int num = 200;
				int num2 = 112;
				double num3 = Math.Max((double)num / (double)image.Width, (double)num2 / (double)image.Height);
				int num4 = Math.Max(1, (int)((double)num / num3));
				int num5 = Math.Max(1, (int)((double)num2 / num3));
				int srcX = (image.Width - num4) / 2;
				int srcY = (image.Height - num5) / 2;
				Bitmap bitmap = new Bitmap(num, num2);
				using (Graphics graphics = Graphics.FromImage(bitmap))
				{
					graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					graphics.DrawImage(image, new Rectangle(0, 0, num, num2), srcX, srcY, num4, num5, GraphicsUnit.Pixel);
				}
				return bitmap;
			}
		}
		catch
		{
			return null;
		}
	}

	private void OpenShotViewer(string path)
	{
		try
		{
			new ViewerForm(path, currentGalleryPaths).Show(this);
		}
		catch (Exception ex)
		{
			ShowErr("เปิดรูปไม่ได้: " + ex.Message);
		}
	}

	private void BuildRconPage()
	{
		AddLbl(pnlRcon, "RCON Panel — คุมเซิร์เวอร์ของคุณ", fTitle, 0, 0, colText);
		AddLbl(pnlRcon, "IP", fSub, 0, 36, colSub);
		txtRconIp = AddField(pnlRcon, "127.0.0.1", 0, 56, 300);
		AddLbl(pnlRcon, "PORT", fSub, 312, 36, colSub);
		txtRconPort = AddField(pnlRcon, "7777", 312, 56, 140);
		AddLbl(pnlRcon, "รหัส RCON", fSub, 464, 36, colSub);
		txtRconPass = AddField(pnlRcon, "", 464, 56, 204);
		txtRconPass.UseSystemPasswordChar = true;
		RoundedBtn roundedBtn = MakeMiniBtn("↻  ทดสอบเชื่อมต่อ", 0, 112, 214, 40);
		roundedBtn.Click += delegate
		{
			RconRun("echo rcon_ok", true);
		};
		RoundedBtn roundedBtn2 = MakeMiniBtn("varlist", 226, 112, 214, 40);
		roundedBtn2.Click += delegate
		{
			RconRun("varlist", false);
		};
		RoundedBtn roundedBtn3 = MakeMiniBtn("⚠  gmx (รีสตาร์ทโหมด)", 452, 112, 216, 40);
		roundedBtn3.Click += delegate
		{
			RconRun("gmx", false);
		};
		pnlRcon.Controls.Add(roundedBtn);
		pnlRcon.Controls.Add(roundedBtn2);
		pnlRcon.Controls.Add(roundedBtn3);
		AddLbl(pnlRcon, "ผลล\u0e31พธ\u0e4c", fSub, 0, 162, colSub);
		txtRconOut = new TextBox();
		txtRconOut.Multiline = true;
		txtRconOut.ReadOnly = true;
		txtRconOut.BackColor = Color.FromArgb(9, 12, 18);
		txtRconOut.ForeColor = colOk;
		txtRconOut.Font = new Font("Consolas", 10f);
		txtRconOut.BorderStyle = BorderStyle.FixedSingle;
		txtRconOut.Location = new Point(0, 182);
		txtRconOut.Size = new Size(668, 180);
		txtRconOut.ScrollBars = ScrollBars.Vertical;
		pnlRcon.Controls.Add(txtRconOut);
		txtRconCmd = AddField(pnlRcon, "พ\u0e34มพ\u0e4cคำส\u0e31\u0e48ง: say / kick <id> / ban <id> / changemode <name> / gmx...", 0, 374, 552);
		txtRconCmd.KeyDown += delegate(object s, KeyEventArgs ev)
		{
			if (ev.KeyCode == Keys.Return)
			{
				ev.SuppressKeyPress = true;
				string text = FieldVal(txtRconCmd);
				if (text.Length > 0)
				{
					RconRun(text, false);
					ForceText(txtRconCmd, "");
				}
			}
		};
		RoundedBtn roundedBtn4 = MakeMiniBtn("ส\u0e48งคำส\u0e31\u0e48ง", 560, 374, 108, 42);
		roundedBtn4.Click += delegate
		{
			string text2 = FieldVal(txtRconCmd);
			if (text2.Length > 0)
			{
				RconRun(text2, false);
				ForceText(txtRconCmd, "");
			}
		};
		pnlRcon.Controls.Add(roundedBtn4);
	}

	private void RconRun(string cmd, bool isTest)
	{
		if (cmd == null || cmd.Trim().Length == 0)
		{
			return;
		}
		string ip = FieldVal(txtRconIp);
		string s = FieldVal(txtRconPort);
		string pass = FieldVal(txtRconPass);
		int port;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(s, out port))
		{
			ShowErr("IP / Port ไม\u0e48ถ\u0e39กต\u0e49อง");
			return;
		}
		SaveConfig();
		AppendRcon("> " + cmd);
		string c = cmd.Trim();
		ThreadPool.QueueUserWorkItem(delegate
		{
			string res = RconSend(ip, port, pass, c);
			if (res.Length == 0)
			{
				res = "(ไม\u0e48ม\u0e35ข\u0e49อความตอบกล\u0e31บ)";
			}
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					AppendRcon(res);
					if (isTest)
					{
						SetStatus((res.Trim() == "rcon_ok") ? "RCON เช\u0e37\u0e48อมต\u0e48อสำเร\u0e47จ ✓" : ("RCON: " + res.Split('\n')[0]));
					}
				});
			}
		});
	}

	private void AppendRcon(string s)
	{
		txtRconOut.AppendText(s + Environment.NewLine);
	}

	private string RconSend(string host, int port, string pass, string cmd)
	{
		try
		{
			string text = ResolveHost(host);
			if (text == null)
			{
				return "(แก\u0e49 hostname ไม\u0e48สำเร\u0e47จ)";
			}
			string[] array = text.Split('.');
			byte[] buffer;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write((byte)83);
					binaryWriter.Write((byte)65);
					binaryWriter.Write((byte)77);
					binaryWriter.Write((byte)80);
					binaryWriter.Write(byte.Parse(array[0]));
					binaryWriter.Write(byte.Parse(array[1]));
					binaryWriter.Write(byte.Parse(array[2]));
					binaryWriter.Write(byte.Parse(array[3]));
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

	private void StartPlaytime(string key)
	{
		StopPlaytime();
		lock (ptLock)
		{
			ptServer = key;
		}
		lock (ptLock)
		{
			if (!playtime.ContainsKey(key))
			{
				playtime[key] = 0L;
			}
		}
		ptTimer = new System.Threading.Timer(PlaytimeTick, null, 30000, 30000);
	}

	private void StopPlaytime()
	{
		System.Threading.Timer timer = ptTimer;
		ptTimer = null;
		if (timer != null)
		{
			timer.Dispose();
		}
		lock (ptLock)
		{
			ptServer = null;
		}
		SavePlaytime();
		DiscordRpc.UpdateLauncherPresence();
	}

	private void PlaytimeTick(object s)
	{
		try
		{
			string text;
			lock (ptLock)
			{
				text = ptServer;
			}
			if (text == null)
			{
				return;
			}
			if (Process.GetProcessesByName("gta_sa").Length == 0)
			{
				StopPlaytime();
				return;
			}
			lock (ptLock)
			{
				playtime[text] = (playtime.ContainsKey(text) ? (playtime[text] + 30) : 30);
			}
			SavePlaytime();
		}
		catch
		{
		}
	}

	private void SavePlaytime()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			lock (ptLock)
			{
				foreach (KeyValuePair<string, long> item in playtime)
				{
					stringBuilder.Append(item.Key).Append('|').Append(item.Value)
						.Append("\r\n");
				}
			}
			File.WriteAllText(playtimeFile, stringBuilder.ToString());
		}
		catch
		{
		}
	}

	private void LoadPlaytime()
	{
		try
		{
			if (!File.Exists(playtimeFile))
			{
				return;
			}
			string[] array = File.ReadAllLines(playtimeFile);
			foreach (string text in array)
			{
				string[] array2 = text.Split('|');
				long result;
				if (array2.Length >= 2 && long.TryParse(array2[1], out result) && result > 0)
				{
					lock (ptLock)
					{
						playtime[array2[0]] = result;
					}
				}
			}
		}
		catch
		{
		}
	}

	private string PlaytimeText(string ip, string port)
	{
		long value;
		lock (ptLock)
		{
			if (!playtime.TryGetValue(ip + "|" + port, out value) || value < 60)
			{
				return "";
			}
		}
		int num = (int)(value / 3600);
		int num2 = (int)(value % 3600 / 60);
		return "เล\u0e48นสะสม " + ((num > 0) ? (num + " ชม. ") : "") + num2 + " นาท\u0e35";
	}

	private void ToggleWatch()
	{
		if (watchTimer != null)
		{
			StopWatch("หย\u0e38ดเฝ\u0e49าด\u0e39ท\u0e35\u0e48ว\u0e48างแล\u0e49ว");
			return;
		}
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เล\u0e37อกเซ\u0e34ร\u0e4cเวอร\u0e4cก\u0e48อน");
			return;
		}
		ServerEntry e = lstSrv.SelectedItems[0].Tag as ServerEntry;
		int port;
		if (e == null || !int.TryParse(e.Port, out port))
		{
			return;
		}
		watchEntry = e;
		SetStatus("กำล\u0e31งตรวจ " + e.Ip + ":" + e.Port + "...");
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = QueryServer(e.Ip, port);
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					if (watchEntry == e)
					{
						if (qr.Online && qr.Players < qr.Max)
						{
							watchEntry = null;
							ShowSlotToast(e, qr.Players, qr.Max);
						}
						else
						{
							watchTimer = new System.Threading.Timer(WatchTick, e, 8000, 25000);
							if (miWatch != null)
							{
								miWatch.Text = "■   หยุดเฝ้าดู (" + e.Ip + ":" + e.Port + ")";
							}
							if (btnWatch != null)
							{
								btnWatch.Text = "■  หยุดเฝ้าดู";
								btnWatch.SetPalette(Color.FromArgb(180, 40, 50), Color.FromArgb(220, 50, 60));
								btnWatch.BorderColor = Color.FromArgb(255, 99, 99);
							}
							string text = (qr.Online ? (qr.Players + "/" + qr.Max) : "ออฟไลน์");
							SetStatus("🔔 กำลังเฝ้าดู " + e.Ip + ":" + e.Port + " (" + text + ") — เช็กทุก 25 วินาที มีที่ว่างจะแจ้งทันที");
						}
					}
				});
			}
		});
	}

	private void WatchTick(object state)
	{
		ServerEntry e = state as ServerEntry;
		int result;
		if (e == null || watchTimer == null || !int.TryParse(e.Port, out result))
		{
			return;
		}
		QueryResult qr = QueryServer(e.Ip, result);
		if (!qr.Online || qr.Players >= qr.Max)
		{
			return;
		}
		System.Threading.Timer timer = watchTimer;
		watchTimer = null;
		if (timer != null)
		{
			timer.Dispose();
		}
		if (base.IsDisposed)
		{
			return;
		}
		BeginInvoke((MethodInvoker)delegate
		{
			if (miWatch != null)
			{
				miWatch.Text = "🔔   เฝ้าดูที่ว่าง";
			}
			if (btnWatch != null)
			{
				btnWatch.Text = "🔔  เฝ้าดูที่ว่าง";
				btnWatch.SetPalette(colField, colFieldHover);
				btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
			}
			ShowSlotToast(e, qr.Players, qr.Max);
			CyberAudio.PlayAlert();
			if (autoJoinSlot)
			{
				ForceText(txtIp, e.Ip);
				ForceText(txtPort, e.Port);
				ShowPage(0);
				ConnectToServer();
			}
		});
	}

	private void StopWatch(string msg)
	{
		System.Threading.Timer timer = watchTimer;
		watchTimer = null;
		watchEntry = null;
		if (timer != null)
		{
			timer.Dispose();
		}
		if (miWatch != null)
		{
			miWatch.Text = "🔔   เฝ้าดูที่ว่าง";
		}
		if (btnWatch != null)
		{
			btnWatch.Text = "🔔  เฝ้าดูที่ว่าง";
			btnWatch.SetPalette(colField, colFieldHover);
			btnWatch.BorderColor = Color.FromArgb(40, 60, 95);
		}
		SetStatus(msg);
	}

	private void ShowSlotToast(ServerEntry e, int players, int max)
	{
		base.WindowState = FormWindowState.Normal;
		Activate();
		SetStatus("✅ ม\u0e35ท\u0e35\u0e48ว\u0e48างแล\u0e49ว! " + e.Ip + ":" + e.Port + " (" + players + "/" + max + ")");
		ToastForm toastForm = new ToastForm("ม\u0e35ท\u0e35\u0e48ว\u0e48างแล\u0e49ว!", ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port)) + "\n" + e.Ip + ":" + e.Port + "  •  " + players + "/" + max + " คน");
		toastForm.OnJoin += delegate
		{
			ForceText(txtIp, e.Ip);
			ForceText(txtPort, e.Port);
			ShowPage(0);
			ConnectToServer();
		};
		toastForm.Show(this);
	}

	private RoundedBtn MakeWinBtn(string glyph)
	{
		RoundedBtn roundedBtn = new RoundedBtn(glyph, colBg, colField, colSub, 8, new Font("Segoe UI", 11f, FontStyle.Bold));
		roundedBtn.Size = new Size(34, 28);
		base.Controls.Add(roundedBtn);
		roundedBtn.BringToFront();
		return roundedBtn;
	}

	private RoundedBtn MakeMiniBtn(string text, int x, int y, int w, int h)
	{
		RoundedBtn roundedBtn = new RoundedBtn(text, colField, colFieldHover, colText, 10, new Font("Segoe UI Semibold", 9f, FontStyle.Bold));
		roundedBtn.BorderColor = Color.FromArgb(35, 52, 80);
		roundedBtn.Location = new Point(x, y);
		roundedBtn.Size = new Size(w, h);
		return roundedBtn;
	}

	private CheckBox MakeChk(string text, int x, int y)
	{
		CheckBox checkBox = new CheckBox();
		checkBox.Text = text;
		checkBox.Font = fSub;
		checkBox.ForeColor = colSub;
		checkBox.BackColor = colBg;
		checkBox.Location = new Point(x, y);
		checkBox.AutoSize = true;
		checkBox.Cursor = Cursors.Hand;
		return checkBox;
	}

	private Label AddLbl(Control host, string text, Font f, int x, int y, Color c)
	{
		Label label = new Label();
		label.Text = text;
		label.Font = f;
		label.ForeColor = c;
		label.BackColor = Color.Transparent;
		label.Location = new Point(x, y);
		label.AutoSize = true;
		label.MouseDown += DragWindow;
		host.Controls.Add(label);
		return label;
	}

	private TextBox AddField(Panel host, string placeholder, int x, int y, int w)
	{
		SmoothPanel box = new SmoothPanel(colField, 8);
		box.Location = new Point(x, y);
		box.Size = new Size(w, 42);
		box.Padding = new Padding(12, 9, 12, 9);
		TextBox tb = new TextBox();
		tb.Dock = DockStyle.Fill;
		tb.BorderStyle = BorderStyle.None;
		tb.BackColor = colField;
		tb.Font = fField;
		tb.ForeColor = colSub;
		tb.Text = placeholder;
		tb.GotFocus += delegate
		{
			if (tb.ForeColor == colSub)
			{
				tb.Text = "";
				tb.ForeColor = colText;
			}
			box.FocusBorder = true;
			box.Invalidate();
		};
		tb.LostFocus += delegate
		{
			if (tb.Text.Length == 0)
			{
				tb.Text = placeholder;
				tb.ForeColor = colSub;
			}
			box.FocusBorder = false;
			box.Invalidate();
		};
		box.Controls.Add(tb);
		host.Controls.Add(box);
		return tb;
	}

	private string FieldVal(TextBox tb)
	{
		if (!(tb.ForeColor == colSub))
		{
			return tb.Text.Trim();
		}
		return "";
	}

	private static bool ValidIp(string s)
	{
		string[] array = s.Split('.');
		if (array.Length != 4)
		{
			return false;
		}
		for (int i = 0; i < 4; i++)
		{
			int result;
			if (array[i].Length == 0 || array[i].Length > 3 || !int.TryParse(array[i], out result) || result < 0 || result > 255)
			{
				return false;
			}
		}
		return true;
	}

	private static bool ValidHost(string s)
	{
		if (ValidIp(s))
		{
			return true;
		}
		if (s.Length < 3 || s.Length > 255)
		{
			return false;
		}
		string[] array = s.Split('.');
		if (array.Length < 2)
		{
			return false;
		}
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (text.Length == 0 || text.Length > 63)
			{
				return false;
			}
			string text2 = text;
			foreach (char c in text2)
			{
				if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
				{
					return false;
				}
			}
		}
		return true;
	}

	private void ForceText(TextBox tb, string val)
	{
		tb.ForeColor = colText;
		tb.Text = val;
	}

	private string RandomNick()
	{
		string[] array = new string[12]
		{
			"Thai", "Pro", "Dark", "Neo", "Max", "Ace", "Xx", "Mr", "Big", "Cyber",
			"Shadow", "Fast"
		};
		string[] array2 = new string[11]
		{
			"Gamer", "Player", "Hunter", "Sniper", "Racer", "King", "Star", "Boy", "XD", "Pro",
			"GG"
		};
		return array[rnd.Next(array.Length)] + array2[rnd.Next(array2.Length)] + rnd.Next(10, 99);
	}

	public static bool IsValidNickChar(char c)
	{
		return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
			c == '_' || c == '[' || c == ']' || c == '(' || c == ')' || c == '$' || c == '@' || c == '.' || c == '=';
	}

	private string SanitizeNick(string raw)
	{
		if (raw == null) return "";
		StringBuilder stringBuilder = new StringBuilder();
		foreach (char c in raw)
		{
			if (IsValidNickChar(c))
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	private void ApplyAccent(Color c)
	{
		colAccent = c;
		colAccentHi = ControlPaint.Light(c, 0.2f);
		if (btnConnect != null)
		{
			btnConnect.SetPalette(c, colAccentHi);
		}
		if (brAccentBar != null)
		{
			brAccentBar.Dispose();
			brAccentBar = new SolidBrush(colAccent);
		}
		if (penHeaderLine != null)
		{
			penHeaderLine.Dispose();
			penHeaderLine = new Pen(Color.FromArgb(70, colAccent));
		}
		if (brPillFav != null)
		{
			brPillFav.Dispose();
			brPillFav = new SolidBrush(Color.FromArgb(42, colAccentHi));
		}
		if (navIndicator != null)
		{
			navIndicator.BackColor = c;
		}
		if (swAuto != null)
		{
			swAuto.OnColor = c;
		}
		if (swClose != null)
		{
			swClose.OnColor = c;
		}
		if (ring != null)
		{
			ring.RingColor = c;
			ring.Invalidate();
		}
		RenderBg();
		StyleNav(btnNavPlay, pnlPlay.Visible);
		StyleNav(btnNavSrv, pnlSrv.Visible);
		StyleNav(btnNavSet, pnlSet.Visible);
		StyleNav(btnNavGal, pnlGal.Visible);
		StyleNav(btnNavRcon, pnlRcon.Visible);
		Invalidate();
	}

	private static byte[] BuildQuery(string ip, int port, byte op)
	{
		string[] array = ip.Split('.');
		return new byte[11]
		{
			83,
			65,
			77,
			80,
			byte.Parse(array[0]),
			byte.Parse(array[1]),
			byte.Parse(array[2]),
			byte.Parse(array[3]),
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
		int num = LE32(b, off);
		off += 4;
		if (num < 0 || off + num > b.Length)
		{
			num = 0;
		}
		string @string = Encoding.GetEncoding(874).GetString(b, off, num);
		off += num;
		return @string;
	}

	private QueryResult QueryServer(string host, int port)
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
					byte[] array2 = QueryRaw(host, port, 114);
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
								string @string = encoding.GetString(array2, num3, num4);
								num3 += num4;
								if (num3 < array2.Length)
								{
									int num5 = array2[num3];
									num3++;
									if (num3 + num5 <= array2.Length)
									{
										string string2 = encoding.GetString(array2, num3, num5);
										num3 += num5;
										if (@string == "version" && string2.Length > 0 && !string2.StartsWith("0.3"))
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

	private static string ResolveHost(string host)
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

	private byte[] QueryRaw(string host, int port, byte op)
	{
		try
		{
			string text = ResolveHost(host);
			if (text == null)
			{
				return null;
			}
			using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
			{
				socket.ReceiveTimeout = 2500;
				socket.SendTimeout = 2500;
				socket.Connect(new IPEndPoint(IPAddress.Parse(text), port));
				socket.Send(BuildQuery(text, port, op));
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

	private List<string[]> QueryPlayers(string host, int port)
	{
		List<string[]> list = new List<string[]>();

		// 1. Try Opcode 100 ('d' - Detailed players: ID, Name, Score, Ping)
		byte[] arrayD = QueryRaw(host, port, 100);
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

		// 2. Fallback to Opcode 99 ('c' - Basic players: Name, Score)
		byte[] array = QueryRaw(host, port, 99);
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
			string @string = encodingC.GetString(array, num2, num3);
			num2 += num3;
			int num4 = LE32(array, num2);
			num2 += 4;
			list.Add(new string[4]
			{
				"-",
				@string,
				num4.ToString(),
				"?"
			});
		}
		return list;
	}

	private List<string[]> QueryRules(string host, int port)
	{
		List<string[]> list = new List<string[]>();
		byte[] array = QueryRaw(host, port, 114);
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
			string @string = encoding.GetString(array, num2, num3);
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
			string string2 = encoding.GetString(array, num2, num4);
			num2 += num4;
			list.Add(new string[2] { @string, string2 });
		}
		return list;
	}

	private void ShowServerInfo()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เล\u0e37อกเซ\u0e34ร\u0e4cเวอร\u0e4cก\u0e48อน");
			return;
		}
		ServerEntry e = lstSrv.SelectedItems[0].Tag as ServerEntry;
		int port;
		if (e == null || !int.TryParse(e.Port, out port))
		{
			return;
		}
		InfoDialog dlg = new InfoDialog(e);
		dlg.Show(this);
		ThreadPool.QueueUserWorkItem(delegate
		{
			List<string[]> pl = QueryPlayers(e.Ip, port);
			List<string[]> rl = QueryRules(e.Ip, port);
			try
			{
				if (!base.IsDisposed && !dlg.IsDisposed)
				{
					dlg.BeginInvoke((MethodInvoker)delegate
					{
						dlg.SetData(pl, rl);
					});
				}
			}
			catch
			{
			}
		});
	}

	private void QueryPlayServer()
	{
		string ip = FieldVal(txtIp);
		string text = FieldVal(txtPort);
		int p;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(text, out p))
		{
			lblHeroName.Text = "IP / Port ไม\u0e48ถ\u0e39กต\u0e49อง";
			lblHeroName.ForeColor = Color.FromArgb(255, 120, 120);
			return;
		}
		string key = ip + ":" + text;
		playQueryKey = key;
		lblHeroName.Text = "กำล\u0e31งตรวจสอบ " + key + " ...";
		lblHeroName.ForeColor = colSub;
		lblHeroIp.Text = key;
		lblHeroMode.Text = "";
		lblHeroPing.Text = "";
		ring.Ratio = 0f;
		ring.Line1 = "...";
		ring.Line2 = "ตรวจสอบ";
		ring.Invalidate();
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = QueryServer(ip, p);
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						if (!(playQueryKey != key))
						{
							ApplyPlayQuery(qr, key);
						}
					});
				}
			}
			catch
			{
			}
		});
	}

	private void ApplyPlayQuery(QueryResult qr, string key)
	{
		if (qr.Online)
		{
			lblHeroName.Text = ((qr.Name.Length > 0) ? qr.Name : key);
			lblHeroName.ForeColor = colOk;
			lblHeroIp.Text = key;
			string text = ((qr.Mode.Length > 0) ? ("โหมด: " + qr.Mode) : "");
			if (qr.IsOpenMp)
			{
				text = text + ((text.Length > 0) ? " • " : "") + "open.mp";
			}
			lblHeroMode.Text = text;
			lblHeroPing.Text = "ป\u0e34ง " + qr.Ping + " ms • ออนไลน\u0e4c";
			ring.Ratio = ((qr.Max > 0) ? ((float)qr.Players / (float)qr.Max) : 0f);
			ring.Current = qr.Players;
			ring.Max = qr.Max;
			ring.Line1 = qr.Players + "/" + qr.Max;
			ring.Line2 = "ผ\u0e39\u0e49เล\u0e48น";
			ring.RingColor = colAccent;
			ring.Invalidate();
			int num = key.LastIndexOf(':');
			if (num <= 0)
			{
				return;
			}
			string ip = key.Substring(0, num);
			string port = key.Substring(num + 1);
			ServerEntry serverEntry = FindEntry(ip, port, false);
			if (serverEntry != null)
			{
				if (qr.IsOpenMp)
				{
					serverEntry.IsOpenMp = true;
				}
				RecordStat(ip, port, qr.Players);
			}
		}
		else
		{
			lblHeroName.Text = key;
			lblHeroName.ForeColor = Color.FromArgb(255, 120, 120);
			lblHeroIp.Text = key;
			lblHeroMode.Text = "";
			lblHeroPing.Text = "ไม\u0e48ตอบสนอง (เซ\u0e34ร\u0e4cเวอร\u0e4cอาจป\u0e34ด หร\u0e37อ IP ผ\u0e34ด)";
			lblHeroPing.ForeColor = colSub;
			ring.Ratio = 0f;
			ring.Line1 = "--";
			ring.Line2 = "ออฟไลน\u0e4c";
			ring.Invalidate();
		}
	}

	private void RefreshAllServers()
	{
		if (querying || !base.IsHandleCreated)
		{
			return;
		}
		querying = true;
		btnRefreshAll.Enabled = false;
		btnRefreshAll.Text = "กำล\u0e31งตรวจ...";
		List<ServerEntry> list = new List<ServerEntry>();
		list.AddRange(favs);
		list.AddRange(hist);
		if (list.Count == 0)
		{
			FinishRefresh();
			return;
		}
		int remaining = list.Count;
		foreach (ServerEntry item in list)
		{
			ServerEntry ee = item;
			int port;
			if (!int.TryParse(ee.Port, out port))
			{
				if (Interlocked.Decrement(ref remaining) == 0)
				{
					FinishRefresh();
				}
				continue;
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				QueryResult qr = QueryServer(ee.Ip, port);
				try
				{
					if (!base.IsDisposed)
					{
						BeginInvoke((MethodInvoker)delegate
						{
							ApplyEntryQuery(ee, qr);
						});
					}
				}
				catch
				{
				}
				if (Interlocked.Decrement(ref remaining) == 0)
				{
					FinishRefresh();
				}
			});
		}
	}

	private void FinishRefresh()
	{
		try
		{
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					querying = false;
					btnRefreshAll.Enabled = true;
					btnRefreshAll.Text = "↻   ร\u0e35เฟรช";
				});
			}
		}
		catch
		{
		}
	}

	private string lastRecordedStat = "";

	private void RecordStat(string ip, string port, int players)
	{
		try
		{
			string newStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
			string newEntry = ip + "|" + port + "|" + newStamp + "|" + players;
			if (lastRecordedStat == newEntry)
			{
				return;
			}
			lastRecordedStat = newEntry;

			FileInfo fileInfo = new FileInfo(statsPath);
			if (fileInfo.Exists && fileInfo.Length > 300000)
			{
				string[] array = File.ReadAllLines(statsPath);
				int num = 3000;
				if (array.Length > num)
				{
					string[] array2 = new string[num];
					Array.Copy(array, array.Length - num, array2, 0, num);
					File.WriteAllLines(statsPath, array2);
				}
			}
			File.AppendAllText(statsPath, newEntry + "\r\n", Encoding.UTF8);
		}
		catch
		{
		}
	}

	private void ClearHistory()
	{
		if (hist.Count == 0)
		{
			SetStatus("ไม\u0e48ม\u0e35ประว\u0e31ต\u0e34ให\u0e49ล\u0e49าง");
		}
		else if (MessageBox.Show(this, "ลบประว\u0e31ต\u0e34เซ\u0e34ร\u0e4cเวอร\u0e4cท\u0e31\u0e49งหมด? (" + hist.Count + " รายการ)\r\nรายการโปรดไม\u0e48ถ\u0e39กลบ", "ย\u0e37นย\u0e31น", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
		{
			hist.Clear();
			SaveConfig();
			RebuildList();
			RebuildChips();
			Toast("ล\u0e49างประว\u0e31ต\u0e34แล\u0e49ว");
		}
	}

	private void ShowStats()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			SetStatus("เล\u0e37อกเซ\u0e34ร\u0e4cเวอร\u0e4cก\u0e48อน");
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		List<int> list = new List<int>();
		try
		{
			if (File.Exists(statsPath))
			{
				string[] array = File.ReadAllLines(statsPath);
				foreach (string text in array)
				{
					string[] array2 = text.Split('|');
					int result;
					if (array2.Length >= 4 && array2[0] == serverEntry.Ip && array2[1] == serverEntry.Port && int.TryParse(array2[3], out result))
					{
						list.Add(result);
					}
				}
			}
		}
		catch
		{
		}
		if (list.Count < 2)
		{
			SetStatus("ข\u0e49อม\u0e39ลสถ\u0e34ต\u0e34ย\u0e31งไม\u0e48พอ — ใช\u0e49งานไปส\u0e31กพ\u0e31กแล\u0e49วกดด\u0e39ใหม\u0e48");
		}
		else
		{
			new StatsDialog((serverEntry.Name.Length > 0) ? serverEntry.Name : (serverEntry.Ip + ":" + serverEntry.Port), list, PlaytimeText(serverEntry.Ip, serverEntry.Port)).Show(this);
		}
	}

	private void ApplyEntryQuery(ServerEntry e, QueryResult qr)
	{
		if (qr.Online)
		{
			e.Name = qr.Name;
			e.Players = qr.Players;
			e.Max = qr.Max;
			e.Ping = qr.Ping;
			if (qr.IsOpenMp)
			{
				e.IsOpenMp = true;
			}
			if (e.Fav || hist.Contains(e))
			{
				RecordStat(e.Ip, e.Port, qr.Players);
			}
		}
		else
		{
			e.Players = -1;
			e.Ping = -1;
		}
		foreach (ListViewItem item in lstSrv.Items)
		{
			if (object.ReferenceEquals(item.Tag, e))
			{
				PaintEntry(item, e);
				break;
			}
		}
	}

	private void PaintEntry(ListViewItem it, ServerEntry e)
	{
		it.SubItems[0].Text = ((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port));
		it.SubItems[1].Text = ((e.Ping == -2) ? "-" : ((e.Players < 0) ? "-" : (e.Players + "/" + e.Max)));
		it.SubItems[2].Text = ((e.Ping == -2) ? "..." : ((e.Ping < 0) ? "ออฟ" : (e.Ping + " ms")));
		it.SubItems[2].ForeColor = ((e.Ping <= 0) ? colSub : ((e.Ping < 120) ? colOk : Color.FromArgb(243, 156, 18)));
		it.SubItems[3].Text = e.Ip + ":" + e.Port;
		it.SubItems[4].Text = (e.Fav ? "★ โปรด" : ((viewMode == "public") ? "สาธารณะ" : "ประว\u0e31ต\u0e34"));
		it.SubItems[4].ForeColor = (e.Fav ? colAccentHi : colSub);
		string text = PlaytimeText(e.Ip, e.Port);
		string text2 = e.Mode;
		if (e.IsOpenMp)
		{
			text2 = text2 + ((text2.Length > 0) ? " • " : "") + "open.mp";
		}
		if (text2.Length == 0)
		{
			text2 = e.Ip + ":" + e.Port;
		}
		it.ToolTipText = text2 + ((text.Length > 0) ? ("\n" + text) : "");
	}

	private void RebuildList()
	{
		ServerEntry serverEntry = ((lstSrv.SelectedItems.Count > 0) ? (lstSrv.SelectedItems[0].Tag as ServerEntry) : null);
		lstSrv.BeginUpdate();
		lstSrv.Items.Clear();
		if (viewMode == "public")
		{
			foreach (ServerEntry item in pub)
			{
				if (MatchFilter(item, filterText))
				{
					AddEntryRow(item);
				}
			}
		}
		else
		{
			foreach (ServerEntry fav in favs)
			{
				if (MatchFilter(fav, filterText))
				{
					AddEntryRow(fav);
				}
			}
			foreach (ServerEntry item2 in hist)
			{
				if (MatchFilter(item2, filterText))
				{
					AddEntryRow(item2);
				}
			}
		}
		lstSrv.EndUpdate();
		if (serverEntry != null)
		{
			foreach (ListViewItem item3 in lstSrv.Items)
			{
				if (object.ReferenceEquals(item3.Tag, serverEntry))
				{
					item3.Selected = true;
					item3.Focused = true;
					break;
				}
			}
		}
		if (emptyLbl != null)
		{
			emptyLbl.Visible = lstSrv.Items.Count == 0;
			if (viewMode == "public")
			{
				emptyLbl.Text = "กำล\u0e31งโหลดรายการ...\r\nถ\u0e49าไม\u0e48ข\u0e36\u0e49น ลองกด ↻ ร\u0e35เฟรชอ\u0e35กคร\u0e31\u0e49ง";
			}
			else
			{
				emptyLbl.Text = "ย\u0e31งไม\u0e48ม\u0e35รายการ\r\nกด \ud83c\udf10 สาธารณะ เพ\u0e37\u0e48อเล\u0e37อกเซ\u0e34ร\u0e4cเวอร\u0e4c หร\u0e37อกรอก IP ในหน\u0e49าเล\u0e48นเกมแล\u0e49วกด + เพ\u0e34\u0e48มโปรด";
			}
		}
	}

	private void AddEntryRow(ServerEntry e)
	{
		ListViewItem listViewItem = new ListViewItem((e.Name.Length > 0) ? e.Name : (e.Ip + ":" + e.Port));
		listViewItem.SubItems.Add((e.Players < 0) ? "-" : (e.Players + "/" + e.Max));
		listViewItem.SubItems.Add((e.Ping < 0) ? "ออฟ" : (e.Ping + " ms"));
		listViewItem.SubItems.Add(e.Ip + ":" + e.Port);
		listViewItem.SubItems.Add(e.Fav ? "★ โปรด" : "ประว\u0e31ต\u0e34");
		listViewItem.Tag = e;
		lstSrv.Items.Add(listViewItem);
		PaintEntry(listViewItem, e);
	}

	private ServerEntry FindEntry(string ip, string port, bool favOnly)
	{
		foreach (ServerEntry fav in favs)
		{
			if (fav.Ip == ip && fav.Port == port)
			{
				return fav;
			}
		}
		if (!favOnly)
		{
			foreach (ServerEntry item in hist)
			{
				if (item.Ip == ip && item.Port == port)
				{
					return item;
				}
			}
		}
		return null;
	}

	private void AddFavorite()
	{
		string ip;
		string text;
		if (lstSrv.SelectedItems.Count > 0)
		{
			ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
			if (serverEntry == null)
			{
				return;
			}
			ip = serverEntry.Ip;
			text = serverEntry.Port;
		}
		else
		{
			ip = FieldVal(txtIp);
			text = FieldVal(txtPort);
		}
		int p;
		if (ip.Length == 0 || !ValidHost(ip) || !int.TryParse(text, out p) || p < 1 || p > 65535)
		{
			ShowErr("กรอก IP และ Port ในหน\u0e49าเล\u0e48นเกมก\u0e48อน");
			return;
		}
		if (FindEntry(ip, text, false) != null)
		{
			SetStatus("เซ\u0e34ร\u0e4cเวอร\u0e4cน\u0e35\u0e49ม\u0e35อย\u0e39\u0e48ในรายการแล\u0e49ว");
			return;
		}
		ServerEntry e = new ServerEntry();
		e.Ip = ip;
		e.Port = text;
		e.Fav = true;
		favs.Add(e);
		RemoveFromHist(ip, text);
		SaveConfig();
		if (viewMode == "public")
		{
			ShowView("mine");
		}
		else
		{
			RebuildList();
		}
		ToastOk("เพ\u0e34\u0e48ม " + ip + ":" + text + " เป\u0e47นรายการโปรดแล\u0e49ว");
		ThreadPool.QueueUserWorkItem(delegate
		{
			QueryResult qr = QueryServer(ip, p);
			try
			{
				if (!base.IsDisposed)
				{
					BeginInvoke((MethodInvoker)delegate
					{
						ApplyEntryQuery(e, qr);
					});
				}
			}
			catch
			{
			}
		});
	}

	private void RemoveFromHist(string ip, string port)
	{
		for (int num = hist.Count - 1; num >= 0; num--)
		{
			if (hist[num].Ip == ip && hist[num].Port == port)
			{
				hist.RemoveAt(num);
			}
		}
	}

	private void DeleteEntry()
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry == null)
		{
			return;
		}
		if (viewMode == "public")
		{
			SetStatus("รายการสาธารณะลบไม\u0e48ได\u0e49 • ใช\u0e49ป\u0e38\u0e48ม + เพ\u0e34\u0e48มโปรดแทน");
			return;
		}
		if (serverEntry.Fav)
		{
			favs.Remove(serverEntry);
		}
		else
		{
			hist.Remove(serverEntry);
		}
		SaveConfig();
		RebuildList();
		RebuildChips();
		Toast("ลบ " + serverEntry.Ip + ":" + serverEntry.Port + " แล\u0e49ว");
	}

	private void UseSelectedEntry(bool andConnect)
	{
		if (lstSrv.SelectedItems.Count == 0)
		{
			return;
		}
		ServerEntry serverEntry = lstSrv.SelectedItems[0].Tag as ServerEntry;
		if (serverEntry != null)
		{
			ForceText(txtIp, serverEntry.Ip);
			ForceText(txtPort, serverEntry.Port);
			ShowPage(0);
			QueryPlayServer();
			if (andConnect)
			{
				ConnectToServer();
			}
		}
	}

	private void AddHistory(string ip, string port)
	{
		ServerEntry serverEntry = FindEntry(ip, port, false);
		if (serverEntry == null || !serverEntry.Fav)
		{
			if (serverEntry != null)
			{
				hist.Remove(serverEntry);
			}
			ServerEntry serverEntry2 = new ServerEntry();
			serverEntry2.Ip = ip;
			serverEntry2.Port = port;
			serverEntry2.Fav = false;
			hist.Insert(0, serverEntry2);
			while (hist.Count > 10)
			{
				hist.RemoveAt(hist.Count - 1);
			}
			SaveConfig();
			RebuildList();
			RebuildChips();
		}
	}

	private void LoadConfig()
	{
		string text = "";
		string text2 = "";
		string text3 = "";
		string text4 = "";
		string text5 = "";
		string text6 = "";
		string text7 = "";
		bool @checked = false;
		bool checked2 = false;
		List<string> list = new List<string>();
		List<string> list2 = new List<string>();
		try
		{
			if (File.Exists(cfgPath))
			{
				string[] array = File.ReadAllLines(cfgPath, Encoding.UTF8);
				foreach (string text8 in array)
				{
					if (text8.StartsWith("nick=", StringComparison.OrdinalIgnoreCase))
					{
						text = text8.Substring(5);
					}
					else if (text8.StartsWith("accent=", StringComparison.OrdinalIgnoreCase))
					{
						text2 = text8.Substring(7);
					}
					else if (text8.StartsWith("gamepath=", StringComparison.OrdinalIgnoreCase))
					{
						text3 = text8.Substring(9);
					}
					else if (text8.StartsWith("autoc=", StringComparison.OrdinalIgnoreCase))
					{
						@checked = text8.Substring(6) == "1";
					}
					else if (text8.StartsWith("closeafter=", StringComparison.OrdinalIgnoreCase))
					{
						checked2 = text8.Substring(11) == "1";
					}
					else if (text8.StartsWith("last=", StringComparison.OrdinalIgnoreCase))
					{
						text4 = text8.Substring(5);
					}
					else if (text8.StartsWith("rconip=", StringComparison.OrdinalIgnoreCase))
					{
						text5 = text8.Substring(7);
					}
					else if (text8.StartsWith("rconport=", StringComparison.OrdinalIgnoreCase))
					{
						text6 = text8.Substring(9);
					}
					else if (text8.StartsWith("rconpass=", StringComparison.OrdinalIgnoreCase))
					{
						text7 = text8.Substring(9);
					}
					else if (text8.StartsWith("sortcol=", StringComparison.OrdinalIgnoreCase))
					{
						int result;
						if (int.TryParse(text8.Substring(8), out result) && result >= 0 && result <= 4)
						{
							sortCol = result;
						}
					}
					else if (text8.StartsWith("sortdesc=", StringComparison.OrdinalIgnoreCase))
					{
						sortDesc = text8.Substring(9) == "1";
					}
					else if (text8.StartsWith("srv=", StringComparison.OrdinalIgnoreCase))
					{
						list.Add(text8.Substring(4));
					}
					else if (text8.StartsWith("hist=", StringComparison.OrdinalIgnoreCase))
					{
						list2.Add(text8.Substring(5));
					}
					else if (text8.StartsWith("nick_prof=", StringComparison.OrdinalIgnoreCase))
					{
						string np = text8.Substring(10).Trim();
						if (np.Length > 0 && !nickProfiles.Contains(np))
						{
							nickProfiles.Add(np);
						}
					}
					else if (text8.StartsWith("skygfx_preset=", StringComparison.OrdinalIgnoreCase))
					{
						activeSkyGfx = text8.Substring(14).Trim();
					}
					else if (text8.StartsWith("fps_limit=", StringComparison.OrdinalIgnoreCase))
					{
						int parsedFps;
						if (int.TryParse(text8.Substring(10).Trim(), out parsedFps))
						{
							activeFpsLimit = parsedFps;
						}
					}
					else if (text8.StartsWith("autoslot=", StringComparison.OrdinalIgnoreCase))
					{
						autoJoinSlot = (text8.Substring(9).Trim() == "1");
					}
					else if (text8.StartsWith("sound_fx=", StringComparison.OrdinalIgnoreCase))
					{
						soundFxEnabled = (text8.Substring(9).Trim() == "1");
					}
					else if (text8.StartsWith("discord_rpc=", StringComparison.OrdinalIgnoreCase))
					{
						discordRpcEnabled = (text8.Substring(12).Trim() == "1");
					}
					else if (text8.StartsWith("discord_app_id=", StringComparison.OrdinalIgnoreCase))
					{
						string cid = text8.Substring(15).Trim();
						if (!string.IsNullOrEmpty(cid))
						{
							discordClientId = cid;
						}
					}
					else if (text8.StartsWith("nopause_alttab=", StringComparison.OrdinalIgnoreCase))
					{
						noPauseAltTabEnabled = (text8.Substring(15).Trim() == "1");
					}
				}
			}
		}
		catch
		{
		}
		if (text3.Length > 0)
		{
			ForceText(txtPath, text3);
		}
		else
		{
			ForceText(txtPath, AppDomain.CurrentDomain.BaseDirectory);
		}
		if (text5.Length > 0)
		{
			ForceText(txtRconIp, text5);
		}
		if (text6.Length > 0)
		{
			ForceText(txtRconPort, text6);
		}
		if (text7.Length > 0)
		{
			ForceText(txtRconPass, text7);
		}
		swAuto.Checked = @checked;
		swClose.Checked = checked2;
		if (swAutoSlot != null)
		{
			swAutoSlot.Checked = autoJoinSlot;
		}
		if (swSoundFx != null)
		{
			swSoundFx.Checked = soundFxEnabled;
		}
		CyberAudio.Enabled = soundFxEnabled;
		if (swDiscordRpc != null)
		{
			swDiscordRpc.Checked = discordRpcEnabled;
		}
		DiscordRpc.Enabled = discordRpcEnabled;
		DiscordRpc.ClientId = discordClientId;
		if (swNoPauseAltTab != null)
		{
			string gDir = (text3.Length > 0 && Directory.Exists(text3)) ? text3 : AppDomain.CurrentDomain.BaseDirectory;
			string wsIni = Path.Combine(gDir, "scripts", "GTASA.WidescreenFix.ini");
			if (File.Exists(wsIni))
			{
				try
				{
					string[] wsLines = File.ReadAllLines(wsIni, Encoding.UTF8);
					foreach (string wsL in wsLines)
					{
						if (wsL.TrimStart().StartsWith("AllowAltTabbingWithoutPausing", StringComparison.OrdinalIgnoreCase))
						{
							string[] parts = wsL.Split('=');
							if (parts.Length > 1)
							{
								noPauseAltTabEnabled = parts[1].Trim().StartsWith("1");
							}
							break;
						}
					}
				}
				catch { }
			}
			swNoPauseAltTab.Checked = noPauseAltTabEnabled;
		}
		UpdateSkyGfxButtons();
		UpdateFpsButtons();
		if (text2.Length > 0)
		{
			string[] array2 = text2.Split(',');
			if (array2.Length == 3)
			{
				try
				{
					ApplyAccent(Color.FromArgb(Clamp255(array2[0]), Clamp255(array2[1]), Clamp255(array2[2])));
				}
				catch
				{
				}
			}
		}
		foreach (string item in list)
		{
			string[] array3 = item.Split('|');
			if (array3.Length >= 2 && array3[0].Length > 0 && array3[1].Length > 0)
			{
				ServerEntry serverEntry = new ServerEntry();
				serverEntry.Ip = array3[0];
				serverEntry.Port = array3[1];
				serverEntry.Fav = true;
				favs.Add(serverEntry);
			}
		}
		foreach (string item2 in list2)
		{
			string[] array4 = item2.Split('|');
			if (array4.Length >= 2 && array4[0].Length > 0 && array4[1].Length > 0)
			{
				ServerEntry serverEntry2 = new ServerEntry();
				serverEntry2.Ip = array4[0];
				serverEntry2.Port = array4[1];
				serverEntry2.Fav = false;
				hist.Add(serverEntry2);
			}
		}
		RebuildList();
		if (text.Length > 0)
		{
			ForceText(txtNick, text);
		}
		else
		{
			ForceText(txtNick, RandomNick());
		}
		if (text4.Length > 0)
		{
			string[] array5 = text4.Split('|');
			if (array5.Length >= 2)
			{
				ForceText(txtIp, array5[0]);
				ForceText(txtPort, array5[1]);
			}
		}
		else
		{
			ForceText(txtIp, "127.0.0.1");
			ForceText(txtPort, "7777");
		}
		SetStatus("โหลดการต\u0e31\u0e49งค\u0e48าเร\u0e35ยบร\u0e49อย");
		cfgReady = true;
		RebuildChips();
		LoadPlaytime();
		RenderBg();
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		StopPubAuto();
		StopPlaytime();
		StopWatch("ป\u0e34ดแล\u0e49ว");
		foreach (System.Windows.Forms.Timer activeTimer in activeTimers)
		{
			try
			{
				activeTimer.Stop();
				activeTimer.Dispose();
			}
			catch
			{
			}
		}
		activeTimers.Clear();
	}

	private int Clamp255(string s)
	{
		int result;
		if (!int.TryParse(s.Trim(), out result))
		{
			result = 0;
		}
		if (result < 0)
		{
			result = 0;
		}
		if (result > 255)
		{
			result = 255;
		}
		return result;
	}

	private void SaveConfig()
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = FieldVal(txtNick);
			if (text.Length > 0)
			{
				stringBuilder.Append("nick=").Append(text).Append("\r\n");
			}
			stringBuilder.Append("accent=").Append(colAccent.R).Append(',')
				.Append(colAccent.G)
				.Append(',')
				.Append(colAccent.B)
				.Append("\r\n");
			string text2 = FieldVal(txtPath);
			if (text2.Length > 0)
			{
				stringBuilder.Append("gamepath=").Append(text2).Append("\r\n");
			}
			stringBuilder.Append("autoc=").Append(swAuto.Checked ? "1" : "0").Append("\r\n");
			stringBuilder.Append("closeafter=").Append(swClose.Checked ? "1" : "0").Append("\r\n");
			string text3 = FieldVal(txtIp);
			string text4 = FieldVal(txtPort);
			if (text3.Length > 0 && text4.Length > 0)
			{
				stringBuilder.Append("last=").Append(text3).Append('|')
					.Append(text4)
					.Append("\r\n");
			}
			string text5 = FieldVal(txtRconIp);
			string text6 = FieldVal(txtRconPort);
			string text7 = FieldVal(txtRconPass);
			if (text5.Length > 0)
			{
				stringBuilder.Append("rconip=").Append(text5).Append("\r\n");
			}
			if (text6.Length > 0)
			{
				stringBuilder.Append("rconport=").Append(text6).Append("\r\n");
			}
			if (text7.Length > 0)
			{
				stringBuilder.Append("rconpass=").Append(text7).Append("\r\n");
			}
			stringBuilder.Append("sortcol=").Append(sortCol).Append("\r\n");
			stringBuilder.Append("sortdesc=").Append(sortDesc ? "1" : "0").Append("\r\n");
			foreach (ServerEntry fav in favs)
			{
				stringBuilder.Append("srv=").Append(fav.Ip).Append('|')
					.Append(fav.Port)
					.Append("\r\n");
			}
			foreach (ServerEntry item in hist)
			{
				stringBuilder.Append("hist=").Append(item.Ip).Append('|')
					.Append(item.Port)
					.Append("\r\n");
			}
			stringBuilder.Append("skygfx_preset=").Append(activeSkyGfx).Append("\r\n");
			stringBuilder.Append("fps_limit=").Append(activeFpsLimit).Append("\r\n");
			stringBuilder.Append("autoslot=").Append(autoJoinSlot ? "1" : "0").Append("\r\n");
			stringBuilder.Append("sound_fx=").Append(soundFxEnabled ? "1" : "0").Append("\r\n");
			stringBuilder.Append("discord_rpc=").Append(discordRpcEnabled ? "1" : "0").Append("\r\n");
			stringBuilder.Append("discord_app_id=").Append(discordClientId).Append("\r\n");
			stringBuilder.Append("nopause_alttab=").Append(noPauseAltTabEnabled ? "1" : "0").Append("\r\n");
			foreach (string prof in nickProfiles)
			{
				stringBuilder.Append("nick_prof=").Append(prof).Append("\r\n");
			}
			File.WriteAllText(cfgPath, stringBuilder.ToString(), Encoding.UTF8);
		}
		catch
		{
		}
	}

	private void BrowseGameDir()
	{
		using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
		{
			folderBrowserDialog.Description = "เล\u0e37อกโฟลเดอร\u0e4cท\u0e35\u0e48ม\u0e35 gta_sa.exe";
			if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
			{
				if (File.Exists(Path.Combine(folderBrowserDialog.SelectedPath, "gta_sa.exe")))
				{
					ForceText(txtPath, folderBrowserDialog.SelectedPath);
					SaveConfig();
					SetStatus("ต\u0e31\u0e49งโฟลเดอร\u0e4cเกมแล\u0e49ว");
				}
				else
				{
					ShowErr("ไม\u0e48พบ gta_sa.exe ในโฟลเดอร\u0e4cท\u0e35\u0e48เล\u0e37อก");
				}
			}
		}
	}

	private string GetGameDir()
	{
		string text = FieldVal(txtPath);
		if (text.Length > 0 && File.Exists(Path.Combine(text, "gta_sa.exe")))
		{
			return text;
		}
		return AppDomain.CurrentDomain.BaseDirectory;
	}

	private void ConnectToServer()
	{
		if (connecting)
		{
			return;
		}
		string text = FieldVal(txtNick);
		string text2 = FieldVal(txtIp);
		string text3 = FieldVal(txtPort);
		if (text.Length == 0 || text2.Length == 0 || text3.Length == 0)
		{
			ShowErr("กรุณากรอกชื่อผู้เล่น / IP / Port ให้ครบถ้วน");
			return;
		}
		if (text.Length > 20)
		{
			ShowErr("Nickname ต้องไม่เกิน 20 ตัวอักษร");
			return;
		}
		if (text.Length < 3)
		{
			ShowErr("Nickname ต้องยาวอย่างน้อย 3 ตัวอักษร");
			return;
		}
		string text4 = text;
		foreach (char c in text4)
		{
			if (!IsValidNickChar(c))
			{
				ShowErr("Nickname ใช้ตัวอักษรไม่ถูกต้อง\r\n(ใช้ได้เฉพาะ a-z, A-Z, 0-9, _, [], (), $, @, . เท่านั้น)");
				return;
			}
		}
		if (!ValidHost(text2))
		{
			ShowErr("IP หรือ Hostname ไม่ถูกต้อง (เช่น: 1.2.3.4 หรือ th.example.com)");
			return;
		}
		int result;
		if (!int.TryParse(text3, out result) || result < 1 || result > 65535)
		{
			ShowErr("Port ไม่ถูกต้อง (ต้องอยู่ระหว่าง 1-65535)");
			return;
		}
		string text5 = ResolveHost(text2);
		if (text5 == null)
		{
			ShowErr("ไม่สามารถค้นหา IP จาก Hostname ได้: " + text2);
			return;
		}
		if (result != 7777 || !text5.StartsWith("127."))
		{
			try
			{
				using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
				{
					socket.SendTimeout = 2000;
					socket.Connect(new IPEndPoint(IPAddress.Parse(text5), result));
					byte[] buffer = BuildQuery(text5, result, 105);
					socket.Send(buffer);
					byte[] buffer2 = new byte[640];
					socket.ReceiveTimeout = 2500;
					int num = socket.Receive(buffer2);
					if (num <= 10)
					{
						if (MessageBox.Show(this, "เซิร์เวอร์ไม่ตอบสนองต่อการตรวจสถานะ (เซิร์เวอร์อาจปิด หรือ Firewall บล็อก Port " + result + " / UDP Query)\r\n\r\nคุณยังต้องการเข้าสู่เกมต่อไปหรือไม่?", "แจ้งเตือนการเชื่อมต่อ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
						{
							return;
						}
					}
				}
			}
			catch
			{
				if (MessageBox.Show(this, "ไม่สามารถตรวจสอบสถานะเซิร์เวอร์ " + text5 + ":" + result + " ได้\r\n(อาจเกิดจาก Firewall / Router บล็อกแพ็กเก็ต UDP Query)\r\n\r\nคุณยังต้องการเข้าสู่เกมต่อไปหรือไม่?", "แจ้งเตือนการเชื่อมต่อ", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
				{
					return;
				}
			}
		}
		string gameDir = GetGameDir();
		string text6 = Path.Combine(gameDir, "gta_sa.exe");
		string text7 = Path.Combine(gameDir, "samp.dll");
		if (!File.Exists(text6))
		{
			ShowErr("ไม่พบ gta_sa.exe ใน:\r\n" + gameDir + "\r\n(ตั้งค่าโฟลเดอร์เกมได้ที่หน้า ⚙)");
			return;
		}
		if (!File.Exists(text7))
		{
			ShowErr("ไม่พบ samp.dll\r\nต้องติดตั้ง SA-MP ไว้ในโฟลเดอร์เกมก่อน");
			return;
		}
		if (Process.GetProcessesByName("gta_sa").Length > 0)
		{
			ShowErr("gta_sa.exe กำลังรันอยู่ ปิดเกมเดิมก่อนเข้าสู่เกมใหม่");
			return;
		}
		try
		{
			SaveIconFromGame(text6);
		}
		catch
		{
		}

		string sPass = (txtServerPass != null) ? FieldVal(txtServerPass) : "";
		ServerEntry matchedEntry = FindEntry(text2, text3, false);
		if (string.IsNullOrEmpty(sPass) && matchedEntry != null && matchedEntry.Locked)
		{
			string prompted = PromptPassword((matchedEntry.Name.Length > 0) ? matchedEntry.Name : (text2 + ":" + text3));
			if (prompted == null)
			{
				return;
			}
			sPass = prompted;
		}

		SaveConfig();
		AddHistory(text2, text3);
		btnConnect.Enabled = false;
		connecting = true;
		SetStatus("กำลังเปิดเกม...");
		Refresh();
		bool flag = false;
		try
		{
			CyberAudio.PlayLaunch();
			StartGameAndInject(text6, text7, gameDir, text, text2, text3, sPass);
			flag = true;
			SetStatus("เกมกำลังทำงาน • เชื่อมต่อ " + text2 + ":" + text3);
		}
		catch (Exception ex)
		{
			ShowErr("เปิดเกมไม่สำเร็จ:\r\n" + ex.Message);
			SetStatus("ล้มเหลว — ลองใหม่อีกครั้ง");
		}
		finally
		{
			connecting = false;
			btnConnect.Enabled = true;
		}
		if (flag)
		{
			StartPlaytime(text2 + "|" + text3);
			string sName = (lblHeroName != null && !string.IsNullOrEmpty(lblHeroName.Text) && !lblHeroName.Text.StartsWith("กรอก") && !lblHeroName.Text.StartsWith("กำลัง")) ? lblHeroName.Text : (text2 + ":" + text3);
			int pCur = (ring != null) ? ring.Current : -1;
			int pMax = (ring != null) ? ring.Max : -1;
			DiscordRpc.UpdateGamePresence(sName, text2, text3, pCur, pMax);
			if (swClose.Checked)
			{
				Close();
			}
			else
			{
				base.WindowState = FormWindowState.Minimized;
			}
		}
	}

	private void StartGameAndInject(string gtaPath, string dllPath, string workDir, string nick, string ip, string port, string pass = "")
	{
		string text = ResolveHost(ip);
		if (text == null)
		{
			text = ip;
		}
		string text2 = SanitizeNick(nick);
		if (text2.Length < 3 || text2.Length > 20)
		{
			ShowErr("Nickname ไม่ถูกต้อง (ต้องยาว 3-20 ตัวอักษร และใช้อักขระที่ SA-MP รองรับ)\r\nชื่อที่กรอก: " + nick);
			return;
		}
		string value = "\"" + gtaPath + "\" -c -n " + text2 + " -h " + text + " -p " + port;
		if (!string.IsNullOrEmpty(pass))
		{
			value += " -z " + pass;
		}
		STARTUPINFO si = default(STARTUPINFO);
		si.cb = (uint)Marshal.SizeOf(typeof(STARTUPINFO));
		PROCESS_INFORMATION pi;
		if (!CreateProcess(gtaPath, new StringBuilder(value), IntPtr.Zero, IntPtr.Zero, false, 4u, IntPtr.Zero, workDir, ref si, out pi))
		{
			throw new Exception("เปิด gta_sa.exe ไม่ได้ (Win32 error " + Marshal.GetLastWin32Error() + ")");
		}
		try
		{
			InjectDll(pi.dwProcessId, dllPath);
		}
		catch
		{
			ResumeThread(pi.hThread);
			throw;
		}
		ResumeThread(pi.hThread);
		CloseHandle(pi.hThread);
		CloseHandle(pi.hProcess);
	}

	private void InjectDll(uint pid, string dllPath)
	{
		IntPtr intPtr = OpenProcess(2035711u, false, pid);
		if (intPtr == IntPtr.Zero)
		{
			throw new Exception("OpenProcess ล้มเหลว (error " + Marshal.GetLastWin32Error() + ")");
		}
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			byte[] bytes = Encoding.Unicode.GetBytes(dllPath + "\0");
			intPtr2 = VirtualAllocEx(intPtr, IntPtr.Zero, (IntPtr)bytes.Length, 12288u, 4u);
			if (intPtr2 == IntPtr.Zero)
			{
				throw new Exception("VirtualAllocEx ล้มเหลว");
			}
			IntPtr written;
			if (!WriteProcessMemory(intPtr, intPtr2, bytes, (IntPtr)bytes.Length, out written))
			{
				throw new Exception("WriteProcessMemory ล้มเหลว");
			}
			IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");
			if (procAddress == IntPtr.Zero)
			{
				throw new Exception("หา LoadLibraryW ไม่เจอ");
			}
			IntPtr tid;
			IntPtr intPtr3 = CreateRemoteThread(intPtr, IntPtr.Zero, IntPtr.Zero, procAddress, intPtr2, 0u, out tid);
			if (intPtr3 == IntPtr.Zero)
			{
				throw new Exception("CreateRemoteThread ล้มเหลว (antivirus อาจบล็อก)");
			}
			if (WaitForSingleObject(intPtr3, 15000u) == 258)
			{
				CloseHandle(intPtr3);
				throw new Exception("LoadLibrary ไม่ตอบสนอง");
			}
			CloseHandle(intPtr3);
			if (intPtr2 != IntPtr.Zero)
			{
				VirtualFreeEx(intPtr, intPtr2, IntPtr.Zero, 0x8000u /* MEM_RELEASE */);
				intPtr2 = IntPtr.Zero;
			}
		}
		finally
		{
			if (intPtr2 != IntPtr.Zero)
			{
				VirtualFreeEx(intPtr, intPtr2, IntPtr.Zero, 0x8000u /* MEM_RELEASE */);
			}
			CloseHandle(intPtr);
		}
	}

	private string PromptPassword(string serverName)
	{
		using (Form prompt = new Form())
		{
			prompt.Text = "เซิร์ฟเวอร์มีรหัสผ่าน (Password)";
			prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
			prompt.StartPosition = FormStartPosition.CenterParent;
			prompt.Size = new Size(390, 184);
			prompt.BackColor = Color.FromArgb(17, 17, 24);
			prompt.MaximizeBox = false;
			prompt.MinimizeBox = false;
			prompt.ShowInTaskbar = false;

			Label lbl = new Label
			{
				Text = "🔒 เซิร์ฟเวอร์ " + serverName + " มีรหัสผ่าน:\r\nกรุณากรอกรหัสผ่านเพื่อเข้าสู่เกม:",
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = Color.White,
				Location = new Point(20, 16),
				AutoSize = true
			};
			prompt.Controls.Add(lbl);

			TextBox tb = new TextBox
			{
				Text = "",
				Font = new Font("Segoe UI", 11f),
				BackColor = colField,
				ForeColor = Color.White,
				UseSystemPasswordChar = true,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(22, 58),
				Size = new Size(330, 28)
			};
			prompt.Controls.Add(tb);

			string entered = null;
			RoundedBtn btnOk = new RoundedBtn("เข้าเกม", colAccent, colAccentHi, Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
			{
				Location = new Point(176, 102),
				Size = new Size(96, 32)
			};
			btnOk.Click += delegate
			{
				entered = tb.Text.Trim();
				prompt.DialogResult = DialogResult.OK;
			};
			prompt.Controls.Add(btnOk);

			RoundedBtn btnCancel = new RoundedBtn("ยกเลิก", Color.FromArgb(36, 36, 52), Color.FromArgb(48, 48, 70), Color.FromArgb(160, 160, 180), 8, new Font("Segoe UI", 9.5f))
			{
				Location = new Point(280, 102),
				Size = new Size(72, 32)
			};
			btnCancel.Click += delegate { prompt.Close(); };
			prompt.Controls.Add(btnCancel);
			prompt.AcceptButton = btnOk;
			prompt.CancelButton = btnCancel;

			if (prompt.ShowDialog(this) == DialogResult.OK)
			{
				return entered ?? "";
			}
			return null;
		}
	}

	private void PromptAddServer()
	{
		if (lstSrv.SelectedItems.Count > 0)
		{
			AddFavorite();
			return;
		}
		using (Form prompt = new Form())
		{
			prompt.Text = "เพิ่มเซิร์ฟเวอร์โปรด (Add Favorite Server)";
			prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
			prompt.StartPosition = FormStartPosition.CenterParent;
			prompt.Size = new Size(420, 192);
			prompt.BackColor = Color.FromArgb(17, 17, 24);
			prompt.MaximizeBox = false;
			prompt.MinimizeBox = false;
			prompt.ShowInTaskbar = false;

			Label lbl = new Label
			{
				Text = "กรอก IP:Port หรือ Hostname ของเซิร์ฟเวอร์:",
				Font = new Font("Segoe UI", 9.5f),
				ForeColor = Color.White,
				Location = new Point(20, 18),
				AutoSize = true
			};
			prompt.Controls.Add(lbl);

			TextBox tb = new TextBox
			{
				Text = "",
				Font = new Font("Segoe UI", 10.5f),
				BackColor = colField,
				ForeColor = Color.White,
				BorderStyle = BorderStyle.FixedSingle,
				Location = new Point(22, 48),
				Size = new Size(360, 28)
			};
			prompt.Controls.Add(tb);

			RoundedBtn btnOk = new RoundedBtn("เพิ่มโปรด", colAccent, colAccentHi, Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold))
			{
				Location = new Point(190, 96),
				Size = new Size(100, 34)
			};
			btnOk.Click += delegate
			{
				string val = tb.Text.Trim();
				if (val.Length == 0) return;
				string addIp = val;
				string addPort = "7777";
				if (val.Contains(":"))
				{
					string[] parts = val.Split(':');
					addIp = parts[0];
					addPort = parts[1];
				}
				int p;
				if (!ValidHost(addIp) || !int.TryParse(addPort, out p) || p < 1 || p > 65535)
				{
					MessageBox.Show(prompt, "รูปแบบ IP หรือ Port ไม่ถูกต้อง (เช่น: 127.0.0.1:7777)", "ข้อผิดพลาด", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
					return;
				}
				if (FindEntry(addIp, addPort, false) != null)
				{
					MessageBox.Show(prompt, "เซิร์ฟเวอร์นี้มีอยู่ในรายการแล้ว", "แจ้งเตือน", MessageBoxButtons.OK, MessageBoxIcon.Information);
					return;
				}
				ServerEntry e = new ServerEntry
				{
					Ip = addIp,
					Port = addPort,
					Fav = true
				};
				favs.Add(e);
				RemoveFromHist(addIp, addPort);
				SaveConfig();
				if (viewMode == "public")
				{
					ShowView("mine");
				}
				else
				{
					RebuildList();
				}
				ToastOk("เพิ่ม " + addIp + ":" + addPort + " เป็นรายการโปรดแล้ว");
				prompt.DialogResult = DialogResult.OK;
				ThreadPool.QueueUserWorkItem(delegate
				{
					QueryResult qr = QueryServer(addIp, p);
					try
					{
						if (!base.IsDisposed)
						{
							BeginInvoke((MethodInvoker)delegate { ApplyEntryQuery(e, qr); });
						}
					}
					catch { }
				});
			};
			prompt.Controls.Add(btnOk);

			RoundedBtn btnCancel = new RoundedBtn("ยกเลิก", Color.FromArgb(36, 36, 52), Color.FromArgb(48, 48, 70), Color.FromArgb(160, 160, 180), 8, new Font("Segoe UI", 9.5f))
			{
				Location = new Point(300, 96),
				Size = new Size(82, 34)
			};
			btnCancel.Click += delegate { prompt.Close(); };
			prompt.Controls.Add(btnCancel);
			prompt.AcceptButton = btnOk;
			prompt.CancelButton = btnCancel;

			prompt.ShowDialog(this);
		}
	}

	private void ResetGameSettings()
	{
		string docPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
		string setPath = Path.Combine(docPath, "GTA San Andreas User Files", "gta_sa.set");
		if (!File.Exists(setPath))
		{
			Toast("ไม่พบไฟล์ gta_sa.set (ยังไม่มีไฟล์คอนฟิกเดิม)");
			return;
		}
		if (MessageBox.Show(this, "คุณต้องการรีเซ็ตการตั้งค่าเกมหรือไม่?\r\n\r\nการทำงานนี้จะช่วยแก้ปัญหา:\r\n- เปิดเกมแล้วเด้ง / จอดำ / ค้างหน้าโหลด\r\n- ความละเอียดหน้าจอไม่ถูกต้อง\r\n\r\n(โปรแกรมจะสำรอง gta_sa.set.bak ไว้ และไฟล์เซฟจะไม่สูญหาย)", "ยืนยันการแก้ปัญหาเกมเด้ง", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
		{
			return;
		}
		try
		{
			Process[] procs = Process.GetProcessesByName("gta_sa");
			foreach (Process p in procs)
			{
				try { p.Kill(); } catch { }
			}
			string bakPath = setPath + ".bak";
			File.Copy(setPath, bakPath, true);
			File.Delete(setPath);
			ToastOk("รีเซ็ต gta_sa.set และแก้ปัญหาเกมเด้งเรียบร้อยแล้ว");
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถรีเซ็ต gta_sa.set: " + ex.Message);
		}
	}

	private void ApplyNoPauseAltTab(bool enable)
	{
		noPauseAltTabEnabled = enable;
		string g = GetGameDir();
		string iniPath = Path.Combine(g, "scripts", "GTASA.WidescreenFix.ini");
		try
		{
			if (File.Exists(iniPath))
			{
				string[] lines = File.ReadAllLines(iniPath, Encoding.UTF8);
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].TrimStart().StartsWith("AllowAltTabbingWithoutPausing", StringComparison.OrdinalIgnoreCase))
					{
						lines[i] = "AllowAltTabbingWithoutPausing = " + (enable ? "1" : "0");
					}
				}
				File.WriteAllLines(iniPath, lines, Encoding.UTF8);
			}
			SaveConfig();
			if (enable)
			{
				ToastOk("เปิดใช้งาน: ไม่ค้าง/ไม่หลุดตอนพับจอ (Alt+Tab)");
			}
			else
			{
				Toast("ปิดใช้งาน: โหมดพับจอมาตรฐาน");
			}
		}
		catch (Exception ex)
		{
			ShowErr("ไม่สามารถบันทึกการตั้งค่า Alt+Tab: " + ex.Message);
		}
	}

	protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
	{
		if (keyData == Keys.F5)
		{
			RefreshCurrentView();
			if (pnlChat != null && pnlChat.Visible) LoadChatLines();
			if (pnlGal != null && pnlGal.Visible) LoadGallery();
			if (pnlSet != null && pnlSet.Visible) RunHealthCheck();
			Toast("รีเฟรชข้อมูลแล้ว (F5)");
			return true;
		}
		if (keyData == Keys.Enter && pnlPlay != null && pnlPlay.Visible &&
			(txtNick == null || !txtNick.Focused) &&
			(txtIp == null || !txtIp.Focused) &&
			(txtPort == null || !txtPort.Focused) &&
			(txtServerPass == null || !txtServerPass.Focused))
		{
			ConnectToServer();
			return true;
		}
		if (keyData == Keys.Escape)
		{
			if (txtSearch != null && txtSearch.Focused)
			{
				ForceText(txtSearch, "");
				filterText = "";
				RebuildList();
				return true;
			}
			if (txtChatSearch != null && txtChatSearch.Focused)
			{
				ForceText(txtChatSearch, "");
				FilterChatLines();
				return true;
			}
		}
		return base.ProcessCmdKey(ref msg, keyData);
	}

	private void SaveIconFromGame(string gtaPath)
	{
		if (File.Exists(gtaPath))
		{
			base.Icon = Icon.ExtractAssociatedIcon(gtaPath);
		}
	}

	private void SetStatus(string s)
	{
		lblStatus.Text = s;
	}

	private void ShowErr(string msg)
	{
		MessageBox.Show(this, msg, "SA-MP Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
		SetStatus("โปรดตรวจข\u0e49อม\u0e39ล");
	}

	private void DragWindow(object sender, MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Left)
		{
			ReleaseCapture();
			SendMessage(base.Handle, 161u, 2, 0);
		}
	}

	protected override void WndProc(ref Message m)
	{
		if (m.Msg == 0x004A)
		{
			try
			{
				Program.COPYDATASTRUCT cds = (Program.COPYDATASTRUCT)Marshal.PtrToStructure(m.LParam, typeof(Program.COPYDATASTRUCT));
				if (cds.cbData > 0 && cds.lpData != IntPtr.Zero)
				{
					byte[] bytes = new byte[cds.cbData];
					Marshal.Copy(cds.lpData, bytes, 0, cds.cbData);
					string received = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
					if (!string.IsNullOrEmpty(received))
					{
						BeginInvoke((MethodInvoker)delegate
						{
							ApplySampUrl(received);
						});
					}
				}
				m.Result = (IntPtr)1;
				return;
			}
			catch
			{
			}
		}
		base.WndProc(ref m);
	}

	public void ApplySampUrl(string url)
	{
		if (string.IsNullOrEmpty(url)) return;
		string text = url.Trim();
		if (text.ToLowerInvariant().StartsWith("samp://"))
		{
			text = text.Substring(7);
		}
		try
		{
			text = Uri.UnescapeDataString(text);
		}
		catch
		{
		}
		text = text.TrimEnd('/');
		string[] array = text.Split(':');
		if (array.Length >= 1 && array[0].Length > 0)
		{
			ForceText(txtIp, array[0]);
			ForceText(txtPort, (array.Length > 1 && array[1].Length > 0) ? array[1] : "7777");
			ShowPage(0);
			QueryPlayServer();
			ToastOk("โหลดที่อยู่เซิร์ฟเวอร์จาก samp:// แล้ว");
		}
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		Animate(280, delegate(float t)
		{
			base.Opacity = t;
		});
		RunHealthCheck();
		ApplySampUrl(bootUrl);
		QueryPlayServer();
		if (swAuto.Checked)
		{
			string text2 = FieldVal(txtIp);
			string text3 = FieldVal(txtPort);
			if (text2.Length > 0 && text3.Length > 0)
			{
				ConnectToServer();
			}
		}
	}

	private static GraphicsPath RoundRect(Rectangle r, int rad)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = rad * 2;
		graphicsPath.AddArc(r.X, r.Y, num, num, 180f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		graphicsPath.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		graphicsPath.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		graphicsPath.CloseFigure();
		return graphicsPath;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (bgCache != null)
			{
				bgCache.Dispose();
				bgCache = null;
			}
			if (brCard != null)
			{
				brCard.Dispose();
			}
			if (brSel != null)
			{
				brSel.Dispose();
			}
			if (brHeader != null)
			{
				brHeader.Dispose();
			}
			if (brAccentBar != null)
			{
				brAccentBar.Dispose();
			}
			if (penHeaderLine != null)
			{
				penHeaderLine.Dispose();
			}
			if (brDotOff != null)
			{
				brDotOff.Dispose();
			}
			if (brPingG != null)
			{
				brPingG.Dispose();
			}
			if (brPingY != null)
			{
				brPingY.Dispose();
			}
			if (brPingR != null)
			{
				brPingR.Dispose();
			}
			if (brPillFav != null)
			{
				brPillFav.Dispose();
			}
			if (brPillPub != null)
			{
				brPillPub.Dispose();
			}
			if (brPillHist != null)
			{
				brPillHist.Dispose();
			}
			if (fTitle != null)
			{
				fTitle.Dispose();
			}
			if (fSub != null)
			{
				fSub.Dispose();
			}
			if (fField != null)
			{
				fField.Dispose();
			}
			if (fButton != null)
			{
				fButton.Dispose();
			}
			if (fSmall != null)
			{
				fSmall.Dispose();
			}
			if (fLogo != null)
			{
				fLogo.Dispose();
			}
			if (fLogoSub != null)
			{
				fLogoSub.Dispose();
			}
			if (fNav != null)
			{
				fNav.Dispose();
			}
			if (fStat != null)
			{
				fStat.Dispose();
			}
			if (fBadge != null)
			{
				fBadge.Dispose();
			}
		}
		base.Dispose(disposing);
	}
}
