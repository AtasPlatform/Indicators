namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Volatility - Historical")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45335-volatility-historical")]
	public class VolatilityHist : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _diffSquareSeries = new("Diff");
		private readonly SMA _sma = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(2, 10000)]
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

		public VolatilityHist()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				Clear();
				_sma.Calculate(bar, 0);
				return;
			}

			var lr = (decimal)Math.Log((double)(value / (decimal)SourceDataSeries[bar - 1]));
			_sma.Calculate(bar, lr);

			var diff = lr - _sma[bar];
			_diffSquareSeries[bar] = diff * diff;

			if (bar < Period)
				return;

			this[bar] = 100 * (decimal)(Math.Sqrt(CurrentBar) * Math.Sqrt((double)_diffSquareSeries.CalcSum(Period, bar) / (Period - 1)));
		}

		#endregion
	}
}