namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Trade Volume Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.TVIDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602296")]
	public class TVI : Indicator
	{
		#region ctor

		public TVI()
		{
			Panel = IndicatorDataProvider.NewPanel;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var volume = GetCandle(bar).Volume;

			if (value - (decimal)SourceDataSeries[bar - 1] > InstrumentInfo.TickSize)
				this[bar] = this[bar - 1] + volume;
			else if (value - (decimal)SourceDataSeries[bar - 1] == InstrumentInfo.TickSize)
				this[bar] = this[bar - 1];
			else
				this[bar] = this[bar - 1] - volume;
		}

		#endregion
	}
}