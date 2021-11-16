namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Percentage Price Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45502-percentage-price-oscillator")]
	public class PercentagePrice : Indicator
	{
		#region Fields

		private readonly EMA _emaLong = new();
		private readonly EMA _emaShort = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _emaShort.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		public int LongPeriod
		{
			get => _emaLong.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public PercentagePrice()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_emaShort.Period = 5;
			_emaLong.Period = 20;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaLong.Calculate(bar, value);
			_emaShort.Calculate(bar, value);

			if (bar == 0)
				return;

			if (_emaLong[bar] != 0)
				_renderSeries[bar] = 100 * (_emaShort[bar] - _emaLong[bar]) / _emaLong[bar];
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}