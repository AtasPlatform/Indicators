namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Linear Regression Slope")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LinRegSlopeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602416")]
	public class LinRegSlope : Indicator
	{
		#region Fields

		private int _period;

		#endregion

		#region Properties

		[Parameter]
		[Range(1, 10000)]
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
		public int Period
		{
			get => _period;
			set
			{
				if (_period == value)
					return;

				_period = value;

				RaisePropertyChanged(nameof(Period));
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public LinRegSlope()
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 14;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < Period + 1)
				return;

			var sumX = Period * (Period - 1) * 0.5m;
			var divisor = sumX * sumX - Period * Period * (Period - 1m) * (2 * Period - 1) / 6;
			decimal sumXY = 0;

			for (var count = 0; count < Period && bar - count >= 0; count++)
			{
				if (bar - count < 0)
					continue;

				sumXY += count * (decimal)SourceDataSeries[bar - count];
			}

			var val = (Period * sumXY - sumX * SourceDataSeries.CalcSum(Period, bar)) / divisor;
			this[bar] = val;
		}

		#endregion
	}
}