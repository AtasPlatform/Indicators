namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Stochastic RSI")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43362-stochastic-rsi")]
	public class StochasticRsi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period = 10;
		private RSI _rsi = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int RsiPeriod
		{
			get => _rsi.Period;
			set
			{
				_rsi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StochasticRsi()
		{
			Panel = IndicatorDataProvider.NewPanel;
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
				_renderSeries[bar] = (_rsi[bar] - minRsi) / (maxRsi - minRsi);

			_renderSeries[bar] = Math.Max(0.01m, _renderSeries[bar]);
		}

		#endregion
	}
}