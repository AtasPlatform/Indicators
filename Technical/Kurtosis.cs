namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Kurtosis")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45322-kurtosis")]
	public class Kurtosis : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _populationSeries = new(Resources.Line);
		private readonly ValueDataSeries _quadSeries = new("Quad");
		private readonly ValueDataSeries _sampleSeries = new(Resources.Estimator);
		private readonly SMA _sma = new();
		private readonly ValueDataSeries _squareSeries = new("Square");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 3)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Kurtosis()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_populationSeries.Color = Colors.Red;
			_sampleSeries.Color = Colors.Blue;

			DataSeries[0] = _populationSeries;
			DataSeries.Add(_sampleSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var diff = Convert.ToDouble(value - _sma.Calculate(bar, value));

			_squareSeries[bar] = Convert.ToDecimal(Math.Pow(diff, 2));
			_quadSeries[bar] = Convert.ToDecimal(Math.Pow(diff, 4));

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