namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Elder Ray")]
	public class ElderRay : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _bearSeries = new(Resources.Bearlish);
		private readonly ValueDataSeries _bullSeries = new(Resources.Bullish);

		private readonly EMA _ema = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ElderRay()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_ema.Period = 10;

			_bullSeries.Color = Colors.Green;
			_bearSeries.Color = Colors.Red;
			DataSeries[0] = _bullSeries;
			DataSeries.Add(_bearSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_ema.Calculate(bar, candle.Close);
			_bullSeries[bar] = candle.High - _ema[bar];
			_bearSeries[bar] = candle.Low - _ema[bar];
		}

		#endregion
	}
}