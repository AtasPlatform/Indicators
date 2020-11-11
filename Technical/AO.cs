namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Awesome Oscillator")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/16995-awesome-oscillator")]
	public class AwesomeOscillator : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _negative;
		private readonly ValueDataSeries _neutral;
		private readonly ValueDataSeries _positive;
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
			Panel = IndicatorDataProvider.NewPanel;

			DataSeries.Clear();

			_positive = new ValueDataSeries("Positive")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Green,
				ShowZeroValue = false,
				IsHidden = true
			};

			_negative = new ValueDataSeries("Negative")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Red,
				ShowZeroValue = false,
				IsHidden = true
			};

			_neutral = new ValueDataSeries("Neutral")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Gray,
				ShowZeroValue = false,
				IsHidden = true
			};

			DataSeries.Add(_positive);
			DataSeries.Add(_negative);
			DataSeries.Add(_neutral);
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
			if (bar == 0)
			{
				_positive.Clear();
				_negative.Clear();
				_neutral.Clear();
			}

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
				var lastAw = 0.0m;

				if (bar > 0)
				{
					if (_positive[bar - 1] != 0)
						lastAw = _positive[bar - 1];
					else if (_negative[bar - 1] != 0)
						lastAw = _negative[bar - 1];
				}

				if (Aw > lastAw)
					_positive[bar] = Aw;
				else if (Aw < lastAw)
					_negative[bar] = Aw;
				else
					_neutral[bar] = Aw;

				this[bar] = Aw;
			}
		}

		#endregion
	}
}