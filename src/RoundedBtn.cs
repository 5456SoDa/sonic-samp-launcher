using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal enum BtnIcon
{
	None,
	Refresh,
	StatsBar,
	CopyDual,
	StarFilled,
	StarOutline
}

internal class RoundedBtn : Control, IButtonControl
{
	private Color cBase;

	private Color cHover;

	private Color cPress;

	private Color cText;

	private Color cCur;

	private bool hovered;

	private bool pressed;

	private Timer tmr;

	private Color fromC;

	private Color toC;

	private Stopwatch sw;

	private int durMs = 130;

	private bool alignLeft;

	public int Radius = 10;

	public Color BorderColor { get; set; }

	private bool pulseGlow;
	private Timer pulseTmr;
	private float pulsePhase;
	private BtnIcon iconGlyph = BtnIcon.None;

	public BtnIcon IconGlyph
	{
		get { return iconGlyph; }
		set
		{
			if (iconGlyph != value)
			{
				iconGlyph = value;
				Invalidate();
			}
		}
	}

	public bool PulseGlow
	{
		get { return pulseGlow; }
		set
		{
			if (pulseGlow == value) return;
			pulseGlow = value;
			if (pulseGlow)
			{
				if (pulseTmr == null)
				{
					pulseTmr = new Timer();
					pulseTmr.Interval = 45;
					pulseTmr.Tick += delegate
					{
						if (!IsDisposed && Visible && pulseGlow)
						{
							pulsePhase += 0.14f;
							if (pulsePhase > 6.28318f) pulsePhase -= 6.28318f;
							Invalidate();
						}
					};
				}
				pulseTmr.Start();
			}
			else if (pulseTmr != null)
			{
				pulseTmr.Stop();
			}
			Invalidate();
		}
	}

	public Color TextColor
	{
		get
		{
			return cText;
		}
		set
		{
			cText = value;
			Invalidate();
		}
	}

	public bool AlignLeft
	{
		get
		{
			return alignLeft;
		}
		set
		{
			alignLeft = value;
			Invalidate();
		}
	}

	public DialogResult DialogResult { get; set; }

	public void NotifyDefault(bool value)
	{
	}

	public void PerformClick()
	{
		OnClick(EventArgs.Empty);
	}

