namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Properties;

	[DisplayName("1 Divided by Price")]
	public class DividedByPrice : Indicator
	{
		#region Fields

		private readonly CandleDataSeries _reversedCandles = new CandleDataSeries(Resources.Candles);

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