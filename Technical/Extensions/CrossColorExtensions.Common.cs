namespace ATAS.Indicators.Technical.Extensions;

public partial class CrossColorExtensions
{
	public static partial CrossColor SetAlpha(this CrossColor color, byte alpha) => CrossColor.FromArgb(alpha, color);

	public static partial CrossColor FromRgb(byte r, byte g, byte b) => FromArgb(255, r, g, b);

	public static partial CrossColor FromArgb(byte a, byte r, byte g, byte b) => CrossColor.FromArgb(a, r, g, b);
}