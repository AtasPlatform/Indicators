namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Mutual Fund Bars")]
[FeatureId("NotApproved")]
public class MutualFundBars : Indicator
{
	#region Fields

	private readonly PaintbarsDataSeries _bars = new("Bars") { IsHidden = true };
	private CandleDataSeries _renderSeries = new(Resources.Visualization);

	#endregion

	#region ctor

	public MutualFundBars()
		: base(true)
	{
		DenyToChangePanel = true;

		DataSeries[0] = _bars;
		DataSeries.Add(_renderSeries);
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    _renderSeries.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	    _renderSeries.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
	    _renderSeries.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
	{
		_bars[bar] = Colors.Transparent;

		if (bar == 0)
		{
			_renderSeries.Clear();
			return;
		}

		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);

		_renderSeries[bar].Open = prevCandle.Close;
		_renderSeries[bar].High = Math.Max(candle.Close, prevCandle.Close);
		_renderSeries[bar].Low = Math.Min(candle.Close, prevCandle.Close);
		_renderSeries[bar].Close = candle.Close;
	}

	#endregion
}