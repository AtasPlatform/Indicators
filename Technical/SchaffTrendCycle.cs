namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Schaff Trend Cycle")]
	public class SchaffTrendCycle : Indicator
	{
		#region Fields

		private readonly Highest _highestMacd = new Highest();
		private readonly Highest _highestPf = new Highest();
		private readonly EMA _longMa = new EMA();
		private readonly Lowest _lowestMacd = new Lowest();
		private readonly Lowest _lowestPf = new Lowest();
		private readonly MACD _macd = new MACD();
		private readonly EMA _shortMa = new EMA();

		private decimal _lastBar;
		private bool _lastBarCalculated;
		private readonly List<decimal> _lastF1 = new List<decimal>();
		private readonly List<decimal> _lastF2 = new List<decimal>();
		private readonly List<decimal> _lastPf = new List<decimal>();
		private readonly List<decimal> _lastPff = new List<decimal>();

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
			get => _shortMa.Period;
			set
			{
				if (value <= 0)
					return;

				_shortMa.Period = value;
				_macd.ShortPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Common")]
		public int LongPeriod
		{
			get => _longMa.Period;
			set
			{
				if (value <= 0)
					return;

				_longMa.Period = value;
				_macd.LongPeriod = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SchaffTrendCycle()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_lastBar = -1;
			_longMa.Period = _macd.LongPeriod = 50;
			_shortMa.Period = _macd.ShortPeriod = 23;
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
			if (bar == 0)
				_lastBarCalculated = false;

			decimal lastF1 = 0, lastF2 = 0, lastPf = 0, lastPff = 0;

			if (bar != _lastBar)
			{
				if (_lastBarCalculated)
				{
					lastF1 = _lastF1.FirstOrDefault();
					lastF2 = _lastF2.FirstOrDefault();
					lastPf = _lastPf.FirstOrDefault();
					lastPff = _lastPff.FirstOrDefault();
				}
				else
				{
					lastF1 = _lastF1.LastOrDefault();
					lastF2 = _lastF2.LastOrDefault();
					lastPf = _lastPf.LastOrDefault();
					lastPff = _lastPff.LastOrDefault();
				}
			}
			else
				_lastBarCalculated = true;

			var candle = GetCandle(bar);

			var macd = _macd.Calculate(bar, candle.Close);

			var v1 = _lowestMacd.Calculate(bar, macd);
			var v2 = _highestMacd.Calculate(bar, macd) - v1;

			var f1 = v2 > 0
				? (macd - v1) / v2 * 100.0m
				: lastF1;

			var pf = lastPf == 0
				? f1
				: lastPf + 0.5m * (f1 - lastPf);

			var v3 = _lowestPf.Calculate(bar, pf);
			var v4 = _highestPf.Calculate(bar, pf) - v3;

			var f2 = v4 > 0
				? (pf - v3) / v4 * 100m
				: lastF2;

			var pff = lastPff == 0
				? f2
				: lastPff + 0.5m * (f2 - lastPff);

			this[bar] = pff;

			

			if (bar == _lastBar)
			{
				_lastF1.RemoveAt(1);
				_lastF2.RemoveAt(1);
				_lastPf.RemoveAt(1);
				_lastPff.RemoveAt(1);
			}
			_lastF1.Add(f1);
			_lastF2.Add(f2);
			_lastPf.Add(pf);
			_lastPff.Add(pff);

			if (_lastF1.Count > 2)
			{
				_lastF1.RemoveAt(0);
				_lastF2.RemoveAt(0);
				_lastPf.RemoveAt(0);
				_lastPff.RemoveAt(0);
			}

			_lastBar = bar;
		}

		#endregion
	}
}