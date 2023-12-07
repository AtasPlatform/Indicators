namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Kurtosis")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.KurtosisDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602556")]
	public class Kurtosis : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _populationSeries = new("PopulationSeries", Strings.Line)
		{
			IgnoredByAlerts = true,
			DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
		};

		private readonly ValueDataSeries _sampleSeries = new("SampleSeries", Strings.Estimator) 
		{ 
			Color = DefaultColors.Blue.Convert(),
            DescriptionKey = nameof(Strings.EstimatorLineSettingsDescription)
        };

        private readonly ValueDataSeries _quadSeries = new("Quad");
        private readonly ValueDataSeries _squareSeries = new("Square");
        private readonly SMA _sma = new();

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(4, 10000)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Kurtosis()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _populationSeries;
			DataSeries.Add(_sampleSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var diff = (double)(value - _sma.Calculate(bar, value));

			_squareSeries[bar] = (decimal)(diff * diff);
			_quadSeries[bar] = (decimal)(diff * diff * diff * diff);

			if (bar < Period)
				return;

			var squareSum = _squareSeries.CalcSum(Period, bar) / Period;
			var quadSum = _quadSeries.CalcSum(Period, bar) / Period;

			_populationSeries[bar] = quadSum / (squareSum * squareSum) - 3;

			_sampleSeries[bar] = (Period - 1m) * (Period + 1m) / ((Period - 2m) * (Period - 3m)) *
				quadSum / (squareSum * squareSum) -
				3m * (Period - 1) * (Period - 1) / ((Period - 2) * (Period - 3));
		}

		#endregion
	}
}