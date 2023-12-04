namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Connie Brown Composite Index")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CBIDescription))] 
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602601")]
	public class CBI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _cbi1Series = new("Cbi1Series", Strings.ShortPeriod) { IgnoredByAlerts = true };
		private readonly ValueDataSeries _cbi2Series = new("Cbi2Series", Strings.MiddleBand);
        private readonly ValueDataSeries _cbi3Series = new("Cbi3Series", Strings.LongPeriod) { IgnoredByAlerts = true };
        private readonly Momentum _momentum = new();

		private readonly RSI _rsi1 = new();
		private readonly RSI _rsi2 = new();
		private readonly SMA _sma1 = new();
		private readonly SMA _sma2 = new();
		private readonly SMA _sma3 = new();

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod1), GroupName = nameof(Strings.RSI), Description = nameof(Strings.PeriodDescription), Order = 100)]
		public int Rsi1Period
		{
			get => _rsi1.Period;
			set
			{
				_rsi1.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod2), GroupName = nameof(Strings.RSI), Description = nameof(Strings.PeriodDescription), Order = 110)]
		public int Rsi2Period
		{
			get => _rsi2.Period;
			set
			{
				_rsi2.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Momentum), Description = nameof(Strings.PeriodDescription), Order = 200)]
		public int MomentumPeriod
		{
			get => _momentum.Period;
			set
			{
				_momentum.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod1), GroupName = nameof(Strings.SMA), Description = nameof(Strings.PeriodDescription), Order = 300)]
		public int Sma1Period
		{
			get => _sma1.Period;
			set
			{
				_sma1.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod2), GroupName = nameof(Strings.SMA), Description = nameof(Strings.PeriodDescription), Order = 310)]
		public int Sma2Period
		{
			get => _sma2.Period;
			set
			{
				_sma2.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod3), GroupName = nameof(Strings.SMA), Description = nameof(Strings.PeriodDescription), Order = 320)]
		public int Sma3Period
		{
			get => _sma3.Period;
			set
			{
				_sma3.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CBI()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_momentum.Period = 9;
			_rsi1.Period = 3;
			_rsi2.Period = 14;

			_sma1.Period = 3;
			_sma2.Period = 13;
			_sma3.Period = 33;

			_cbi1Series.Color = DefaultColors.Red.Convert();
			_cbi2Series.Color = DefaultColors.Orange.Convert();
			_cbi3Series.Color = DefaultColors.Green.Convert();

			DataSeries[0] = _cbi1Series;
			DataSeries.Add(_cbi2Series);
			DataSeries.Add(_cbi3Series);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_cbi1Series[bar] = _momentum.Calculate(bar, _rsi1.Calculate(bar, value)) + _sma1.Calculate(bar, _rsi2.Calculate(bar, value));
			_cbi2Series[bar] = _sma2.Calculate(bar, _cbi1Series[bar]);
			_cbi3Series[bar] = _sma3.Calculate(bar, _cbi1Series[bar]);
		}

		#endregion
	}
}