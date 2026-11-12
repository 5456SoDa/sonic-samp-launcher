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
	private string bootUrl;
	private string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samp_launcher.cfg");
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

			using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, base.Width, base.Height), Color.FromArgb(11, 14, 20), Color.FromArgb(16, 23, 36), LinearGradientMode.Vertical))
			{
				graphics.FillRectangle(brush, 0, 0, base.Width, base.Height);
			}

			using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(9, 12, 17)))
			{
				graphics.FillRectangle(brush2, 0, base.Height - 50, base.Width, 50);
			}

			using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(24, colAccent)))
			{
				graphics.FillEllipse(brush3, base.Width - 320, -160, 440, 440);
			}
			using (SolidBrush brush4 = new SolidBrush(Color.FromArgb(14, colAccent)))
			{
				graphics.FillEllipse(brush4, -120, base.Height - 220, 360, 360);
			}

			using (Pen penGrid = new Pen(Color.FromArgb(18, colAccent), 1f))
			{
				for (int i = 0; i < 7; i++)
				{
					int offset = i * 32;
					graphics.DrawLine(penGrid, base.Width - 260 + offset, 0, base.Width, 260 - offset);
				}
			}

			string s1 = "SONIC ";
			string s2 = "SAMP ";
			string s3 = "LAUNCHER";
			SizeF sz1 = graphics.MeasureString(s1, fLogo);
			SizeF sz2 = graphics.MeasureString(s2, fLogo);
			SizeF sz3 = graphics.MeasureString(s3, fLogo);

			using (SolidBrush b1 = new SolidBrush(colAccent))
			using (SolidBrush b2 = new SolidBrush(colText))
			using (SolidBrush b3 = new SolidBrush(colAccentHi))
			{
				graphics.DrawString(s1, fLogo, b1, 200f, 16f);
				graphics.DrawString(s2, fLogo, b2, 200f + sz1.Width, 16f);
				graphics.DrawString(s3, fLogo, b3, 200f + sz1.Width + sz2.Width, 16f);
			}

			Rectangle rBadge = new Rectangle((int)(200f + sz1.Width + sz2.Width + sz3.Width + 12f), 22, 114, 22);
			using (SolidBrush bBadge = new SolidBrush(Color.FromArgb(30, colYellow)))
			using (SolidBrush bBadgeTxt = new SolidBrush(colYellow))
			using (Pen pBadge = new Pen(colYellow, 1f))
			{
				graphics.FillRectangle(bBadge, rBadge);
				graphics.DrawRectangle(pBadge, rBadge);
				graphics.DrawString("CYBER EDITION", fBadge, bBadgeTxt, rBadge.X + 8, rBadge.Y + 3);
			}

			using (SolidBrush bSub = new SolidBrush(colSub))
			{
				graphics.DrawString("HIGH-PERFORMANCE MULTIPLAYER CLIENT  •  SA-MP 0.3.7 R2", fLogoSub, bSub, 202f, 56f);
			}

			using (Pen pen = new Pen(Color.FromArgb(140, colAccent), 2f))
			{
				graphics.DrawLine(pen, 200, 86, base.Width - 26, 86);
			}

			graphics.Dispose();
			bgCache = image;
		}
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
		btnNavPlay = MakeNav("   ▶  เล่นเกม", 14);
		btnNavSrv = MakeNav("   ☰  เซิร์ฟเวอร์", 56);
		btnNavChat = MakeNav("   📜  บันทึกแชท", 98);
		btnNavGal = MakeNav("   🖼  แกลเลอรี", 140);
		btnNavSet = MakeNav("   ⚙  ตั้งค่า", 182);
		btnNavRcon = MakeNav("   🛠  RCON", 224);
		btnNavPlay.Click += delegate { ShowPage(0); };
		btnNavSrv.Click += delegate { ShowPage(1); };
		btnNavChat.Click += delegate { ShowPage(2); };
		btnNavGal.Click += delegate { ShowPage(3); };
		btnNavSet.Click += delegate { ShowPage(4); };
		btnNavRcon.Click += delegate { ShowPage(5); };
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
		lblStatus.Text = "พร้อมเชื่อมต่อ";
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

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		StopPubAuto();
		StopPlaytime();
		StopWatch("ปิดแล้ว");
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

	private void Toast(string msg)
	{
		new MiniToast(msg, colAccent).Show(this);
	}

	private void ToastOk(string msg)
	{
		new MiniToast(msg, colOk).Show(this);
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

	private void ForceText(TextBox tb, string val)
	{
		tb.ForeColor = colText;
		tb.Text = val;
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
		StyleNav(btnNavChat, pnlChat.Visible);
		StyleNav(btnNavGal, pnlGal.Visible);
		StyleNav(btnNavSet, pnlSet.Visible);
		StyleNav(btnNavRcon, pnlRcon.Visible);
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
		MessageBox.Show(this, msg, "SA-MP Launcher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
