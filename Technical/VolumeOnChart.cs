namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;

	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/2471-volume")]
	[DisplayName("Volume on the chart")]
	public class VolumeOnChart : Volume
	{
		#region Fields

		private decimal _height = 15;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Height", GroupName = "Colors")]
		public decimal Height
		{
			get => _height;
			set
			{
				if (value < 10 || value > 100)
					return;

				_height = value;
			}
		}

		#endregion

		#region ctor

		public VolumeOnChart()
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
			Panel = IndicatorDataProvider.CandlesPanel;
			DenyToChangePanel = true;
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Volume on the chart";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var maxValue = 0m;

			var maxHeight = Container.Region.Height * _height / 100m;
			var positiveColor = ((ValueDataSeries)DataSeries[0]).Color.Convert(); // color from positive dataseries
			var negativeColor = ((ValueDataSeries)DataSeries[1]).Color.Convert(); // color from negative dataseries
			var neutralColor = ((ValueDataSeries)DataSeries[2]).Color.Convert(); // color from neutral dataseries
			var filterColor = ((ValueDataSeries)DataSeries[3]).Color.Convert(); // color from filter dataseries
			var barsWidth = Math.Max(1, (int)ChartInfo.PriceChartContainer.BarsWidth);

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

				Color volumeColor;

				if (UseFilter && volumeValue > FilterValue)
					volumeColor = filterColor;
				else
				{
					if (DeltaColored)
					{
						if (candle.Delta > 0)
							volumeColor = positiveColor;
						else if (candle.Delta < 0)
							volumeColor = negativeColor;
						else
							volumeColor = neutralColor;
					}
					else
					{
						if (candle.Close > candle.Open)
							volumeColor = positiveColor;
						else if (candle.Close < candle.Open)
							volumeColor = negativeColor;
						else
							volumeColor = neutralColor;
					}
				}

				var x = ChartInfo.GetXByBar(i);
				var height = (int)(maxHeight * volumeValue / maxValue);

				var rectangle = new Rectangle(x, Container.Region.Bottom - height, barsWidth, height);
				context.FillRectangle(volumeColor, rectangle);
			}
		}

		#endregion
	}
}