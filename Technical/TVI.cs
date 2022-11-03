namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Trade Volume Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45339-trade-volume-index")]
	public class TVI : Indicator
	{
		#region ctor

		public TVI()
		{
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var volume = GetCandle(bar).Volume;

			if (value - (decimal)SourceDataSeries[bar - 1] > InstrumentInfo.TickSize)
				this[bar] = this[bar - 1] + volume;
			else if (value - (decimal)SourceDataSeries[bar - 1] == InstrumentInfo.TickSize)
				this[bar] = this[bar - 1];
			else
				this[bar] = this[bar - 1] - volume;
		}

		#endregion
	}
}