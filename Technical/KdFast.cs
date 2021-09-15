namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("KD - Fast")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45425-kd-fast")]
	public class KdFast : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dSeries = new(Strings.SMA);
		private readonly Highest _highest = new();

		private readonly ValueDataSeries _kSeries = new(Strings.Line);
		private readonly Lowest _lowest = new();
		private readonly SMA _sma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "PeriodK", GroupName = "Settings", Order = 100)]
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

		[Display(ResourceType = typeof(Strings), Name = "PeriodD", GroupName = "Settings", Order = 110)]
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