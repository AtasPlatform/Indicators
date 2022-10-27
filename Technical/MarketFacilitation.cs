namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Market Facilitation Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45433-market-facilitation-index")]
	public class MarketFacilitation : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private decimal _multiplier = 1;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Settings", Order = 100)]
		[Range(0.000000001, 1000000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MarketFacilitation()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			_renderSeries[bar] = candle.Volume != 0
				? (candle.High - candle.Low) * _multiplier / candle.Volume
				: 0;
		}

		#endregion
	}
}