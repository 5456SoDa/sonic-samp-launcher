using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

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
				if (BorderColor != Color.Empty && BorderColor != Color.Transparent)
				{
					using (Pen p = new Pen(BorderColor, 1.2f))
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
		TextFormatFlags flags = (TextFormatFlags)(((!alignLeft) ? 1u : 0u) | 4u | 0x20);
		if (alignLeft)
		{
			bounds = new Rectangle(14, 0, base.Width - 18, base.Height);
		}
		TextRenderer.DrawText(graphics, Text, Font, bounds, foreColor, flags);
	}

	private GraphicsPath Rp(Rectangle r)
	{
		int num = Radius * 2;
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
		if (disposing && tmr != null)
		{
			tmr.Dispose();
		}
		base.Dispose(disposing);
	}
}
