namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Schaff Trend Cycle")]
	public class SchaffTrendCycle : Indicator
	{
		#region Fields

		private readonly Highest _highestMacd = new Highest();
		private readonly Highest _highestPf = new Highest();
		private readonly EMA _longMA = new EMA();
		private readonly Lowest _lowestMacd = new Lowest();
		private readonly Lowest _lowestPf = new Lowest();
		private readonly MACD _macd = new MACD();
		private readonly EMA _shortMA = new EMA();

		private decimal _lastBar;
		private decimal _lastF1;
		private decimal _lastF2;
		private decimal _lastPf;
		private decimal _lastPff;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common")]
		public int Period
		{
			get => _highestMacd.Period;
			set
			{
				if (value <= 0)
					return;

				_highestMacd.Period = value;
				_lowestMacd.Period = value;
				_highestPf.Period = value;
				_lowestPf.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Common")]
		public int ShortPeriod
		{
			get => _shortMA.Period;
			set
			{
				if (value <= 0)
					return;

				_shortMA.Period = value;
				_macd.ShortPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Common")]
		public int LongPeriod
		{
			get => _longMA.Period;
			set
			{
				if (value <= 0)
					return;

				_longMA.Period = value;
				_macd.LongPeriod = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SchaffTrendCycle()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_longMA.Period = _macd.LongPeriod = 50;
			_shortMA.Period = _macd.ShortPeriod = 23;
			_highestMacd.Period = _lowestMacd.Period = 10;
			_highestPf.Period = _lowestPf.Period = 10;

			LineSeries.Add(new LineSeries("Up") { Value = 75, Width = 1 });
			LineSeries.Add(new LineSeries("Down") { Value = 25, Width = 1 });
			((ValueDataSeries)DataSeries[0]).Width = 2;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var macd = _macd.Calculate(bar, candle.Close);

			var v1 = _lowestMacd.Calculate(bar, macd);
			var v2 = _highestMacd.Calculate(bar, macd) - v1;

			var f1 = v2 > 0
				? (macd - v1) / v2 * 100.0m
				: _lastF1;

			var pf = _lastPf == 0
				? f1
				: _lastPf + 0.5m * (f1 - _lastPf);

			var v3 = _lowestPf.Calculate(bar, pf);
			var v4 = _highestPf.Calculate(bar, pf) - v3;

			var f2 = v4 > 0
				? (pf - v3) / v4 * 100m
				: _lastF2;

			var pff = _lastPff == 0
				? f2
				: _lastPff + 0.5m * (f2 - _lastPff);

			this[bar] = pff;

			if (_lastBar != bar)
			{
				_lastF1 = f1;
				_lastF2 = f2;
				_lastPf = pf;
				_lastPff = pff;
				_lastBar = bar;
			}
		}

		#endregion
	}
}