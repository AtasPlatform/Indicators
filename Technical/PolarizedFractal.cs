namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Polarized Fractal Efficiency")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45500-polarized-fractal-efficiency")]
	public class PolarizedFractal : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly ValueDataSeries _sqrtSeries = new("SqrtSum");
		private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(2,10000000)]
		public int ShortPeriod
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 110)]
		[Range(1, 10000000)]
		public int Smooth
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public PolarizedFractal()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderSeries.Clear();
				_sqrtSeries.Clear();
				return;
			}

			var squareDiff = (value - (decimal)SourceDataSeries[bar - 1]) * (value - (decimal)SourceDataSeries[bar - 1]);

			_sqrtSeries[bar] = (decimal)Math.Sqrt((double)squareDiff + 1);

			if (bar < _period)
				return;

			var squarePeriod = (value - (decimal)SourceDataSeries[bar - _period]) * (value - (decimal)SourceDataSeries[bar - _period]) + _period * _period;
			var sqrtPeriod = (decimal)Math.Sqrt((double)squarePeriod);

			var pfe = sqrtPeriod / _sqrtSeries.CalcSum(_period - 1, bar);

			if (value < (decimal)SourceDataSeries[bar - 1])
				pfe = -pfe;

			_renderSeries[bar] = 100 * _ema.Calculate(bar, pfe);
		}

		#endregion
	}
}