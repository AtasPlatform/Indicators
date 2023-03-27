namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.Windows.Media;

using OFT.Attributes;

[DisplayName("Bid Ask")]
[Category("Bid x Ask,Delta,Volume")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/457-bid-ask")]
public class BidAsk : Indicator
{
	#region Fields

	private readonly ValueDataSeries _asks = new("Ask")
	{
		VisualType = VisualMode.Histogram,
		Color = Colors.Green,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

	private readonly ValueDataSeries _bids = new("Bid")
	{
		VisualType = VisualMode.Histogram,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
    };

	#endregion

	#region ctor

	public BidAsk()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _bids;
		DataSeries.Add(_asks);
	}

	#endregion

	#region Public methods

	public override string ToString()
	{
		return "Bid Ask";
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);
		_bids[bar] = -candle.Bid;
		_asks[bar] = candle.Ask;
	}
	
	protected override void OnApplyDefaultColors()
	{
		if(ChartInfo is null)
			return;

		_bids.Color = ChartInfo.ColorsStore.FootprintBidColor.Convert();
		_asks.Color = ChartInfo.ColorsStore.FootprintAskColor.Convert();
	}

	#endregion
}