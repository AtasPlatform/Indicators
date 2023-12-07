namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using OFT.Attributes;
using OFT.Localization;

[DisplayName("Ask/Bid Volume Difference Bars")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.AskBidBarsDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602527")]
public class AskBidBars : Indicator
{
	#region Fields

	private readonly CandleDataSeries _renderSeries = new("RenderSeries", Strings.Candles)
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