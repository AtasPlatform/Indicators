namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Aroon Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AroonOscillatorDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602317")]
	public class AroonOscillator : Indicator
	{
		#region Fields

		private readonly AroonIndicator _ai = new();

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", "Aroon");
		private int _period;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 110)]
		public int Period
		{
			get => _ai.Period;
			set
			{
				_ai.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AroonOscillator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			_ai.Period = _period;
			Add(_ai);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = ((ValueDataSeries)_ai.DataSeries[0])[bar] - ((ValueDataSeries)_ai.DataSeries[1])[bar];
		}

		#endregion
	}
}