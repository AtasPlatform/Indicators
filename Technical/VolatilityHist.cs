namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Volatility - Historical")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45335-volatility-historical")]
	public class VolatilityHist : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _diffSquareSeries = new("Diff");
		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);

		private readonly SMA _sma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 1)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VolatilityHist()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_sma.Period = 10;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_sma.Calculate(bar, 0);
				return;
			}

			var lr = (decimal)Math.Log((double)(value / (decimal)SourceDataSeries[bar - 1]));
			_sma.Calculate(bar, lr);

			var diff = lr - _sma[bar];
			_diffSquareSeries[bar] = diff * diff;

			if (bar < Period)
				return;

			_renderSeries[bar] = 100 * (decimal)(Math.Sqrt(CurrentBar) * Math.Sqrt((double)_diffSquareSeries.CalcSum(Period, bar) / (Period - 1)));
		}

		#endregion
	}
}