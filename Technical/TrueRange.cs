namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("True Range")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.TrueRangeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602234")]
	public class TrueRange : Indicator
	{
		#region ctor

		public TrueRange()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var highLow = candle.High - candle.Low;
			var highCloseDiff = Math.Abs(candle.High - prevCandle.Close);
			var lowCloseDiff = Math.Abs(candle.Low - prevCandle.Close);

			var trueRange = Math.Max(highLow, highCloseDiff);
			trueRange = Math.Max(trueRange, lowCloseDiff);

			this[bar] = trueRange;
		}

		#endregion
	}
}