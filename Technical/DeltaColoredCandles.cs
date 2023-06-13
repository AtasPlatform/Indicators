namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Technical.Properties;
using OFT.Rendering.Heatmap;

[DisplayName("Delta Colored Candles")]
public class DeltaColoredCandles : Indicator
{
    #region Fields

    private readonly PaintbarsDataSeries _colorBars = new(Resources.Candles) { IsHidden = true };
    private decimal _maxDelta = 600;
    private HeatmapTypes _colorScheme = HeatmapTypes.RedToDarkToGreen;

    #endregion

    #region Properties

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "MaximumDelta", GroupName = "General")]
    public decimal MaxDelta 
    { 
        get => _maxDelta;
        set
        {
            _maxDelta = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ColorScheme", GroupName = "General")]
    public HeatmapTypes ColorScheme 
    { 
        get => _colorScheme;
        set
        {
            _colorScheme = value;
            RecalculateValues();
        }
    }

    #endregion

    #region ctor

    public DeltaColoredCandles() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0] = _colorBars;
    }

    #endregion

    #region Protected Methods

    protected override void OnCalculate(int bar, decimal value)
    {
        var candle = GetCandle(bar);
        var percent = candle.Delta * 100 / MaxDelta;
        var rate = 50 + percent / 2;
        var color = HeatmapExtensions.GetColor(ColorScheme, (int)rate);
        _colorBars[bar] = color.Convert();
    }

    #endregion
}