	public RoundedBtn(string text, Color baseC, Color hoverC, Color textC, int rad, Font f)
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		SetStyle(ControlStyles.Selectable, false);
		Text = text;
		Font = f;
		cBase = baseC;
		cHover = hoverC;
		cPress = ControlPaint.Dark(hoverC, 0.08f);
		cText = textC;
		cCur = baseC;
		Radius = rad;
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		base.TabStop = false;
		tmr = new Timer();
		tmr.Interval = 15;
		tmr.Tick += TmrTick;
	}

	private void Go(Color target, int milliseconds)
	{
		fromC = cCur;
		toC = target;
		durMs = milliseconds;
		sw = Stopwatch.StartNew();
		tmr.Stop();
		tmr.Start();
	}

	private void TmrTick(object s, EventArgs e)
	{
		float num = Math.Min(1f, (float)sw.ElapsedMilliseconds / (float)durMs);
		cCur = Anim.Lerp(fromC, toC, Anim.EaseOut(num));
		Invalidate();
		if (num >= 1f)
		{
			tmr.Stop();
		}
	}

	public void SetPalette(Color baseC, Color hoverC)
	{
		cBase = baseC;
		cHover = hoverC;
		cPress = ControlPaint.Dark(hoverC, 0.08f);
		Go((!base.Enabled) ? cCur : (hovered ? cHover : cBase), 150);
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		hovered = true;
		if (base.Enabled)
		{
			Go(cHover, 130);
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		hovered = false;
		pressed = false;
		if (base.Enabled)
		{
			Go(cBase, 170);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.Button == MouseButtons.Left && base.Enabled)
		{
			pressed = true;
			tmr.Stop();
			cCur = cPress;
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		pressed = false;
		if (hovered && base.Enabled)
		{
			Go(cHover, 90);
		}
		Invalidate();
	}

	protected override void OnClick(EventArgs e)
	{
		CyberAudio.PlayClick();
		base.OnClick(e);
	}

	protected override void OnEnabledChanged(EventArgs e)
	{
		base.OnEnabledChanged(e);
		if (!base.Enabled)
		{
			tmr.Stop();
			cCur = Color.FromArgb(30, 30, 44);
		}
		else
		{
			cCur = (hovered ? cHover : cBase);
		}
		Invalidate();
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		Rectangle r = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
		using (SolidBrush brush = new SolidBrush(cCur))
		{
			using (GraphicsPath path = Rp(r))
			{
				graphics.FillPath(brush, path);
				if (pulseGlow && base.Enabled)
				{
					int alpha = 75 + (int)(Math.Sin(pulsePhase) * 55.0 + 55.0);
					Color glowCol = Color.FromArgb(Math.Max(20, Math.Min(220, alpha)),
						BorderColor != Color.Empty ? BorderColor : Color.FromArgb(255, 235, 0));
					using (Pen gp = new Pen(glowCol, 2.2f))
					{
						graphics.DrawPath(gp, path);
					}
				}
				else if (BorderColor != Color.Empty && BorderColor != Color.Transparent)
				{
					int alpha = hovered ? 255 : 190;
					Color bc = Color.FromArgb(alpha, BorderColor);
					using (Pen p = new Pen(bc, hovered ? 1.5f : 1.15f))
					{
						graphics.DrawPath(p, path);
					}
				}
			}
		}
		Color foreColor = (base.Enabled ? cText : Color.FromArgb(105, 105, 125));
		Rectangle bounds = base.ClientRectangle;
		if (pressed)
		{
			bounds.Offset(0, 1);
		}
		if (iconGlyph != BtnIcon.None)
		{
			DrawVectorIcon(graphics, bounds, foreColor);
		}
		else
		{
			TextFormatFlags flags = (alignLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter)
				| TextFormatFlags.VerticalCenter
				| TextFormatFlags.EndEllipsis
				| TextFormatFlags.NoPrefix
				| TextFormatFlags.NoPadding;
			if (alignLeft)
			{
				int leftPad = DpiHelper.S(14);
				bounds = new Rectangle(leftPad, 0, Math.Max(10, base.Width - leftPad - DpiHelper.S(4)), base.Height);
			}
			string cleanText = (Text ?? "").Replace("\uFE0F", "");
			TextRenderer.DrawText(graphics, cleanText, Font, bounds, foreColor, flags);
		}
	}

	private void DrawVectorIcon(Graphics g, Rectangle bounds, Color foreColor)
	{
		float sc = Math.Max(0.7f, Math.Min(bounds.Width, bounds.Height) / 26.0f);
		float cx = bounds.X + bounds.Width * 0.5f;
		float cy = bounds.Y + bounds.Height * 0.5f;
		float stroke = Math.Max(1.35f, 1.65f * sc);

		if (iconGlyph == BtnIcon.Refresh)
		{
			float r = 5.6f * sc;
			using (Pen p = new Pen(foreColor, stroke))
			{
				p.StartCap = LineCap.Round;
				p.EndCap = LineCap.Round;
				g.DrawArc(p, cx - r, cy - r, r * 2f, r * 2f, -45f, 275f);
			}
			float ax = cx + r * 0.72f;
			float ay = cy - r * 0.70f;
			PointF[] tri = new PointF[3]
			{
				new PointF(ax + 0.5f * sc, ay + 1.8f * sc),
				new PointF(ax + 2.2f * sc, ay - 2.6f * sc),
				new PointF(ax - 2.3f * sc, ay - 1.2f * sc)
			};
			using (SolidBrush br = new SolidBrush(foreColor))
			{
				g.FillPolygon(br, tri);
			}
		}
		else if (iconGlyph == BtnIcon.StatsBar)
		{
			float bw = Math.Max(2f, 2.3f * sc);
			float gap = 1.8f * sc;
			float startX = cx - (bw * 3f + gap * 2f) * 0.5f;
			float baseY = cy + 5.6f * sc;
			float[] hs = new float[3] { 5.2f * sc, 8.6f * sc, 11.4f * sc };
			using (SolidBrush br = new SolidBrush(foreColor))
			{
				for (int i = 0; i < 3; i++)
				{
					float bx = startX + i * (bw + gap);
					g.FillRectangle(br, bx, baseY - hs[i], bw, hs[i]);
				}
				g.FillRectangle(br, startX - 1f * sc, baseY + 0.5f * sc, bw * 3f + gap * 2f + 2f * sc, Math.Max(1f, 1.2f * sc));
			}
		}
		else if (iconGlyph == BtnIcon.CopyDual)
		{
			float w = 7.4f * sc;
			float h = 9.0f * sc;
			RectangleF rBack = new RectangleF(cx - w * 0.5f - 2.0f * sc, cy - h * 0.5f - 2.0f * sc, w, h);
			RectangleF rFront = new RectangleF(cx - w * 0.5f + 2.0f * sc, cy - h * 0.5f + 2.0f * sc, w, h);
			using (Pen p = new Pen(foreColor, Math.Max(1.25f, 1.45f * sc)))
			using (SolidBrush bg = new SolidBrush(cCur))
			{
				g.DrawRectangle(p, rBack.X, rBack.Y, rBack.Width, rBack.Height);
				g.FillRectangle(bg, rFront);
				g.DrawRectangle(p, rFront.X, rFront.Y, rFront.Width, rFront.Height);
			}
		}
		else if (iconGlyph == BtnIcon.StarFilled || iconGlyph == BtnIcon.StarOutline)
		{
			PointF[] pts = new PointF[10];
			float outerR = 6.7f * sc;
			float innerR = 2.8f * sc;
			for (int i = 0; i < 10; i++)
			{
				double ang = -Math.PI * 0.5 + i * (Math.PI / 5.0);
				float rad = (i % 2 == 0) ? outerR : innerR;
				pts[i] = new PointF(cx + (float)(Math.Cos(ang) * rad), cy + (float)(Math.Sin(ang) * rad));
			}
			if (iconGlyph == BtnIcon.StarFilled)
			{
				using (SolidBrush br = new SolidBrush(foreColor))
				{
					g.FillPolygon(br, pts);
				}
			}
			else
			{
				using (Pen p = new Pen(foreColor, Math.Max(1.25f, 1.4f * sc)))
				{
					p.LineJoin = LineJoin.Round;
					g.DrawPolygon(p, pts);
				}
			}
		}
	}

	private GraphicsPath Rp(Rectangle r)
	{
		int num = Math.Min(Radius * 2, Math.Min(Math.Max(2, r.Width), Math.Max(2, r.Height)));
		GraphicsPath graphicsPath = new GraphicsPath();
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
			if (tmr != null) tmr.Dispose();
			if (pulseTmr != null) pulseTmr.Dispose();
		}
		base.Dispose(disposing);
	}
}
