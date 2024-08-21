namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("On Balance Open Interest")]
	[Category(IndicatorCategories.OrderFlow)]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BalanceOIDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602438")]
    public class BalanceOI : Indicator
	{
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _oiSignedSeries = new("Signed");
		
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.ShortValues), Description = nameof(Strings.UsePeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public FilterInt MinimizedMode { get; set; } = new(true) { Value = 10, Enabled = false };

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
			RedrawChart();
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
