namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("Volume Per Trade")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumePerTradeIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000619357")]
    public class VolumePerTrade : Indicator
    {
	    private ValueDataSeries _renderSeries = new("RenderSeries", Strings.Values)
	    {
			UseMinimizedModeIfEnabled = true,
			VisualType = VisualMode.Histogram,
			ResetAlertsOnNewBar = true
	    };

	    public VolumePerTrade()
		    :base(true)
	    {
		    DenyToChangePanel = true;
		    Panel = IndicatorDataProvider.NewPanel;
		    DataSeries[0] = _renderSeries;
	    }
	    
	    protected override void OnCalculate(int bar, decimal value)
	    {
		    var candle = GetCandle(bar);
		    this[bar] = candle.Volume / candle.Ticks;
	    }
    }
}
