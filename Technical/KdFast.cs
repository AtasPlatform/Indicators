namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("KD - Fast")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45425-kd-fast")]
	public class KdFast : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dSeries = new(Resources.SMA)
		{
			Color = DefaultColors.Green.Convert(),
			IgnoredByAlerts = true
		};
		private readonly Highest _highest = new() { Period = 10 };

		private readonly ValueDataSeries _kSeries = new(Resources.Line) { Color = DefaultColors.Red.Convert() };
		private readonly Lowest _lowest = new() { Period = 10 };
        private readonly SMA _sma = new() { Period = 10 };

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "PeriodK", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int PeriodK
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PeriodD", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
		public int PeriodD
		{
			get => _sma.Period;
			set
			{
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

			_kSeries[bar] = high != low
				? 100m * (candle.Close - low) / (high - low)
				: 100m;

			_dSeries[bar] = _sma.Calculate(bar, _kSeries[bar]);
		}

		#endregion
	}
}