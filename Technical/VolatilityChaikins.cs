namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Volatility - Chaikins")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45334-volatility-chaikins")]
	public class VolatilityChaikins : Indicator
	{
		#region Fields

		private readonly EMA _ema = new();
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

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

		public VolatilityChaikins()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_ema.Period = 10;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();

			var candle = GetCandle(bar);
			_ema.Calculate(bar, candle.High - candle.Low);

			if (bar < Period)
				return;

			if (_ema[bar] != 0)
				_renderSeries[bar] = 100 * (_ema[bar] - _ema[bar - Period]) / _ema[bar - Period];
		}

		#endregion
	}
}