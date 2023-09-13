namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Color Bar Open/Close")]
public class ColorBarOpenClose : Indicator
{
    #region Fields
    
    private Color _highColor = Colors.Aqua;
    private Color _lowColor = Colors.DarkMagenta;

    private readonly PaintbarsDataSeries _renderSeries = new("RenderSeries", "PaintBars")
    {
        IsHidden = true
    };

    #endregion

    #region Properties
    
    [Display(ResourceType = typeof(Strings), Name = "Highest", GroupName = "Color", Order = 100)]
    public Color HighColor
    {
        get => _highColor;
        set
        {
            _highColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Lowest", GroupName = "Color", Order = 100)]
    public Color LowColor
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