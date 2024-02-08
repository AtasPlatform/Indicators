namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Simple Moving Average - Skip Zeros")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SZMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602237")]
	public class SZMA : Indicator
	{
		#region Fields

		private int _period = 10;

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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
			var sum = 0m;
			var nonZeroValues = 0;

			for (var i = Math.Max(0, bar - _period); i <= bar; i++)
			{
				if ((decimal)SourceDataSeries[i] == 0)
					continue;

				sum += (decimal)SourceDataSeries[i];
				nonZeroValues++;
			}

			this[bar] = nonZeroValues != 0 
				? sum / nonZeroValues
				: 0;
		}

		#endregion
	}
}