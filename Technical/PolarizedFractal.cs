namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Polarized Fractal Efficiency")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.PolarizedFractalDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602281")]
	public class PolarizedFractal : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly ValueDataSeries _sqrtSeries = new("SqrtSum");
		private int _period = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(2, 10000000)]
		public int ShortPeriod
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Smooth), GroupName = nameof(Strings.Settings), Description = nameof(Strings.EMAPeriodDescription), Order = 110)]
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