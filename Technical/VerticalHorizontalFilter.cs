namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;

[DisplayName("Vertical Horizontal Filter")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.VerticalHorizontalFilterDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000619282")]
public class VerticalHorizontalFilter : Indicator
{
    #region Nested Types

    public enum InputType
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
        Volume,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
        Ticks,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
        Asks,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
        Bids,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Open))]
        Open,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.High))]
        High,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low))]
        Low,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Close))]
        Close,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OHLCAverage))]
        OHLCAverage,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HLCAverage))]
        HLCAverage,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HLAverage))]
        HLAverage
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _volume = new("Volume", Strings.Volume)
    {
        IsHidden = true,
        VisualType = VisualMode.Histogram,
        ShowZeroValue = false
    };

    private readonly ValueDataSeries _data = new("Data")
    {
        IsHidden = true,
        VisualType = VisualMode.Hide,
    };

    private int _period = 10;
    private InputType _type;
    private Color _histogramColor = DefaultColors.Blue.Convert();

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
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Type), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.SourceTypeDescription))]
    public InputType Type 
    {
        get => _type; 
        set
        {
            _type = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColorDescription))]
    public Color HistogramColor 
    {
        get => _histogramColor;
        set
        {
            _histogramColor = value;
            _volume.Color = value;
        }
    }

    #endregion

    #region ctor

    public VerticalHorizontalFilter() : base(true)
    {
        Panel = IndicatorDataProvider.NewPanel;
        DenyToChangePanel = true;

        DataSeries[0] = _volume;
        _volume.Color = _histogramColor;
    }

    #endregion

    #region Protected Methods

    protected override void OnCalculate(int bar, decimal value)
    {
        _data[bar] = GetSource(bar);
        var sum = _data.CalcSum(_period, bar);

        if (sum != 0)
            _volume[bar] = (_data.MAX(_period, bar) - _data.MIN(_period, bar)) / sum;
    }

    #endregion

    #region Private Methods

    private decimal GetSource(int bar)
    {
        var candle = GetCandle(bar);

        return _type switch
        {
            InputType.Volume => candle.Volume,
            InputType.Ticks => candle.Ticks,
            InputType.Asks => candle.Ask,
            InputType.Bids => candle.Bid,
            InputType.Open => candle.Open,
            InputType.High => candle.High,
            InputType.Low => candle.Low,
            InputType.Close => candle.Close,
            InputType.OHLCAverage => (candle.Open + candle.High + candle.Low + candle.Close) / 4,
            InputType.HLCAverage => (candle.High + candle.Low + candle.Close) / 3,
            InputType.HLAverage => (candle.High + candle.Low) / 2,
            _ => 0,
        };
    }

    #endregion
}