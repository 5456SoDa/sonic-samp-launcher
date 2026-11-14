using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class ToggleSwitch : Control
{
	private bool on;

	private float pos;

	private float startPos;

	private Timer tmr;

	private Stopwatch swt;

	public Color OnColor = Color.FromArgb(51, 204, 255);

	public bool Checked
	{
		get
		{
			return on;
		}
		set
		{
			if (on != value)
			{
				on = value;
				if (!IsHandleCreated)
				{
					pos = on ? 1f : 0f;
					Invalidate();
				}
				else
				{
					StartAnim();
				}
				if (this.CheckedChanged != null)
				{
					this.CheckedChanged(this, EventArgs.Empty);
				}
			}
		}
	}

	public void SetCheckedSilent(bool value)
	{
		on = value;
		pos = value ? 1f : 0f;
		startPos = pos;
		if (tmr != null)
		{
			tmr.Stop();
		}
		Invalidate();
	}

	public event EventHandler CheckedChanged;

	public ToggleSwitch()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		SetStyle(ControlStyles.Selectable, false);
		base.Size = new Size(46, 22);
		BackColor = Color.Transparent;
		Cursor = Cursors.Hand;
		tmr = new Timer();
		tmr.Interval = 15;
		tmr.Tick += Tick;
	}

	private void StartAnim()
	{
		if (tmr == null)
		{
			pos = on ? 1f : 0f;
			Invalidate();
			return;
		}
		startPos = pos;
		swt = Stopwatch.StartNew();
		tmr.Stop();
		tmr.Start();
	}

	private void Tick(object s, EventArgs e)
	{
		if (base.IsDisposed || tmr == null)
		{
			if (tmr != null)
			{
				tmr.Stop();
				tmr.Dispose();
				tmr = null;
			}
			return;
		}
		float num = Math.Min(1f, (float)swt.ElapsedMilliseconds / 150f);
		float target = on ? 1f : 0f;
		pos = startPos + (target - startPos) * Anim.EaseOut(num);
		Invalidate();
		if (num >= 1f)
		{
			pos = target;
			tmr.Stop();
		}
	}

	protected override void OnClick(EventArgs e)
	{
		CyberAudio.PlayClick();
		base.OnClick(e);
		Checked = !Checked;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		float num = Math.Max(0f, Math.Min(1f, pos));
		Color color = Anim.Lerp(Color.FromArgb(16, 23, 36), OnColor, num);
		using (SolidBrush brush = new SolidBrush(color))
		{
			using (GraphicsPath graphicsPath = new GraphicsPath())
			{
				int num2 = base.Height - 1;
				graphicsPath.AddArc(0, 0, num2, num2, 180f, 90f);
				graphicsPath.AddArc(base.Width - 1 - num2, 0, num2, num2, 270f, 90f);
				graphicsPath.AddArc(base.Width - 1 - num2, base.Height - 1 - num2, num2, num2, 0f, 90f);
				graphicsPath.AddArc(0, base.Height - 1 - num2, num2, num2, 90f, 90f);
				graphicsPath.CloseFigure();
				graphics.FillPath(brush, graphicsPath);
				using (Pen p = new Pen(Color.FromArgb(40, 60, 95), 1f))
				{
					graphics.DrawPath(p, graphicsPath);
				}
			}
		}
		int pad = Math.Max(2, base.Height / 7);
		int knob = Math.Max(6, base.Height - pad * 2);
		int travel = Math.Max(0, base.Width - knob - pad * 2);
		int kx = pad + (int)(travel * num);
		using (SolidBrush brush2 = new SolidBrush(Color.White))
		{
			graphics.FillEllipse(brush2, kx, pad, knob, knob);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && tmr != null)
		{
			tmr.Stop();
			tmr.Dispose();
			tmr = null;
		}
		base.Dispose(disposing);
	}
}
