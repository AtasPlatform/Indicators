namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("1 Divided by Price")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DividedByPriceDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602309")]
	public class DividedByPrice : Indicator
	{
		#region Fields

		private readonly CandleDataSeries _reversedCandles = new("ReversedCandles", Strings.Candles);

		#endregion

		#region ctor

		public DividedByPrice()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _reversedCandles;
		}

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
	        if (ChartInfo is null)
		        return;

	        _reversedCandles.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	        _reversedCandles.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
	        _reversedCandles.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }
        protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var open = 1 / candle.Open;
			var close = 1 / candle.Close;
			var high = 1 / candle.High;
			var low = 1 / candle.Low;

			_reversedCandles[bar] = new Candle
			{
				Open = open,
				Close = close,
				High = low,
				Low = high
			};
		}

		#endregion
	}
}