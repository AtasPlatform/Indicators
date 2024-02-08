namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Sine-Wave Weighted Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SWWMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602467")]
	public class SWWMA : Indicator
	{
		#region Static and constants

		private const decimal _sinSum = 3.73205080757m;

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < 5)
			{
				this[bar] = value;
				return;
			}

			var valueSum = 0m;

			for (var i = 1; i <= 5; i++)
				valueSum += (decimal)Math.Sin(i * Math.PI / 6.0) * (decimal)SourceDataSeries[bar - i];

			this[bar] = valueSum / _sinSum;
		}

		#endregion
	}
}