namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("AD")]
	[LocalizedDescription(typeof(Resources), "AD_Description")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8022-ad")]
	public class AD : Indicator
	{
		#region ctor

		public AD()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var prev = bar == 0 ? 0m : this[bar - 1];

			var diff = candle.High - candle.Low;

			this[bar] = diff == 0
				? prev
				: candle.Close - candle.Low - (candle.High - candle.Close) * candle.Volume / diff + prev;
		}

		#endregion
	}
}