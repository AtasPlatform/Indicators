namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	[DisplayName("Accelerator Oscillator")]
	public class AC : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _negative;
		private readonly ValueDataSeries _neutral;
		private readonly ValueDataSeries _positive;
		private readonly SMA _smaAc = new SMA();
		private readonly SMA _smaLong = new SMA();
		private readonly SMA _smaShort = new SMA();

		#endregion

		#region ctor

		public AC()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_smaShort.Period = 5;
			_smaLong.Period = 34;
			_smaAc.Period = 5;

			DataSeries.Clear();

			_positive = new ValueDataSeries("Positive")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Green,
				ShowZeroValue = false
			};

			_negative = new ValueDataSeries("Negative")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Red,
				ShowZeroValue = false
			};

			_neutral = new ValueDataSeries("Neutral")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Gray,
				ShowZeroValue = false
			};

			DataSeries.Add(_positive);
			DataSeries.Add(_negative);
			DataSeries.Add(_neutral);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var medPrice = (candle.High - candle.Low) / 2.0m;
			var ao = _smaShort.Calculate(bar, medPrice) - _smaLong.Calculate(bar, medPrice);

			var seriesValue = ao - _smaAc.Calculate(bar, ao);

			if (bar > 0)
			{
				var prevValue = 0.0m;

				if (_positive[bar - 1] != 0)
					prevValue = _positive[bar - 1];
				else if (_negative[bar - 1] != 0)
					prevValue = _negative[bar - 1];
				else if (_neutral[bar - 1] != 0)
					prevValue = _neutral[bar - 1];

				if (seriesValue > prevValue)
				{
					_positive[bar] = seriesValue;
					_negative[bar] = _neutral[bar] = 0;
				}
				else if (seriesValue < prevValue)
				{
					_negative[bar] = seriesValue;
					_positive[bar] = _neutral[bar] = 0;
				}
				else
				{
					_neutral[bar] = seriesValue;
					_positive[bar] = _negative[bar] = 0;
				}
			}
		}

		#endregion
	}
}