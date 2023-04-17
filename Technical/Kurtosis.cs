namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Kurtosis")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45322-kurtosis")]
	public class Kurtosis : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _populationSeries = new(Resources.Line) { IgnoredByAlerts = true };
		private readonly ValueDataSeries _quadSeries = new("Quad");
		private readonly ValueDataSeries _sampleSeries = new(Resources.Estimator) { Color = DefaultColors.Blue.Convert() };
		private readonly SMA _sma = new();
		private readonly ValueDataSeries _squareSeries = new("Square");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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