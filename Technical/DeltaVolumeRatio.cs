// Delta Volume Ratio (Delta / Volume)
// Ratio Value will be a number between -1 to 1.
// It will be plotted as a line chart in a separate panel.

namespace ATAS_INDICATOR_1;

using ATAS.Indicators;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using System;
using System.ComponentModel;

[DisplayName("Delta Volume Ratio")]
public class DeltaVolumeRatio : Indicator
{
    #region Fields

    private readonly ValueDataSeries _ratioSeries = new("Ratio", "Delta Volume Ratio")
    {
        VisualType = VisualMode.Line
    };

    #endregion

    #region ctor

    public DeltaVolumeRatio()
        : base(true)
    {
        // Show in a separate panel
        Panel = IndicatorDataProvider.NewPanel;

        DataSeries[0] = _ratioSeries;
    }

    #endregion

    #region Protected methods

    protected override void OnCalculate(int bar, decimal value)
    {
        var candle = GetCandle(bar);

        var delta = candle.Delta;
        var volume = candle.Volume;

        // Calculate ratio: Delta / Volume
        // Clamp to -1 to 1 range
        decimal ratio = 0;

        if (volume != 0)
        {
            ratio = delta / volume;
            // Clamp to -1 to 1
            ratio = Math.Max(-1, Math.Min(1, ratio));
        }

        _ratioSeries[bar] = ratio;
    }

    #endregion
}
