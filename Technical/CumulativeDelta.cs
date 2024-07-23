namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;
using Utils.Common.Logging;

using Color = System.Drawing.Color;

[DisplayName("CVD - Cumulative Volume Delta")]
[Category("Bid x Ask,Delta,Volume")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.CumulativeDeltaDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602360-cumulative-volume-delta")]
public class CumulativeDelta : Indicator
{
    #region Nested types

    [Serializable]
    public enum SessionDeltaVisualMode
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Candles))]
        Candles = 0,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bars))]
        Bars = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line))]
        Line = 2
    }

    public enum SessionMode
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
        None,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Default))]
        DefaultSession,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession))]
        CustomSession
    }

    #endregion

    #region Fields

    private CandleDataSeries _candleSeries = new("CandleSeries", Strings.Candles) 
    { 
        UseMinimizedModeIfEnabled = true ,
        IsHidden = true
    };

    private ValueDataSeries _lineHistSeries = new("LineHistSeries", Strings.Line)
    {
        UseMinimizedModeIfEnabled = true,
        VisualType = VisualMode.Hide,
        ShowZeroValue = false,
        IsHidden = true
    };

    private bool _isAlerted;
    private int _lastBar = -1;
    private bool _subscribedToChangeZeroLine;
    private Candle _currentCandle;

    private decimal _cumDelta;
    private decimal _open;
    private decimal _high;
    private decimal _low;
    private SessionDeltaVisualMode _mode = SessionDeltaVisualMode.Candles;
    private Color _negColor = Color.Red;
    private Color _posColor = Color.Green;
    private bool _sessionDeltaMode;
    private decimal _changeSize;
    private TimeSpan _customSessionStart;
    private SessionMode _sessionCumDeltaMode = SessionMode.DefaultSession;
    private bool _isVisible = true;

    #endregion

    #region Properties

    #region Settings

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ChartDisplayModeDescription), Order = 10)]
    public SessionDeltaVisualMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;

            if (_mode == SessionDeltaVisualMode.Candles)
            {
                _candleSeries.Visible = _isVisible;
                _lineHistSeries.VisualType = VisualMode.Hide;
            }
            else
            {
                _candleSeries.Visible = false;

                _lineHistSeries.VisualType = !_isVisible
                    ? VisualMode.Hide
                    : _mode == SessionDeltaVisualMode.Line
                          ? VisualMode.Line
                          : VisualMode.Histogram;
            }

            SetFiltersEnabled();
            RecalculateValues();
        }
    }

    [Browsable(false)]
    public bool SessionDeltaMode
    {
        get => _sessionDeltaMode;
        set
        {
            _sessionDeltaMode = value;

            if (_sessionDeltaMode)
                _sessionCumDeltaMode = SessionMode.DefaultSession;
            else
                _sessionCumDeltaMode = SessionMode.None;

            RaisePropertyChanged("SessionDeltaMode");
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionDeltaMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionModeDescription), Order = 20)]
    public SessionMode SessionCumDeltaMode 
    { 
        get => _sessionCumDeltaMode;
        set
        {
            _sessionCumDeltaMode = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSessionStart), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SessionBeginDescription), Order = 25)]
    public TimeSpan CustomSessionStart
    {
        get => _customSessionStart;
        set
        {
            _customSessionStart = value;

            if (_sessionCumDeltaMode == SessionMode.CustomSession)
                RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseScale), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DisplayFromZeroDescription), Order = 30)]
    public bool UseScale
    {
        get => LineSeries[0].UseScale;
        set => LineSeries[0].UseScale = value;
    }

    #endregion

    #region Alerts

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription), Order = 110)]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 120)]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RequiredChange), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFilterDescription), Order = 130)]
    public decimal ChangeSize
    {
        get => _changeSize;
        set
        {
            _changeSize = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription), Order = 140)]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription), Order = 150)]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    #endregion

    #region Drawing

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 210)]
    public CrossColor PosColor
    {
        get => _posColor.Convert();
        set
        {
            _posColor = value.Convert();
            _candleSeries.UpCandleColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 220)]
    public CrossColor NegColor
    {
        get => _negColor.Convert();
        set
        {
            _negColor = value.Convert();
            _candleSeries.DownCandleColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.AxisTextColorDescription), Order = 230)]
    public CrossColor TextColor
    {
        get => _candleSeries.ValuesColor.Convert();
        set
        {
           _candleSeries.ValuesColor = _lineHistSeries.ValuesColor = value.Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.BorderColorDescription), Order = 240)]
    public Indicators.FilterColor BorderColorFilter { get; set; } = new(false) { Value = Color.Gray.Convert() };


    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ElementDisplayModeDescription), Order = 250)]
    public FilterEnum<CandleVisualMode> CandleModeFilter { get; set; } = new(false);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowValue), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ShowValueOnLabelDescription), Order = 260)]
    public bool ShowValue
    {
        get => _candleSeries.ShowCurrentValue;
        set
        {
            _candleSeries.ShowCurrentValue = _lineHistSeries.ShowCurrentValue = value;
            RecalculateValues();
        }
    }


    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Visible), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.IsVisibleDescription), Order = 270)]
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = _candleSeries.Visible = value;
            _lineHistSeries.VisualType = !value
                                       ? VisualMode.Hide
                                       : _mode switch
                                       {
                                           SessionDeltaVisualMode.Bars => VisualMode.Histogram,
                                           SessionDeltaVisualMode.Line => VisualMode.Line,
                                           _ => VisualMode.Hide
                                       };
        }
    }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.WidthDataSeriesDescription), Order = 280)]
    public FilterInt WidthFilter { get; set; } = new(false) { Value = 1 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineStyle), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LineDashStyleDescription), Order = 290)]
    public FilterEnum<LineDashStyle> LineStyleFilter { get; set; } = new(false);

    #endregion

    #endregion

    #region ctor

    public CumulativeDelta()
        : base(true)
    {
        var series = (ValueDataSeries)DataSeries[0];
        series.VisualType = VisualMode.Hide;

        var zeroLine = new LineSeries("ZeroId", "Zero")
        {
            Color = DefaultColors.Gray.Convert(),
            Width = 1,
            UseScale = false,
            DescriptionKey = nameof(Strings.ZeroLineDescription)
        };

        LineSeries.Add(zeroLine);

        DataSeries[0] = _lineHistSeries;
        DataSeries.Add(_candleSeries);

        Panel = IndicatorDataProvider.NewPanel;
        DenyToChangePanel = true;

        SetFiltersEnabled();

        BorderColorFilter.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(BorderColorFilter.Value))
            {
                _candleSeries.BorderColor = BorderColorFilter.Value;
                RedrawChart();
            }
        };

        CandleModeFilter.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(CandleModeFilter.Value))
            {
                _candleSeries.Mode = CandleModeFilter.Value;
                BorderColorFilter.Enabled = CandleModeFilter.Value == CandleVisualMode.Candles;
                RedrawChart();
            }
        };

        WidthFilter.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(WidthFilter.Value))
            {
                _lineHistSeries.Width = WidthFilter.Value;
                RedrawChart();
            }
        };

        LineStyleFilter.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(LineStyleFilter.Value))
            {
                _lineHistSeries.LineDashStyle = LineStyleFilter.Value;
                RedrawChart();
            }
        };
    }

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        _posColor = ChartInfo.ColorsStore.UpCandleColor;
        _negColor = ChartInfo.ColorsStore.DownCandleColor;
        _lineHistSeries.Color = _candleSeries.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
        _candleSeries.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
        _candleSeries.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (!_subscribedToChangeZeroLine)
        {
            _subscribedToChangeZeroLine = true;

            _lineHistSeries.ZeroValue = LineSeries[0].Value;

            LineSeries[0].PropertyChanged += (sender, arg) =>
            {
	            if (arg.PropertyName == "UseScale" || arg.PropertyName == "Value")
	            {
		            _lineHistSeries.ZeroValue = LineSeries[0].Value;
                    RecalculateValues();
	            }
            };
        }

        var candle = GetCandle(bar);

        if (bar != _lastBar)
        {
            _currentCandle = null;
            _isAlerted = false;
        }

        try
        {
	        var zero = 0;// LineSeries[0].Value;

            if (CheckStartBar(bar))
            {
                _open = zero;
                _low = zero + candle.MinDelta;
                _high = zero + candle.MaxDelta;
                _cumDelta = zero + candle.Delta;

                if (bar > 0)
                    _lineHistSeries.SetPointOfEndLine(bar - 1);
            }
            else
            {
                var prev = (decimal)DataSeries[0][bar - 1];
                _open = prev;
                _cumDelta = prev + candle.Delta;
                var dh = candle.MaxDelta - candle.Delta;
                var dl = candle.Delta - candle.MinDelta;
                _low = _cumDelta - dl;
                _high = _cumDelta + dh;
            }

            _lineHistSeries[bar] = _cumDelta;

            switch (Mode)
            {
                case SessionDeltaVisualMode.Candles:

                    if (_currentCandle is null)
                    {
                        _currentCandle = new();
                        _candleSeries[bar] = _currentCandle;
                    }

                    _currentCandle.Close = _cumDelta;
                    _currentCandle.High = _high;
                    _currentCandle.Low = _low;
                    _currentCandle.Open = _open;

                    break;
                case SessionDeltaVisualMode.Bars:
                case SessionDeltaVisualMode.Line:

                    if (_cumDelta >= LineSeries[0].Value)
                        _lineHistSeries.Colors[bar] = _posColor;
                    else
                        _lineHistSeries.Colors[bar] = _negColor;
                    break;
            }
        }
        catch (Exception exc)
        {
            this.LogError("CumulativeDelta calculation error", exc);
        }

        if (bar == CurrentBar - 1)
        {
            if (UseAlerts && Math.Abs(candle.Delta) >= _changeSize && !_isAlerted)
            {
                AddAlert(AlertFile, InstrumentInfo.Instrument, "Delta changed!", AlertBGColor.Convert(), AlertForeColor.Convert());
                _isAlerted = true;
            }
        }

        _lastBar = bar;
    }

    private bool CheckStartBar(int bar)
    {
        switch (_sessionCumDeltaMode)
        {
            case SessionMode.None:
                return bar == 0;
            case SessionMode.DefaultSession:
                return IsNewSession(bar);
            case SessionMode.CustomSession:
                if (bar == 0)
                    return true;

                var candle = GetCandle(bar);
                var prevCandle = GetCandle(bar - 1);

                return prevCandle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay < _customSessionStart
                    && candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay >= _customSessionStart;
            default:
                return false;
        }
    }

    #endregion

    #region Private methods

    private void SetFiltersEnabled()
    {
        BorderColorFilter.Enabled = _mode == SessionDeltaVisualMode.Candles && _candleSeries.Mode == CandleVisualMode.Candles;
        CandleModeFilter.Enabled  = _mode == SessionDeltaVisualMode.Candles;
        WidthFilter.Enabled       = _mode != SessionDeltaVisualMode.Candles;
        LineStyleFilter.Enabled   = _mode == SessionDeltaVisualMode.Line;
    }

    #endregion
}