namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Accumulation / Distribution - Williams")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.WADDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602568")]
	public class WAD : Indicator
	{
		#region ctor

		public WAD()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			if (candle.Close > prevCandle.Close)
				this[bar] = this[bar - 1] + candle.Close - Math.Min(candle.Low, prevCandle.Close);
			else if (candle.Close < prevCandle.Close)
				this[bar] = this[bar - 1] + candle.Close - Math.Max(candle.High, prevCandle.Close);
			else
				this[bar] = this[bar - 1];
		}

		#endregion
	}
}