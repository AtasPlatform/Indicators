namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using Utils.Common.Attributes;

	[DisplayName("Awesome Oscillator")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/16995-awesome-oscillator")]
	public class AwesomeOscillator : Indicator
	{
		#region Fields

		private readonly CandleDataSeries _reversalCandles = new CandleDataSeries("Candles");
		private int p1 = 34;
		private int p2 = 5;

		#endregion

		#region Properties

		public int P1
		{
			get => p1;
			set
			{
				p1 = value;
				RecalculateValues();
			}
		}

		public int P2
		{
			get => p2;
			set
			{
				p2 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AwesomeOscillator()
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DataSeries.Add(_reversalCandles);
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Awesome Oscillator";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar >= p1 - 1)
			{
				var f = bar;
				decimal sma1 = 0;
				decimal sma2 = 0;
				for (var ct = 1; ct <= p1; ct += 1)
				{
					var candleCt = GetCandle(f);
					sma1 = sma1 + (candleCt.High + candleCt.Low) / 2;
					if (ct <= p2)
						sma2 = sma2 + (candleCt.High + candleCt.Low) / 2;
					f -= 1;
				}

				var Aw = sma2 / p2 - sma1 / p1;
				var lastAw = bar >= 0 ? _reversalCandles[bar - 1].Close : Aw;
				if (Aw >= lastAw)
				{
					if (Aw > 0)
					{
						_reversalCandles[bar].Open = 0;
						_reversalCandles[bar].Close = Aw;
						_reversalCandles[bar].High = Aw;
						_reversalCandles[bar].Low = 0;
					}
					else
					{
						_reversalCandles[bar].Open = Aw;
						_reversalCandles[bar].Close = 0;
						_reversalCandles[bar].High = 0;
						_reversalCandles[bar].Low = Aw;
					}
				}
				else
				{
					if (Aw > 0)
					{
						_reversalCandles[bar].Open = Aw;
						_reversalCandles[bar].Close = 0;
						_reversalCandles[bar].High = Aw;
						_reversalCandles[bar].Low = 0;
					}
					else
					{
						_reversalCandles[bar].Open = 0;
						_reversalCandles[bar].Close = Aw;
						_reversalCandles[bar].High = 0;
						_reversalCandles[bar].Low = Aw;
					}
				}

				this[bar] = Aw;
			}
		}

		#endregion
	}
}