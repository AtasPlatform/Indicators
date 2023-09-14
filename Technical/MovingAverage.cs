namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Drawing;
using OFT.Localization;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

[DisplayName("Moving Average")]
public class MovingAverage : Indicator
{
    #region Nested Types

    public enum MovingType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BWMA))]
        Bwma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DEMA))]
        Dema,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMA))]
        Ema,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA))]
        Sma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMMA))]
        Smma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SZMA))]
        Szma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TEMA))]
        Tema,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TMA))]
        Tma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WMA))]
        Wma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WWMA))]
        Wwma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ZLEMA))]
        Zlema,
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _data = new("Data", "MA");

    private BWMA _bwma;
    private DEMA _dema;
    private EMA _ema;
    private SMA _sma;
    private SMMA _smma;
    private SZMA _szma;
    private TEMA _tema;
    private TMA _tma;
    private WMA _wma;
    private WWMA _wwma;
    private ZLEMA _zlema;

    private bool _onLine;
    private int _lastAlert;

    private int _period = 10;
    private MovingType _movType = MovingType.Sma;
    private bool _coloredDirection;
    private Color _bullishColor = DefaultColors.Green;
    private Color _bearishColor = DefaultColors.Red;

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings))]
    public int Period 
    { 
        get => _period;
        set
        {
            _period = value;
            SetPeriod(value);
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MovingType), GroupName = nameof(Strings.Settings))]
    public MovingType MovType 
    { 
        get => _movType; 
        set
        {
            _movType = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColoredDirection), GroupName = nameof(Strings.Visualization))]
    public bool ColoredDirection 
    {
        get => _coloredDirection; 
        set
        {
            _coloredDirection = value;
            RecalculateValues();
        } 
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Visualization))]
    public Color BullishColor 
    {
        get => _bullishColor;
        set
        {
            _bullishColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.Visualization))]
    public Color BearishColor 
    { 
        get => _bearishColor;
        set
        {
            _bearishColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.ApproximationAlert))]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RepeatAlert), GroupName = nameof(Strings.ApproximationAlert))]
    public bool RepeatAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationFilter), GroupName = nameof(Strings.ApproximationAlert))]
    [Range(0, 100000)]
    public int AlertSensitivity { get; set; } = 1;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.ApproximationAlert))]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.ApproximationAlert))]
    public Color FontColor { get; set; } = DefaultColors.White;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.ApproximationAlert))]
    public Color BackgroundColor { get; set; } = DefaultColors.Gray;

    #endregion

    #region ctor

    public MovingAverage() 
    {
        DataSeries[0] = _data;
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        switch (_movType)
        {
            case MovingType.Bwma:
                _bwma = new() { Period = _period };
                break;
            case MovingType.Dema:
                _dema = new() { Period = _period };
                break;
            case MovingType.Ema:
                _ema = new() { Period = _period };
                break;
            case MovingType.Szma:
                _szma = new() { Period = _period };
                break;
            case MovingType.Smma:
                _smma = new() { Period = _period };
                break;
            case MovingType.Sma:
                _sma = new() { Period = _period };
                break;
            case MovingType.Tema:
                _tema = new() { Period = _period };
                break;
            case MovingType.Tma:
                _tma = new() { Period = _period };
                break;
            case MovingType.Zlema:
                _zlema = new() { Period = _period };
                break;
            case MovingType.Wma:
                _wma = new() { Period = _period };
                break;
            case MovingType.Wwma:
                _wwma = new() { Period = _period };
                break;
        }
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        _data[bar] = GetMA(bar, value);

        if (bar == 0)
            return;

        if (ColoredDirection)
        {
            _data.Colors[bar] = _data[bar] > _data[bar - 1]
           ? _bullishColor
           : _bearishColor;
        }

        if (bar != CurrentBar - 1 || !UseAlerts)
            return;

        if (_lastAlert == bar && !RepeatAlert)
            return;

        var close = GetCandle(bar).Close;
        var onLine = Math.Abs(_data[bar] - close) / InstrumentInfo.TickSize <= AlertSensitivity;

        if (onLine && !_onLine)
        {
            AddAlert(AlertFile, InstrumentInfo.Instrument, $"MA approximation alert: {_data[bar]:0.#####}", BackgroundColor.Convert(), FontColor.Convert());
            _lastAlert = bar;
        }

        _onLine = onLine;
    }

    #endregion

    #region Private Methods

    private decimal GetMA(int bar, decimal value)
    {
        return _movType switch
        {
            MovingType.Bwma => _bwma.Calculate(bar, value),
            MovingType.Dema => _dema.Calculate(bar, value),
            MovingType.Ema => _ema.Calculate(bar, value),
            MovingType.Szma => _szma.Calculate(bar, value),
            MovingType.Smma => _smma.Calculate(bar, value),
            MovingType.Sma => _sma.Calculate(bar, value),
            MovingType.Tema => _tema.Calculate(bar, value),
            MovingType.Tma => _tma.Calculate(bar, value),
            MovingType.Zlema => _zlema.Calculate(bar, value),
            MovingType.Wma => _wma.Calculate(bar, value),
            MovingType.Wwma => _wwma.Calculate(bar, value),
            _ => 0m
        };
    }

    private void SetPeriod(int period)
    {
        switch (_movType)
        {
            case MovingType.Bwma:
                if (_bwma != null)
                    _bwma.Period = period;
                break;
            case MovingType.Dema:
                if (_dema != null)
                    _dema.Period = period;
                break;
            case MovingType.Ema:
                if (_ema != null)
                    _ema.Period = period;
                break;
            case MovingType.Szma:
                if (_szma != null)
                    _szma.Period = period;
                break;
            case MovingType.Smma:
                if (_smma != null)
                    _smma.Period = period;
                break;
            case MovingType.Sma:
                if (_sma != null)
                    _sma.Period = period;
                break;
            case MovingType.Tema:
                if (_tema != null)
                    _tema.Period = period;
                break;
            case MovingType.Tma:
                if (_tma != null)
                    _tma.Period = period;
                break;
            case MovingType.Zlema:
                if (_zlema != null)
                    _zlema.Period = period;
                break;
            case MovingType.Wma:
                if (_wma != null)
                    _wma.Period = period;
                break;
            case MovingType.Wwma:
                if (_wwma != null)
                    _wwma.Period = period;
                break;
        }
    }


    #endregion
}
