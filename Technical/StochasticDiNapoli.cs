namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Preferred Stochastic - DiNapoli")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StochasticDiNapoliDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602575")]
	public class StochasticDiNapoli : Indicator
	{
		#region Fields

		private readonly EMA _ema = new();
        private readonly KdFast _kdFast = new();
        private readonly KdSlow _kdSlow = new();

		private readonly ValueDataSeries _fastSeries = new("FastSeries", Strings.FastLine)
		{
			DescriptionKey = nameof(Strings.FastLineSettingsDescription)
		};

		private readonly ValueDataSeries _slowSeries = new("SlowSeries", Strings.SlowLine)
		{
            DescriptionKey = nameof(Strings.SlowLineSettingsDescription)
        };

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodK), GroupName = nameof(Strings.ShortPeriod), Description = nameof(Strings.ShortPeriodKDescription), Order = 100)]
		public int PeriodK
		{
			get => _kdFast.PeriodK;
			set
			{
				_kdFast.PeriodK = _kdSlow.PeriodK = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodD), GroupName = nameof(Strings.ShortPeriod), Description = nameof(Strings.ShortPeriodDDescription), Order = 110)]
		public int PeriodD
		{
			get => _kdFast.PeriodD;
			set
			{
				_kdFast.PeriodD = _kdSlow.PeriodD = _ema.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodD), GroupName = nameof(Strings.LongPeriod), Description = nameof(Strings.LongPeriodDDescription), Order = 110)]
		public int SlowPeriodD
		{
			get => _kdSlow.SlowPeriodD;
			set
			{
				_kdSlow.SlowPeriodD = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StochasticDiNapoli()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_ema.Period = _kdFast.PeriodD;
			Add(_kdFast);
			Add(_kdSlow);

			_fastSeries.Color = DefaultColors.Blue.Convert();
			_slowSeries.Color = DefaultColors.Red.Convert();

			DataSeries[0] = _fastSeries;
			DataSeries.Add(_slowSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			_fastSeries[bar] = _ema.Calculate(bar, ((ValueDataSeries)_kdFast.DataSeries[0])[bar]);
			var prevSlowD = ((ValueDataSeries)_kdSlow.DataSeries[1])[bar - 1];
			var fastD = ((ValueDataSeries)_kdFast.DataSeries[1])[bar];

			_slowSeries[bar] = prevSlowD + (fastD - prevSlowD) / SlowPeriodD;
		}

		#endregion
	}
}