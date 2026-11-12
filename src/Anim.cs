using System.Drawing;

internal static class Anim
{
	public static float EaseOut(float t)
	{
		float num = 1f - t;
		return 1f - num * num * num;
	}

	public static Color Lerp(Color a, Color b, float t)
	{
		return Color.FromArgb(C(a.R, b.R, t), C(a.G, b.G, t), C(a.B, b.B, t));
	}

	private static int C(int x, int y, float t)
	{
		int num = (int)((float)x + (float)(y - x) * t);
		if (num >= 0)
		{
			if (num <= 255)
			{
				return num;
			}
			return 255;
		}
		return 0;
	}
}
