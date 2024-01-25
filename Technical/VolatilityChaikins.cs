namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Volatility - Chaikins")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolatilityChaikinsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602497")]
	public class VolatilityChaikins : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
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
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				Clear();

			var candle = GetCandle(bar);
			_ema.Calculate(bar, candle.High - candle.Low);

			if (bar < Period)
				return;

			if (_ema[bar] != 0)
				this[bar] = 100 * (_ema[bar] - _ema[bar - Period]) / _ema[bar - Period];
		}

		#endregion
	}
}