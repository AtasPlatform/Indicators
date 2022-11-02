namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("VSA Better Volume")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38322-vsa-better-volume")]
	public class VsaBetterVolume : Indicator
	{
        #region Fields

        private int _period = 14;
		private decimal _tickSize;
		
		private readonly Highest _highestAbs = new() { Period = 20 };
		private readonly Highest _highestComp = new() { Period = 20 };

		private readonly Lowest _lowest = new() { Period = 20 };
		private readonly Highest _lowestComp = new() { Period = 20 };

		private readonly ValueDataSeries _volume = new("Volume");
        private readonly ValueDataSeries _blue = new("Blue")
		{
			Color = Colors.DodgerBlue,
			Width = 2,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _green = new("Green")
		{
			Color = Colors.Green,
			Width = 2,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

        private readonly ValueDataSeries _magenta = new("Magenta")
		{
			Color = Colors.DarkMagenta,
			Width = 2,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _red = new("Red")
		{
			Color = Colors.DarkRed,
			Width = 3,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _v4Series = new("V4")
		{
			Color = Colors.LightSeaGreen,
			Width = 1,
			VisualType = VisualMode.Line,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _white = new("White")
		{
			Color = Colors.LightGray,
			Width = 3,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _yellow = new("Yellow")
		{
			Color = Colors.Orange,
			Width = 2,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 0)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RetrospectiveAnalysis", GroupName = "Common", Order = 1)]
		[Range(1, 10000)]
        public int LookBack
		{
			get => _highestAbs.Period;
			set
			{
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
			{
				_yellow[bar] = candle.Volume;
				_red[bar] = _blue[bar] = _white[bar] = _magenta[bar] = _green[bar] = 0;
			}
			else
			{
				_blue[bar] = candle.Volume;
				_yellow[bar] = _red[bar] = _white[bar] = _magenta[bar] = _green[bar] = 0;
			}

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

			if (value2 == hiValue2 && candle.Close > (candle.High + candle.Low) / 2.0m && candle.Close >= candle.Open)
			{
				_red[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _white[bar] = _magenta[bar] = _green[bar] = 0;
			}

			if (value3 == _highestComp[bar])
			{
				_green[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _white[bar] = _magenta[bar] = _red[bar] = 0;
			}

			if (value2 == hiValue2 && value3 == _highestComp[bar])
			{
				_magenta[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _white[bar] = _green[bar] = _red[bar] = 0;
			}

			if (value2 == hiValue2 && candle.Close <= (candle.High + candle.Low) / 2.0m && candle.Close <= candle.Open)
			{
				_white[bar] = candle.Volume;
				_yellow[bar] = _blue[bar] = _red[bar] = _magenta[bar] = _green[bar] = 0;
			}
		}

		#endregion
	}
}