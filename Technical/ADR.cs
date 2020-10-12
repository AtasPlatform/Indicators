namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	[DisplayName("ADR")]
	public class ADR : Indicator
	{
		#region Fields

		private readonly List<decimal> _adrPrev = new List<decimal>();
		private readonly List<decimal> _closeValues = new List<decimal>();

		private readonly SMA _sma = new SMA();
		private readonly Pen _style = new Pen(Color.Green, 2);
		private decimal _adrHigh;
		private decimal _adrLow;

		private int _daysPeriod;
		private int _lastBar;
		private bool _lastSessionAdded;
		private int _startSession;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "DaysPeriod", GroupName = "Common", Order = 20)]
		public int DaysPeriod
		{
			get => _daysPeriod;
			set
			{
				if (value <= 0)
					return;

				_daysPeriod = value;
				RecalculateValues();
			}
		}

		[Parameter]
		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ADR()
			: base(true)
		{
			_sma.Period = 10;
			_startSession = 0;
			_adrHigh = 0;
			_adrLow = 0;
			_lastSessionAdded = false;
			_daysPeriod = 3;
			_lastBar = -1;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);

			if (bar == 0)
			{
				HorizontalLinesTillTouch.Clear();
				_adrPrev.Clear();
				_closeValues.Clear();
				_startSession = bar;
				_adrHigh = 0;
				_adrLow = 0;
				_lastSessionAdded = false;
			}

			if (IsNewSession(bar) && bar != 0)
			{
				var lineLength = bar - _startSession;

				var avgRange = CalcAvg();

				if (avgRange != null)
				{
					var avgValue = _closeValues.Average();

					HorizontalLinesTillTouch.Add(new LineTillTouch(_startSession, avgValue + (avgRange ?? 0m) / 2.0m, _style, lineLength));

					var valueToShow = $"{avgRange / ChartInfo.PriceChartContainer.Step:0.00}";

					AddText($"avg{Guid.NewGuid()}", "AveR: " + valueToShow, true, bar, HorizontalLinesTillTouch.Last().FirstPrice,
						Color.Green, Color.Empty, 12, DrawingText.TextAlign.Left);

					HorizontalLinesTillTouch.Add(new LineTillTouch(_startSession, avgValue - (avgRange ?? 0m) / 2.0m, _style, lineLength));
				}

				_startSession = bar;
				_adrHigh = 0;
				_adrLow = 0;
				_closeValues.Clear();
			}

			var difference = currentCandle.High - currentCandle.Low;
			var adr = _sma.Calculate(bar, difference);

			var adrHigh = difference < adr
				? currentCandle.Low + adr
				: currentCandle.Close >= currentCandle.Open
					? currentCandle.Low + adr
					: currentCandle.High;

			var adrLow = difference < adr
				? currentCandle.High - adr
				: currentCandle.Close >= currentCandle.Open
					? currentCandle.Low
					: currentCandle.High - adr;

			if (_adrHigh < adrHigh)
				_adrHigh = adrHigh;

			if (_adrLow > adrLow || bar == 0 || IsNewSession(bar))
				_adrLow = adrLow;

			if (bar == SourceDataSeries.Count - 1)
			{
				if (IsNewSession(bar))
					_lastSessionAdded = false;

				var lineLength = bar - _startSession;

				if (!_lastSessionAdded)
				{
					var avgRange = CalcAvg();
					var avgValue = _closeValues.Average();

					HorizontalLinesTillTouch.Add(new LineTillTouch(_startSession, avgValue + (avgRange ?? 0m) / 2.0m, _style, lineLength));

					var valueToShow = $"{avgRange / ChartInfo.PriceChartContainer.Step:0.00}";

					AddText($"avg{Guid.NewGuid()}", "AveR: " + valueToShow, true, bar, HorizontalLinesTillTouch.Last().FirstPrice,
						Color.Green, Color.Empty, 12, DrawingText.TextAlign.Left);

					HorizontalLinesTillTouch.Add(new LineTillTouch(_startSession, avgValue - (avgRange ?? 0m) / 2.0m, _style, lineLength));

					_lastSessionAdded = true;
				}
				else
				{
					var lastInd = HorizontalLinesTillTouch.FindLastIndex(x => true);

					HorizontalLinesTillTouch[lastInd].FirstPrice = HorizontalLinesTillTouch[lastInd].SecondPrice
						= _adrHigh;

					HorizontalLinesTillTouch[lastInd - 1].FirstPrice = HorizontalLinesTillTouch[lastInd - 1].SecondPrice
						= _adrLow;
				}
			}

			if (_lastBar == bar)
				_closeValues.RemoveAt(_closeValues.Count - 1);

			_closeValues.Add(currentCandle.Close);
			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private decimal? CalcAvg()
		{
			decimal? avgValue = null;

			if (_adrPrev.Count == DaysPeriod)
			{
				avgValue = _adrPrev.Average(x => x);
				_adrPrev.RemoveAt(0);
			}

			_adrPrev.Add(_adrHigh - _adrLow);
			return avgValue;
		}

		#endregion
	}
}