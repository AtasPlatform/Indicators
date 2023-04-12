namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Relative Vigor Index")]
	public class RelativeVigorIndex : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _rviSeries = new("RVI") { IgnoredByAlerts = true };
		private readonly ValueDataSeries _signalSeries = new(Resources.Signal) { Color = DefaultColors.Blue.Convert() };
		private readonly SMA _smaRvi = new() { Period = 4 };
		private readonly SMA _smaSig = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _smaSig.Period;
			set
			{
				_smaSig.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _smaRvi.Period;
			set
			{
				_smaRvi.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public RelativeVigorIndex()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _signalSeries;
			DataSeries.Add(_rviSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var rvi = 0m;

			if (candle.High - candle.Low != 0)
				rvi = (candle.Close - candle.Open) / (candle.High - candle.Low);

			_rviSeries[bar] = _smaRvi.Calculate(bar, rvi);

			_signalSeries[bar] =_smaSig.Calculate(bar, rvi);
		}

		#endregion
	}
}