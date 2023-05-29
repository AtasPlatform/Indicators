namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

[DisplayName("Vertical Horizontal Filter")]
public class VerticalHorizontalFilter : Indicator
{
    #region Nested Types

    public enum InputType
    {
        [Display(ResourceType = typeof(Resources), Name = "Volume")]
        Volume,

        [Display(ResourceType = typeof(Resources), Name = "Ticks")]
        Ticks,

        [Display(ResourceType = typeof(Resources), Name = "Ask")]
        Asks,

        [Display(ResourceType = typeof(Resources), Name = "Bid")]
        Bids,

        [Display(ResourceType = typeof(Resources), Name = "Open")]
        Open,

        [Display(ResourceType = typeof(Resources), Name = "High")]
        High,

        [Display(ResourceType = typeof(Resources), Name = "Low")]
        Low,

        [Display(ResourceType = typeof(Resources), Name = "Close")]
        Close,

        [Display(ResourceType = typeof(Resources), Name = "OHLCAverage")]
        OHLCAverage,

        [Display(ResourceType = typeof(Resources), Name = "HLCAverage")]
        HLCAverage,

        [Display(ResourceType = typeof(Resources), Name = "HLAverage")]
        HLAverage
    }

    #endregion

    #region Fields

    private readonly ValueDataSeries _volume = new(Resources.Volume)
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

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Calculation")]
    public int Period 
    { 
        get => _period; 
        set
        {
            _period = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Calculation")]
    public InputType Type 
    {
        get => _type; 
        set
        {
            _type = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Visualization")]
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