namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Highest High/Lowest Low Over N Bars")]
	[FeatureId("NotReady")]
	public class HighLow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeries = new("High");
		private readonly ValueDataSeries _lowSeries = new("Low");

		private readonly ValueDataSeries _maxSeries = new(Resources.Highest);
		private readonly ValueDataSeries _minSeries = new(Resources.Lowest);
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

		#endregion

		#region ctor

		public HighLow()
		{
			_period = 15;

			_maxSeries.Color = Colors.Green;
			_minSeries.Color = Colors.Red;

			DataSeries[0] = _maxSeries;
			DataSeries.Add(_minSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highSeries[bar] = candle.High;
			_lowSeries[bar] = candle.Low;

			_maxSeries[bar] = _highSeries.MAX(_period, bar);
			_minSeries[bar] = _lowSeries.MIN(_period, bar);
		}

		#endregion
	}
}