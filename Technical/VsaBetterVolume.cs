namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("VSA Better Volume")]
	public class VsaBetterVolume : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _blue = new ValueDataSeries("Blue");
		private readonly ValueDataSeries _green = new ValueDataSeries("Green");
		private readonly Highest _highestAbs = new Highest();
		private readonly Highest _highestComp = new Highest();

		private readonly Lowest _lowest = new Lowest();
		private readonly Highest _lowestComp = new Highest();
		private readonly ValueDataSeries _magenta = new ValueDataSeries("Magenta");

		private readonly ValueDataSeries _red = new ValueDataSeries("Red");
		private readonly ValueDataSeries _v4Series = new ValueDataSeries("V4");

		private readonly ValueDataSeries _volume = new ValueDataSeries("Volume");
		private readonly ValueDataSeries _white = new ValueDataSeries("White");
		private readonly ValueDataSeries _yellow = new ValueDataSeries("Yellow");
		private int _period;

		private decimal _tickSize;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 0)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RetrospectiveAnalysis", GroupName = "Common", Order = 1)]
		public int LookBack
		{
			get => _highestAbs.Period;
			set
			{
				if (value <= 0)
					return;

				_highestAbs.Period = _highestComp.Period = value;
				_lowestComp.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VsaBetterVolume()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 14;
			_highestAbs.Period = _highestComp.Period = 20;
			_lowestComp.Period = 20;
			_lowest.Period = 20;

			_red.Color = Colors.DarkRed;
			_red.Width = 3;
			_red.VisualType = VisualMode.Histogram;
			_red.ShowZeroValue = false;

			_blue.Color = Colors.DodgerBlue;
			_blue.Width = 2;
			_blue.VisualType = VisualMode.Histogram;
			_blue.ShowZeroValue = false;

			_yellow.Color = Colors.Orange;
			_yellow.Width = 2;
			_yellow.VisualType = VisualMode.Histogram;
			_yellow.ShowZeroValue = false;

			_green.Color = Colors.Green;
			_green.Width = 2;
			_green.VisualType = VisualMode.Histogram;
			_green.ShowZeroValue = false;

			_white.Color = Colors.LightGray;
			_white.Width = 3;
			_white.VisualType = VisualMode.Histogram;
			_white.ShowZeroValue = false;

			_magenta.Color = Colors.DarkMagenta;
			_magenta.Width = 2;
			_magenta.VisualType = VisualMode.Histogram;
			_magenta.ShowZeroValue = false;

			_v4Series.Color = Colors.LightSeaGreen;
			_v4Series.Width = 1;
			_v4Series.VisualType = VisualMode.Line;

			DataSeries[0] = _red;
			DataSeries.Add(_blue);
			DataSeries.Add(_yellow);
			DataSeries.Add(_green);
			DataSeries.Add(_white);
			DataSeries.Add(_magenta);
			DataSeries.Add(_v4Series);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_tickSize = ChartInfo.PriceChartContainer.Step;
			}

			var candle = GetCandle(bar);
			_volume[bar] = candle.Volume;

			var volLowest = _lowest.Calculate(bar, candle.Volume);

			if (candle.Volume == volLowest)
				_yellow[bar] = candle.Volume;
			else
				_blue[bar] = candle.Volume;

			var range = (candle.High - candle.Low) / _tickSize;
			var value2 = candle.Volume * range;

			var value3 = 0.0m;

			if (range != 0)
				value3 = candle.Volume / range;

			var sumVolume = _volume.CalcSum(Period, bar);

			_v4Series[bar] = sumVolume / Period;

			var hiValue2 = _highestAbs.Calculate(bar, value2);

			if (value2 != 0)
				_highestComp.Calculate(bar, value3);

			if (value2 == hiValue2 && candle.Close > (candle.High + candle.Low) / 2.0m)
			{
				_red[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = 0;
			}

			if (value3 == _highestComp[bar])
			{
				_green[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _red[bar] = 0;
			}

			if (value2 == hiValue2 && value3 == _highestComp[bar])
			{
				_magenta[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _red[bar] = _magenta[bar] = 0;
			}

			if (value2 == hiValue2 && candle.Close <= (candle.High + candle.Low) / 2.0m)
			{
				_white[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _red[bar] = _magenta[bar] = _magenta[bar] = 0;
			}
		}

		#endregion
	}
}