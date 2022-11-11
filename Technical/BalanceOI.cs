namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

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

		private readonly ValueDataSeries _oiSignedSeries = new("Signed");
		
        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "ShortValues", Order = 100)]
		[Range(1, 10000)]
		public Filter<int> MinimizedMode { get; set; } = new(true) { Value = 10, Enabled = false };

        public BalanceOI()
		    : base(true)
	    {
		    Panel = IndicatorDataProvider.NewPanel;
		    DataSeries[0] = _renderSeries;

		    MinimizedMode.PropertyChanged += FilterChanged;
	    }

        private void FilterChanged(object sender, PropertyChangedEventArgs e)
        {
	        RecalculateValues();
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

		    if (!MinimizedMode.Enabled)
		    {
			    _renderSeries[bar] = candle.Close > prevCandle.Close
				    ? _renderSeries[bar - 1] + candle.OI
				    : candle.Close < prevCandle.Close
					    ? _renderSeries[bar - 1] - candle.OI
                        : _renderSeries[bar - 1];

			    return;
		    }

		    _oiSignedSeries[bar] = candle.Close > prevCandle.Close
			    ? candle.OI
			    : candle.Close < prevCandle.Close
				    ? -candle.OI
				    : 0;

		    _renderSeries[bar] = bar < MinimizedMode.Value
			    ? _renderSeries[bar - 1] + _oiSignedSeries[bar]
			    : _renderSeries[bar - 1] + _oiSignedSeries[bar] - _oiSignedSeries[bar - MinimizedMode.Value];
	    }
    }
}
