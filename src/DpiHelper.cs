using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class DpiHelper
{
	private static float _factor = 1.0f;

	public static float Factor
	{
		get { return _factor; }
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetDC(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

	[DllImport("gdi32.dll")]
	private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

	private const int LOGPIXELSX = 88;
	public const int WM_DPICHANGED = 0x02E0;

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	public static bool HandleDpiChanged(Form form, ref Message m)
	{
		if (form == null || m.Msg != WM_DPICHANGED)
		{
			return false;
		}
		int newDpi = (int)(m.WParam.ToInt64() & 0xFFFF);
		if (newDpi < 72 || _factor <= 0.01f)
		{
			return false;
		}
		float newFactor = newDpi / 96.0f;
		float ratio = newFactor / _factor;
		_factor = newFactor;

		if (m.LParam != IntPtr.Zero)
		{
			try
			{
				RECT rc = (RECT)Marshal.PtrToStructure(m.LParam, typeof(RECT));
				int w = rc.Right - rc.Left;
				int h = rc.Bottom - rc.Top;
				if (w > 100 && h > 100)
				{
					form.SetBounds(rc.Left, rc.Top, w, h);
				}
			}
			catch
			{
			}
		}

		if (Math.Abs(ratio - 1.0f) >= 0.02f)
		{
			form.SuspendLayout();
			try
			{
				ScaleByRatioTree(form, ratio);
			}
			finally
			{
				form.ResumeLayout(true);
			}
		}
		return true;
	}

	private static void ScaleByRatioTree(Control ctrl, float ratio)
	{
		if (ctrl == null) return;
		if (!(ctrl is Form) && ctrl.Dock == DockStyle.None)
		{
			ctrl.Location = new Point((int)Math.Round(ctrl.Left * ratio), (int)Math.Round(ctrl.Top * ratio));
			ctrl.Size = new Size(Math.Max(1, (int)Math.Round(ctrl.Width * ratio)), Math.Max(1, (int)Math.Round(ctrl.Height * ratio)));
		}
		ListView lv = ctrl as ListView;
		if (lv != null)
		{
			foreach (ColumnHeader col in lv.Columns)
			{
				col.Width = Math.Max(16, (int)Math.Round(col.Width * ratio));
			}
			if (lv.SmallImageList != null)
			{
				Size cur = lv.SmallImageList.ImageSize;
				lv.SmallImageList.ImageSize = new Size(Math.Max(1, cur.Width), Math.Max(1, (int)Math.Round(cur.Height * ratio)));
			}
		}
		RoundedBtn rb = ctrl as RoundedBtn;
		if (rb != null)
		{
			rb.Radius = Math.Max(2, (int)Math.Round(rb.Radius * ratio));
		}
		SmoothPanel sp = ctrl as SmoothPanel;
		if (sp != null)
		{
			sp.Radius = Math.Max(2, (int)Math.Round(sp.Radius * ratio));
		}
		foreach (Control child in ctrl.Controls)
		{
			ScaleByRatioTree(child, ratio);
		}
	}

	public static void Init()
	{
		try
		{
			IntPtr hdc = GetDC(IntPtr.Zero);
			if (hdc != IntPtr.Zero)
			{
				int dpi = GetDeviceCaps(hdc, LOGPIXELSX);
				ReleaseDC(IntPtr.Zero, hdc);
				if (dpi >= 96)
				{
					_factor = dpi / 96.0f;
					return;
				}
			}
		}
		catch
		{
		}
		_factor = 1.0f;
	}

	public static int S(int px)
	{
		if (Math.Abs(_factor - 1.0f) < 0.01f)
		{
			return px;
		}
		return (int)Math.Round(px * _factor);
	}

	public static float Sf(float px)
	{
		return px * _factor;
	}

	public static Size Sz(int w, int h)
	{
		return new Size(S(w), S(h));
	}

	public static Point Pt(int x, int y)
	{
		return new Point(S(x), S(y));
	}

	/// <summary>
	/// Recursively scales pixel coordinates and sizes of all controls designed at 96 DPI.
	/// Point-based Fonts scale natively under PerMonitorV2 DPI awareness, so Font objects are preserved.
	/// </summary>
	public static void ScaleControlTree(Control ctrl)
	{
		if (ctrl == null || Math.Abs(_factor - 1.0f) < 0.01f)
		{
			return;
		}

		ScaleSingleControl(ctrl);

		foreach (Control child in ctrl.Controls)
		{
			ScaleControlTree(child);
		}
	}

	private static void ScaleSingleControl(Control c)
	{
		if (c is Form)
		{
			// Form outer bounds are scaled directly by the caller
		}
		else if (c.Dock == DockStyle.None)
		{
			c.Location = new Point(S(c.Left), S(c.Top));
			c.Size = new Size(S(c.Width), S(c.Height));
		}

		if (c.Padding != Padding.Empty)
		{
			c.Padding = new Padding(
				S(c.Padding.Left),
				S(c.Padding.Top),
				S(c.Padding.Right),
				S(c.Padding.Bottom));
		}

		RoundedBtn rb = c as RoundedBtn;
		if (rb != null)
		{
			rb.Radius = Math.Max(2, S(rb.Radius));
		}

		SmoothPanel sp = c as SmoothPanel;
		if (sp != null)
		{
			sp.Radius = Math.Max(2, S(sp.Radius));
		}

		ListView lv = c as ListView;
		if (lv != null)
		{
			foreach (ColumnHeader col in lv.Columns)
			{
				col.Width = S(col.Width);
			}
			if (lv.SmallImageList != null)
			{
				Size cur = lv.SmallImageList.ImageSize;
				lv.SmallImageList.ImageSize = new Size(Math.Max(1, cur.Width), S(cur.Height));
			}
		}
	}
}
