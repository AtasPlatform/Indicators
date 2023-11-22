namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bar Difference")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarDifferenceDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602523")]
	public class BarDifference : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private int _period = 1;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BarDifference()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();
			
			if (bar < _period)
				return;

			_renderSeries[bar] = (value - (decimal)SourceDataSeries[bar - _period]) / InstrumentInfo.TickSize;
		}

		#endregion
	}
}