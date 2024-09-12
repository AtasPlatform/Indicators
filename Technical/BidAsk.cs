namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;

[DisplayName("Bid Ask")]
[Category(IndicatorCategories.BidAskDeltaVolume)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.BidAskDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602329")]
public class BidAsk : Indicator
{
	#region Fields

	private readonly ValueDataSeries _asks = new("Asks", Strings.Ask)
	{
		VisualType = VisualMode.Histogram,
		Color = DefaultColors.Green.Convert(),
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true,
		DescriptionKey = nameof(Strings.AskVisualizationSettingsDescription)
    };

	private readonly ValueDataSeries _bids = new("Bids", Strings.Bid)
	{
		VisualType = VisualMode.Histogram,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true,
		DescriptionKey = nameof(Strings.BidVisualizationSettingsDescription)
    };

	#endregion

	#region ctor

	public BidAsk()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DenyToChangePanel = true;
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