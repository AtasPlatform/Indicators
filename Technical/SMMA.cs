namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("SMMA")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SMMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602532")]
	public class SMMA : Indicator
	{
		#region Fields

		private int _period = 10;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.PeriodDescription),
            Order = 20)]
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

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			this[bar] = bar == 0 
				? value 
				: this[bar] = (this[bar - 1] * (Period - 1) + value) / Period;
		}

		#endregion
	}
}