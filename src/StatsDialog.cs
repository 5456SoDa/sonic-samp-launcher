using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class StatsDialog : Form
{
	private List<int> vals;

	private string title;

	private Font fChartLabel = new Font("Segoe UI", 8.5f);

	public StatsDialog(string titleText, List<int> v, string extra)
	{
		vals = v;
		title = titleText;
		Text = "สถิติเซิร์ฟเวอร์";
		BackColor = Color.FromArgb(17, 17, 24);
		base.StartPosition = FormStartPosition.CenterParent;
		base.ClientSize = DpiHelper.Sz(564, 408);
		base.ShowInTaskbar = false;
		base.FormBorderStyle = FormBorderStyle.FixedDialog;
		base.MaximizeBox = false;
		base.MinimizeBox = false;

		base.FormClosed += delegate
		{
			if (fChartLabel != null)
			{
				fChartLabel.Dispose();
				fChartLabel = null;
			}
		};

		Label value = new Label
		{
			Text = "📊 " + title,
			Font = new Font("Segoe UI Semibold", 12.5f, FontStyle.Bold),
			ForeColor = Color.FromArgb(51, 204, 255),
			BackColor = Color.Transparent,
			Location = new Point(20, 12),
			AutoSize = true
		};
		base.Controls.Add(value);
		if (extra != null && extra.Length > 0)
		{
			Label value2 = new Label
			{
				Text = "⏱ " + extra,
				Font = new Font("Segoe UI Semibold", 9f),
				ForeColor = Color.FromArgb(90, 210, 130),
				BackColor = Color.Transparent,
				Location = new Point(20, 60),
				AutoSize = true
			};
			base.Controls.Add(value2);
		}
		if (vals == null)
		{
			vals = new List<int>();
		}
		int num = (vals.Count > 0) ? vals[0] : 0;
		int num2 = (vals.Count > 0) ? vals[0] : 0;
		long num3 = 0L;
		foreach (int val in vals)
		{
			if (val > num)
			{
				num = val;
			}
			if (val < num2)
			{
				num2 = val;
			}
			num3 += val;
		}
		int num4 = (vals.Count > 0) ? (int)(num3 / vals.Count) : 0;
		int lastVal = (vals.Count > 0) ? vals[vals.Count - 1] : 0;
		Label value3 = new Label
		{
			Text = "ล่าสุด " + lastVal + "  •  สูงสุด " + num + "  •  ต่ำสุด " + num2 + "  •  เฉลี่ย " + num4 + "  •  (" + vals.Count + " ตัวอย่าง)",
			Font = new Font("Segoe UI", 9f),
			ForeColor = Color.FromArgb(140, 140, 165),
			BackColor = Color.Transparent,
			Location = new Point(20, 38),
			AutoSize = true
		};
		base.Controls.Add(value3);
		RoundedBtn roundedBtn = new RoundedBtn("ปิด (Esc)", Color.FromArgb(51, 204, 255), Color.FromArgb(102, 224, 255), Color.FromArgb(11, 14, 20), 8, new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold));
		roundedBtn.Location = new Point(440, 358);
		roundedBtn.Size = new Size(104, 34);
		roundedBtn.Click += delegate
		{
			Close();
		};
		base.Controls.Add(roundedBtn);
		base.AcceptButton = roundedBtn;
		base.CancelButton = roundedBtn;
		DpiHelper.ScaleControlTree(this);
		base.Paint += delegate(object s, PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			int chartTop = DpiHelper.S((extra != null && extra.Length > 0) ? 88 : 74);
			int chartHeight = DpiHelper.S((extra != null && extra.Length > 0) ? 254 : 268);
			Rectangle rectangle = new Rectangle(DpiHelper.S(20), chartTop, Math.Max(DpiHelper.S(60), base.ClientSize.Width - DpiHelper.S(40)), chartHeight);
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(26, 26, 38)))
			{
				using (GraphicsPath graphicsPath = new GraphicsPath())
				{
					int num5 = Math.Min(DpiHelper.S(24), Math.Min(rectangle.Width, rectangle.Height));
					if (num5 >= 2)
					{
						graphicsPath.AddArc(rectangle.X, rectangle.Y, num5, num5, 180f, 90f);
						graphicsPath.AddArc(rectangle.Right - num5, rectangle.Y, num5, num5, 270f, 90f);
						graphicsPath.AddArc(rectangle.Right - num5, rectangle.Bottom - num5, num5, num5, 0f, 90f);
						graphicsPath.AddArc(rectangle.X, rectangle.Bottom - num5, num5, num5, 90f, 90f);
						graphicsPath.CloseFigure();
					}
					else
					{
						graphicsPath.AddRectangle(rectangle);
					}
					graphics.FillPath(brush, graphicsPath);
				}
			}
			int num6 = DpiHelper.S(24);
			Rectangle rectangle2 = new Rectangle(rectangle.X + num6, rectangle.Y + num6, Math.Max(10, rectangle.Width - num6 * 2), Math.Max(10, rectangle.Height - num6 * 2));
			int num7 = Math.Min(vals.Count, 120);
			int num8 = vals.Count - num7;
			int num9 = 1;
			for (int i = num8; i < vals.Count; i++)
			{
				if (vals[i] > num9)
				{
					num9 = vals[i];
				}
			}
			using (Pen pen = new Pen(Color.FromArgb(40, 40, 58)))
			{
				for (int j = 0; j <= 4; j++)
				{
					int num10 = rectangle2.Y + rectangle2.Height * j / 4;
					graphics.DrawLine(pen, rectangle2.X, num10, rectangle2.Right, num10);
				}
			}
			List<Point> list = new List<Point>();
			if (num7 == 1)
			{
				int ySingle = rectangle2.Bottom - rectangle2.Height * Math.Max(0, vals[num8]) / num9;
				list.Add(new Point(rectangle2.X + rectangle2.Width / 2, ySingle));
			}
			else if (num7 > 1)
			{
				for (int k = 0; k < num7; k++)
				{
					int num11 = rectangle2.X + rectangle2.Width * k / (num7 - 1);
					int num12 = rectangle2.Bottom - rectangle2.Height * Math.Max(0, vals[num8 + k]) / num9;
					list.Add(new Point(num11, num12));
				}
			}
			if (list.Count == 0)
			{
				TextRenderer.DrawText(graphics, "ยังไม่มีข้อมูลสถิติผู้เล่นที่บันทึกไว้", fChartLabel, rectangle2, Color.FromArgb(140, 155, 180), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
			}
			else if (list.Count == 1)
			{
				Point ptOnly = list[0];
				int dotSz = DpiHelper.S(11);
				using (SolidBrush brushSingle = new SolidBrush(Color.FromArgb(90, 210, 130)))
				{
					graphics.FillEllipse(brushSingle, ptOnly.X - dotSz / 2, ptOnly.Y - dotSz / 2, dotSz, dotSz);
				}
			}
			else if (list.Count >= 2)
			{
				GraphicsPath graphicsPath2 = new GraphicsPath();
				graphicsPath2.AddCurve(list.ToArray(), 0.4f);
				GraphicsPath graphicsPath3 = (GraphicsPath)graphicsPath2.Clone();
				graphicsPath3.AddLine(list[list.Count - 1].X, rectangle2.Bottom, list[0].X, rectangle2.Bottom);
				graphicsPath3.CloseFigure();
				using (SolidBrush brush2 = new SolidBrush(Color.FromArgb(36, 51, 204, 255)))
				{
					graphics.FillPath(brush2, graphicsPath3);
				}
				using (Pen pen2 = new Pen(Color.FromArgb(51, 204, 255), DpiHelper.Sf(2.2f)))
				{
					graphics.DrawPath(pen2, graphicsPath2);
				}
				graphicsPath2.Dispose();
				graphicsPath3.Dispose();
				Point point = list[list.Count - 1];
				int dotSz = DpiHelper.S(9);
				using (SolidBrush brush3 = new SolidBrush(Color.FromArgb(90, 210, 130)))
				{
					graphics.FillEllipse(brush3, point.X - dotSz / 2, point.Y - dotSz / 2, dotSz, dotSz);
				}
			}
			TextRenderer.DrawText(graphics, "จำนวนผู้เล่น (" + num7 + " ตัวอย่างล่าสุด)", fChartLabel, new Point(rectangle.X + num6, rectangle.Bottom - DpiHelper.S(20)), Color.FromArgb(120, 120, 145));
		};
	}
}
