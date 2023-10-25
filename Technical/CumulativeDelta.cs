namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using OFT.Attributes;
using OFT.Localization;
using Utils.Common.Logging;

using Color = System.Drawing.Color;

[DisplayName("CVD - Cumulative Volume Delta")]
[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/412-cumulative-delta")]
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

    private CandleDataSeries _candleSeries = new("CandleSeries", Strings.Candles) { UseMinimizedModeIfEnabled = true };
    private ValueDataSeries _lineHistSeries = new("LineHistSeries", Strings.Line)
    {
        UseMinimizedModeIfEnabled = true,
        VisualType = VisualMode.Hide,
        ShowZeroValue = false
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

    #endregion

    #region Properties

    #region Settings

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Settings), Order = 10)]
    public SessionDeltaVisualMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;

            if (_mode == SessionDeltaVisualMode.Candles)
            {
                _candleSeries.Visible = true;
                _lineHistSeries.VisualType = VisualMode.Hide;
            }
            else
            {
                _candleSeries.Visible = false;

                _lineHistSeries.VisualType = _mode == SessionDeltaVisualMode.Line
                    ? VisualMode.Line
                    : VisualMode.Histogram;
            }

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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionDeltaMode), GroupName = nameof(Strings.Settings), Order = 20)]
    public SessionMode SessionCumDeltaMode 
    { 
        get => _sessionCumDeltaMode;
        set
        {
            _sessionCumDeltaMode = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSessionStart), GroupName = nameof(Strings.Settings), Order = 25)]
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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseScale), GroupName = nameof(Strings.Settings), Order = 30)]
    public bool UseScale
    {
        get => LineSeries[0].UseScale;
        set => LineSeries[0].UseScale = value;
    }

    #endregion

    #region Alerts

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Order = 110)]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Order = 120)]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RequiredChange), GroupName = nameof(Strings.Alerts), Order = 130)]
    public decimal ChangeSize
    {
        get => _changeSize;
        set
        {
            _changeSize = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Order = 140)]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Order = 150)]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    #endregion

    #region Drawing

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Order = 210)]
    public System.Windows.Media.Color PosColor
    {
        get => _posColor.Convert();
        set
        {
            _posColor = value.Convert();
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Order = 220)]
    public System.Windows.Media.Color NegColor
    {
        get => _negColor.Convert();
        set
        {
            _negColor = value.Convert();
            RecalculateValues();
        }
    }

    #endregion

    #endregion

    #region ctor

    public CumulativeDelta()
        : base(true)
    {
        var series = (ValueDataSeries)DataSeries[0];
        series.VisualType = VisualMode.Hide;

        LineSeries.Add(new LineSeries("ZeroId", "Zero") { Color = Colors.Gray, Width = 1, UseScale = false });

        DataSeries[0] = _lineHistSeries;
        DataSeries.Add(_candleSeries);

        Panel = IndicatorDataProvider.NewPanel;
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

            LineSeries[0].PropertyChanged += (sender, arg) =>
            {
                if (arg.PropertyName == "UseScale" || arg.PropertyName == "Value")
                    RecalculateValues();
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
	        var zero = LineSeries[0].Value;

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

            if (Mode is SessionDeltaVisualMode.Bars)
            {
                if (_cumDelta >= zero)
                    _lineHistSeries.Colors[bar] = _posColor;
                else
                    _lineHistSeries.Colors[bar] = _negColor;

                return;
            }

            if(_currentCandle is null)
            {
                _currentCandle = new();
                _candleSeries[bar] = _currentCandle;
            }
            
            _currentCandle.Close = _cumDelta;
            _currentCandle.High = _high;
            _currentCandle.Low = _low;
            _currentCandle.Open = _open;
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
}