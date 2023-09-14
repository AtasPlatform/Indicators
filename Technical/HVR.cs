namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Historical Volatility Ratio")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45248-historical-volatility-ratio")]
	public class HVR : Indicator
	{
		#region Fields
		
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly StdDev _shortDev = new() { Period = 6 };
		private readonly StdDev _longDev = new() { Period = 100 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Order = 100)]
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
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Order = 110)]
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
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var lr = (decimal)Math.Log((double)(candle.Close / prevCandle.Close));
			_renderSeries[bar] = _shortDev.Calculate(bar, lr) / _longDev.Calculate(bar, lr);
		}

		#endregion
	}
}