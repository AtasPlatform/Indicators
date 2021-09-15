namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Localization;

	[DisplayName("COT High")]
	public class CotHigh : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _negSeries = new(Strings.Negative);
		private readonly ValueDataSeries _posSeries = new(Strings.Positive);
		private decimal _high;
		private decimal _levelBar;

		#endregion

		#region ctor

		public CotHigh()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_posSeries.VisualType = _negSeries.VisualType = VisualMode.Histogram;
			_posSeries.Color = Colors.Green;
			_negSeries.Color = Colors.Red;

			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_high = 0;
				_levelBar = 0;
				DataSeries.ForEach(x => x.Clear());
			}

			var candle = GetCandle(bar);

			if (candle.High >= _high)
			{
				_high = candle.High;
				_levelBar = bar;
				DrawValue(bar, candle.Delta);
			}
			else
			{
				var renderValue = LastValue(bar) + candle.Delta;
				DrawValue(bar, renderValue);
			}
		}

		#endregion

		#region Private methods

		private void DrawValue(int bar, decimal value)
		{
			if (value > 0)
				_posSeries[bar] = value;
			else
				_negSeries[bar] = value;
		}

		private decimal LastValue(int bar)
		{
			return
				_posSeries[bar - 1] != 0
					? _posSeries[bar - 1]
					: _negSeries[bar - 1];
		}

		#endregion
	}
}