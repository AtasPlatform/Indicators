namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.Windows.Media;

	using Utils.Common.Attributes;

	[DisplayName("Heiken Ashi")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17003-heiken-ashi")]
	public class HeikenAshi : Indicator
	{
		#region Fields

		private readonly PaintbarsDataSeries _bars = new PaintbarsDataSeries("Bars") { IsHidden = true };
		private readonly CandleDataSeries _candles = new CandleDataSeries("Heiken Ashi");

		#endregion

		#region ctor

		public HeikenAshi()
			: base(true)
		{
			DenyToChangePanel = true;
			DataSeries[0] = _bars;
			DataSeries.Add(_candles);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_bars[bar] = Colors.Transparent;
			if (bar == 0)
			{
				_candles[bar] = new Candle
				{
					Close = candle.Close,
					High = candle.High,
					Low = candle.Low,
					Open = candle.Open
				};
			}
			else
			{
				var prevCandle = _candles[bar - 1];
				var close = (candle.Open + candle.Close + candle.High + candle.Low) * 0.25m;
				var open = (prevCandle.Open + prevCandle.Close) * 0.5m;
				var high = Math.Max(Math.Max(close, open), candle.High);
				var low = Math.Min(Math.Min(close, open), candle.Low);
				_candles[bar] = new Candle
				{
					Close = close,
					High = high,
					Low = low,
					Open = open
				};
			}
		}

		#endregion
	}
}