namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;

using Color = System.Windows.Media.Color;

[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/2471-volume")]
[DisplayName("Volume on the chart")]
public class VolumeOnChart : Volume
{
	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Height", GroupName = "Colors")]
	[Range(10, 100)]
	public decimal Height { get; set; } = 15;

	[Browsable(false)]
	public new bool ShowMaxVolume { get; set; }

	[Browsable(false)]
	public new int HiVolPeriod { get; set; }

	[Browsable(false)]
	public new Color LineColor { get; set; }

	#endregion

	#region ctor

	public VolumeOnChart()
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
		Panel = IndicatorDataProvider.CandlesPanel;
		DenyToChangePanel = true;
		MaxVolSeries.VisualType = VisualMode.Hide;
		DataSeries.ForEach(x => x.IsHidden = true);
	}

	#endregion

	#region Public methods

	public override string ToString()
	{
		return "Volume on the chart";
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		DataSeries.ForEach(x => x.Clear());
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		((ValueDataSeries)DataSeries[1]).VisualType = VisualMode.Hide;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		HighestVol.Calculate(bar, GetCandle(bar).Volume);
		base.OnCalculate(bar, value);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		var maxValue = 0m;

		var maxHeight = Container.Region.Height * Height / 100m;
		var barsWidth = Math.Max(1, (int)ChartInfo.PriceChartContainer.BarsWidth);
		var textY = (int)(Container.Region.Height - maxHeight / 2);

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			var candle = GetCandle(i);
			var volumeValue = Input == InputType.Volume ? candle.Volume : candle.Ticks;

			maxValue = Math.Max(volumeValue, maxValue);
		}

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			var candle = GetCandle(i);
			var volumeValue = Input == InputType.Volume ? candle.Volume : candle.Ticks;

			var volumeColor = ((ValueDataSeries)DataSeries[0]).Colors[i];

			var x = ChartInfo.GetXByBar(i);
			var height = (int)(maxHeight * volumeValue / maxValue);

			var rectangle = new Rectangle(x, Container.Region.Bottom - height, barsWidth, height);
			context.FillRectangle(volumeColor, rectangle);

			if (!ShowVolume || ChartInfo.ChartVisualMode != ChartVisualModes.Clusters)
				continue;

			var renderText = volumeValue.ToString(CultureInfo.InvariantCulture);
			var textSize = context.MeasureString(renderText, Font.RenderObject);

			var strRect = new Rectangle(ChartInfo.GetXByBar(i),
				textY,
				Math.Max(barsWidth, textSize.Width),
				textSize.Height);
			context.DrawString(renderText, Font.RenderObject, TextColor, strRect, Format);
		}
	}

	#endregion
}