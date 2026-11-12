using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class MiniToast : Form
{
	private static int stackCount;

	private int mySlot;

	private Timer life;

	public MiniToast(string msg, Color accent)
	{
		MiniToast miniToast = this;
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		base.Size = new Size(TextRenderer.MeasureText(msg, new Font("Segoe UI", 9f)).Width + 46, 44);
		mySlot = stackCount++;
		Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
		base.Location = new Point(workingArea.Right - base.Width - 24, workingArea.Bottom - base.Height - 58 - mySlot * (base.Height + 8));
		BackColor = Color.FromArgb(26, 26, 38);
		base.Region = new Region(RoundRectP(new Rectangle(0, 0, base.Width, base.Height), 10));
		Label value = new Label
		{
			Text = msg,
			Font = new Font("Segoe UI", 9f),
			ForeColor = Color.FromArgb(230, 230, 245),
			BackColor = Color.Transparent,
			Location = new Point(18, 0),
			Size = new Size(base.Width - 22, base.Height),
			TextAlign = ContentAlignment.MiddleLeft
		};
		base.Controls.Add(value);
		PaintEventHandler value2 = delegate(object s, PaintEventArgs e2)
		{
			e2.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush brush = new SolidBrush(accent))
			{
				e2.Graphics.FillRectangle(brush, 0, 8, 3, miniToast.Height - 16);
			}
		};
		base.Paint += value2;
		life = new Timer();
		life.Interval = 2200;
		life.Tick += delegate
		{
			FadeClose();
		};
		life.Start();
	}

	private void FadeClose()
	{
		life.Stop();
		Timer ft = new Timer();
		ft.Interval = 15;
		Stopwatch swt = Stopwatch.StartNew();
		ft.Tick += delegate
		{
			if (base.IsDisposed)
			{
				ft.Stop();
				ft.Dispose();
			}
			else
			{
				float num = Math.Min(1f, (float)swt.ElapsedMilliseconds / 200f);
				base.Opacity = 1f - num;
				if (num >= 1f)
				{
					ft.Stop();
					ft.Dispose();
					Close();
				}
			}
		};
		ft.Start();
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		base.Opacity = 0.0;
		Timer ft = new Timer();
		ft.Interval = 15;
		Stopwatch swt = Stopwatch.StartNew();
		ft.Tick += delegate
		{
			if (base.IsDisposed)
			{
				ft.Stop();
				ft.Dispose();
			}
			else
			{
				float num = Math.Min(1f, (float)swt.ElapsedMilliseconds / 180f);
				base.Opacity = num;
				if (num >= 1f)
				{
					ft.Stop();
					ft.Dispose();
				}
			}
		};
		ft.Start();
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		stackCount--;
		if (stackCount < 0)
		{
			stackCount = 0;
		}
	}

	private static GraphicsPath RoundRectP(Rectangle r, int rad)
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
		if (disposing && life != null)
		{
			life.Dispose();
		}
		base.Dispose(disposing);
	}
}
