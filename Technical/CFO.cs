namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Chande Forecast Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CFODescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602274")]
	public class CFO : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new()
		{
			Period = 10
		};

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _linReg.Period;
			set
			{
				_linReg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CFO()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var cfoValue = value != 0
				? 100m * (value - _linReg.Calculate(bar, value)) / value
				: 0;
			
			_renderSeries[bar] = cfoValue;
		}

		#endregion
	}
}