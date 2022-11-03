namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Squeeze Momentum")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38307-squeeze-momentum")]
	public class SqueezeMomentum : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dotsFalse = new("DotsFalse")
		{
			Color = Colors.Gray,
			VisualType = VisualMode.Dots,
			Width = 2,
			Digits = 6,
			ShowZeroValue = false,
			ShowTooltip = false,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _dotsNull = new("DotsNull")
		{
			Color = Colors.Blue,
			VisualType = VisualMode.Dots,
			Width = 2,
            Digits = 6,
            ShowZeroValue = false,
            ShowTooltip = false,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _dotsTrue = new("DotsTrue")
		{
			Color = Colors.Black,
			VisualType = VisualMode.Dots,
			Width = 2,
            Digits = 6,
            ShowZeroValue = false,
            ShowTooltip = false,
            UseMinimizedModeIfEnabled = true
		};

		private readonly Highest _highest = new() { Period = 20 };
		private readonly LinearReg _linRegr = new() { Period = 20 };
		private readonly ValueDataSeries _low = new("Low")
		{
			Color = Colors.Maroon,
			VisualType = VisualMode.Histogram,
            Digits = 6,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _lower = new("Lower")
		{
			Color = Colors.Red,
			VisualType = VisualMode.Histogram,
            Digits = 6,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
		private readonly Lowest _lowest = new() { Period = 20 };
		private readonly SMA _smaBb = new() { Period = 20 };
		private readonly SMA _smaKc = new() { Period = 20 };
		private readonly SMA _smaKcRange = new() { Period = 20 };
		private readonly StdDev _stdDev = new() { Period = 20 };

		private readonly ValueDataSeries _up = new("Up")
		{
			Color = Colors.Green,
			VisualType = VisualMode.Histogram,
			Digits = 6,
			ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};
		private readonly ValueDataSeries _upper = new("Upper")
		{
			Color = Colors.Lime,
			VisualType = VisualMode.Histogram,
            Digits = 6,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = true
		};

		private decimal _bbMultFactor = 2.0m;
        private decimal _kcMultFactor = 1.5m;
        private bool _useTrueRange;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BBPeriod", Order = 10)]
		[Range(1, 10000)]
		public int BBPeriod
		{
			get => _smaBb.Period;
			set
			{
				_smaBb.Period = value;
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBMultFactor", Order = 11)]
		[Range(1, 10000)]
        public decimal BBMultFactor
		{
			get => _bbMultFactor;
			set
			{
				_bbMultFactor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCPeriod", Order = 20)]
		[Range(1, 10000)]
        public int KCPeriod
		{
			get => _smaKc.Period;
			set
			{
				_smaKc.Period = value;
				_smaKcRange.Period = value;
				_linRegr.Period = value;
				_highest.Period = value;
				_lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCMultFactor", Order = 21)]
		[Range(0.00000001, 100000000)]
        public decimal KCMultFactor
		{
			get => _kcMultFactor;
			set
			{
				_kcMultFactor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseTrueRangeKc", Order = 22)]
		public bool UseTrueRange
		{
			get => _useTrueRange;
			set
			{
				_useTrueRange = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SqueezeMomentum()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _up;
			DataSeries.Add(_upper);
			DataSeries.Add(_low);
			DataSeries.Add(_lower);
			DataSeries.Add(_dotsFalse);
			DataSeries.Add(_dotsTrue);
			DataSeries.Add(_dotsNull);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_up.Clear();
				_upper.Clear();
				_low.Clear();
				_lower.Clear();
				_dotsFalse.Clear();
				_dotsTrue.Clear();
				_dotsNull.Clear();
			}

			var candle = GetCandle(bar);
			var basis = _smaBb.Calculate(bar, candle.Close);
			var dev = BBMultFactor * _stdDev.Calculate(bar, candle.Close);

			var upperBB = basis + dev;
			var lowerBB = basis - dev;

			var ma = _smaKc.Calculate(bar, candle.Close);
			var range = candle.High - candle.Low;

			if (UseTrueRange && bar > 0)
			{
				var trueRange = Math.Max(range, Math.Abs(candle.High - GetCandle(bar - 1).Close));

				range = Math.Max(trueRange, Math.Abs(candle.Low - GetCandle(bar - 1).Close));
			}

			var rangeSma = _smaKcRange.Calculate(bar, range);

			var upperKC = ma + rangeSma * KCMultFactor;
			var lowerKC = ma - rangeSma * KCMultFactor;

			var sqzOn = lowerBB > lowerKC && upperBB < upperKC;
			var sqzOff = lowerBB < lowerKC && upperBB > upperKC;
			var noSqz = !sqzOff && !sqzOn;

			var high = _highest.Calculate(bar, candle.High);
			var low = _lowest.Calculate(bar, candle.Low);

			var val = _linRegr.Calculate(bar, candle.Close - ((high + low) / 2.0m + ma) / 2.0m);

			if (bar == 0)
				return;

			var lastValue = GetValue(bar - 1);

			if (val > 0)
			{
				if (val > lastValue)
					_upper[bar] = val;
				else
					_up[bar] = val;
			}
			else
			{
				if (val < lastValue)
					_lower[bar] = val;
				else
					_low[bar] = val;
			}

			if (noSqz)
				_dotsNull[bar] = 0.0000000001m;
			else
			{
				if (sqzOn)
					_dotsTrue[bar] = 0.0000000001m;
				else
					_dotsFalse[bar] = 0.0000000001m;
			}
		}

		#endregion

		#region Private methods

		private decimal GetValue(int bar)
		{
			var value = _up[bar];

			if (_upper[bar] != 0)
				value = _upper[bar];

			if (_low[bar] != 0)
				value = _low[bar];

			if (_lower[bar] != 0)
				value = _lower[bar];

			return value;
		}

		#endregion
	}
}