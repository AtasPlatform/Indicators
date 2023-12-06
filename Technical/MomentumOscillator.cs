namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Price Momentum Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MomentumOscillatorDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602449")]
	public class MomentumOscillator : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private readonly ValueDataSeries _rateSeries = new("Rate");

		private readonly ValueDataSeries _signalSeries = new("SignalSeries", Strings.Line)
		{
			Color = DefaultColors.Red.Convert(),
			UseMinimizedModeIfEnabled = true,
			DescriptionKey = nameof(Strings.SignalLineSettingsDescription)
		};

		private readonly ValueDataSeries _smoothSeries = new("SmoothSeries", Strings.EMA)
		{
			Color = DefaultColors.Blue.Convert(),
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.EMALineSettingsDescription)
        };

		private int _period1 = 10;
		private int _period2 = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SignalPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.EMAPeriodDescription), Order = 110)]
		[Range(1, 10000)]
		public int SignalPeriod
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period1), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 120)]
		[Range(1, 10000)]
        public int Period1
		{
			get => _period1;
			set
			{
				_period1 = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period2), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 120)]
		[Range(1, 10000)]
		public int Period2
		{
			get => _period2;
			set
			{
				_period2 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MomentumOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _signalSeries;
			DataSeries.Add(_smoothSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var rate = 100m * (value - (decimal)SourceDataSeries[bar - 1]) / (decimal)SourceDataSeries[bar - 1];
			_rateSeries[bar] = 2m / _period1 * (rate - _rateSeries[bar - 1]) + _rateSeries[bar - 1];
			_signalSeries[bar] = 2m / _period2 * (_rateSeries[bar] - _signalSeries[bar - 1]) + _signalSeries[bar - 1];

			_smoothSeries[bar] = _ema.Calculate(bar, 10 * _signalSeries[bar]);
		}

		#endregion
	}
}