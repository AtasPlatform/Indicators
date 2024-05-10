namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
	using Colors = System.Drawing.Color;
#else
using CrossColor = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
#endif

[DisplayName("Color Bar Open/Close")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ColorBarOpenCloseDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618541")]
public class ColorBarOpenClose : Indicator
{
    #region Fields
    
    private CrossColor _highColor = Colors.Aqua;
    private CrossColor _lowColor = Colors.DarkMagenta;

    private readonly PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
    {
        IsHidden = true
    };

    #endregion

    #region Properties
    
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Highest), GroupName = nameof(Strings.Color), Description = nameof(Strings.BullishColorDescription), Order = 100)]
    public CrossColor HighColor
    {
        get => _highColor;
        set
        {
            _highColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Lowest), GroupName = nameof(Strings.Color), Description = nameof(Strings.BearishColorDescription), Order = 100)]
    public CrossColor LowColor
    {
        get => _lowColor;
        set
        {
            _lowColor = value;
            RecalculateValues();
        }
    }

    #endregion

    #region ctor

    public ColorBarOpenClose()
        : base(true)
    {
	    DenyToChangePanel = true;
        DataSeries[0] = _renderSeries;
    }

    #endregion

    #region Protected methods

    protected override void OnRecalculate()
    {
        Clear();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == 0)
            return;

        var candle = GetCandle(bar);

        if (candle.Close > candle.Open)
        {
	        _renderSeries[bar] = HighColor;
            return;
        }

        if (candle.Close < candle.Open)
        {
	        _renderSeries[bar] = LowColor;
	        return;
        }

        _renderSeries[bar] = _renderSeries[bar - 1];
    }

    #endregion
}