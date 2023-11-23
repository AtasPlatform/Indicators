namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Adaptive Binary Wave")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ABWMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602535")]
	public class AdaptiveBinaryWaveMA : Indicator
	{
		#region Fields

		private readonly AMA _ama = new();

		private readonly ValueDataSeries _amaHigh = new("High");
		private readonly ValueDataSeries _amaLow = new("Low");

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly StdDev _stdDev = new();
		private decimal _percent;

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		public int Period
		{
			get => _ama.Period;
			set
			{
				_ama.Period = _stdDev.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.FastConstDescription), Order = 110)]
		public decimal ShortPeriod
		{
			get => _ama.FastConstant;
			set
			{
				_ama.FastConstant = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SlowConstDescription), Order = 120)]
		public decimal LongPeriod
		{
			get => _ama.SlowConstant;
			set
			{
				_ama.SlowConstant = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 100)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DeviationPercentageDescription), Order = 130)]
		public decimal Percent
		{
			get => _percent;
			set
			{
				_percent = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AdaptiveBinaryWaveMA()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_stdDev.Period = _ama.Period;
			_percent = 30;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_ama.Calculate(bar, value);
			_stdDev.Calculate(bar, _ama[bar]);

			if (bar == 0)
			{
				_renderSeries.Clear();
				_amaHigh[bar] = _amaLow[bar] = _ama[bar];
				return;
			}

			_amaLow[bar] = _ama[bar] < _ama[bar - 1] 
					 	 ? _ama[bar] 
						 : _amaLow[bar - 1];

			_amaHigh[bar] = _ama[bar] > _ama[bar - 1] 
						  ? _ama[bar] 
					   	  : _amaHigh[bar - 1];

			var deviation = _percent * 0.01m * _stdDev[bar];

			if (_ama[bar] - _amaLow[bar] > deviation)
			{
				_renderSeries[bar] = 1;
				return;
			}

			if (_amaHigh[bar] - _ama[bar] > deviation)
			{
				_renderSeries[bar] = -1;
				return;
			}

			_renderSeries[bar] = 0;
		}

		#endregion
	}
}