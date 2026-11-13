using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class SparklineGraph : Control
{
	private PingSparkReport report;
	private Font fBig;
	private Font fSmall;

	public Color AccentColor = Color.FromArgb(51, 204, 255);
	public Color OkColor = Color.FromArgb(102, 255, 0);
	public Color WarnColor = Color.FromArgb(255, 235, 0);
	public Color ErrColor = Color.FromArgb(255, 85, 105);
	public bool CompactMode = false;

	public SparklineGraph()
	{
		SetStyle(
			ControlStyles.UserPaint |
			ControlStyles.AllPaintingInWmPaint |
			ControlStyles.OptimizedDoubleBuffer |
			ControlStyles.SupportsTransparentBackColor |
			ControlStyles.ResizeRedraw,
			true);
		SetStyle(ControlStyles.Selectable, false);
		BackColor = Color.Transparent;
		fBig = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold);
		fSmall = new Font("Segoe UI", 8.2f);
	}

	public void UpdateData(PingSparkReport rep)
	{
		report = rep;
		Invalidate();
	}

	public void ClearData()
	{
		report = null;
		Invalidate();
	}

	private Color LatencyColor(int ms)
	{
		if (ms < 0) return ErrColor;
		if (ms <= 90) return AccentColor;
		if (ms <= 170) return WarnColor;
		return ErrColor;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics g = e.Graphics;
		g.SmoothingMode = SmoothingMode.AntiAlias;

		int w = Math.Max(20, base.Width - 1);
		int h = Math.Max(20, base.Height - 1);
		int rad = DpiHelper.S(8);
		Rectangle box = new Rectangle(0, 0, w, h);

		// Subtle glass container
		using (GraphicsPath bgPath = RoundRect(box, rad))
		{
			using (SolidBrush brBg = new SolidBrush(Color.FromArgb(215, 14, 20, 34)))
			{
				g.FillPath(brBg, bgPath);
			}
			using (Pen penBorder = new Pen(Color.FromArgb(40, 60, 95), 1f))
			{
				g.DrawPath(penBorder, bgPath);
			}
		}

		int padX = DpiHelper.S(8);
		int headerH = CompactMode ? DpiHelper.S(18) : DpiHelper.S(22);
		int footerH = CompactMode ? DpiHelper.S(4) : DpiHelper.S(20);
		Rectangle plot = new Rectangle(
			padX,
			headerH + DpiHelper.S(4),
			Math.Max(10, w - padX * 2),
			Math.Max(10, h - headerH - footerH - DpiHelper.S(6)));

		if (report == null || report.History == null || report.History.Count == 0)
		{
			string waitTxt = CompactMode ? "กำลังวัดคลื่นปิง..." : "กำลังเก็บสถิติคลื่นปิง (1.5 วิ)...";
			TextRenderer.DrawText(g, waitTxt, fSmall, box, Color.FromArgb(130, 155, 185),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			return;
		}

		// Header: Label + Live ms badge
		int latest = report.Latest;
		Color waveCol = LatencyColor(latest);
		string titleTxt = CompactMode ? "PING WAVE" : "LIVE LATENCY WAVE";
		TextRenderer.DrawText(g, titleTxt, fSmall, new Rectangle(padX, DpiHelper.S(4), w / 2, headerH),
			Color.FromArgb(140, 165, 195), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

		string msTxt = (latest >= 0) ? (latest + " ms") : "TIMEOUT";
		TextRenderer.DrawText(g, msTxt, CompactMode ? fSmall : fBig,
			new Rectangle(w / 2, DpiHelper.S(3), w / 2 - padX, headerH),
			waveCol, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

		// Subtle horizontal grid line in plot area
		using (Pen pGrid = new Pen(Color.FromArgb(25, 140, 165, 195), 1f))
		{
			int midY = plot.Top + plot.Height / 2;
			g.DrawLine(pGrid, plot.Left, midY, plot.Right, midY);
		}

		List<int> hist = report.History;
		List<int> validVals = new List<int>();
		for (int i = 0; i < hist.Count; i++)
		{
			if (hist[i] >= 0) validVals.Add(hist[i]);
		}

		if (validVals.Count > 0)
		{
			int minV = int.MaxValue;
			int maxV = int.MinValue;
			foreach (int v in validVals)
			{
				if (v < minV) minV = v;
				if (v > maxV) maxV = v;
			}
			// Keep at least a 20ms range so flat lines stay centered gracefully
			int lo = Math.Max(0, minV - 5);
			int hi = Math.Max(lo + 20, maxV + 8);
			float range = Math.Max(1f, (float)(hi - lo));

			// Average line
			if (report.Avg > 0)
			{
				float avgNorm = (report.Avg - lo) / range;
				int avgY = plot.Bottom - (int)(avgNorm * plot.Height);
				avgY = Math.Max(plot.Top, Math.Min(plot.Bottom, avgY));
				using (Pen pAvg = new Pen(Color.FromArgb(80, 140, 165, 195), 1f))
				{
					pAvg.DashStyle = DashStyle.Dash;
					g.DrawLine(pAvg, plot.Left, avgY, plot.Right, avgY);
				}
			}

			// Build contiguous valid segments across sample window so packet drops render visible breaks
			int slots = Math.Max(hist.Count, 2);
			float stepX = (float)plot.Width / Math.Max(1, slots - 1);
			List<List<PointF>> segments = new List<List<PointF>>();
			List<PointF> curSeg = new List<PointF>();
			for (int i = 0; i < hist.Count; i++)
			{
				float x = plot.Left + i * stepX;
				int val = hist[i];
				if (val < 0)
				{
					if (curSeg.Count > 0)
					{
						segments.Add(curSeg);
						curSeg = new List<PointF>();
					}
					using (Pen pStripe = new Pen(Color.FromArgb(35, ErrColor), 1f))
					{
						pStripe.DashStyle = DashStyle.Dot;
						g.DrawLine(pStripe, x, plot.Top, x, plot.Bottom);
					}
					using (Pen pDrop = new Pen(Color.FromArgb(195, ErrColor), DpiHelper.Sf(1.8f)))
					{
						g.DrawLine(pDrop, x, plot.Bottom - DpiHelper.S(6), x, plot.Bottom);
					}
					continue;
				}
				float norm = (val - lo) / range;
				float y = plot.Bottom - norm * plot.Height;
				y = Math.Max(plot.Top + 2, Math.Min(plot.Bottom - 2, y));
				curSeg.Add(new PointF(x, y));
			}
			if (curSeg.Count > 0)
			{
				segments.Add(curSeg);
			}

			PointF lastValidPt = PointF.Empty;
			bool hasLastValid = false;
			foreach (List<PointF> seg in segments)
			{
				if (seg.Count == 1)
				{
					PointF p0 = seg[0];
					if (hist.Count == 1)
					{
						PointF p1 = new PointF(plot.Right, p0.Y);
						DrawWaveSegment(g, plot, new PointF[] { p0, p1 }, waveCol);
						lastValidPt = p1;
					}
					else
					{
						float rIso = DpiHelper.Sf(3.0f);
						using (SolidBrush brIso = new SolidBrush(Color.FromArgb(200, waveCol)))
						{
							g.FillEllipse(brIso, p0.X - rIso, p0.Y - rIso, rIso * 2, rIso * 2);
						}
						lastValidPt = p0;
					}
					hasLastValid = true;
				}
				else if (seg.Count >= 2)
				{
					PointF[] arr = seg.ToArray();
					DrawWaveSegment(g, plot, arr, waveCol);
					lastValidPt = arr[arr.Length - 1];
					hasLastValid = true;
				}
			}

			if (latest < 0)
			{
				float dropX = plot.Left + Math.Max(0, hist.Count - 1) * stepX;
				float dropY = plot.Bottom - DpiHelper.Sf(4f);
				float rHalo = DpiHelper.Sf(5f);
				float rDot = DpiHelper.Sf(2.6f);
				using (SolidBrush brErrHalo = new SolidBrush(Color.FromArgb(95, ErrColor)))
				{
					g.FillEllipse(brErrHalo, dropX - rHalo, dropY - rHalo, rHalo * 2, rHalo * 2);
				}
				using (SolidBrush brErrDot = new SolidBrush(ErrColor))
				{
					g.FillEllipse(brErrDot, dropX - rDot, dropY - rDot, rDot * 2, rDot * 2);
				}
			}
			else if (hasLastValid)
			{
				float rHalo = DpiHelper.Sf(5f);
				float rDot = DpiHelper.Sf(2.8f);
				using (SolidBrush brHalo = new SolidBrush(Color.FromArgb(85, waveCol)))
				{
					g.FillEllipse(brHalo, lastValidPt.X - rHalo, lastValidPt.Y - rHalo, rHalo * 2, rHalo * 2);
				}
				using (SolidBrush brDot = new SolidBrush(Color.White))
				{
					g.FillEllipse(brDot, lastValidPt.X - rDot, lastValidPt.Y - rDot, rDot * 2, rDot * 2);
				}
			}
		}
		else
		{
			// All samples timed out -> draw baseline packet loss markers + clear overlay
			int slots = Math.Max(hist.Count, 2);
			float stepX = (float)plot.Width / Math.Max(1, slots - 1);
			using (Pen pDrop = new Pen(Color.FromArgb(150, ErrColor), DpiHelper.Sf(1.5f)))
			{
				for (int i = 0; i < hist.Count; i++)
				{
					float x = plot.Left + i * stepX;
					g.DrawLine(pDrop, x, plot.Bottom - DpiHelper.S(5), x, plot.Bottom);
				}
			}
			string offTxt = CompactMode ? "NO RESPONSE" : "NO RESPONSE / PACKET LOSS 100%";
			TextRenderer.DrawText(g, offTxt, fSmall, plot, Color.FromArgb(220, ErrColor),
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
		}

		// Footer stats (Full Mode)
		if (!CompactMode)
		{
			string statTxt = string.Format("Avg: {0}ms  •  Jitter: {1:0.0}ms  •  Loss: {2:0.0}%",
				report.Avg, report.Jitter, report.Loss);
			Color stCol = (report.Loss > 5.0) ? ErrColor : ((report.Jitter > 25.0) ? WarnColor : Color.FromArgb(145, 175, 205));
			TextRenderer.DrawText(g, statTxt, fSmall,
				new Rectangle(padX, h - footerH - DpiHelper.S(2), w - padX * 2, footerH),
				stCol, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
		}
	}

	private static void DrawWaveSegment(Graphics g, Rectangle plot, PointF[] arr, Color waveCol)
	{
		using (GraphicsPath wavePath = new GraphicsPath())
		using (GraphicsPath fillPath = new GraphicsPath())
		{
			if (arr.Length == 2)
			{
				wavePath.AddLine(arr[0], arr[1]);
				fillPath.AddLine(arr[0], arr[1]);
			}
			else
			{
				wavePath.AddCurve(arr, 0.35f);
				fillPath.AddCurve(arr, 0.35f);
			}

			fillPath.AddLine(arr[arr.Length - 1].X, plot.Bottom, arr[0].X, plot.Bottom);
			fillPath.CloseFigure();

			using (LinearGradientBrush lgb = new LinearGradientBrush(
				new Rectangle(plot.Left, plot.Top - 2, plot.Width, plot.Height + 4),
				Color.FromArgb(85, waveCol),
				Color.FromArgb(4, waveCol),
				LinearGradientMode.Vertical))
			{
				g.FillPath(lgb, fillPath);
			}

			using (Pen penGlow = new Pen(Color.FromArgb(70, waveCol), DpiHelper.Sf(4.2f)))
			{
				penGlow.LineJoin = LineJoin.Round;
				g.DrawPath(penGlow, wavePath);
			}
			using (Pen penWave = new Pen(waveCol, DpiHelper.Sf(1.9f)))
			{
				penWave.LineJoin = LineJoin.Round;
				g.DrawPath(penWave, wavePath);
			}
		}
	}

	private static GraphicsPath RoundRect(Rectangle r, int rad)
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
			if (fBig != null) fBig.Dispose();
			if (fSmall != null) fSmall.Dispose();
		}
		base.Dispose(disposing);
	}
}
