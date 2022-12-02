namespace ATAS.Indicators.Technical;

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
		M15,
		M30,
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
	private readonly ValueDataSeries _lower = new(Resources.LowerStd1) { Color = Colors.DodgerBlue };
	private readonly ValueDataSeries _lower1 = new(Resources.LowerStd2) { Color = Colors.DodgerBlue };
	private readonly ValueDataSeries _lower2 = new(Resources.LowerStd3) { Color = Colors.DodgerBlue };
	private readonly ValueDataSeries _vwapTwap = new("VWAP/TWAP") { Color = Colors.Firebrick };

	private readonly RangeDataSeries _lower2Background = new(Resources.LowerFill2)
	{
		RangeColor = Color.FromArgb(153, 0, 255, 0)
	};

	private readonly RangeDataSeries _lower2BackgroundRes = new("Lower Fill 2 res")
	{
		RangeColor = Color.FromArgb(153, 0, 255, 0),
		IsHidden = true
	};

	private readonly RangeDataSeries _lowerBackground = new(Resources.LowerFill)
	{
		RangeColor = Color.FromArgb(153, 0, 255, 0)
	};

	private readonly RangeDataSeries _lowerBackgroundRes = new("Lower Fill res")
	{
		RangeColor = Color.FromArgb(153, 0, 255, 0),
		IsHidden = true
	};

	private readonly RangeDataSeries _midDownBackground = new(Resources.MiddleFillDown)
	{
		RangeColor = Color.FromArgb(153, 128, 128, 128)
	};

	private readonly RangeDataSeries _midDownBackgroundRes = new("Middle Fill Down res")
	{
		RangeColor = Color.FromArgb(153, 128, 128, 128),
		IsHidden = true
	};

	private readonly RangeDataSeries _midUpBackground = new(Resources.MiddleFillUp)
	{
		RangeColor = Color.FromArgb(153, 128, 128, 128)
	};

	private readonly RangeDataSeries _midUpBackgroundRes = new("Middle Fill Up Res")
	{
		RangeColor = Color.FromArgb(153, 128, 128, 128),
		IsHidden = true
	};

	private readonly ValueDataSeries _prevNegValueSeries = new("Previous lower value")
		{ Color = Colors.IndianRed, VisualType = VisualMode.Cross, Width = 5 };

	private readonly ValueDataSeries _prevPosValueSeries = new("Previous upper value") { Color = Colors.Green, VisualType = VisualMode.Cross, Width = 5 };

	private readonly ValueDataSeries _sqrt = new("sqrt");
	private readonly ValueDataSeries _totalVolToClose = new("volToClose");

	private readonly ValueDataSeries _totalVolume = new("totalVolume");
	private readonly ValueDataSeries _squareSum = new("squareSum");
	private readonly ValueDataSeries _typical = new("typical");
	private readonly ValueDataSeries _upper = new(Resources.UpperStd1) { Color = Colors.DodgerBlue };
	private readonly ValueDataSeries _upper1 = new(Resources.UpperStd2) { Color = Colors.DodgerBlue };
	private readonly ValueDataSeries _upper2 = new(Resources.UpperStd3) { Color = Colors.DodgerBlue };

	private readonly RangeDataSeries _upper2Background = new(Resources.UpperFill2)
	{
		RangeColor = Color.FromArgb(153, 225, 0, 0)
	};

	private readonly RangeDataSeries _upper2BackgroundRes = new("Upper Fill 2 res")
	{
		RangeColor = Color.FromArgb(153, 225, 0, 0),
		IsHidden = true
	};

	private readonly RangeDataSeries _upperBackground = new(Resources.UpperFill)
	{
		RangeColor = Color.FromArgb(153, 225, 0, 0)
	};

	private readonly RangeDataSeries _upperBackgroundRes = new("Upper Fill res")
	{
		RangeColor = Color.FromArgb(153, 225, 0, 0),
		IsHidden = true
	};

	private bool _allowCustomStartPoint;
	private bool _calcStarted;

	private TimeSpan _customSession;
	private int _days;

	private bool _isReserved;

	private int _n;

	private int _period = 300;
	private VWAPPeriodType _periodType = VWAPPeriodType.Daily;
	private bool _resetOnSession;
	private bool _showFirstPeriod;
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
	[Range(0, 1000)]
	public int Days
	{
		get => _days;
		set
		{
			_days = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "ShowFirstPartialPeriod", GroupName = "Settings", Order = 80)]
	public bool ShowFirstPeriod
	{
		get => _showFirstPeriod;
		set
		{
			_showFirstPeriod = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public VWAP()
	{
		_resetOnSession = true;
		_days = 20;
		DataSeries[0] = _vwapTwap;
		DataSeries.Add(_lower2);
		DataSeries.Add(_upper2);
		DataSeries.Add(_lower1);
		DataSeries.Add(_upper1);
		DataSeries.Add(_lower);
		DataSeries.Add(_upper);
		DataSeries.Add(_prevPosValueSeries);
		DataSeries.Add(_prevNegValueSeries);

		_upper2Background.PropertyChanged += Upper2Changed;
		DataSeries.Add(_upper2Background);
		DataSeries.Add(_upper2BackgroundRes);

		_upperBackground.PropertyChanged += UpperChanged;
		DataSeries.Add(_upperBackground);
		DataSeries.Add(_upperBackgroundRes);

		_midUpBackground.PropertyChanged += MidUpChanged;
		DataSeries.Add(_midUpBackground);
		DataSeries.Add(_midUpBackgroundRes);

		_midDownBackground.PropertyChanged += MidDownChanged;
		DataSeries.Add(_midDownBackground);
		DataSeries.Add(_midDownBackgroundRes);

		_lowerBackground.PropertyChanged += LowerChanged;
		DataSeries.Add(_lowerBackground);
		DataSeries.Add(_lowerBackgroundRes);

		_lower2Background.PropertyChanged += Lower2Changed;
		DataSeries.Add(_lower2Background);
		DataSeries.Add(_lower2BackgroundRes);
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
	
	protected override void OnRecalculate()
	{
		_upper2BackgroundRes.Visible = _upper2Background.Visible;
		_upper2BackgroundRes.DrawAbovePrice = _upper2Background.DrawAbovePrice;

		_upperBackgroundRes.Visible = _upperBackground.Visible;
		_upperBackgroundRes.DrawAbovePrice = _upperBackground.DrawAbovePrice;

		_midUpBackgroundRes.Visible = _midUpBackground.Visible;
		_midUpBackgroundRes.DrawAbovePrice = _midUpBackground.DrawAbovePrice;

		_midDownBackgroundRes.Visible = _midDownBackground.Visible;
		_midDownBackgroundRes.DrawAbovePrice = _midDownBackground.DrawAbovePrice;

		_lowerBackgroundRes.Visible = _lowerBackground.Visible;
		_lowerBackgroundRes.DrawAbovePrice = _lowerBackground.DrawAbovePrice;

		_lower2BackgroundRes.Visible = _lower2Background.Visible;
		_lower2BackgroundRes.DrawAbovePrice = _lower2Background.DrawAbovePrice;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_calcStarted = false;

			if (SavePoint)
				_targetBar = BarFromDate(StartDate);
			DataSeries.ForEach(x => x.Clear());
			_totalVolToClose.Clear();
			_totalVolume.Clear();
			_sqrt.Clear();

			if (_userCalculation && SavePoint)
			{
				if (_targetBar > 0)
				{
					DataSeries.ForEach(x =>
					{
						if (x is ValueDataSeries series)
							series.SetPointOfEndLine(_targetBar - 1);
					});
				}
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
					{
						DataSeries.ForEach(x =>
						{
							if (x is ValueDataSeries series)
								series.SetPointOfEndLine(_targetBar - 1);
						});
					}
				}
			}
		}

		if (bar < _targetBar)
			return;

		if (!ShowFirstPeriod && !AllowCustomStartPoint && !_calcStarted
		    && Type is VWAPPeriodType.Weekly or VWAPPeriodType.Monthly or VWAPPeriodType.Custom)
		{
			if (bar == 0)
				return;

			switch (Type)
			{
				case VWAPPeriodType.Weekly:
					_calcStarted = IsNewWeek(bar);
					break;
				case VWAPPeriodType.Monthly:
					_calcStarted = IsNewMonth(bar);
					break;
				case VWAPPeriodType.Custom:
					_calcStarted = IsNewCustomSession(bar);
					break;
			}

			if (!_calcStarted)
				return;
		}

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
				this[bar] = _upper[bar] = _lower[bar] = _upper1[bar] = _lower1[bar] = _upper2[bar] = _lower2[bar] = candle.Close;
				_totalVolToClose[bar] = typical * volume;
			}

			_totalVolume[bar] = candle.Volume;
			return;
		}

		var prevCandle = GetCandle(bar - 1);

		switch (Type)
		{
			case VWAPPeriodType.M15 when (int)(prevCandle.Time.TimeOfDay.TotalMinutes / 15) != (int)(candle.Time.TimeOfDay.TotalMinutes / 15):
			case VWAPPeriodType.M30 when (int)(prevCandle.Time.TimeOfDay.TotalMinutes / 30) != (int)(candle.Time.TimeOfDay.TotalMinutes / 30):
			case VWAPPeriodType.Hourly when GetCandle(bar - 1).Time.Hour != candle.Time.Hour:
			case VWAPPeriodType.Daily when IsNewSession(bar):
			case VWAPPeriodType.Weekly when IsNewWeek(bar):
			case VWAPPeriodType.Monthly when IsNewMonth(bar):
			case VWAPPeriodType.Custom when IsNewCustomSession(bar):
				needReset = true;
				break;
		}

		var setStartOfLine = needReset;

		if (setStartOfLine && Type == VWAPPeriodType.Daily && ChartInfo.TimeFrame == "Daily")
			setStartOfLine = false;

		if (needReset && ((AllowCustomStartPoint && _resetOnSession) || !AllowCustomStartPoint))
		{
			_zeroBar = bar;
			_n = 0;
			_sum = 0;
			_totalVolume[bar] = volume;
			_totalVolToClose[bar] = _twapMode == VWAPMode.TWAP ? typical : typical * volume;

			if (setStartOfLine)
			{
				if (!_upper1.IsThisPointOfStartBar(bar - 1))
					_isReserved = !_isReserved;

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
			_totalVolToClose[bar] = _totalVolToClose[bar - 1] + (_twapMode == VWAPMode.TWAP ? typical : typical * volume);
		}

		if (_twapMode == VWAPMode.TWAP)
			this[bar] = _totalVolToClose[bar] / (bar - _zeroBar + 1);
		else
			this[bar] = _totalVolToClose[bar] / _totalVolume[bar];

		var currentValue = this[bar];
		var lastValue = this[bar - 1];
        
        var stdDev = 0m;
		
        if (bar != _zeroBar)
        {
	        var average = _vwapTwap.CalcAverage(bar - _zeroBar, bar);
	        var sqrSum = 0m;
	        
	        for (var i = bar; i >= _zeroBar; i--)
	        {
				var diff = average - _vwapTwap[i];

		        sqrSum += diff * diff;
	        }
			
	        stdDev = (decimal)Math.Sqrt((double)(sqrSum / (bar - _zeroBar)));
        }
		
        var std = stdDev * _stdev;
        var std1 = stdDev * _stdev1;
		var std2 = stdDev * _stdev2;

		_upper[bar] = currentValue + std;
		_lower[bar] = currentValue - std;
		_upper1[bar] = currentValue + std1;
		_lower1[bar] = currentValue - std1;
		_upper2[bar] = currentValue + std2;
		_lower2[bar] = currentValue - std2;

		SetBackgroundValues(bar, currentValue);

		if (bar == 0)
			return;

		if (needReset)
		{
			if (lastValue < currentValue)
				_prevPosValueSeries[bar] = lastValue;
			else
				_prevNegValueSeries[bar] = lastValue;
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

	private void SetBackgroundValues(int bar, decimal value)
	{
		if (_isReserved)
		{
			_upper2BackgroundRes[bar].Upper = _upper2[bar];
			_upper2BackgroundRes[bar].Lower = _upper1[bar];

			_upperBackgroundRes[bar].Upper = _upper1[bar];
			_upperBackgroundRes[bar].Lower = _upper[bar];

			_midUpBackgroundRes[bar].Upper = _upper[bar];
			_midUpBackgroundRes[bar].Lower = value;

			_midDownBackgroundRes[bar].Upper = value;
			_midDownBackgroundRes[bar].Lower = _lower[bar];

			_lowerBackgroundRes[bar].Upper = _lower[bar];
			_lowerBackgroundRes[bar].Lower = _lower1[bar];

			_lower2BackgroundRes[bar].Upper = _lower1[bar];
			_lower2BackgroundRes[bar].Lower = _lower2[bar];
		}
		else
		{
			_upper2Background[bar].Upper = _upper2[bar];
			_upper2Background[bar].Lower = _upper1[bar];

			_upperBackground[bar].Upper = _upper1[bar];
			_upperBackground[bar].Lower = _upper[bar];

			_midUpBackground[bar].Upper = _upper[bar];
			_midUpBackground[bar].Lower = value;

			_midDownBackground[bar].Upper = value;
			_midDownBackground[bar].Lower = _lower[bar];

			_lowerBackground[bar].Upper = _lower[bar];
			_lowerBackground[bar].Lower = _lower1[bar];

			_lower2Background[bar].Upper = _lower1[bar];
			_lower2Background[bar].Lower = _lower2[bar];
		}
	}

	private void Lower2Changed(object sender, PropertyChangedEventArgs e)
	{
		var value = _lower2Background.GetType().GetProperty(e.PropertyName)?.GetValue(_lower2Background, null);
		_lower2BackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_lower2BackgroundRes, value);
	}

	private void LowerChanged(object sender, PropertyChangedEventArgs e)
	{
		var value = _lowerBackground.GetType().GetProperty(e.PropertyName)?.GetValue(_lowerBackground, null);
		_lowerBackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_lowerBackgroundRes, value);
	}

	private void MidDownChanged(object sender, PropertyChangedEventArgs e)
	{
		var value = _midDownBackground.GetType().GetProperty(e.PropertyName)?.GetValue(_midDownBackground, null);
		_midDownBackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_midDownBackgroundRes, value);
	}

	private void MidUpChanged(object sender, PropertyChangedEventArgs e)
	{
		var value = _midUpBackground.GetType().GetProperty(e.PropertyName)?.GetValue(_midUpBackground, null);
		_midUpBackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_midUpBackgroundRes, value);
	}

	private void UpperChanged(object sender, PropertyChangedEventArgs e)
	{
		var value = _upperBackground.GetType().GetProperty(e.PropertyName)?.GetValue(_upperBackground, null);
		_upperBackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_upperBackgroundRes, value);
	}

	private void Upper2Changed(object sender, PropertyChangedEventArgs e)
	{
		var value = _upper2Background.GetType().GetProperty(e.PropertyName)?.GetValue(_upper2Background, null);
		_upper2BackgroundRes.GetType().GetProperty(e.PropertyName)?.SetValue(_upper2BackgroundRes, value);
	}

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
			return ShowFirstPeriod;

		var prevTime = GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
		var curTime = GetCandle(bar).Time.AddHours(InstrumentInfo.TimeZone);
		return curTime.TimeOfDay >= _customSession && (prevTime.TimeOfDay < _customSession || prevTime.Date < curTime.Date);
	}

	#endregion
}