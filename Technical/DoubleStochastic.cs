namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Double Stochastic")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DoubleStochasticDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602610")]
	public class DoubleStochastic : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private readonly EMA _emaSecond = new() { Period = 10 };

		private readonly Highest _max = new() { Period = 10 };
		private readonly Lowest _min = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _max.Period;
			set
			{
				_max.Period = _min.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMAPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _ema.Period;
			set
			{
				_ema.Period = _emaSecond.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DoubleStochastic()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var max = _max.Calculate(bar, candle.High);
			var min = _min.Calculate(bar, candle.Low);

			var fastK1 = max - min != 0
				? 100m * (candle.Close - min) / (max - min)
				: 100m * (candle.Close - min);

            var fastD1 = _ema.Calculate(bar, fastK1);

			var maxD1 = ((ValueDataSeries)_ema.DataSeries[0]).MAX(_max.Period, bar);
			var minD1 = ((ValueDataSeries)_ema.DataSeries[0]).MIN(_max.Period, bar);

			var fastK2 = maxD1 - minD1 != 0
				? 100m * (fastD1 - minD1) / (maxD1 - minD1)
				: 100m * (fastD1 - minD1);
			
			_renderSeries[bar] = _emaSecond.Calculate(bar, fastK2);
		}

		#endregion
	}
}