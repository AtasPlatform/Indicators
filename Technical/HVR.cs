namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Historical Volatility Ratio")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.HVRDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602393")]
	public class HVR : Indicator
	{
		#region Fields
		
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly StdDev _shortDev = new() { Period = 6 };
		private readonly StdDev _longDev = new() { Period = 100 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortStdDevPeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int ShortPeriod
		{
			get => _shortDev.Period;
			set
			{
				_shortDev.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.LongStdDevPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _longDev.Period;
			set
			{
				_longDev.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HVR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_shortDev.Calculate(0, 0);
				_longDev.Calculate(0, 0);
				_renderSeries[0] = 0;
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var lr = (decimal)Math.Log((double)(candle.Close / prevCandle.Close));
			var shortDeviation = _shortDev.Calculate(bar, lr);
			var longDeviation = _longDev.Calculate(bar, lr);
			_renderSeries[bar] = longDeviation == 0 ? 0 : shortDeviation / longDeviation;
		}

		#endregion
	}
}
