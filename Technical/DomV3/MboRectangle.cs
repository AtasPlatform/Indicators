namespace ATAS.Indicators.Technical;

using System;
using System.Drawing;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

public partial class DomV3
{
	#region Nested types

	public struct MboRectanglePadding
	{
		public MboRectanglePadding(int top, int right, int bottom, int left)
		{
			Top = top;
			Right = right;
			Left = left;
			Bottom = bottom;
		}

		public int Top { set; get; } = 0;

		public int Right { set; get; } = 0;

		public int Left { set; get; } = 0;

		public int Bottom { set; get; } = 0;
	}

	public class MboRectangle
	{
		#region Fields

		public MboRectanglePadding Padding = new();

		#endregion

		#region Properties

		public bool FillBox { get; set; }

		public int X1 { get; set; }

		public int X2 { get; set; }

		public int Y1 { get; set; }

		public int Y2 { get; set; }

		public int Width => Math.Abs(X1 - X2);

		public int Height => Math.Abs(Y1 - Y2);

		public RenderPen Pen { init; get; } = RenderPens.Gray;

		public object Data { get; set; } = string.Empty;

		public RenderPen BorderColor { get; set; } = RenderPens.Transparent;

		#endregion

		#region ctor

		public MboRectangle(bool fillBox)
		{
			FillBox = fillBox;
		}

		#endregion

		#region Public methods

		public void Render(RenderContext context, RenderFont font, RenderStringFormat format)
		{
			var box = new Rectangle
			{
				X = X1 + Padding.Left, Y = Y1 + Padding.Top,
				Width = Math.Max(Width - (Padding.Left + Padding.Right), 1),
				Height = Math.Max(Height - (Padding.Top + Padding.Bottom), 1)
			};

			if (FillBox)
				context.DrawFillRectangle(BorderColor, Pen.Color, box);
			else
				context.DrawRectangle(Pen, box);

			if (Data is string { Length: > 0 } data)
				context.DrawString(data, font, Color.Wheat, box, format);

			if (Data is decimal vol)
				context.DrawString(vol.ToString("0.##"), font, Color.Wheat, box, format);
		}

		#endregion
	}

	#endregion
}