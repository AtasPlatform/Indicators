namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Heatmap;

[DisplayName("Delta Colored Candles")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DeltaColoredCandlesDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618743")]
public class DeltaColoredCandles : Indicator
{
    #region Fields

    private readonly ValueDataSeries _delta = new("delta");
    private readonly PaintbarsDataSeries _colorBars = new("ColorBars", Strings.Candles) { IsHidden = true };
    private decimal _maxDelta = 600;
    private HeatmapTypes _colorScheme = HeatmapTypes.RedToDarkToGreen;
    private int _period = 14;

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.General), Description = nameof(Strings.PeriodDescription))]
    public int Period 
    { 
        get => _period; 
        set
        {
            _period = value;
            RecalculateValues();
        }
    }

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumDelta), GroupName = nameof(Strings.General), Description = nameof(Strings.MaxDeltaVolumeFilterCommonDescription))]
    public decimal MaxDelta 
    { 
        get => _maxDelta;
        set
        {
            _maxDelta = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ColorScheme), GroupName = nameof(Strings.General), Description = nameof(Strings.ColorSchemeDescription))]
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
        _delta[bar] = GetCandle(bar).Delta;
        var sumDelta = _delta.CalcSum(_period, bar);
        var percent = sumDelta * 100 / MaxDelta;
        var rate = 50 + percent / 2;
        var color = HeatmapExtensions.GetColor(ColorScheme, (int)rate);
        _colorBars[bar] = color.Convert();
    }

    #endregion
}
