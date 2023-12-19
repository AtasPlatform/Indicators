using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;

namespace ATAS.Indicators.Technical;

[DisplayName("Average Delta")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.AverageDeltaIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618456")]
public class AverageDelta : Indicator
{
    #region Nested Types

    public enum CalculationType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA))]
        Sma,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMA))]
        Ema
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _data = new(nameof(_data), Strings.Data)
    {
        IsHidden = true,
        VisualType = VisualMode.Histogram,
        ShowZeroValue = false
    };

    private int _period = 10;
    private SMA _sma;
    private EMA _ema;
    private CalculationType _calcType;
    private Color _posColor = DefaultColors.Green;
    private Color _negColor = DefaultColors.Red;

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.PeriodDescription))]
    public int Period
    {
        get => _period;
        set
        {
            _period = value;

            if (_sma is not null && _ema is not null)
            {
                _sma.Period = value;
                _ema.Period = value;
            }

            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.CalculationModeDescription))]
    public CalculationType CalcType
    {
        get => _calcType;
        set
        {
            _calcType = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PositiveValueColorDescription))]
    public Color PosColor
    {
        get => _posColor;
        set
        {
            _posColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.NegativeValueColorDescription))]
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
        _sma = new()
        {
            Period = _period
        };

        _ema = new()
        {
            Period = _period
        };
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



