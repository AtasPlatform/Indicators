namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	
	using OFT.Attributes;

	[DisplayName("Volume Bar Range Ratio")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45333-volume-bar-range-ratio")]
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