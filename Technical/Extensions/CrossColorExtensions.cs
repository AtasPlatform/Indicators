namespace ATAS.Indicators.Technical.Extensions;

public static partial class CrossColorExtensions
{
	public static partial CrossColor SetAlpha(this CrossColor color, byte alpha);

	public static partial CrossColor FromRgb(byte  r, byte g, byte b);

	public static partial CrossColor FromArgb(byte a, byte r, byte g, byte b);
}