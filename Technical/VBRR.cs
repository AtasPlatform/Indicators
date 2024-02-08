namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Volume Bar Range Ratio")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VBRRDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602499")]
	public class VBRR : Indicator
	{
		#region ctor

		public VBRR()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
				return;

			this[bar] = candle.High != candle.Low
				? candle.Volume / (candle.High - candle.Low)
				: this[bar - 1];
		}

		#endregion
	}
}