namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Ask/Bid Volume Difference Bars")]
	public class AskBidBars : Indicator
	{
		#region Fields

		private bool _bigTradesIsReceived;
		private int _firstBar;
		private int _lastBar;
		private readonly CandleDataSeries _renderSeries = new CandleDataSeries(Resources.Candles);
		private List<MarketDataArg> _trades = new List<MarketDataArg>();

		#endregion

		#region ctor

		public AskBidBars()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_bigTradesIsReceived = false;
				_trades.Clear();
				DataSeries.ForEach(x => x.Clear());

				var totalBars = CurrentBar - 1;
				_firstBar = 0;
				_lastBar = totalBars;
				var lastCandle = GetCandle(CurrentBar - 1);

				for (var i = totalBars; i >= 0; i--)
				{
					if (!IsNewSession(i))
						continue;

					_firstBar = i;
					break;
				}

				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_firstBar).Time));
			}
			else
				_lastBar = bar;
		}

		protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
		{
			var trades = cumulativeTrades.SelectMany(x => x.Ticks).ToList();
			CalculateHistory(trades);

			_trades.AddRange(trades
				.Where(x => x.Time >= GetCandle(CurrentBar - 2).LastTime)
				.ToList());

			_bigTradesIsReceived = true;
		}

		#region Overrides of ExtendedIndicator

		protected override void OnNewTrade(MarketDataArg trade)
		{
			if (!_bigTradesIsReceived)
				return;

			var totalBars = CurrentBar - 1;

			lock (_trades)
			{
				_trades.Add(trade);

				var newBar = _lastBar < CurrentBar - 1;

				if (newBar)
				{
					_lastBar = CurrentBar - 1;

					_trades = _trades
						.Where(x => x.Time > GetCandle(totalBars - 2).LastTime)
						.ToList();
				}

				CalculateBarTrades(_trades, CurrentBar - 1);
			}
		}

		#endregion

		#endregion

		#region Private methods

		private void CalculateHistory(List<MarketDataArg> trades)
		{
			for (var i = _firstBar; i <= CurrentBar - 1; i++)
			{
				CalculateBarTrades(trades, i);
				RaiseBarValueChanged(i);
			}
		}

		private void CalculateBarTrades(List<MarketDataArg> trades, int bar)
		{
			var candle = GetCandle(bar);

			var candleTrades = trades
				.Where(x => x.Time >= candle.Time && x.Time <= candle.LastTime && x.Direction != TradeDirection.Between)
				.OrderBy(x => x.Time)
				.ToList();

			if (candleTrades.Count == 0)
				return;

			var renderCandle = new Candle();

			var max = 0m;
			var min = 0m;

			var ask = 0m;
			var bid = 0m;

			foreach (var trade in candleTrades)
			{
				if (trade.Direction == TradeDirection.Buy)
					ask += trade.Volume;
				else
					bid += trade.Volume;

				var diff = ask - bid;

				if (diff < min || min == 0)
					min = diff;

				if (diff > max || max == 0)
					max = diff;
			}

			var high = max;
			var low = min;
			var close = ask - bid;

			var open = 0m;

			if (candleTrades.Count == 1)
			{
				if (close > high)
					open = high;

				if (close <= high && close >= low)
					open = close;

				if (close < low)
					open = low;
			}
			else
			{
				if (high < 0)
					open = high;

				if (low <= 0 && high >= 0)
					open = 0;

				if (low > 0)
					open = low;
			}

			renderCandle.High = high;
			renderCandle.Low = low;
			renderCandle.Open = open;
			renderCandle.Close = close;

			_renderSeries[bar] = renderCandle;
		}

		#endregion
	}
}