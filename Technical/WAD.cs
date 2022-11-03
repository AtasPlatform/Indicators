namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;

	using OFT.Attributes;

	[DisplayName("Accumulation / Distribution - Williams")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43351-accumulation-distribution-williams")]
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