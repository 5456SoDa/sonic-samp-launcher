using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal class SmoothPanel : Panel
{
	private Color innerColor;

	public int Radius = 8;

	public bool FocusBorder;

	public Color BorderColor = Color.FromArgb(32, 48, 76);

	public Color FocusColor = Color.FromArgb(51, 204, 255);

	public SmoothPanel(Color inner, int rad)
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		BackColor = Color.Transparent;
		innerColor = inner;
		Radius = rad;
	}

	protected override void OnPaintBackground(PaintEventArgs e)
	{
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.SmoothingMode = SmoothingMode.AntiAlias;
		Rectangle r = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
		using (SolidBrush brush = new SolidBrush(innerColor))
		{
			using (GraphicsPath path = RoundPath(r, Radius))
			{
				graphics.FillPath(brush, path);
			}
		}
		Color curBorder = FocusBorder ? FocusColor : BorderColor;
		if (curBorder != Color.Empty && curBorder != Color.Transparent)
		{
			using (Pen pen = new Pen(curBorder, FocusBorder ? 1.5f : 1f))
			{
				using (GraphicsPath path2 = RoundPath(new Rectangle(0, 0, base.Width - 2, base.Height - 2), Radius))
				{
					graphics.DrawPath(pen, path2);
				}
			}
		}
	}

	private static GraphicsPath RoundPath(Rectangle r, int rad)
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
}
