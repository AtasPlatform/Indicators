namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("LinRegSlope")]
	[Description("Linear Regression Slope")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7053-linregslope")]
	public class LinRegSlope : Indicator
	{
		#region Fields

		private int _period;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (_period == value)
					return;

				if (value <= 1)
					return;

				_period = value;

				RaisePropertyChanged("Period");
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