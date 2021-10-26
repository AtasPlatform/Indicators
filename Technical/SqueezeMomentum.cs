namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Squeeze Momentum")]
	public class SqueezeMomentum : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dotsFalse = new("DotsFalse");
		private readonly ValueDataSeries _dotsNull = new("DotsNull");
		private readonly ValueDataSeries _dotsTrue = new("DotsTrue");

		private readonly Highest _highest = new();
		private readonly LinearReg _linRegr = new();
		private readonly ValueDataSeries _low = new("Low");
		private readonly ValueDataSeries _lower = new("Lower");
		private readonly Lowest _lowest = new();
		private readonly SMA _smaBb = new();
		private readonly SMA _smaKc = new();
		private readonly SMA _smaKcRange = new();
		private readonly StdDev _stdDev = new();

		private readonly ValueDataSeries _up = new("Up");
		private readonly ValueDataSeries _upper = new("Upper");

		private decimal _bbMultFactor;
		private decimal _kcMultFactor;
		private bool _useTrueRange;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BBPeriod", Order = 10)]
		public int BBPeriod
		{
			get => _smaBb.Period;
			set
			{
				if (value <= 0)
					return;

				_smaBb.Period = value;
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBMultFactor", Order = 11)]
		public decimal BBMultFactor
		{
			get => _bbMultFactor;
			set
			{
				if (value <= 0)
					return;

				_bbMultFactor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCPeriod", Order = 20)]
		public int KCPeriod
		{
			get => _smaKc.Period;
			set
			{
				if (value <= 0)
					return;

				_smaKc.Period = value;
				_smaKcRange.Period = value;
				_linRegr.Period = value;
				_highest.Period = value;
				_lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "KCMultFactor", Order = 21)]
		public decimal KCMultFactor
		{
			get => _kcMultFactor;
			set
			{
				if (value <= 0)
					return;

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

			_smaBb.Period = 20;
			_stdDev.Period = 20;
			_bbMultFactor = 2.0m;
			_smaKc.Period = _smaKcRange.Period = 20;
			_kcMultFactor = 1.5m;
			_highest.Period = _lowest.Period = 20;
			_linRegr.Period = 20;

			_up.Color = Colors.Green;
			_upper.Color = Colors.Lime;
			_low.Color = Colors.Maroon;
			_lower.Color = Colors.Red;
			_dotsTrue.Color = Colors.Black;
			_dotsFalse.Color = Colors.Gray;
			_dotsNull.Color = Colors.Blue;

			_up.VisualType = VisualMode.Histogram;
			_upper.VisualType = VisualMode.Histogram;
			_low.VisualType = VisualMode.Histogram;
			_lower.VisualType = VisualMode.Histogram;
			_dotsTrue.VisualType = VisualMode.Dots;
			_dotsFalse.VisualType = VisualMode.Dots;
			_dotsNull.VisualType = VisualMode.Dots;

			_up.ShowZeroValue = false;
			_upper.ShowZeroValue = false;
			_low.ShowZeroValue = false;
			_lower.ShowZeroValue = false;
			_dotsFalse.ShowZeroValue = false;
			_dotsTrue.ShowZeroValue = false;
			_dotsNull.ShowZeroValue = false;

			_dotsFalse.Width = 2;
			_dotsTrue.Width = 2;
			_dotsNull.Width = 2;

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
				_dotsNull[bar] = 0.001m;
			else
			{
				if (sqzOn)
					_dotsTrue[bar] = 0.001m;
				else
					_dotsFalse[bar] = 0.001m;
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