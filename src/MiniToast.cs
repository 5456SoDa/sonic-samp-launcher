using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class MiniToast : Form
{
	private static readonly List<MiniToast> activeToasts = new List<MiniToast>();

	private Color accentCol;
	private string messageText;
	private int durationMs;
	private Timer animTimer;
	private Stopwatch lifeSw;
	private bool closing;
	private float progress = 1.0f;
	private Font msgFont;

	protected override bool ShowWithoutActivation
	{
		get { return true; }
	}

	protected override CreateParams CreateParams
	{
		get
		{
			CreateParams cp = base.CreateParams;
			cp.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
			return cp;
		}
	}

	public MiniToast(string msg, Color accent)
		: this(null, msg, accent, 3000)
	{
	}

	public MiniToast(Form ownerForm, string msg, Color accent, int durationMilliseconds)
	{
		accentCol = accent;
		messageText = msg ?? "";
		durationMs = Math.Max(1500, durationMilliseconds);

		SetStyle(
			ControlStyles.UserPaint |
			ControlStyles.AllPaintingInWmPaint |
			ControlStyles.OptimizedDoubleBuffer,
			true);

		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		BackColor = Color.FromArgb(19, 27, 43);
		Cursor = Cursors.Hand;

		msgFont = new Font("Segoe UI", 9.2f);
		int maxTextW = DpiHelper.S(340);
		Size textSz = TextRenderer.MeasureText(
			messageText,
			msgFont,
			new Size(maxTextW, 0),
			TextFormatFlags.WordBreak);

		int w = Math.Max(DpiHelper.S(240), Math.Min(DpiHelper.S(420), textSz.Width + DpiHelper.S(54)));
		int h = Math.Max(DpiHelper.S(44), textSz.Height + DpiHelper.S(22));
		base.Size = new Size(w, h);
		base.Region = new Region(RoundRectP(new Rectangle(0, 0, w, h), DpiHelper.S(10)));

		activeToasts.Add(this);
		PositionToast(ownerForm);

		base.Click += delegate { BeginClose(); };

		lifeSw = Stopwatch.StartNew();
		animTimer = new Timer();
		animTimer.Interval = 25;
		animTimer.Tick += OnAnimTick;
		animTimer.Start();
	}

	private void PositionToast(Form ownerForm)
	{
		int offsetY = 0;
		int gap = DpiHelper.S(8);
		for (int i = 0; i < activeToasts.Count - 1; i++)
		{
			if (!activeToasts[i].IsDisposed)
			{
				offsetY += activeToasts[i].Height + gap;
			}
		}

		Rectangle area;
		if (ownerForm != null && !ownerForm.IsDisposed && ownerForm.Visible && ownerForm.WindowState != FormWindowState.Minimized)
		{
			area = ownerForm.Bounds;
			base.Location = new Point(
				area.Right - base.Width - DpiHelper.S(18),
				area.Bottom - base.Height - DpiHelper.S(44) - offsetY);
		}
		else
		{
			area = Screen.FromPoint(Cursor.Position).WorkingArea;
			base.Location = new Point(
				area.Right - base.Width - DpiHelper.S(24),
				area.Bottom - base.Height - DpiHelper.S(48) - offsetY);
		}
	}

	private void OnAnimTick(object sender, EventArgs e)
	{
		if (base.IsDisposed)
		{
			animTimer.Stop();
			return;
		}
		long ms = lifeSw.ElapsedMilliseconds;
		if (!closing)
		{
			if (ms < 160)
			{
				base.Opacity = Math.Min(0.96, ms / 160.0);
			}
			else
			{
				base.Opacity = 0.96;
			}
			progress = Math.Max(0f, 1.0f - (float)ms / (float)durationMs);
			Invalidate(new Rectangle(0, base.Height - DpiHelper.S(4), base.Width, DpiHelper.S(4)));
			if (ms >= durationMs)
			{
				BeginClose();
			}
		}
		else
		{
			double fade = 1.0 - (ms / 180.0);
			if (fade <= 0.0)
			{
				animTimer.Stop();
				Close();
			}
			else
			{
				base.Opacity = 0.96 * fade;
			}
		}
	}

	private void BeginClose()
	{
		if (!closing)
		{
			closing = true;
			lifeSw = Stopwatch.StartNew();
		}
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		base.Opacity = 0.0;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;
		int rad = DpiHelper.S(10);
		Rectangle r = new Rectangle(0, 0, base.Width - 1, base.Height - 1);

		using (GraphicsPath path = RoundRectP(r, rad))
		{
			using (SolidBrush brBg = new SolidBrush(Color.FromArgb(19, 27, 43)))
			{
				g.FillPath(brBg, path);
			}
			using (Pen pBorder = new Pen(Color.FromArgb(110, accentCol), 1.2f))
			{
				g.DrawPath(pBorder, path);
			}
		}

		// Left vertical indicator
		int barW = DpiHelper.S(4);
		int barPad = DpiHelper.S(8);
		using (SolidBrush brBar = new SolidBrush(accentCol))
		{
			g.FillRectangle(brBar, 0, barPad, barW, Math.Max(4, base.Height - barPad * 2));
		}

		// Bottom countdown line
		int progH = Math.Max(2, DpiHelper.S(2));
		int progW = (int)(base.Width * progress);
		if (progW > 0)
		{
			using (SolidBrush brProg = new SolidBrush(Color.FromArgb(180, accentCol)))
			{
				g.FillRectangle(brProg, 0, base.Height - progH, progW, progH);
			}
		}

		// Text
		int txtX = DpiHelper.S(16);
		Rectangle txtRect = new Rectangle(txtX, DpiHelper.S(4), base.Width - txtX - DpiHelper.S(12), base.Height - DpiHelper.S(10));
		TextRenderer.DrawText(g, messageText, msgFont, txtRect, Color.FromArgb(238, 244, 255),
			TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		activeToasts.Remove(this);
	}

	private static GraphicsPath RoundRectP(Rectangle r, int rad)
	{
		GraphicsPath path = new GraphicsPath();
		int d = Math.Min(rad * 2, Math.Min(Math.Max(2, r.Width), Math.Max(2, r.Height)));
		if (d < 2)
		{
			path.AddRectangle(r);
			return path;
		}
		path.AddArc(r.X, r.Y, d, d, 180f, 90f);
		path.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
		path.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
		path.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
		path.CloseFigure();
		return path;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			if (animTimer != null) animTimer.Dispose();
			if (msgFont != null) msgFont.Dispose();
		}
		base.Dispose(disposing);
	}
}
