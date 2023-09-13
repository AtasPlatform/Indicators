using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators.Drawing;
using OFT.Localization;

namespace ATAS.Indicators.Technical;

[DisplayName("Average Delta")]
public class AverageDelta : Indicator
{
    #region Nested Types

    public enum CalculationType
    {
        [Display(ResourceType = typeof(Strings), Name = "SMA")]
        Sma,

        [Display(ResourceType = typeof(Strings), Name = "EMA")]
        Ema
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _data = new("Data", Strings.Data)
    {
        IsHidden = true,
        VisualType = VisualMode.Histogram,
        ShowZeroValue = false
    };

    private int _periodDefault = 10;
    private SMA _sma;
    private EMA _ema;
    private CalculationType _calcType;
    private Color _posColor = DefaultColors.Green;
    private Color _negColor = DefaultColors.Red;

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Calculation")]
    public int Period
    {
        get => _sma.Period;
        set
        {
            if (_sma is not null && _ema is not null)
            {
                _sma.Period = value;
                _ema.Period = value;
            }

            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "CalculationMode", GroupName = "Calculation")]
    public CalculationType CalcType
    {
        get => _calcType;
        set
        {
            _calcType = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Positive", GroupName = "Visualization")]
    public Color PosColor
    {
        get => _posColor;
        set
        {
            _posColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Negative", GroupName = "Visualization")]
    public Color NegColor 
    { 
        get => _negColor;
        set
        {
            _negColor = value;
            RecalculateValues();
        }
    }

    #endregion

    #region ctor

    public AverageDelta() : base(true)
    {
        Panel = IndicatorDataProvider.NewPanel;
        DenyToChangePanel = true;

        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

        DataSeries.Add(_data);
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        var period = _sma is null ? _periodDefault : _sma.Period;

        _sma = new();
        _sma.Period = period;
        _ema = new();
        _ema.Period = period;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var val = 0m;
        var candle = GetCandle(bar);

        switch (_calcType)
        {
            case CalculationType.Sma:
                val = _sma.Calculate(bar, candle.Delta);
                break;
            case CalculationType.Ema:
                val = _ema.Calculate(bar, candle.Delta);
                break;
        }

        _data[bar] = val;

        if (val > 0)
            _data.Colors[bar] = PosColor;
        else
            _data.Colors[bar] = NegColor;
    }

    #endregion
}



