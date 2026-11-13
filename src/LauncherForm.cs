using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal partial class LauncherForm : Form
{
	private readonly ICore core = CoreFactory.Create();

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
	private RoundedBtn btnNavGal;
	private RoundedBtn btnNavRcon;
	private RoundedBtn btnNavMods;

	private Panel pnlChat;
	private RichTextBox txtChatLog;
	private TextBox txtChatSearch;
	private Label lblChatInfo;
	private List<string> chatLines = new List<string>();
	private Panel pnlPlay;
	private Panel pnlSrv;
	private Panel pnlSet;
	private Panel pnlGal;
	private Panel pnlRcon;
	private Panel pnlMods;

	private TextBox txtNick;
	private TextBox txtIp;
	private TextBox txtPort;
	private TextBox txtPath;
	private TextBox txtServerPass;

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
	private ToggleSwitch swAutoSlot;
	private ToggleSwitch swNoPauseAltTab;

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

	private FlowLayoutPanel galFlow;
	private Label galCount;
	private bool galLoaded;
	private int galGen;
	private List<string> currentGalleryPaths = new List<string>();

	private TextBox txtRconIp;
	private TextBox txtRconPort;
	private TextBox txtRconPass;
	private TextBox txtRconCmd;
	private TextBox txtRconOut;

	private ServerEntry watchEntry;
	private System.Threading.Timer watchTimer;
	private ToolStripMenuItem miWatch;
	private RoundedBtn btnWatch;

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
	private Label lblEngine;
	private RoundedBtn btnAutoFix;
	private HealthCheckReport lastHealthReport;
	private string bootUrl;
	private string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samp_launcher.cfg");
	private string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.db");
	private string statsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "server_stats.txt");
	private string lastRecordedStat = "";
	private Random rnd = new Random();
	private bool querying;
	private string playQueryKey = "";
	private bool cfgReady;
	private List<System.Windows.Forms.Timer> activeTimers = new List<System.Windows.Forms.Timer>();

	private List<string> nickProfiles = new List<string>();
	private string activeSkyGfx = "ps2";
	private int activeFpsLimit = 144;
	private bool autoJoinSlot = false;
	private bool noPauseAltTabEnabled = false;
	private RoundedBtn btnSgPs2;
	private RoundedBtn btnSgPc;
	private RoundedBtn btnSgMob;
	private RoundedBtn btnSgOff;
	private RoundedBtn btnFps60;
	private RoundedBtn btnFps90;
	private RoundedBtn btnFps120;
	private RoundedBtn btnFps144;
	private RoundedBtn btnFpsMax;

	private Panel pnlSide;
	private Label lblSideVer;
	private Label lblFooterRight;
	private RoundedBtn btnWinMin;
	private RoundedBtn btnWinMax;
	private RoundedBtn btnWinClose;
	private bool dwmApplied;
	private bool uiBuilt;
	private bool wasMinimized;
	private List<Label> accentLabels = new List<Label>();

	private System.Windows.Forms.Timer sparkPollTimer;
	private string activeSparkHost = "";
	private int activeSparkPort;
	private SparklineGraph heroSpark;
	private SparklineGraph detSpark;

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, uint msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsZoomed(IntPtr hWnd);

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams cp = base.CreateParams;
			// WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU
			cp.Style |= 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
			return cp;
		}
	}

	public LauncherForm()
		: this(null)
	{
	}

	public LauncherForm(string bootUrlArg)
	{
		DpiHelper.Init();
		bootUrl = bootUrlArg;
		Text = "SA-MP Launcher";
		base.AutoScaleMode = AutoScaleMode.None;
		base.FormBorderStyle = FormBorderStyle.None;
		base.StartPosition = FormStartPosition.CenterScreen;
		base.Size = DpiHelper.Sz(980, 620);
		base.MinimumSize = DpiHelper.Sz(920, 580);
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
		DpiHelper.ScaleControlTree(this);
		uiBuilt = true;
		PerformResponsiveLayout();
		LoadConfig();
	}

	protected override void OnHandleCreated(EventArgs e)
	{
		base.OnHandleCreated(e);
		dwmApplied = core.ApplyDwm(base.Handle);
		core.SetDwmAccent(base.Handle, colAccent);
		StyleAllListViews();
		UpdateRegion();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (base.WindowState == FormWindowState.Minimized)
		{
			wasMinimized = true;
			core.StopPingSpark();
			if (sparkPollTimer != null)
			{
				sparkPollTimer.Stop();
			}
			StopPubAuto();
			return;
		}
		if (wasMinimized)
		{
			wasMinimized = false;
			if (sparkPollTimer != null)
			{
				sparkPollTimer.Start();
			}
			if (pnlPlay != null && pnlPlay.Visible)
			{
				ResumePlaySpark();
			}
			else if (pnlSrv != null && pnlSrv.Visible)
			{
				ResumeSelectedServerSpark();
				if (viewMode == "public")
				{
					StartPubAuto();
				}
			}
		}
		if (uiBuilt)
		{
			PerformResponsiveLayout();
		}
		UpdateRegion();
		RenderBg();
		Invalidate();
	}

	private void UpdateRegion()
	{
		if (base.WindowState == FormWindowState.Maximized || dwmApplied)
		{
			if (base.Region != null)
			{
				base.Region = null;
			}
			return;
		}
		bool isWin11OrNewer = Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
		if (isWin11OrNewer && base.IsHandleCreated)
		{
			if (base.Region != null)
			{
				base.Region = null;
			}
			return;
		}
		base.Region = new Region(RoundRect(new Rectangle(0, 0, base.Width, base.Height), DpiHelper.S(14)));
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
		bool isWin11OrNewer = Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000;
		if (base.WindowState != FormWindowState.Maximized && !(dwmApplied && isWin11OrNewer))
		{
			using (Pen pen = new Pen(Color.FromArgb(65, colAccent), 1f))
			{
				graphics.DrawPath(pen, RoundRect(new Rectangle(0, 0, base.Width - 1, base.Height - 1), DpiHelper.S(14)));
			}
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

			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, base.Width, base.Height), Color.FromArgb(11, 14, 20), Color.FromArgb(16, 23, 36), LinearGradientMode.Vertical))
			{
				graphics.FillRectangle(brush, 0, 0, base.Width, base.Height);
			}

			int footH = DpiHelper.S(46);
			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(9, 12, 17)))
			{
				graphics.FillRectangle(brush2, 0, base.Height - footH, base.Width, footH);
			}

			int orb1 = DpiHelper.S(440);
			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(24, colAccent)))
			{
				graphics.FillEllipse(brush3, base.Width - DpiHelper.S(320), -DpiHelper.S(160), orb1, orb1);
			}
			int orb2 = DpiHelper.S(360);
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(14, colAccent)))
			{
				graphics.FillEllipse(brush4, -DpiHelper.S(120), base.Height - DpiHelper.S(220), orb2, orb2);
			}

			using (Pen penGrid = new Pen(Color.FromArgb(18, colAccent), 1f))
			{
				for (int i = 0; i < 7; i++)
				{
					int offset = DpiHelper.S(i * 32);
					graphics.DrawLine(penGrid, base.Width - DpiHelper.S(260) + offset, 0, base.Width, DpiHelper.S(260) - offset);
				}
			}

			string s1 = "SONIC ";
			string s2 = "SAMP ";
			string s3 = "LAUNCHER";
			SizeF sz1 = graphics.MeasureString(s1, fLogo);
			SizeF sz2 = graphics.MeasureString(s2, fLogo);
			SizeF sz3 = graphics.MeasureString(s3, fLogo);

			float logoX = DpiHelper.Sf(202f);
			float logoY = DpiHelper.Sf(14f);
			using (SolidBrush b1 = new SolidBrush(colAccent))
			using (SolidBrush b2 = new SolidBrush(colText))
			using (SolidBrush b3 = new SolidBrush(colAccentHi))
			{
				graphics.DrawString(s1, fLogo, b1, logoX, logoY);
				graphics.DrawString(s2, fLogo, b2, logoX + sz1.Width, logoY);
				graphics.DrawString(s3, fLogo, b3, logoX + sz1.Width + sz2.Width, logoY);
			}

			SizeF badgeTextSz = graphics.MeasureString("CYBER EDITION", fBadge);
			int badgePadX = DpiHelper.S(8);
			int badgePadY = DpiHelper.S(3);
			Rectangle rBadge = new Rectangle(
				(int)(logoX + sz1.Width + sz2.Width + sz3.Width + DpiHelper.Sf(10f)),
				(int)(logoY + DpiHelper.Sf(5f)),
				(int)Math.Ceiling(badgeTextSz.Width) + badgePadX * 2,
				(int)Math.Ceiling(badgeTextSz.Height) + badgePadY * 2);
			if (rBadge.Right < base.Width - DpiHelper.S(140))
			{
				using (SolidBrush bBadge = new SolidBrush(Color.FromArgb(28, colAccent)))
				using (SolidBrush bBadgeTxt = new SolidBrush(colAccentHi))
				using (Pen pBadge = new Pen(Color.FromArgb(180, colAccent), 1f))
				{
					graphics.FillRectangle(bBadge, rBadge);
					graphics.DrawRectangle(pBadge, rBadge);
					graphics.DrawString("CYBER EDITION", fBadge, bBadgeTxt, rBadge.X + badgePadX, rBadge.Y + badgePadY);
				}
			}

			using (SolidBrush bSub = new SolidBrush(colSub))
			{
				graphics.DrawString("HIGH-PERFORMANCE MULTIPLAYER CLIENT  •  SA-MP 0.3.7 R2", fLogoSub, bSub, DpiHelper.Sf(204f), DpiHelper.Sf(54f));
			}

			using (Pen pen = new Pen(Color.FromArgb(140, colAccent), DpiHelper.Sf(2f)))
			{
				int lineY = DpiHelper.S(86);
				graphics.DrawLine(pen, DpiHelper.S(200), lineY, base.Width - DpiHelper.S(22), lineY);
			}

			graphics.Dispose();
			bgCache = image;
		}
	}

	private void BuildUi()
	{
		base.MouseDown += DragWindow;
		btnWinMin = MakeWinBtn("–");
		btnWinMin.Location = new Point(980 - 122, 14);
		btnWinMin.Click += delegate
		{
			base.WindowState = FormWindowState.Minimized;
		};
		btnWinMax = MakeWinBtn("□");
		btnWinMax.Location = new Point(980 - 84, 14);
		btnWinMax.Click += delegate
		{
			ToggleMaximize();
		};
		btnWinClose = MakeWinBtn("×");
		btnWinClose.Location = new Point(980 - 46, 14);
		btnWinClose.TextColor = Color.FromArgb(255, 99, 99);
		btnWinClose.Click += delegate
		{
			Close();
		};
		pnlSide = new Panel();
		pnlSide.Location = new Point(0, 87);
		pnlSide.Size = new Size(190, 620 - 87 - 36);
		pnlSide.BackColor = colSide;
		EnableDoubleBuffer(pnlSide);
		base.Controls.Add(pnlSide);
		btnNavPlay = MakeNav("   ▶  เล่นเกม", 14);
		btnNavSrv = MakeNav("   ☰  เซิร์ฟเวอร์", 56);
		btnNavChat = MakeNav("   📜  บันทึกแชท", 98);
		btnNavGal = MakeNav("   🖼  แกลเลอรี", 140);
		btnNavSet = MakeNav("   ⚙  ตั้งค่า", 182);
		btnNavRcon = MakeNav("   🛠  RCON", 224);
		btnNavMods = MakeNav("   🧩  ม็อด & ตัวช่วย", 266);
		btnNavPlay.Click += delegate { ShowPage(0); };
		btnNavSrv.Click += delegate { ShowPage(1); };
		btnNavChat.Click += delegate { ShowPage(2); };
		btnNavGal.Click += delegate { ShowPage(3); };
		btnNavSet.Click += delegate { ShowPage(4); };
		btnNavRcon.Click += delegate { ShowPage(5); };
		btnNavMods.Click += delegate { ShowPage(6); };
		pnlSide.Controls.Add(btnNavPlay);
		pnlSide.Controls.Add(btnNavSrv);
		pnlSide.Controls.Add(btnNavChat);
		pnlSide.Controls.Add(btnNavGal);
		pnlSide.Controls.Add(btnNavSet);
		pnlSide.Controls.Add(btnNavRcon);
		pnlSide.Controls.Add(btnNavMods);
		navIndicator = new Panel();
		navIndicator.Size = new Size(4, 26);
		navIndicator.Location = new Point(0, 20);
		navIndicator.BackColor = colAccent;
		pnlSide.Controls.Add(navIndicator);
		navIndicator.BringToFront();
		lblSideVer = new Label();
		lblSideVer.Text = "SONIC CLIENT v2.4";
		lblSideVer.Font = fSmall;
		lblSideVer.ForeColor = colSub;
		lblSideVer.BackColor = Color.Transparent;
		lblSideVer.Location = new Point(18, pnlSide.Height - 32);
		lblSideVer.AutoSize = true;
		pnlSide.Controls.Add(lblSideVer);
		pnlPlay = MakePage();
		pnlSrv = MakePage();
		pnlChat = MakePage();
		pnlGal = MakePage();
		pnlSet = MakePage();
		pnlRcon = MakePage();
		pnlMods = MakePage();
		EnableDoubleBuffer(pnlPlay);
		EnableDoubleBuffer(pnlSrv);
		EnableDoubleBuffer(pnlChat);
		EnableDoubleBuffer(pnlGal);
		EnableDoubleBuffer(pnlSet);
		EnableDoubleBuffer(pnlRcon);
		EnableDoubleBuffer(pnlMods);
		BuildPlayPage();
		BuildSrvPage();
		BuildChatPage();
		BuildGalPage();
		BuildSetPage();
		BuildRconPage();
		BuildModsPage();
		lblStatus = new Label();
		lblStatus.Text = "พร้อมเชื่อมต่อ";
		lblStatus.Font = fSub;
		lblStatus.ForeColor = colSub;
		lblStatus.BackColor = Color.Transparent;
		lblStatus.Location = new Point(206, 620 - 32);
		lblStatus.AutoSize = true;
		base.Controls.Add(lblStatus);
		lblFooterRight = new Label();
		lblFooterRight.Text = "SA-MP 0.3.7 R2 • RUST ENGINE";
		lblFooterRight.Font = fLogoSub;
		lblFooterRight.ForeColor = colSub;
		lblFooterRight.BackColor = Color.Transparent;
		lblFooterRight.AutoSize = true;
		lblFooterRight.Location = new Point(980 - 240, 620 - 32);
		base.Controls.Add(lblFooterRight);

		sparkPollTimer = new System.Windows.Forms.Timer();
		sparkPollTimer.Interval = 1400;
		sparkPollTimer.Tick += SparkPollTick;
		sparkPollTimer.Start();

		ShowPage(0);
		CyberAudio.Enabled = soundFxEnabled;
		CyberAudio.Init();
		DiscordRpc.Enabled = discordRpcEnabled;
		DiscordRpc.Initialize(discordClientId);
		DiscordRpc.UpdateLauncherPresence();
	}

	private void ToggleMaximize()
	{
		if (base.WindowState == FormWindowState.Maximized)
		{
			base.WindowState = FormWindowState.Normal;
		}
		else
		{
			base.WindowState = FormWindowState.Maximized;
		}
	}

	private void StartActiveSpark(string host, int port)
	{
		if (string.IsNullOrEmpty(host) || port <= 0 || port > 65535)
		{
			return;
		}
		if (!string.Equals(activeSparkHost, host, StringComparison.OrdinalIgnoreCase) || activeSparkPort != port)
		{
			activeSparkHost = host;
			activeSparkPort = port;
			if (heroSpark != null) heroSpark.ClearData();
			if (detSpark != null) detSpark.ClearData();
			core.StartPingSpark(host, port);

			System.Windows.Forms.Timer quickTick = new System.Windows.Forms.Timer();
			quickTick.Interval = 220;
			activeTimers.Add(quickTick);
			quickTick.Tick += delegate
			{
				quickTick.Stop();
				quickTick.Dispose();
				activeTimers.Remove(quickTick);
				if (!base.IsDisposed)
				{
					SparkPollTick(this, EventArgs.Empty);
				}
			};
			quickTick.Start();
		}
	}

	private void StopActiveSpark()
	{
		activeSparkHost = "";
		activeSparkPort = 0;
		core.StopPingSpark();
	}

	private void SparkPollTick(object sender, EventArgs e)
	{
		if (base.IsDisposed || !base.Visible || base.WindowState == FormWindowState.Minimized)
		{
			return;
		}
		bool onPlayOrSrv = (pnlPlay != null && pnlPlay.Visible) || (pnlSrv != null && pnlSrv.Visible);
		if (!onPlayOrSrv || string.IsNullOrEmpty(activeSparkHost) || activeSparkPort <= 0)
		{
			return;
		}
		PingSparkReport rep = core.GetPingSpark(activeSparkHost, activeSparkPort);
		if (pnlPlay != null && pnlPlay.Visible && heroSpark != null)
		{
			heroSpark.UpdateData(rep);
		}
		if (pnlSrv != null && pnlSrv.Visible && detSpark != null)
		{
			detSpark.UpdateData(rep);
		}
	}

	private void PerformResponsiveLayout()
	{
		int cw = base.ClientSize.Width;
		int ch = base.ClientSize.Height;
		if (cw < 200 || ch < 200) return;

		if (btnWinClose != null) btnWinClose.Location = new Point(cw - DpiHelper.S(46), DpiHelper.S(14));
		if (btnWinMax != null)
		{
			btnWinMax.Location = new Point(cw - DpiHelper.S(84), DpiHelper.S(14));
			btnWinMax.Text = (base.WindowState == FormWindowState.Maximized) ? "❐" : "□";
		}
		if (btnWinMin != null) btnWinMin.Location = new Point(cw - DpiHelper.S(122), DpiHelper.S(14));

		if (pnlSide != null)
		{
			pnlSide.Location = new Point(0, DpiHelper.S(87));
			pnlSide.Size = new Size(DpiHelper.S(190), Math.Max(DpiHelper.S(200), ch - DpiHelper.S(87 + 36)));
			if (lblSideVer != null)
			{
				lblSideVer.Location = new Point(DpiHelper.S(18), pnlSide.Height - DpiHelper.S(30));
			}
		}

		int pageX = DpiHelper.S(206);
		int pageY = DpiHelper.S(98);
		int pageW = Math.Max(DpiHelper.S(660), cw - pageX - DpiHelper.S(18));
		int pageH = Math.Max(DpiHelper.S(430), ch - pageY - DpiHelper.S(44));

		Panel[] pages = new Panel[] { pnlPlay, pnlSrv, pnlChat, pnlGal, pnlSet, pnlRcon, pnlMods };
		foreach (Panel p in pages)
		{
			if (p != null)
			{
				p.Top = pageY;
				p.Left = pageX;
				p.Size = new Size(pageW, pageH);
			}
		}

		if (lblStatus != null)
		{
			lblStatus.Location = new Point(pageX, ch - DpiHelper.S(30));
		}
		if (lblFooterRight != null)
		{
			lblFooterRight.Location = new Point(cw - lblFooterRight.Width - DpiHelper.S(24), ch - DpiHelper.S(30));
		}

		LayoutPlayPage(pageW, pageH);
		LayoutServersPage(pageW, pageH);
		LayoutToolsPages(pageW, pageH);
		LayoutModsPage(pageW, pageH);
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		DiscordRpc.Shutdown();
		core.StopPingSpark();
		base.OnFormClosing(e);
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		CancelActiveScan();
		if (scanPollTimer != null)
		{
			scanPollTimer.Dispose();
			scanPollTimer = null;
		}
		if (sparkPollTimer != null)
		{
			sparkPollTimer.Stop();
			sparkPollTimer.Dispose();
			sparkPollTimer = null;
		}
		StopPubAuto();
		StopPlaytime();
		StopWatch("ปิดแล้ว");
		core.CloseDb();
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

	private Panel MakePage()
	{
		Panel panel = new TransPanel();
		panel.Location = new Point(206, 98);
		panel.Size = new Size(756, 480);
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
		case 6:
			panel = pnlMods;
			break;
		case 5:
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
		default:
			panel = pnlPlay;
			break;
		}
		Panel target = panel;
		int baseX = DpiHelper.S(206);
		int baseY = DpiHelper.S(98);
		Panel[] array = new Panel[7] { pnlPlay, pnlSrv, pnlChat, pnlGal, pnlSet, pnlRcon, pnlMods };
		foreach (Panel panel2 in array)
		{
			if (panel2 != target)
			{
				panel2.Visible = false;
				panel2.Location = new Point(baseX, baseY);
			}
		}
		target.Visible = true;
		StyleNav(btnNavPlay, idx == 0);
		StyleNav(btnNavSrv, idx == 1);
		StyleNav(btnNavChat, idx == 2);
		StyleNav(btnNavGal, idx == 3);
		StyleNav(btnNavSet, idx == 4);
		StyleNav(btnNavRcon, idx == 5);
		StyleNav(btnNavMods, idx == 6);

		RoundedBtn activeBtn = (idx == 0) ? btnNavPlay :
			((idx == 1) ? btnNavSrv :
			((idx == 2) ? btnNavChat :
			((idx == 3) ? btnNavGal :
			((idx == 4) ? btnNavSet :
			((idx == 5) ? btnNavRcon : btnNavMods)))));
		if (activeBtn != null && navIndicator != null)
		{
			navIndicator.Top = activeBtn.Top + DpiHelper.S(6);
		}

		int slidePx = DpiHelper.S(24);
		target.Location = new Point(baseX + slidePx, baseY);
		Animate(190, delegate(float t)
		{
			if (!target.IsDisposed)
			{
				target.Location = new Point(baseX + (int)((float)slidePx * (1f - t)), baseY);
			}
		});
		if (idx == 0)
		{
			ResumePlaySpark();
		}
		else if (idx == 1)
		{
			ResumeSelectedServerSpark();
			if (viewMode == "mine")
			{
				RefreshAllServers(false);
			}
		}
		else
		{
			StopActiveSpark();
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
		if (idx == 6)
		{
			RefreshModsPage();
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

	private void Toast(string msg)
	{
		new MiniToast(this, msg, colAccent, 2800).Show();
	}

	private void ToastOk(string msg)
	{
		new MiniToast(this, msg, colOk, 3000).Show();
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

	private Label AddLbl(Control host, string text, Font f, int x, int y, Color c)
	{
		Label label = new Label();
		label.UseMnemonic = false;
		label.Text = (text ?? "").Replace("\uFE0F", "");
		label.Font = f;
		label.ForeColor = c;
		label.BackColor = Color.Transparent;
		label.Location = new Point(x, y);
		label.AutoSize = true;
		label.MouseDown += DragWindow;
		host.Controls.Add(label);
		if (c == colAccent)
		{
			accentLabels.Add(label);
		}
		return label;
	}

	private sealed class FieldState
	{
		public string Placeholder;
		public bool IsPlaceholder;
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
		FieldState st = new FieldState();
		st.Placeholder = placeholder ?? "";
		st.IsPlaceholder = true;
		tb.ForeColor = colSub;
		tb.Tag = st;
		tb.Text = st.Placeholder;
		tb.GotFocus += delegate
		{
			if (st.IsPlaceholder)
			{
				st.IsPlaceholder = false;
				tb.ForeColor = colText;
				tb.Text = "";
			}
			box.FocusBorder = true;
			box.Invalidate();
		};
		tb.LostFocus += delegate
		{
			if (!st.IsPlaceholder && tb.Text.Length == 0)
			{
				st.IsPlaceholder = true;
				tb.ForeColor = colSub;
				tb.Text = st.Placeholder;
			}
			box.FocusBorder = false;
			box.Invalidate();
		};
		box.Controls.Add(tb);
		host.Controls.Add(box);
		return tb;
	}

	private bool IsFieldPlaceholder(TextBox tb)
	{
		if (tb == null)
		{
			return true;
		}
		FieldState st = tb.Tag as FieldState;
		if (st != null)
		{
			return st.IsPlaceholder;
		}
		return tb.ForeColor == colSub;
	}

	private string FieldVal(TextBox tb)
	{
		if (IsFieldPlaceholder(tb))
		{
			return "";
		}
		return tb.Text.Trim();
	}

	private void ForceText(TextBox tb, string val)
	{
		if (tb == null)
		{
			return;
		}
		FieldState st = tb.Tag as FieldState;
		string next = val ?? "";
		if (next.Length == 0 && st != null && !tb.Focused)
		{
			st.IsPlaceholder = true;
			tb.ForeColor = colSub;
			tb.Text = st.Placeholder;
		}
		else
		{
			if (st != null)
			{
				st.IsPlaceholder = false;
			}
			tb.ForeColor = colText;
			tb.Text = next;
		}
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
		if (swAutoSlot != null)
		{
			swAutoSlot.OnColor = c;
		}
		if (swSoundFx != null)
		{
			swSoundFx.OnColor = c;
		}
		if (swDiscordRpc != null)
		{
			swDiscordRpc.OnColor = c;
		}
		if (swNoPauseAltTab != null)
		{
			swNoPauseAltTab.OnColor = c;
		}
		if (ring != null)
		{
			ring.RingColor = c;
			ring.Invalidate();
		}
		RenderBg();
		StyleNav(btnNavPlay, pnlPlay.Visible);
		StyleNav(btnNavSrv, pnlSrv.Visible);
		StyleNav(btnNavChat, pnlChat.Visible);
		StyleNav(btnNavGal, pnlGal.Visible);
		StyleNav(btnNavSet, pnlSet.Visible);
		StyleNav(btnNavRcon, pnlRcon.Visible);
		StyleNav(btnNavMods, pnlMods.Visible);
		if (heroSpark != null) { heroSpark.AccentColor = c; heroSpark.Invalidate(); }
		if (detSpark != null) { detSpark.AccentColor = c; detSpark.Invalidate(); }
		foreach (Label lbl in accentLabels)
		{
			if (lbl != null && !lbl.IsDisposed)
			{
				lbl.ForeColor = c;
			}
		}
		if (lblHeroIp != null) lblHeroIp.ForeColor = c;
		if (lblDetIp != null) lblDetIp.ForeColor = c;
		if (btnSetBrowse != null) btnSetBrowse.BorderColor = c;
		if (btnSetModFolder != null) btnSetModFolder.BorderColor = c;
		if (btnHealthRecheck != null) btnHealthRecheck.BorderColor = c;
		if (btnSetTestSound != null) btnSetTestSound.BorderColor = c;
		if (btnSrvAdd != null) btnSrvAdd.BorderColor = c;
		if (btnSrvInfo != null) btnSrvInfo.BorderColor = c;
		if (btnRconSend != null) btnRconSend.BorderColor = c;
		UpdateSkyGfxButtons();
		UpdateServerModeTabs();
		UpdateFilterChipsStyle();
		UpdateDetTabStyle();
		UpdateRefreshButtonState();
		RebuildChips();
		if (base.IsHandleCreated)
		{
			core.SetDwmAccent(base.Handle, c);
			StyleAllListViews();
		}
		if (lstSrv != null) lstSrv.Invalidate();
		if (lvDetList != null) lvDetList.Invalidate();
		Invalidate();
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

	private void SetStatus(string s)
	{
		lblStatus.Text = s;
	}

	private void ShowErr(string msg)
	{
		CyberAudio.PlayAlert();
		new MiniToast(this, "✖  " + msg, Color.FromArgb(255, 85, 105), 4200).Show();
		SetStatus("โปรดตรวจข้อมูล");
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
		const int WM_NCCALCSIZE = 0x0083;
		const int WM_NCHITTEST = 0x0084;
		const int WM_NCPAINT = 0x0085;
		const int WM_NCACTIVATE = 0x0086;
		const int WM_NCUAHDRAWCAPTION = 0x00AE;
		const int WM_NCUAHDRAWFRAME = 0x00AF;
		const int WM_COPYDATA = 0x004A;

		if (m.Msg == WM_NCACTIVATE)
		{
			m.Result = (IntPtr)1;
			return;
		}
		if (m.Msg == WM_NCPAINT || m.Msg == WM_NCUAHDRAWCAPTION || m.Msg == WM_NCUAHDRAWFRAME)
		{
			m.Result = IntPtr.Zero;
			return;
		}
		if (m.Msg == WM_NCCALCSIZE)
		{
			if (m.WParam != IntPtr.Zero && IsZoomed(base.Handle))
			{
				RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
				int cx = GetSystemMetrics(32) + GetSystemMetrics(92); // SM_CXSIZEFRAME + SM_CXPADDEDBORDER
				int cy = GetSystemMetrics(33) + GetSystemMetrics(92); // SM_CYSIZEFRAME + SM_CXPADDEDBORDER
				rc.Left += cx;
				rc.Top += cy;
				rc.Right -= cx;
				rc.Bottom -= cy;
				Marshal.StructureToPtr(rc, m.LParam, false);
			}
			m.Result = IntPtr.Zero;
			return;
		}

		if (m.Msg == WM_NCHITTEST)
		{
			base.WndProc(ref m);
			if (m.Result == (IntPtr)1) // HTCLIENT
			{
				int lp = m.LParam.ToInt32();
				Point screenPt = new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF));
				Point pt = PointToClient(screenPt);

				if (base.WindowState == FormWindowState.Normal)
				{
					int grip = DpiHelper.S(7);
					bool left = pt.X <= grip;
					bool right = pt.X >= base.ClientSize.Width - grip;
					bool top = pt.Y <= grip;
					bool bottom = pt.Y >= base.ClientSize.Height - grip;

					if (top && left) { m.Result = (IntPtr)13; return; }      // HTTOPLEFT
					if (top && right) { m.Result = (IntPtr)14; return; }     // HTTOPRIGHT
					if (bottom && left) { m.Result = (IntPtr)16; return; }   // HTBOTTOMLEFT
					if (bottom && right) { m.Result = (IntPtr)17; return; }  // HTBOTTOMRIGHT
					if (left) { m.Result = (IntPtr)10; return; }             // HTLEFT
					if (right) { m.Result = (IntPtr)11; return; }            // HTRIGHT
					if (top) { m.Result = (IntPtr)12; return; }              // HTTOP
					if (bottom) { m.Result = (IntPtr)15; return; }           // HTBOTTOM
				}

				if (pt.Y < DpiHelper.S(86) && pt.X < base.ClientSize.Width - DpiHelper.S(136))
				{
					m.Result = (IntPtr)2; // HTCAPTION (Aero Snap + Double-Click Maximize)
					return;
				}
			}
			return;
		}

		if (m.Msg == WM_COPYDATA)
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
		if (r.Width <= 0 || r.Height <= 0)
		{
			graphicsPath.AddRectangle(new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)));
			return graphicsPath;
		}
		int num = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
		if (num < 2)
		{
			graphicsPath.AddRectangle(r);
			return graphicsPath;
		}
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
			if (sparkPollTimer != null)
			{
				sparkPollTimer.Stop();
				sparkPollTimer.Dispose();
				sparkPollTimer = null;
			}
			StopServerTimers();
			if (bgCache != null) { bgCache.Dispose(); bgCache = null; }
			if (brCard != null) brCard.Dispose();
			if (brSel != null) brSel.Dispose();
			if (brHeader != null) brHeader.Dispose();
			if (brAccentBar != null) brAccentBar.Dispose();
			if (penHeaderLine != null) penHeaderLine.Dispose();
			if (brDotOff != null) brDotOff.Dispose();
			if (brPingG != null) brPingG.Dispose();
			if (brPingY != null) brPingY.Dispose();
			if (brPingR != null) brPingR.Dispose();
			if (brPillFav != null) brPillFav.Dispose();
			if (brPillPub != null) brPillPub.Dispose();
			if (brPillHist != null) brPillHist.Dispose();
			if (fTitle != null) fTitle.Dispose();
			if (fSub != null) fSub.Dispose();
			if (fField != null) fField.Dispose();
			if (fButton != null) fButton.Dispose();
			if (fSmall != null) fSmall.Dispose();
			if (fLogo != null) fLogo.Dispose();
			if (fLogoSub != null) fLogoSub.Dispose();
			if (fNav != null) fNav.Dispose();
			if (fStat != null) fStat.Dispose();
			if (fBadge != null) fBadge.Dispose();
		}
		base.Dispose(disposing);
	}
}
