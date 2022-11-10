namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("On Balance Open Interest")]
	[Category("Order Flow")]
	[FeatureId("NotApproved")]
	public class BalanceOI : Indicator
	{
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};

	    public BalanceOI()
		    : base(true)
	    {
		    Panel = IndicatorDataProvider.NewPanel;
		    DataSeries[0] = _renderSeries;
	    }

	    protected override void OnCalculate(int bar, decimal value)
	    {
		    var candle = GetCandle(bar);

		    if (bar == 0)
		    {
			    _renderSeries[bar] = candle.OI;
			    return;
		    }

		    var prevCandle = GetCandle(bar - 1);

		    if (candle.Close > prevCandle.Close)
		    {
			    _renderSeries[bar] = _renderSeries[bar - 1] + candle.OI;
		    }
			else if (candle.Close < prevCandle.Close)
		    {
			    _renderSeries[bar] = _renderSeries[bar - 1] - candle.OI;
            }
		    else
		    {
			    _renderSeries[bar] = _renderSeries[bar - 1];
            }

        }
    }
}
