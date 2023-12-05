namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Localization;

	using Utils.Common.Attributes;

    [DisplayName("Moving Average of Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OSMADescription))]
    [OFT.Attributes.HelpLink("https://help.atas.net/en/support/solutions/articles/72000602432")]	
	public class OSMA : Indicator
	{
		#region Fields

		private EMA _shortEma = new() { Period = 9 };
		private SMA _signalSma = new() { Period = 26 };
		private EMA _longEma = new() { Period = 12 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Period), Description = nameof(Strings.ShortPeriodDescription), Order = 100)]
		[Range(2, 10000)]
		[LessThan<int>(nameof(LongPeriod), ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.ValueMustBeLessThan))]
		public int ShortPeriod
		{
			get => _shortEma.Period;
			set
			{
				if (value >= LongPeriod)
					return;

				_shortEma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Period), Description = nameof(Strings.LongPeriodDescription), Order = 110)]
		[Range(2, 10000)]
		[GreaterThan<int>(nameof(ShortPeriod), ErrorMessageResourceType = typeof(Strings), ErrorMessageResourceName = nameof(Strings.ValueMustBeGreaterThan))]
		public int LongPeriod
		{
			get => _longEma.Period;
			set
			{
				if (value <= ShortPeriod)
					return;

				_longEma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SignalPeriod), GroupName = nameof(Strings.Period), Description = nameof(Strings.SignalPeriodDescription), Order = 120)]
		[Range(2, 10000)]
		public int SignalPeriod
		{
			get => _signalSma.Period;
			set
			{
				_signalSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OSMA()
		{
			Panel = IndicatorDataProvider.NewPanel;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Histogram;
			DataSeries[0].UseMinimizedModeIfEnabled = true;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_shortEma.Calculate(bar, value);
			_longEma.Calculate(bar, value);

			var macd = _shortEma[bar] - _longEma[bar];

			_signalSma.Calculate(bar, macd);

			this[bar] = macd - _signalSma[bar];
		}

		#endregion
	}
}