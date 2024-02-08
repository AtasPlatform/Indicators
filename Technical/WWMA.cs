namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Welles Wilders Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.WWMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602508")]
	public class WWMA : Indicator
	{
		#region Fields
		
		private readonly SZMA _szma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _szma.Period;
			set
			{
				_szma.Period = value;
				RecalculateValues();
			}
		}

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			_szma.Calculate(bar, value);

			if (bar == 0)
			{
				this[bar] = value;
				return;
			}
			
			this[bar] = this[bar - 1] == 0
				? _szma[bar]
				: this[bar - 1] + (value - this[bar - 1]) / Period;
		}

		#endregion
	}
}