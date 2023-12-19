namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Williams' %R")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.WilliamsRDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602308")]
	public class WilliamsR : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };
		
		private bool _invertOutput;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.InvertOutput), GroupName = nameof(Strings.Settings), Description = nameof(Strings.InvertOutputDescription), Order = 110)]
		public bool InvertOutput
		{
			get => _invertOutput;
			set
			{
				_invertOutput = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WilliamsR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
            _highest.Period = _lowest.Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);

			var renderValue = _highest[bar] != _lowest[bar]
				? 100 * (_highest[bar] - candle.Close) / (_highest[bar] - _lowest[bar])
				: 0m;
			
			this[bar] = _invertOutput
				? -renderValue
				: renderValue;
		}

		#endregion
	}
}