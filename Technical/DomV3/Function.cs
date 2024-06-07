namespace DomV10;

using System;
using System.Drawing;

using OFT.Rendering.Context;
using OFT.Rendering.Tools;

public partial class MainIndicator
{
	#region Fields

	private readonly RenderStringFormat _stringLeftFormat = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly RenderStringFormat _stringRightFormat = new()
	{
		Alignment = StringAlignment.Far,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private RenderFont _aggFont = new("Arial", 0);
	private RenderFont _font = new("Arial", 0);

	private RenderStringFormat _stringCenterFormat = new()
	{
		Alignment = StringAlignment.Center,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	#endregion

	#region Private methods

	private int ItemWidthCalculation(decimal currentVol, decimal totalVolume, int maxScreenSize, int itemCount,
		int space, decimal fontWidth)
	{
		var eachPic = maxScreenSize / totalVolume;
		var w = (int)(eachPic * currentVol);

		if (w < fontWidth)
			return (int)Math.Max(fontWidth, 1);

		return w;

		// var w = (int)((currentVol / totalVolume) * (maxScreenSize - (itemCount - 1) * space));
		// if (w < fontWidth) return (int)Math.Max(fontWidth, 1);
		// return w;

		return 100;
	}

	private (float fontSize, float fontWidth) SetFontSize(RenderContext context, decimal maxHeight, decimal maxFontSize)
	{
		if (maxHeight < 2)
			return (0, 0);

		var direction = 0;
		var bestSize = _font.Size;
		var bestW = 0;
		var increment = 0.1m;

		var x = 0;

		var indicate = maxHeight - 2;

		if (indicate > maxFontSize)
			indicate = maxFontSize;

		_font = new RenderFont("Arial", bestSize);

		do
		{
			var size = context.MeasureString("#", _font);
			var textSize = size.Height;

			if (size.Height == indicate)
			{
				bestSize = _font.Size;
				bestW = size.Width;
				break;
			}

			if (textSize < indicate)
			{
				if (direction == 0)
					direction = 1;

				if (direction == -1)
					break;

				bestSize = _font.Size;
				bestW = size.Width;

				_font = new RenderFont(_font.FontFamily, (float)((decimal)_font.Size + increment));
				continue;
			}

			if (textSize > indicate)
			{
				if (direction == 0)
					direction = -1;

				if (direction == 1)
					break;

				_font = new RenderFont(_font.FontFamily, (float)((decimal)_font.Size - increment));

				bestSize = _font.Size;
				bestW = size.Width;
			}
		}
		while (x++ <= 100);

		_aggFont = new RenderFont(_font.FontFamily, (float)((decimal)_font.Size + 2), FontStyle.Bold);
		return (bestSize, bestW);
	}

	private decimal GetFixPrice(decimal value, bool isTop)
	{
		if (InstrumentInfo == null)
			return value;

		var tick = InstrumentInfo.TickSize;
		var left = value % tick;
		value = value - left;

		if (isTop)
			value += tick;
		else
			value -= tick;

		if (isTop)
			value += tick;
		else
			value -= tick;
		return value;
	}

	#endregion
}