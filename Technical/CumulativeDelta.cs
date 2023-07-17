namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

using Utils.Common.Logging;

using Color = System.Drawing.Color;

[DisplayName("Cumulative Delta Volume")]
[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/412-cumulative-delta")]
public class CumulativeDelta : Indicator
{
    #region Nested types

    [Serializable]
    public enum SessionDeltaVisualMode
    {
        [Display(ResourceType = typeof(Resources), Name = "Candles")]
        Candles = 0,

        [Display(ResourceType = typeof(Resources), Name = "Bars")]
        Bars = 1,

        [Display(ResourceType = typeof(Resources), Name = "Line")]
        Line = 2
    }

    #endregion

    #region Fields

    private CandleDataSeries _candleSeries = new("CandleSeries", Resources.Candles) { UseMinimizedModeIfEnabled = true };

    private decimal _cumDelta;
    private decimal _high;

    private bool _isAlerted;
    private int _lastBar = -1;

    private ValueDataSeries _lineHistSeries = new("LineHistSeries", Resources.Line)
    {
        UseMinimizedModeIfEnabled = true,
        VisualType = VisualMode.Hide,
        ShowZeroValue = false
    };

    private decimal _low;

    private SessionDeltaVisualMode _mode = SessionDeltaVisualMode.Candles;

    private Color _negColor = Color.Red;
    private decimal _open;
    private Color _posColor = Color.Green;

    private bool _sessionDeltaMode = true;
    private bool _subscribedToChangeZeroLine;
    private decimal _changeSize;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Settings", Order = 10)]
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

    [Display(ResourceType = typeof(Resources), Name = "SessionDeltaMode", GroupName = "Settings", Order = 20)]
    public bool SessionDeltaMode
    {
        get => _sessionDeltaMode;
        set
        {
            _sessionDeltaMode = value;
            RaisePropertyChanged("SessionDeltaMode");
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "UseScale", GroupName = "Settings", Order = 30)]
    public bool UseScale
    {
        get => LineSeries[0].UseScale;
        set => LineSeries[0].UseScale = value;
    }

    [Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 110)]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 120)]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Resources), Name = "RequiredChange", GroupName = "Alerts", Order = 130)]
    public decimal ChangeSize
    {
        get => _changeSize;
        set
        {
            _changeSize = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "FontColor", GroupName = "Alerts", Order = 140)]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Resources), Name = "BackGround", GroupName = "Alerts", Order = 150)]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    #endregion

    [Display(ResourceType = typeof(Resources), Name = "Positive", GroupName = "Drawing", Order = 210)]
    public System.Windows.Media.Color PosColor
    {
        get => _posColor.Convert();
        set
        {
            _posColor = value.Convert();
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "Negative", GroupName = "Drawing", Order = 220)]
    public System.Windows.Media.Color NegColor
    {
        get => _negColor.Convert();
        set
        {
            _negColor = value.Convert();
            RecalculateValues();
        }
    }

    #region ctor

    public CumulativeDelta()
        : base(true)
    {
        var series = (ValueDataSeries)DataSeries[0];
        series.VisualType = VisualMode.Hide;

        LineSeries.Add(new LineSeries("Zero") { Color = Colors.Gray, Width = 1, UseScale = false });

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

    protected override void OnCalculate(int i, decimal value)
    {
        if (!_subscribedToChangeZeroLine)
        {
            _subscribedToChangeZeroLine = true;

            LineSeries[0].PropertyChanged += (sender, arg) =>
            {
                if (arg.PropertyName == "UseScale")
                    RecalculateValues();
            };
        }

        if (i == 0)
            _cumDelta = 0;

        var currentCandle = GetCandle(i);

        try
        {
            var newSession = false;

            if (i > CurrentBar)
                return;

            if (SessionDeltaMode && i > 0 && IsNewSession(i))
            {
                _open = _cumDelta = _high = _low = 0;
                newSession = true;
            }

            if (newSession)
                _lineHistSeries.SetPointOfEndLine(i - 1);



            if (i == 0 || newSession)
                _cumDelta += currentCandle.Ask - currentCandle.Bid;
            else
            {
                if (SessionDeltaMode && i > 0 && IsNewSession(i))
                {
                    _open = 0;
                    _low = currentCandle.MinDelta;
                    _high = currentCandle.MaxDelta;
                    _cumDelta = currentCandle.Delta;
                }
                else
                {
                    var prev = (decimal)DataSeries[0][i - 1];
                    _open = prev;
                    _cumDelta = prev + currentCandle.Delta;
                    var dh = currentCandle.MaxDelta - currentCandle.Delta;
                    var dl = currentCandle.Delta - currentCandle.MinDelta;
                    _low = _cumDelta - dl;
                    _high = _cumDelta + dh;
                }
            }

            _lineHistSeries[i] = _cumDelta;

            if (Mode is SessionDeltaVisualMode.Bars)
            {
                if (_cumDelta >= 0)
                    _lineHistSeries.Colors[i] = _posColor;
                else
                    _lineHistSeries.Colors[i] = _negColor;

                return;
            }

            _candleSeries[i] = new Candle
            {
                Close = _cumDelta,
                High = _high,
                Low = _low,
                Open = _open
            };
        }
        catch (Exception exc)
        {
            this.LogError("CumulativeDelta calculation error", exc);
        }

        if (_lastBar != i)
        {
            _isAlerted = false;
        }

        if (i == CurrentBar - 1)
        {
            if (UseAlerts && Math.Abs(currentCandle.Delta) >= _changeSize && !_isAlerted)
            {
                AddAlert(AlertFile, InstrumentInfo.Instrument, "Delta changed!", AlertBGColor.Convert(), AlertForeColor.Convert());
                _isAlerted = true;
            }
        }

        _lastBar = i;
    }

    #endregion
}