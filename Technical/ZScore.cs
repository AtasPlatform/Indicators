namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Z-Score")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ZScoreIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602269e")]
	public class ZScore : Indicator
	{
		#region Fields

		private readonly SMA _sma = new() { Period = 10 };
		private readonly StdDev _stdDev = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA), GroupName = nameof(Strings.Period), Description = nameof(Strings.SMAPeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StdDev), GroupName = nameof(Strings.Period), Description = nameof(Strings.StdDevPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int StdPeriod
		{
			get => _stdDev.Period;
			set
			{
				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ZScore()
		{
			Panel = IndicatorDataProvider.NewPanel;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_stdDev.Calculate(bar, value);

			this[bar] = _stdDev[bar] != 0
				? (value - _sma[bar]) / _stdDev[bar]
				: 0;
		}

		#endregion
	}
}