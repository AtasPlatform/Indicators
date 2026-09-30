namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;

[DisplayName("Volume On The Chart")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeOnChartDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000619334")]
public class VolumeOnChart : Volume
{
	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Height), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PanelHeightDescription))]
	[Range(10, 100)]
	public decimal Height { get; set; } = 15;

	[Browsable(false)]
	public new bool ShowMaxVolume { get; set; }

	[Browsable(false)]
	public new int HiVolPeriod { get; set; }

	[Browsable(false)]
	public new CrossColor LineColor { get; set; }

	#endregion

	#region ctor

	public VolumeOnChart()
	{
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);
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
		base.OnCalculate(bar, value);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (FirstVisibleBarNumber > LastVisibleBarNumber)
			return;

		var renderSeries = (ValueDataSeries)DataSeries[0];
		var maxValue = 0m;
		var maxHeight = Container.Region.Height * Height / 100m;

		if (maxHeight <= 0)
			return;

		var barsWidth = Math.Max(1, (int)ChartInfo.PriceChartContainer.BarsWidth);
		var showLabels = ShowVolume && ChartInfo.ChartVisualMode == ChartVisualModes.Clusters;
		var textY = 0;
		var textSizes = new Dictionary<string, Size>();

		if (showLabels)
		{
			var strHeight = context.MeasureString("0", Font.RenderObject).Height;
			textY = VolLocation switch
			{
				Location.Up => (int)(Container.Region.Bottom - maxHeight),
				Location.Down => Container.Region.Bottom - strHeight,
				_ => (int)(Container.Region.Bottom - maxHeight / 2)
			};
		}

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			maxValue = Math.Max(renderSeries[i], maxValue);
		}

		if (maxValue <= 0)
			return;

		var hasPendingBar = false;
		var pendingX = 0;
		var pendingValue = 0m;
		var pendingColor = default(Color);

		void DrawPendingBar()
		{
			var height = (int)(maxHeight * pendingValue / maxValue);
			var rectangle = new Rectangle(pendingX, Container.Region.Bottom - height, barsWidth, height);
			context.FillRectangle(pendingColor, rectangle);

			if (!showLabels)
				return;

			var renderText = FormatValue(pendingValue);
			if (!textSizes.TryGetValue(renderText, out var textSize))
			{
				textSize = context.MeasureString(renderText, Font.RenderObject);
				textSizes[renderText] = textSize;
			}

			var strRect = new Rectangle(pendingX,
				textY,
				Math.Max(barsWidth, textSize.Width),
				textSize.Height);
			context.DrawString(renderText, Font.RenderObject, TextColor, strRect, Format);
		}

		for (var i = FirstVisibleBarNumber; i <= LastVisibleBarNumber; i++)
		{
			var volumeValue = renderSeries[i];
			var volumeColor = renderSeries.Colors[i];
			var x = ChartInfo.GetXByBar(i);

			if (!hasPendingBar)
			{
				hasPendingBar = true;
				pendingX = x;
				pendingValue = volumeValue;
				pendingColor = volumeColor;
				continue;
			}

			if (x == pendingX)
			{
				if (volumeValue > pendingValue)
				{
					pendingValue = volumeValue;
					pendingColor = volumeColor;
				}

				continue;
			}

			DrawPendingBar();
			pendingX = x;
			pendingValue = volumeValue;
			pendingColor = volumeColor;
		}

		if (hasPendingBar)
			DrawPendingBar();
	}

	#endregion
}
