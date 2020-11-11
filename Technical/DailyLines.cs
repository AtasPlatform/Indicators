namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Reflection;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Logging;

	[DisplayName("Daily Lines")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17029-daily-lines")]
	public class DailyLines : Indicator
	{
		#region Nested types

		[Serializable]
		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum Period
		{
			[Display(ResourceType = typeof(Resources), Name = "CurrentDay")]
			CurrentDay,

			[Display(ResourceType = typeof(Resources), Name = "PreviousDay")]
			PreviousDay,

			[Display(ResourceType = typeof(Resources), Name = "CurrentWeek")]
			CurrenWeek,

			[Display(ResourceType = typeof(Resources), Name = "PreviousWeek")]
			PreviousWeek,

			[Display(ResourceType = typeof(Resources), Name = "CurrentMonth")]
			CurrentMonth,

			[Display(ResourceType = typeof(Resources), Name = "PreviousMonth")]
			PreviousMonth
		}

		#endregion

		#region Fields

		private readonly LineSeries _lsClose = new LineSeries("Close") { Color = Colors.Red };
		private readonly LineSeries _lsHigh = new LineSeries("High") { Color = Colors.Red };
		private readonly LineSeries _lsLow = new LineSeries("Low") { Color = Colors.Red };
		private readonly LineSeries _lsOpen = new LineSeries("Open") { Color = Colors.Red };

		private decimal _close;
		private DynamicLevels.DynamicCandle _currentCandle = new DynamicLevels.DynamicCandle();
		private decimal _high;
		private int _lastNewSessionBar;
		private decimal _low;
		private decimal _open;
		private DynamicLevels.DynamicCandle _previousCandle = new DynamicLevels.DynamicCandle();
		private bool _showTest = true;
		private bool _tickBasedCalculation;
		private Period per = Period.PreviousDay;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters")]
		public Period period
		{
			get => per;
			set
			{
				per = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "Text")]
		public bool ShowText
		{
			get => _showTest;
			set
			{
				_showTest = value;
				RecalculateValues();
			}
		}

		private decimal Open
		{
			get => _open;
			set
			{
				if (_open != value)
					_lsOpen.Value = value;
				_open = value;
			}
		}

		private decimal High
		{
			get => _high;
			set
			{
				if (_high != value)
					_lsHigh.Value = value;
				_high = value;
			}
		}

		private decimal Low
		{
			get => _low;
			set
			{
				if (_low != value)
					_lsLow.Value = value;
				_low = value;
			}
		}

		private decimal Close
		{
			get => _close;
			set
			{
				if (_close != value)
					_lsClose.Value = value;
				_close = value;
			}
		}

		#endregion

		#region ctor

		public DailyLines()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).ScaleIt = false;
			((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			LineSeries.Add(_lsOpen);
			LineSeries.Add(_lsHigh);
			LineSeries.Add(_lsLow);
			LineSeries.Add(_lsClose);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Daily Lines";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			try
			{
				if (bar == 0)
				{
					_tickBasedCalculation = false;
					_currentCandle = new DynamicLevels.DynamicCandle();
					_previousCandle = new DynamicLevels.DynamicCandle();
					_lastNewSessionBar = -1;

					var periodStr = "";
					switch (period)
					{
						case Period.CurrentDay:
						{
							periodStr = "Curr. Day ";
							break;
						}
						case Period.PreviousDay:
						{
							periodStr = "Prev. Day ";
							break;
						}
						case Period.CurrenWeek:
						{
							periodStr = "Curr. Week ";
							break;
						}
						case Period.PreviousWeek:
						{
							periodStr = "Prev. Week ";
							break;
						}
						case Period.CurrentMonth:
						{
							periodStr = "Curr. Month ";
							break;
						}
						case Period.PreviousMonth:
						{
							periodStr = "Prev. Month ";
							break;
						}
						default:
							throw new ArgumentOutOfRangeException();
					}

					foreach (var lineSeries in LineSeries)
					{
						if (ShowText)
							lineSeries.Text = periodStr + lineSeries.Name;
						else
							lineSeries.Text = "";
					}

					return;
				}

				if (bar != _lastNewSessionBar)
				{
					if ((period == Period.CurrentDay || period == Period.PreviousDay) && IsNewSession(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if ((period == Period.CurrenWeek || period == Period.PreviousWeek) && IsNewWeek(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if ((period == Period.CurrentMonth || period == Period.PreviousMonth) && IsNewMonth(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
				}

				if (!_tickBasedCalculation)
					_currentCandle.AddCandle(GetCandle(bar), InstrumentInfo.TickSize);

				var showedCandle = period == Period.CurrentDay || period == Period.CurrenWeek || period == Period.CurrentMonth
					? _currentCandle
					: _previousCandle;

				if (bar == CurrentBar - 1)
				{
					Open = showedCandle.Open;
					Close = showedCandle.Close;
					High = showedCandle.High;
					Low = showedCandle.Low;
					_tickBasedCalculation = true;
				}
			}
			catch (Exception e)
			{
				this.LogError("Daily lines error ", e);
			}
		}

		protected override void OnNewTrade(MarketDataArg arg)
		{
			if (_tickBasedCalculation)
				_currentCandle.AddTick(arg);
		}

		#endregion
	}
}