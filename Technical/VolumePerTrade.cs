using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[FeatureId("NotApproved")]
	[DisplayName("Volume per trade")]
    public class VolumePerTrade : Indicator
    {
	    private ValueDataSeries _renderSeries = new(Resources.Values)
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
