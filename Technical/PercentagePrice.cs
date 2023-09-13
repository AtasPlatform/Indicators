namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Percentage Price Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45502-percentage-price-oscillator")]
	public class PercentagePrice : Indicator
	{
		#region Fields

		private readonly EMA _emaLong = new() { Period = 20 };
		private readonly EMA _emaShort = new() { Period = 5 };

        private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int ShortPeriod
		{
			get => _emaShort.Period;
			set
			{
				_emaShort.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _emaLong.Period;
			set
			{
				_emaLong.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public PercentagePrice()
		{
			Panel = IndicatorDataProvider.NewPanel;
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

			_renderSeries[bar] = _emaLong[bar] != 0
				? 100 * (_emaShort[bar] - _emaLong[bar]) / _emaLong[bar]
				: _renderSeries[bar - 1];
		}

		#endregion
	}
}