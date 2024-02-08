namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Synthetic VIX")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SyntheticVixDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602484")]
	public class SyntheticVix : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				_highest.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public SyntheticVix()
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
			_highest.Calculate(bar, candle.Close);
			var highestDataSeries = (ValueDataSeries)_highest.DataSeries[0];
            var maxClose = highestDataSeries.MAX(Period, bar);
			_renderSeries[bar] = 100 * (maxClose - candle.Low) / maxClose;
		}

		#endregion
	}
}