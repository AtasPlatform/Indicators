namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Price Volume Trend")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45342-price-volume-trend")]
	public class VolumeTrend : Indicator
	{
		#region ctor

		public VolumeTrend()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			LineSeries.Add(new LineSeries(Resources.ZeroValue) { Color = Colors.Gray, Value = 0, Width = 2 });
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

			this[bar] = candle.Close != 0
				? (candle.Close - prevCandle.Close) / candle.Close * candle.Volume + this[bar - 1]
				: this[bar - 1];
		}

		#endregion
	}
}