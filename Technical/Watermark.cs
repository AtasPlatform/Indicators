﻿namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

[DisplayName("Watermark")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.WatermarkIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602668")]
public class Watermark : Indicator
{
	#region Nested types

	public enum Location
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Center))]
		Center,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopLeft))]
		TopLeft,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopRight))]
		TopRight,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomLeft))]
		BottomLeft,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomRight))]
		BottomRight
	}

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelTextColorDescription), Order = 10)]
	public CrossColor TextColor { get; set; } = CrossColor.FromArgb(255, 225, 225, 225);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextLocation), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelLocationDescription), Order = 20)]
	public Location TextLocation { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HorizontalOffset), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelOffsetXDescription), Order = 30)]
	[Range(-100000, 100000)]
	public int HorizontalOffset { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalOffset), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelOffsetYDescription), Order = 40)]
	[Range(-100000, 100000)]
	public int VerticalOffset { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowInstrument), GroupName = nameof(Strings.FirstLine), Description = nameof(Strings.ShowCurrentParameterNameDescription), Order = 50)]
	public bool ShowInstrument { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowPeriod), GroupName = nameof(Strings.FirstLine), Description = nameof(Strings.ShowCurrentParameterNameDescription), Order = 60)]
	public bool ShowPeriod { get; set; } = true;

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.FirstLine), Description = nameof(Strings.FontSettingDescription), Order = 70)]
	public FontSetting Font { get; set; } = new()
		{ Size = 60, Bold = true };

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.SecondLine), Description = nameof(Strings.LabelTextDescription), Order = 80)]
	public string AdditionalText { get; set; } = "";

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Font), GroupName = nameof(Strings.SecondLine), Description = nameof(Strings.FontSettingDescription), Order = 90)]
	public FontSetting AdditionalFont { get; set; } = new()
		{ Size = 55 };

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VerticalOffset), GroupName = nameof(Strings.SecondLine), Description = nameof(Strings.LabelOffsetYDescription), Order = 100)]
	[Range(-100000, 100000)]
	public int AdditionalTextYOffset { get; set; } = -40;

	#endregion

	#region ctor

	public Watermark()
		: base(true)
	{
		Font.PropertyChanged += (a, b) => RedrawChart();
		AdditionalFont.PropertyChanged += (a, b) => RedrawChart();

		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

        DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical);
		DrawAbovePrice = false;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		var showSecondLine = !string.IsNullOrWhiteSpace(AdditionalText);

		if (!showSecondLine && !ShowInstrument && !ShowPeriod)
			return;

		var textColor = TextColor.Convert();
		var mainTextRectangle = new Rectangle();
		var additionalTextRectangle = new Rectangle();
		var firstLine = string.Empty;

		if (showSecondLine && !string.IsNullOrEmpty(AdditionalText))
		{
			var size = context.MeasureString(AdditionalText, AdditionalFont.RenderObject);
			additionalTextRectangle = new Rectangle(0, 0, size.Width, size.Height);
		}

		if (ShowInstrument || ShowPeriod)
		{
			if (ShowInstrument)
				firstLine = InstrumentInfo.Instrument;

			if (ShowPeriod)
			{
				var period = ChartInfo.ChartType == "TimeFrame" ? ChartInfo.TimeFrame : $"{ChartInfo.ChartType} {ChartInfo.TimeFrame}";

				if (ShowInstrument)
					firstLine += $", {period}";
				else
					firstLine += $"{period}";
			}

			var size = context.MeasureString(firstLine, Font.RenderObject);
			mainTextRectangle = new Rectangle(0, 0, size.Width, size.Height);
		}

		if (mainTextRectangle.Height > 0 && additionalTextRectangle.Height > 0)
		{
			int firstLineX;
			int secondLineX;
			var y = 0;

			var totalHeight = mainTextRectangle.Height + additionalTextRectangle.Height + AdditionalTextYOffset;

			switch (TextLocation)
			{
				case Location.Center:
				{
					firstLineX = ChartInfo.PriceChartContainer.Region.Width / 2 - mainTextRectangle.Width / 2 + HorizontalOffset;
					secondLineX = ChartInfo.PriceChartContainer.Region.Width / 2 - additionalTextRectangle.Width / 2 + HorizontalOffset;
					y = ChartInfo.PriceChartContainer.Region.Height / 2 - totalHeight / 2 + VerticalOffset;

					break;
				}
				case Location.TopLeft:
				{
					firstLineX = secondLineX = HorizontalOffset;
					break;
				}
				case Location.TopRight:
				{
					firstLineX = ChartInfo.PriceChartContainer.Region.Width - mainTextRectangle.Width + HorizontalOffset;
					secondLineX = ChartInfo.PriceChartContainer.Region.Width - additionalTextRectangle.Width + HorizontalOffset;

					break;
				}
				case Location.BottomLeft:
				{
					firstLineX = secondLineX = HorizontalOffset;
					y = ChartInfo.PriceChartContainer.Region.Height - totalHeight + VerticalOffset;

					break;
				}
				case Location.BottomRight:
				{
					firstLineX = ChartInfo.PriceChartContainer.Region.Width - mainTextRectangle.Width + HorizontalOffset;
					secondLineX = ChartInfo.PriceChartContainer.Region.Width - additionalTextRectangle.Width + HorizontalOffset;
					y = ChartInfo.PriceChartContainer.Region.Height - totalHeight + VerticalOffset;

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			context.DrawString(firstLine, Font.RenderObject, textColor, firstLineX, y);
			context.DrawString(AdditionalText, AdditionalFont.RenderObject, textColor, secondLineX, y + mainTextRectangle.Height + AdditionalTextYOffset);
		}
		else if (mainTextRectangle.Height > 0)
			DrawString(context, firstLine, Font.RenderObject, textColor, mainTextRectangle);
		else if (additionalTextRectangle.Height > 0)
			DrawString(context, AdditionalText, AdditionalFont.RenderObject, textColor, additionalTextRectangle);
	}

	#endregion

	#region Private methods

	private void DrawString(RenderContext context, string text, RenderFont font, System.Drawing.Color color, Rectangle rectangle)
	{
		switch (TextLocation)
		{
			case Location.Center:
			{
				context.DrawString(text, font, color, ChartInfo.PriceChartContainer.Region.Width / 2 - rectangle.Width / 2 + HorizontalOffset,
					ChartInfo.PriceChartContainer.Region.Height / 2 - rectangle.Height / 2 + VerticalOffset);
				break;
			}
			case Location.TopLeft:
			{
				context.DrawString(text, font, color, HorizontalOffset, VerticalOffset);
				break;
			}
			case Location.TopRight:
			{
				context.DrawString(text, font, color, ChartInfo.PriceChartContainer.Region.Width - rectangle.Width + HorizontalOffset, VerticalOffset);
				break;
			}
			case Location.BottomLeft:
			{
				context.DrawString(text, font, color, HorizontalOffset, ChartInfo.PriceChartContainer.Region.Height - rectangle.Height + VerticalOffset);
				break;
			}
			case Location.BottomRight:
			{
				context.DrawString(text, font, color, ChartInfo.PriceChartContainer.Region.Width - rectangle.Width + HorizontalOffset,
					ChartInfo.PriceChartContainer.Region.Height - rectangle.Height + VerticalOffset);
				break;
			}
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	#endregion
}