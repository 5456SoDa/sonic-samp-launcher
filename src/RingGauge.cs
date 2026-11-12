using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class RingGauge : Control
{
	public float Ratio;

	public int Current = -1;

	public int Max = -1;

	public string Line1 = "--";

	public string Line2 = "ผ\u0e39\u0e49เล\u0e48น";

	public Color RingColor = Color.FromArgb(51, 204, 255);

	private static Font fR1 = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold);

	private static Font fR2 = new Font("Segoe UI", 9f);

	public RingGauge()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		SetStyle(ControlStyles.Selectable, false);
		base.Size = new Size(100, 100);
		BackColor = Color.Transparent;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		int pad = Math.Max(4, base.Width / 12);
		float stroke = Math.Max(4f, base.Width / 12.5f);
		Rectangle rect = new Rectangle(pad, pad, base.Width - pad * 2 - 1, base.Height - pad * 2 - 1);
		using (Pen pen = new Pen(Color.FromArgb(16, 23, 36), stroke))
		{
			graphics.DrawArc(pen, rect, -90f, 360f);
		}
		if (Ratio > 0f)
		{
			Color activeRing = (Ratio > 0.95f) ? Color.FromArgb(255, 235, 0) : ((Ratio > 0f) ? RingColor : Color.FromArgb(102, 255, 0));
			using (Pen pen2 = new Pen(activeRing, stroke))
			{
				graphics.DrawArc(pen2, rect, -90f, 360f * Math.Min(1f, Ratio));
			}
		}
		int half = base.Height / 2;
		int lh1 = Math.Max(18, base.Height / 4);
		int lh2 = Math.Max(16, base.Height / 5);
		TextRenderer.DrawText(graphics, Line1, fR1, new Rectangle(0, half - lh1, base.Width, lh1), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		TextRenderer.DrawText(graphics, Line2, fR2, new Rectangle(0, half + 1, base.Width, lh2), Color.FromArgb(140, 165, 195), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
	}
}
