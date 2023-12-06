namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bollinger Bands: Bandwidth")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BollingerBandsBandwidthDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602340")]
	public class BollingerBandsBandwidth : Indicator
	{
		#region Fields

		private readonly BollingerBands _bb = new();

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		public int Period
		{
			get => _bb.Period;
			set
			{
				_bb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBandsWidth), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DeviationRangeDescription), Order = 110)]
		[Range(0.0, 999999)]
		public decimal Width
		{
			get => _bb.Width;
			set
			{
				_bb.Width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBandsBandwidth()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_bb.Period = 10;
			_bb.Width = 1;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_bb.Calculate(bar, value);
			var sma = ((ValueDataSeries)_bb.DataSeries[0])[bar];
			var top = ((ValueDataSeries)_bb.DataSeries[1])[bar];
			var bot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

			if (sma == 0)
				return;

			_renderSeries[bar] = 100 * (top - bot) / sma;
		}

		#endregion
	}
}