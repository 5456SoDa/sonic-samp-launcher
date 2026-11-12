using System.Drawing;
using System.Windows.Forms;

internal class DarkMenuColors : ProfessionalColorTable
{
	public override Color MenuItemSelected
	{
		get
		{
			return Color.FromArgb(48, 48, 70);
		}
	}

	public override Color MenuItemBorder
	{
		get
		{
			return Color.Transparent;
		}
	}

	public override Color MenuBorder
	{
		get
		{
			return Color.FromArgb(60, 60, 85);
		}
	}

	public override Color ToolStripDropDownBackground
	{
		get
		{
			return Color.FromArgb(26, 26, 38);
		}
	}

	public override Color SeparatorDark
	{
		get
		{
			return Color.FromArgb(60, 60, 85);
		}
	}

	public override Color SeparatorLight
	{
		get
		{
			return Color.FromArgb(60, 60, 85);
		}
	}
}
