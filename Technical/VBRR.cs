namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Volume Bar Range Ratio")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45333-volume-bar-range-ratio")]
	public class VBRR : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region ctor

		public VBRR()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
				return;

			if (candle.High != candle.Low)
				_renderSeries[bar] = candle.Volume / (candle.High - candle.Low);
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}