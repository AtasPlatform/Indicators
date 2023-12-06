namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Market Facilitation Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MarketFacilitationDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602423")]
	public class MarketFacilitation : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private decimal _multiplier = 1;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription), Order = 100)]
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