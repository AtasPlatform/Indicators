namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Technical.Properties;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

[DisplayName("Moving Average")]
public class MovingAverage : Indicator
{
    #region Nested Types

    public enum MovingType
    {
        [Display(ResourceType = typeof(Resources), Name = "BWMA")]
        Bwma,

        [Display(ResourceType = typeof(Resources), Name = "DEMA")]
        Dema,

        [Display(ResourceType = typeof(Resources), Name = "EMA")]
        Ema,

        [Display(ResourceType = typeof(Resources), Name = "SZMA")]
        Szma,

        [Display(ResourceType = typeof(Resources), Name = "SMMA")]
        Smma,

        [Display(ResourceType = typeof(Resources), Name = "SMA")]
        Sma,

        [Display(ResourceType = typeof(Resources), Name = "TEMA")]
        Tema,

        [Display(ResourceType = typeof(Resources), Name = "TMA")]
        Tma,

        [Display(ResourceType = typeof(Resources), Name = "ZLEMA")]
        Zlema,

        [Display(ResourceType = typeof(Resources), Name = "WMA")]
        Wma,

        [Display(ResourceType = typeof(Resources), Name = "WWMA")]
        Wwma,
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _data = new("MA");

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

    private int _period = 10;
    private MovingType _movType;

    #endregion

    #region Properties

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common")]
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

    [Display(ResourceType = typeof(Resources), Name = "MovingType", GroupName = "Common")]
    public MovingType MovType 
    { 
        get => _movType; 
        set
        {
            _movType = value;
            RecalculateValues();
        }
    }

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
