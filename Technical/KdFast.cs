namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("KD - Fast")]
	[FeatureId("NotReady")]
	public class KdFast : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dSeries = new(Resources.SMA);
		private readonly Highest _highest = new();

		private readonly ValueDataSeries _kSeries = new(Resources.Line);
		private readonly Lowest _lowest = new();
		private readonly SMA _sma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "PeriodK", GroupName = "Settings", Order = 100)]
		public int PeriodK
		{
			get => _highest.Period;
			set
			{
				if (value <= 0)
					return;

				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "Settings", Order = 110)]
		public int PeriodD
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KdFast()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_dSeries.Color = Colors.Green;
			_kSeries.Color = Colors.Red;

			_highest.Period = _lowest.Period = 10;
			_sma.Period = 10;

			DataSeries[0] = _kSeries;
			DataSeries.Add(_dSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var high = _highest.Calculate(bar, candle.High);
			var low = _lowest.Calculate(bar, candle.Low);

			if (high != low)
				_kSeries[bar] = 100m * (candle.Close - low) / (high - low);
			else
				_kSeries[bar] = 100m;

			_dSeries[bar] = _sma.Calculate(bar, _kSeries[bar]);
		}

		#endregion
	}
}