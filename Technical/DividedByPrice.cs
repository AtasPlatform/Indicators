namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("1 Divided by Price")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43353-1-divided-by-price")]
	public class DividedByPrice : Indicator
	{
		#region Fields

		private readonly CandleDataSeries _reversedCandles = new(Resources.Candles);

		#endregion

		#region ctor

		public DividedByPrice()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _reversedCandles;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var open = 1 / candle.Open;
			var close = 1 / candle.Close;
			var high = 1 / candle.High;
			var low = 1 / candle.Low;

			_reversedCandles[bar] = new Candle
			{
				Open = open,
				Close = close,
				High = low,
				Low = high
			};
		}

		#endregion
	}
}