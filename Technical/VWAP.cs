namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("VWAP/TWAP")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.VWAPDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602503")]
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

    public enum VolumeType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Total))]
        Total,
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
        Bid,
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
        Ask
    }

    #endregion

    #region Fields

    private static readonly VisualMode _upperLowerDefaultMode = VisualMode.Hide;
    private static readonly CrossColor _upperLowerDefaultColor = GetColorFromHex("#FF00BCD4");
    private static readonly CrossColor _upperFillDefaultColor = GetColorFromHex("#99C30101");
    private static readonly CrossColor _middleFillDefaultColor = GetColorFromHex("#99808080");
    private static readonly CrossColor _lowerFillDefaultColor = GetColorFromHex("#9900FF00");

    private readonly ValueDataSeries _lower = new("Lower", Strings.LowerStd1) 
    {
        Color = _upperLowerDefaultColor,
        VisualType = _upperLowerDefaultMode
    };

    private readonly ValueDataSeries _lower1 = new("Lower1", Strings.LowerStd2) 
    {
        Color = _upperLowerDefaultColor,
        VisualType = _upperLowerDefaultMode
    };

    private readonly ValueDataSeries _lower2 = new("Lower2", Strings.LowerStd3)
    { 
        Color = _upperLowerDefaultColor, 
        VisualType = _upperLowerDefaultMode
    };

    private readonly ValueDataSeries _upper = new("Upper", Strings.UpperStd1)
    {
        Color = _upperLowerDefaultColor,
        VisualType = _upperLowerDefaultMode
    };

    private readonly ValueDataSeries _upper1 = new("Upper1", Strings.UpperStd2)
    {
        Color = _upperLowerDefaultColor,
        VisualType = _upperLowerDefaultMode
    };

    private readonly ValueDataSeries _upper2 = new("Upper2", Strings.UpperStd3)
    {
        Color = _upperLowerDefaultColor,
        VisualType = _upperLowerDefaultMode
    };

    private readonly RangeDataSeries _lower2Background = new("Lower2Background", Strings.LowerFill2)
    {
        RangeColor = _lowerFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false
    };

    private readonly RangeDataSeries _lower2BackgroundRes = new("Lower2BackgroundRes", "Lower Fill 2 res")
    {
        RangeColor = _lowerFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly RangeDataSeries _lowerBackground = new("LowerBackground", Strings.LowerFill)
    {
        RangeColor = _lowerFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false
    };

    private readonly RangeDataSeries _lowerBackgroundRes = new("LowerBackgroundRes", "Lower Fill res")
    {
        RangeColor = _lowerFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly RangeDataSeries _midDownBackground = new("MidDownBackground", Strings.MiddleFillDown)
    {
        RangeColor = _middleFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false
    };

    private readonly RangeDataSeries _midDownBackgroundRes = new("MidDownBackgroundRes", "Middle Fill Down res")
    {
        RangeColor = _middleFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly RangeDataSeries _midUpBackground = new("MidUpBackground", Strings.MiddleFillUp)
    {
        RangeColor = _middleFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
    };

    private readonly RangeDataSeries _midUpBackgroundRes = new("MidUpBackgroundRes", "Middle Fill Up Res")
    {
        RangeColor = _middleFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly RangeDataSeries _upper2Background = new("Upper2Background", Strings.UpperFill2)
    {
        RangeColor = _upperFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false
    };

    private readonly RangeDataSeries _upper2BackgroundRes = new("Upper2BackgroundRes", "Upper Fill 2 res")
    {
        RangeColor = _upperFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly RangeDataSeries _upperBackground = new("UpperBackground", Strings.UpperFill)
    {
        RangeColor = _upperFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false
    };

    private readonly RangeDataSeries _upperBackgroundRes = new("UpperBackgroundRes", "Upper Fill res")
    {
        RangeColor = _upperFillDefaultColor,
        DrawAbovePrice = false,
        Visible = false,
        IsHidden = true
    };

    private readonly ValueDataSeries _prevNegValueSeries = new("PrevNegValueSeries", "Previous lower value")
    {
        Color = GetColorFromHex("#FFCD5C5C"),
        VisualType = VisualMode.Cross,
        Width = 5
    };

    private readonly ValueDataSeries _prevPosValueSeries = new("PrevPosValueSeries", "Previous upper value")
    {
        Color = GetColorFromHex("#FF4CAF50"),
        VisualType = VisualMode.Cross,
        Width = 5
    };

    private readonly ValueDataSeries _sumSrcSrcVol = new("sumSrcSrcVol");
    private readonly ValueDataSeries _totalVolToClose = new("volToClose");
    private readonly ValueDataSeries _totalVolume = new("totalVolume");
    private readonly ValueDataSeries _vwapTwap = new("VwapTwap", "VWAP|TWAP") 
    {
        Width = 3,
        Color = GetColorFromHex("#FFFF5252"),
        DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
    };

    private bool _allowCustomStartPoint;
    private bool _calcStarted;
    private int _days;
    private bool _isReserved;

    private int _period = 300;
    private VWAPPeriodType _periodType = VWAPPeriodType.Daily;
    private bool _showFirstPeriod;
    private decimal _stdev = 1;
    private decimal _stdev1 = 2;
    private decimal _stdev2 = 3;
    private int _targetBar;
    private VWAPMode _twapMode = VWAPMode.VWAP;
    private bool _userCalculation;
    private int _zeroBar;
    private VolumeType _volumeMode = VolumeType.Total;
    private System.Drawing.Color _bullishColor = GetColorFromHex("#FF2196F3").Convert();
    private System.Drawing.Color _bearishColor = GetColorFromHex("#FFB22222").Convert();
    private bool _coloredDirection = true;
    private TimeSpan _customSessionEnd = new(23, 59, 59);
    private TimeSpan _customSessionStart;
    private bool _vWAPOnly = true;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AllowCustomStartPoint), GroupName = nameof(Strings.CustomVWAP), Description = nameof(Strings.AllowCustomStartPointDescription), Order = 1001)]
    public bool AllowCustomStartPoint
    {
        get => _allowCustomStartPoint;
        set
        {
            _allowCustomStartPoint = value;
            StartKeyFilter.Enabled = DeleteKeyFilter.Enabled = SavePointFilter.Enabled = ResetOnSessionFilter.Enabled = value;

            if (!_allowCustomStartPoint)
                StartBar = _targetBar = 0;

            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SetStartPoint), GroupName = nameof(Strings.CustomVWAP), Description = nameof(Strings.SetStartPointKeyFilterDescription), Order = 1010)]
    public FilterKey StartKeyFilter { get; set; } = new(false) { Value = CrossKey.F };

    [Browsable(false)]
    public CrossKey StartKey 
    { 
        get => StartKeyFilter.Value; 
        set => StartKeyFilter.Value = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DeleteStartPoint), GroupName = nameof(Strings.CustomVWAP), Description = nameof(Strings.DeleteStartPointKeyFilterDescription), Order = 1020)]

    public FilterKey DeleteKeyFilter { get; set; } = new(false) { Value = CrossKey.G };

    [Browsable(false)]
    public CrossKey DeleteKey
    {
        get => DeleteKeyFilter.Value;
        set => DeleteKeyFilter.Value = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SaveStartPoint), GroupName = nameof(Strings.CustomVWAP), Description = nameof(Strings.SaveCustomStartingPointDescription), Order = 1030)]
    public FilterBool SavePointFilter { get; set; } = new(false);

    [Browsable(false)]
    public bool SavePoint 
    { 
        get => SavePointFilter.Value;
        set => SavePointFilter.Value = value;
    }

    [Browsable(false)]
    public DateTime StartDate { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResetOnSession), GroupName = nameof(Strings.CustomVWAP), Description = nameof(Strings.ResetOnSessionFilterDescription), Order = 1040)]
    public FilterBool ResetOnSessionFilter { get; set; } = new(false);

    [Browsable(false)]
    public bool ResetOnSession
    {
        get => ResetOnSessionFilter.Value;
        set => ResetOnSessionFilter.Value = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColoredDirection), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColoredDirectionDescription), Order = 200)]
    [Range(1, 10000)]
    public bool ColoredDirection
    {
        get => _coloredDirection;
        set
        {
            _coloredDirection = value;

            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BullishColorDescription), Order = 210)]
    public CrossColor BullishColor
    {
        get => _bullishColor.Convert();
        set
        {
            _bullishColor = value.Convert();
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.BearishColorDescription), Order = 220)]
    public CrossColor BearishColor
    {
        get => _bearishColor.Convert();
        set
        {
            _bearishColor = value.Convert();
            RecalculateValues();
        }
    }

    [Browsable(false)]
    public int StartBar { get; set; }

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodTypeDescription), Order = 10)]
    public VWAPPeriodType Type
    {
        get => _periodType;
        set
        {
            _periodType = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 20)]
    public VWAPMode TWAPMode
    {
        get => _twapMode;
        set
        {
            _twapMode = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VolumeType), GroupName = nameof(Strings.Settings), Description = nameof(Strings.VolumeTypeDescription), Order = 25)]
    public VolumeType VolumeMode
    {
        get => _volumeMode;
        set
        {
            _volumeMode = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TwapPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 30)]
    [Range(1, 10000)]
    public int Period
    {
        get => _period;
        set
        {
            _period = Math.Max(value, 1);
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FirstDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 40)]
    [Range(0.0000001, 10000)]
    public decimal StDev
    {
        get => _stdev;
        set
        {
            _stdev = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SecondDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 50)]
    [Range(0.0000001, 10000)]
    public decimal StDev1
    {
        get => _stdev1;
        set
        {
            _stdev1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ThirdDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 60)]
    [Range(0.0000001, 10000)]
    public decimal StDev2
    {
        get => _stdev2;
        set
        {
            _stdev2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionBegin), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionBeginDescription), Order = 70)]
    public TimeSpan CustomSessionStart
    {
	    get => _customSessionStart;
	    set
	    {
		    _customSessionStart = value;
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionEnd), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionEndDescription), Order = 80)]
    public TimeSpan CustomSessionEnd
    {
	    get => _customSessionEnd;
	    set
	    {
		    _customSessionEnd = value;
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowFirstPartialPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShowFirstPeriodDescription), Order = 90)]
    public bool ShowFirstPeriod
    {
        get => _showFirstPeriod;
        set
        {
            _showFirstPeriod = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAPOnly), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShowBaseElementOnlyDescription), Order = 100)]
    public bool VWAPOnly
    { 
        get => _vWAPOnly;
        set
        {
            _vWAPOnly = value;
            SetVWAPOnly(_vWAPOnly);
        }
    }

    #endregion

    #region ctor

    public VWAP()
    {
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

        ResetOnSessionFilter.PropertyChanged += ResetOnSessionFilter_PropertyChanged;
    }

    private void ResetOnSessionFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Value")
            RecalculateValues();
    }

    #endregion

    #region Public methods

    public override bool ProcessKeyDown(CrossKeyEventArgs e)
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

        if (e.Key != StartKeyFilter.Value)
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

            if (SavePointFilter.Enabled && SavePointFilter.Value)
                _targetBar = BarFromDate(StartDate);
            DataSeries.ForEach(x => x.Clear());
            _totalVolToClose.Clear();
            _totalVolume.Clear();

            if (_userCalculation && SavePointFilter.Enabled && SavePointFilter.Value)
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

        if (Type is VWAPPeriodType.Custom && !InsideSession(bar))
        {
	        DataSeries.ForEach(x =>
	        {
		        if (x is ValueDataSeries series)
			        series.SetPointOfEndLine(bar - 1);
	        });
	        return;
        }

        if (!ShowFirstPeriod && !AllowCustomStartPoint && !_calcStarted
            && Type is VWAPPeriodType.Weekly or VWAPPeriodType.Monthly or VWAPPeriodType.Custom)
        {
	        if (bar == 0 && Type is not VWAPPeriodType.Custom)
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

        var volume = VolumeMode switch
        {
            VolumeType.Total => candle.Volume,
            VolumeType.Bid => candle.Bid,
            VolumeType.Ask => candle.Ask,
            _ => candle.Volume
        };

        var typical = value;

        if (bar == _targetBar)
        {
            _zeroBar = bar;
            _totalVolume[bar] = volume;
            _sumSrcSrcVol[bar] = volume * typical * typical;

            if (_twapMode == VWAPMode.TWAP)
                _vwapTwap[bar] = _totalVolToClose[bar] = _upper[bar] = _lower[bar] = _upper1[bar] = _lower1[bar] = _upper2[bar] = _lower2[bar] = typical;
            else
            {
                _totalVolToClose[bar] = typical * volume;

                _vwapTwap[bar] = _upper[bar] =
                    _lower[bar] = _upper1[bar] = _lower1[bar] = _upper2[bar] = _lower2[bar] = _totalVolToClose[bar] / _totalVolume[bar];
            }


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

		if (needReset && ((AllowCustomStartPoint && ResetOnSessionFilter.Enabled && ResetOnSessionFilter.Value) || !AllowCustomStartPoint))
		{
			_zeroBar = bar;
			_totalVolume[bar] = volume;
			_totalVolToClose[bar] = _twapMode == VWAPMode.TWAP ? typical : typical * volume;
			_sumSrcSrcVol[bar] = volume * typical * typical;

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

			if (_twapMode is VWAPMode.VWAP)
			{
				var barVariance = volume * typical * typical;
				_sumSrcSrcVol[bar] = _sumSrcSrcVol[bar - 1] + barVariance;
			}
		}

		decimal stdDev = 0m, currentValue, lastValue;

		if (_twapMode == VWAPMode.TWAP)
		{
			_vwapTwap[bar] = _totalVolToClose[bar] / (bar - _zeroBar + 1);
			currentValue = _vwapTwap[bar];
			lastValue = _vwapTwap[bar - 1];

			if (bar != _zeroBar)
			{
				var period = Math.Min(bar - _zeroBar, Period);
				var average = _vwapTwap.CalcAverage(period, bar);
				var sqrSum = 0m;

				for (var i = bar - period; i <= bar; i++)
				{
					var diff = average - _vwapTwap[i];
					sqrSum += diff * diff;
				}

				stdDev = (decimal)Math.Sqrt((double)sqrSum / period);
			}
		}
		else
		{
			_vwapTwap[bar] = _totalVolToClose[bar] / _totalVolume[bar];
			currentValue = _vwapTwap[bar];
			lastValue = _vwapTwap[bar - 1];

			var variance = _sumSrcSrcVol[bar] / _totalVolume[bar] - currentValue * currentValue;
			variance = variance < 0 ? 0 : variance;
			stdDev = (decimal)Math.Sqrt((double)variance);
		}

		if (bar - _zeroBar > 1) //temp solution, colored series point bug
		{
			if (ColoredDirection && bar != 0)
			{
				_vwapTwap.Colors[bar] = _vwapTwap[bar] > _vwapTwap[bar - 1]
					? _bullishColor
					: _bearishColor;
			}
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

    private void SetVWAPOnly(bool toHideAll)
    {
        _upperBackground.Visible = !toHideAll;
        _lowerBackground.Visible = !toHideAll;
        _upper2Background.Visible = !toHideAll;
        _lower2Background.Visible = !toHideAll;
        _midUpBackground.Visible = !toHideAll;
        _midDownBackground.Visible = !toHideAll;

        if (toHideAll)
        {
            _upper.VisualType = VisualMode.Hide;
            _upper1.VisualType = VisualMode.Hide;
            _upper2.VisualType = VisualMode.Hide;
            _lower.VisualType = VisualMode.Hide;
            _lower1.VisualType = VisualMode.Hide;
            _lower2.VisualType = VisualMode.Hide;
        }
        else
        {
            _upper.VisualType = VisualMode.Line;
            _upper1.VisualType = VisualMode.Line;
            _upper2.VisualType = VisualMode.Line;
            _lower.VisualType = VisualMode.Line;
            _lower1.VisualType = VisualMode.Line;
            _lower2.VisualType = VisualMode.Line;
        }
    }

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
		var currentBar = GetCandle(bar);
		var previousBar = bar > 0 ? GetCandle(bar - 1) : null;

		var startTime = currentBar.Time.AddHours(InstrumentInfo.TimeZone);
		var endTime = currentBar.LastTime.AddHours(InstrumentInfo.TimeZone);

		var prevEndTime = previousBar?.LastTime.AddHours(InstrumentInfo.TimeZone) ?? default;

		var sessionCrossesMidnight = _customSessionStart > _customSessionEnd;

		var isFirstBarNewSession = bar == 0 && (
			sessionCrossesMidnight
				? startTime.TimeOfDay <= _customSessionStart || endTime.TimeOfDay > _customSessionStart
				: startTime.TimeOfDay <= _customSessionStart && endTime.TimeOfDay > _customSessionStart
		);

		if (isFirstBarNewSession)
		{
			return true;
		}

		bool newSessionInCurrentBar;
		
		if (sessionCrossesMidnight)
		{
			newSessionInCurrentBar = (startTime.TimeOfDay <= _customSessionStart && endTime.TimeOfDay > _customSessionStart) ||
				(startTime.TimeOfDay > endTime.TimeOfDay && (endTime.TimeOfDay > _customSessionStart || startTime.TimeOfDay <= _customSessionStart));
		}
		else
		{
			newSessionInCurrentBar = startTime.TimeOfDay <= _customSessionStart && endTime.TimeOfDay > _customSessionStart;
		}

		var newSessionBetweenBars = previousBar != null && (
			sessionCrossesMidnight
				? (prevEndTime.TimeOfDay <= _customSessionStart && startTime.TimeOfDay > _customSessionStart) ||
				(prevEndTime.TimeOfDay > prevEndTime.TimeOfDay && _customSessionStart <= startTime.TimeOfDay)
				: prevEndTime.TimeOfDay <= _customSessionStart && startTime.TimeOfDay > _customSessionStart
		);

		return newSessionInCurrentBar || newSessionBetweenBars;
	}

    private bool InsideSession(int bar)
	{
		var currentBar = GetCandle(bar);

		var startTime = currentBar.Time.AddHours(InstrumentInfo.TimeZone);
		var endTime = currentBar.LastTime.AddHours(InstrumentInfo.TimeZone);

		var sessionCrossesMidnight = _customSessionStart > _customSessionEnd;

		bool isInSession;
		if (sessionCrossesMidnight)
		{
			isInSession = (startTime.TimeOfDay >= _customSessionStart || startTime.TimeOfDay <= _customSessionEnd) ||
				(endTime.TimeOfDay >= _customSessionStart || endTime.TimeOfDay <= _customSessionEnd) ||
				(startTime.TimeOfDay <= _customSessionEnd && endTime.TimeOfDay >= _customSessionStart);
		}
		else
		{
			isInSession = (startTime.TimeOfDay >= _customSessionStart && startTime.TimeOfDay <= _customSessionEnd) ||
				(endTime.TimeOfDay >= _customSessionStart && endTime.TimeOfDay <= _customSessionEnd) ||
				(startTime.TimeOfDay <= _customSessionEnd && endTime.TimeOfDay >= _customSessionStart);
		}

		return isInSession;
    }

    private static CrossColor GetColorFromHex(string hexString)
    {
        return (CrossColor)TypeDescriptor.GetConverter(typeof(CrossColor)).ConvertFromString(hexString);
    }

    #endregion
}