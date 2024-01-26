namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Mutual Fund Bars")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.MutualFundBarsDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000619006")]
public class MutualFundBars : Indicator
{
	#region Fields

	private readonly PaintbarsDataSeries _bars = new("BarsId", "Bars") { IsHidden = true };
	private CandleDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

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

	    _renderSeries.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor;
	    _renderSeries.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor;
	    _renderSeries.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color;
    }

    protected override void OnCalculate(int bar, decimal value)
	{
		_bars[bar] = Color.Transparent;

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