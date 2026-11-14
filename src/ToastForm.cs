using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class ToastForm : Form
{
	private static readonly System.Collections.Generic.List<ToastForm> activeToasts = new System.Collections.Generic.List<ToastForm>();
	private Timer autoClose;
	private Timer fadeTimer;
	private Font fTitle;
	private Font fBody;
	private Font fBtnBold;
	private Font fBtnNorm;

	public event Action OnJoin;

	public ToastForm(string title, string body)
	{
		base.FormBorderStyle = FormBorderStyle.None;
		base.ShowInTaskbar = false;
		base.TopMost = true;
		base.StartPosition = FormStartPosition.Manual;
		BackColor = Color.FromArgb(17, 17, 24);
		fTitle = new Font("Segoe UI Semibold", 13f, FontStyle.Bold);
		fBody = new Font("Segoe UI", 9.5f);
		fBtnBold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
		fBtnNorm = new Font("Segoe UI", 9.5f);

		Label value = new Label
		{
			Text = "\ud83d\udd14  " + title,
			Font = fTitle,
			ForeColor = Color.FromArgb(90, 210, 130),
			BackColor = Color.Transparent,
			Location = new Point(20, 12),
			AutoSize = true
		};
		base.Controls.Add(value);
		Label value2 = new Label
		{
			Text = body,
			Font = fBody,
			ForeColor = Color.FromArgb(220, 220, 235),
			BackColor = Color.Transparent,
			Location = new Point(22, 46),
			Size = new Size(360, 44)
		};
		base.Controls.Add(value2);
		RoundedBtn roundedBtn = new RoundedBtn("▶   เข\u0e49าเกมเลย", Color.FromArgb(90, 210, 130), Color.FromArgb(120, 230, 160), Color.FromArgb(15, 25, 18), 10, fBtnBold)
		{
			Location = new Point(20, 92),
			Size = new Size(160, 34)
		};
		EventHandler value3 = delegate
		{
			Close();
			if (this.OnJoin != null)
			{
				this.OnJoin();
			}
		};
		roundedBtn.Click += value3;
		base.Controls.Add(roundedBtn);
		RoundedBtn roundedBtn2 = new RoundedBtn("ป\u0e34ด", Color.FromArgb(36, 36, 52), Color.FromArgb(48, 48, 70), Color.FromArgb(160, 160, 180), 10, fBtnNorm);
		roundedBtn2.Location = new Point(192, 92);
		roundedBtn2.Size = new Size(70, 34);
		roundedBtn2.Click += delegate
		{
			Close();
		};
		base.Controls.Add(roundedBtn2);
		DpiHelper.ScaleControlTree(this);
		base.Size = DpiHelper.Sz(400, 136);
		activeToasts.Add(this);
		int offsetY = 0;
		int gap = DpiHelper.S(10);
		for (int i = 0; i < activeToasts.Count - 1; i++)
		{
			if (!activeToasts[i].IsDisposed)
			{
				offsetY += activeToasts[i].Height + gap;
			}
		}
		Screen scr = Screen.FromPoint(Cursor.Position);
		Rectangle workingArea = (scr != null) ? scr.WorkingArea : Screen.PrimaryScreen.WorkingArea;
		int margin = DpiHelper.S(24);
		base.Location = new Point(workingArea.Right - base.Width - margin, workingArea.Bottom - base.Height - margin - offsetY);
		using (GraphicsPath gp = RoundRectP(new Rectangle(0, 0, base.Width, base.Height), DpiHelper.S(14)))
		{
			base.Region = new Region(gp);
		}
		autoClose = new Timer();
		autoClose.Interval = 9000;
		autoClose.Tick += delegate
		{
			Close();
		};
		autoClose.Start();
	}

	protected override void OnClosed(EventArgs e)
	{
		base.OnClosed(e);
		activeToasts.Remove(this);
	}

	protected override void OnShown(EventArgs e)
	{
		base.OnShown(e);
		base.Opacity = 0.0;
		fadeTimer = new Timer();
		fadeTimer.Interval = 15;
		Stopwatch swt = Stopwatch.StartNew();
		fadeTimer.Tick += delegate
		{
			if (base.IsDisposed || fadeTimer == null)
			{
				if (fadeTimer != null) { fadeTimer.Stop(); fadeTimer.Dispose(); fadeTimer = null; }
			}
			else
			{
				float num = Math.Min(1f, (float)swt.ElapsedMilliseconds / 220f);
				base.Opacity = num;
				if (num >= 1f)
				{
					fadeTimer.Stop();
					fadeTimer.Dispose();
					fadeTimer = null;
				}
			}
		};
		fadeTimer.Start();
	}

	private static GraphicsPath RoundRectP(Rectangle r, int rad)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		int num = Math.Min(rad * 2, Math.Min(Math.Max(1, r.Width), Math.Max(1, r.Height)));
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
			if (autoClose != null) { autoClose.Stop(); autoClose.Dispose(); autoClose = null; }
			if (fadeTimer != null) { fadeTimer.Stop(); fadeTimer.Dispose(); fadeTimer = null; }
			if (fTitle != null) { fTitle.Dispose(); fTitle = null; }
			if (fBody != null) { fBody.Dispose(); fBody = null; }
			if (fBtnBold != null) { fBtnBold.Dispose(); fBtnBold = null; }
			if (fBtnNorm != null) { fBtnNorm.Dispose(); fBtnNorm = null; }
			if (base.Region != null) { base.Region.Dispose(); base.Region = null; }
		}
		base.Dispose(disposing);
	}
}
