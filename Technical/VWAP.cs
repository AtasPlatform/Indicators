namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Input;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("VWAP/TWAP")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8569-vwap")]
	public class VWAP : Indicator
	{
		#region Nested types

		public enum VWAPMode
		{
			VWAP = 0,
			TWAP = 1
		}

		public enum VWAPPeriodType
		{
			Hourly,
			Daily,
			Weekly,
			Monthly,
			All,
			Custom
		}

		#endregion

		#region Fields

		private readonly int _lastbar = -1;
		private readonly ValueDataSeries _lower = new("Lower std1") { Color = Colors.DodgerBlue };
		private readonly ValueDataSeries _lower1 = new("Lower std2") { Color = Colors.DodgerBlue };
		private readonly ValueDataSeries _lower2 = new("Lower std3") { Color = Colors.DodgerBlue, VisualType = VisualMode.Hide };

		private readonly ValueDataSeries _prevNegValueSeries = new("Previous lower value")
			{ Color = Colors.IndianRed, VisualType = VisualMode.Cross, Width = 5 };

		private readonly ValueDataSeries _prevPosValueSeries = new("Previous upper value") { Color = Colors.Green, VisualType = VisualMode.Cross, Width = 5 };

		private readonly ValueDataSeries _sqrt = new("sqrt");
		private readonly ValueDataSeries _totalVolToClose = new("volToClose");

		private readonly ValueDataSeries _totalVolume = new("totalVolume");
		private readonly ValueDataSeries _upper = new("Upper std1") { Color = Colors.DodgerBlue };
		private readonly ValueDataSeries _upper1 = new("Upper std2") { Color = Colors.DodgerBlue };
		private readonly ValueDataSeries _upper2 = new("Upper std3") { Color = Colors.DodgerBlue, VisualType = VisualMode.Hide };
		private bool _allowCustomStartPoint;

		private TimeSpan _customSession;
		private int _days;

		private int _n;

		private int _period = 300;
		private VWAPPeriodType _periodType = VWAPPeriodType.Daily;
		private bool _resetOnSession;
		private decimal _stdev = 1;
		private decimal _stdev1 = 2;
		private decimal _stdev2 = 2.5m;
		private decimal _sum;
		private int _targetBar;
		private VWAPMode _twapMode = VWAPMode.VWAP;
		private bool _userCalculation;
		private int _zeroBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "AllowCustomStartPoint", GroupName = "CustomVWAP", Order = 100001)]
		public bool AllowCustomStartPoint
		{
			get => _allowCustomStartPoint;
			set
			{
				_allowCustomStartPoint = value;

				if (!_allowCustomStartPoint)
					StartBar = _targetBar = 0;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SetStartPoint", GroupName = "CustomVWAP", Order = 100010)]
		public Key StartKey { get; set; } = Key.F;

		[Display(ResourceType = typeof(Resources), Name = "DeleteStartPoint", GroupName = "CustomVWAP", Order = 100020)]
		public Key DeleteKey { get; set; } = Key.D;

		[Display(ResourceType = typeof(Resources), Name = "SaveStartPoint", GroupName = "CustomVWAP", Order = 100030)]
		public bool SavePoint { get; set; } = true;

		[Browsable(false)]
		public DateTime StartDate { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ResetOnSession", GroupName = "CustomVWAP", Order = 100040)]
		public bool ResetOnSession
		{
			get => _resetOnSession;
			set
			{
				_resetOnSession = value;
				RecalculateValues();
			}
		}

		[Browsable(false)]
		public int StartBar { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 10)]
		public VWAPPeriodType Type
		{
			get => _periodType;
			set
			{
				_periodType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Mode", GroupName = "Settings", Order = 20)]
		public VWAPMode TWAPMode
		{
			get => _twapMode;
			set
			{
				_twapMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 30)]
		public int Period
		{
			get => _period;
			set
			{
				_period = Math.Max(value, 1);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "FirstDev", GroupName = "Settings", Order = 40)]
		public decimal StDev
		{
			get => _stdev;
			set
			{
				_stdev = Math.Max(value, 0);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SecondDev", GroupName = "Settings", Order = 50)]
		public decimal StDev1
		{
			get => _stdev1;
			set
			{
				_stdev1 = Math.Max(value, 0);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ThirdDev", GroupName = "Settings", Order = 60)]
		public decimal StDev2
		{
			get => _stdev2;
			set
			{
				_stdev2 = Math.Max(value, 0);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CustomSessionStart", GroupName = "Settings", Order = 70)]
		public TimeSpan CustomSessionStart
		{
			get => _customSession;
			set
			{
				_customSession = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Settings", Order = 80)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VWAP()
		{
			_resetOnSession = true;
			_days = 20;
			var series = (ValueDataSeries)DataSeries[0];
			series.Color = Colors.Firebrick;
			DataSeries.Add(_lower2);
			DataSeries.Add(_upper2);
			DataSeries.Add(_lower1);
			DataSeries.Add(_upper1);
			DataSeries.Add(_lower);
			DataSeries.Add(_upper);
			DataSeries.Add(_prevPosValueSeries);
			DataSeries.Add(_prevNegValueSeries);
		}

		#endregion

		#region Public methods

		public override bool ProcessKeyDown(KeyEventArgs e)
		{
			if (!AllowCustomStartPoint)
				return false;

			if (e.Key == DeleteKey)
			{
				_targetBar = 0;
				StartDate = GetCandle(0).Time;
				RecalculateValues();
				RedrawChart();
				return false;
			}

			if (e.Key != StartKey)
				return false;

			var targetBar = ChartInfo.MouseLocationInfo.BarBelowMouse;

			if (targetBar <= -1)
				return false;

			_targetBar = targetBar;
			StartDate = GetCandle(targetBar).Time;
			_userCalculation = true;
			RecalculateValues();
			RedrawChart();
			_userCalculation = false;

			return false;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				if (SavePoint)
					_targetBar = BarFromDate(StartDate);
				DataSeries.ForEach(x => x.Clear());
				_totalVolToClose.Clear();
				_totalVolume.Clear();
				_sqrt.Clear();

				if (_userCalculation && SavePoint)
				{
					if (_targetBar > 0)
						DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(_targetBar - 1));
				}
				else
				{
					_targetBar = 0;

					if (_days > 0)
					{
						var days = 0;

						for (var i = CurrentBar - 1; i >= 0; i--)
						{
							_targetBar = i;

							if (!IsNewSession(i))
								continue;

							days++;

							if (days == _days)
								break;
						}

						if (_targetBar > 0)
							DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(_targetBar - 1));
					}
				}
			}

			if (bar < _targetBar)
				return;

			var needReset = false;
			var candle = GetCandle(bar);
			var volume = Math.Max(1, candle.Volume);
			var typical = (candle.Open + candle.Close + candle.High + candle.Low) / 4;

			if (bar == _targetBar)
			{
				_zeroBar = bar;
				_n = 0;
				_sum = 0;

				if (_twapMode == VWAPMode.TWAP)
					this[bar] = _totalVolToClose[bar] = _upper[bar] = _lower[bar] = _upper1[bar] = _lower1[bar] = _upper2[bar] = _lower2[bar] = typical;
				else
				{
					this[bar] = _upper[bar] = _lower[bar] = _upper1[bar] = _lower1[bar] = _upper1[bar] = _lower1[bar] = candle.Close;
					_totalVolToClose[bar] = 0;
				}

				return;
			}

			if (Type == VWAPPeriodType.Hourly && GetCandle(bar - 1).Time.Hour != candle.Time.Hour)
				needReset = true;
			else if (Type == VWAPPeriodType.Daily && IsNewSession(bar))
				needReset = true;
			else if (Type == VWAPPeriodType.Weekly && IsNewWeek(bar))
				needReset = true;
			else if (Type == VWAPPeriodType.Monthly && IsNewMonth(bar))
				needReset = true;
			else if (Type == VWAPPeriodType.Custom && IsNewCustomSession(bar))
				needReset = true;

			var setStartOfLine = needReset;

			if (setStartOfLine && Type == VWAPPeriodType.Daily && TimeFrame == "Daily")
				setStartOfLine = false;

			if (needReset && (AllowCustomStartPoint && _resetOnSession || !AllowCustomStartPoint))
			{
				_zeroBar = bar;
				_n = 0;
				_sum = 0;
				_totalVolume[bar] = volume;
				_totalVolToClose[bar] = _twapMode == VWAPMode.TWAP ? typical : candle.Close * volume;

				if (setStartOfLine)
				{
					((ValueDataSeries)DataSeries[0]).SetPointOfEndLine(bar - 1);
					_upper.SetPointOfEndLine(bar - 1);
					_lower.SetPointOfEndLine(bar - 1);
					_upper1.SetPointOfEndLine(bar - 1);
					_lower1.SetPointOfEndLine(bar - 1);
					_upper2.SetPointOfEndLine(bar - 1);
					_lower2.SetPointOfEndLine(bar - 1);
				}
			}
			else
			{
				_totalVolume[bar] = _totalVolume[bar - 1] + volume;
				_totalVolToClose[bar] = _totalVolToClose[bar - 1] + (_twapMode == VWAPMode.TWAP ? typical : candle.Close * volume);
			}

			if (_twapMode == VWAPMode.TWAP)
				this[bar] = _totalVolToClose[bar] / (bar - _zeroBar + 1);
			else
				this[bar] = _totalVolToClose[bar] / _totalVolume[bar];

			var sqrt = (decimal)Math.Pow((double)((candle.Close - this[bar]) / TickSize), 2);
			_sqrt[bar] = sqrt;

			var k = bar;

			if (_lastbar != bar)
			{
				_n = 0;
				_sum = 0;

				for (var j = 0; j < Period; j++, _n++, k--)
				{
					if (k < _zeroBar)
						break;

					_sum += _sqrt[k];
				}
			}

			var summ = _sum + sqrt;
			var stdDev = (decimal)Math.Sqrt((double)summ / (_n + 1));

			_upper[bar] = this[bar] + stdDev * _stdev * TickSize;
			_lower[bar] = this[bar] - stdDev * _stdev * TickSize;
			_upper1[bar] = this[bar] + stdDev * _stdev1 * TickSize;
			_lower1[bar] = this[bar] - stdDev * _stdev1 * TickSize;
			_upper2[bar] = this[bar] + stdDev * _stdev2 * TickSize;
			_lower2[bar] = this[bar] - stdDev * _stdev2 * TickSize;

			if (bar == 0)
				return;

			if (needReset)
			{
				if (this[bar - 1] < this[bar])
					_prevPosValueSeries[bar] = this[bar - 1];
				else
					_prevNegValueSeries[bar] = this[bar - 1];
			}
			else
			{
				var prevValue = _prevPosValueSeries[bar - 1] != 0
					? _prevPosValueSeries[bar - 1]
					: _prevNegValueSeries[bar - 1];

				if (candle.Close >= prevValue)
					_prevPosValueSeries[bar] = prevValue;
				else
					_prevNegValueSeries[bar] = prevValue;
			}
		}

		#endregion

		#region Private methods

		private int BarFromDate(DateTime date)
		{
			var bar = CurrentBar - 1;

			for (var i = CurrentBar - 1; i >= 0; i--)
			{
				var candle = GetCandle(i);
				bar = i;

				if (candle.Time <= date && candle.LastTime >= date)
					break;
			}

			return bar;
		}

		private bool IsNewCustomSession(int bar)
		{
			if (bar == 0)
				return true;

			var prevTime = GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
			var curTime = GetCandle(bar).Time.AddHours(InstrumentInfo.TimeZone);
			return curTime.TimeOfDay >= _customSession && (prevTime.TimeOfDay < _customSession || prevTime.Date < curTime.Date);
		}

		#endregion
	}
}