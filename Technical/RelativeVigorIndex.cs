namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Localization;

	[DisplayName("Relative Vigor Index")]
	public class RelativeVigorIndex : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _rviSeries = new("RVI");
		private readonly ValueDataSeries _signalSeries = new(Strings.Signal);
		private readonly SMA _smaRvi = new();
		private readonly SMA _smaSig = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "SignalPeriod", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _smaSig.Period;
			set
			{
				if (value <= 0)
					return;

				_smaSig.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		public int SmaPeriod
		{
			get => _smaRvi.Period;
			set
			{
				if (value <= 0)
					return;

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

			_signalSeries.Color = Colors.Blue;
			_rviSeries.Color = Colors.Red;

			_smaRvi.Period = 4;
			_smaSig.Period = 10;

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

			_rviSeries[bar] = decimal.Round(_smaRvi.Calculate(bar, rvi), 4);

			_signalSeries[bar] = decimal.Round(_smaSig.Calculate(bar, rvi), 4);
		}

		#endregion
	}
}