namespace ATAS.Indicators.Technical;

using System.ComponentModel;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Ask/Bid Volume Difference Bars")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43412-askbid-volume-difference-bars")]
public class AskBidBars : Indicator
{
	#region Fields

	private readonly CandleDataSeries _renderSeries = new(Resources.Candles)
	{
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

	#endregion

	#region ctor

	public AskBidBars()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _renderSeries;
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
		if (bar == 0)
			_renderSeries.Clear();

		var candle = GetCandle(bar);

		_renderSeries[bar].High = candle.MaxDelta;
		_renderSeries[bar].Low = candle.MinDelta;
		_renderSeries[bar].Close = candle.Delta;
	}

	#endregion
}