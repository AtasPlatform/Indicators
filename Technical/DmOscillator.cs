namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Directional Movement Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DmOscillatorDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602371")]
	public class DmOscillator : Indicator
	{
		#region Fields

		private readonly DmIndex _dm = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 110)]
		[Range(1, 10000)]
		public int Period
		{
			get => _dm.Period;
			set
			{
				_dm.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DmOscillator()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Add(_dm);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = ((ValueDataSeries)_dm.DataSeries[0])[bar] - ((ValueDataSeries)_dm.DataSeries[1])[bar];
		}

		#endregion
	}
}