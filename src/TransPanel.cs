using System.Drawing;
using System.Windows.Forms;

internal class TransPanel : Panel
{
	public TransPanel()
	{
		SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
		BackColor = Color.Transparent;
	}
}
