namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Stochastic RSI")]
	public class StochasticRsi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private RSI _rsi = new RSI();
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Settings", Order = 100)]
		public int RsiPeriod
		{
			get => _rsi.Period;
			set
			{
				if (value <= 0)
					return;

				_rsi.Period = value;
				RecalculateValues();
			}
		}
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

		public StochasticRsi()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_rsi.Period = 10;
			_period = 10;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_rsi.Calculate(bar, value);

			if (bar == 0)
				return;

			var maxRsi = ((ValueDataSeries)_rsi.DataSeries[0]).MAX(_period, bar);
			var minRsi = ((ValueDataSeries)_rsi.DataSeries[0]).MIN(_period, bar);

			if (maxRsi - minRsi == 0)
				_renderSeries[bar] = _renderSeries[bar - 1];
			else
			{
				_renderSeries[bar] = (_rsi[bar] - minRsi) / (maxRsi - minRsi);
			}
		}

		#endregion
	}
}