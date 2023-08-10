namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes.Editors;
	using OFT.Localization;
    using Utils.Common.Attributes;

	[OFT.Attributes.HelpLink("https://support.atas.net/knowledge-bases/2/articles/53395-moving-average-of-oscillator")]
	[DisplayName("Moving Averages of Oscillator")]
	public class OSMA : Indicator
	{
		#region Fields

		private EMA _shortEma = new() { Period = 9 };
		private SMA _signalSma = new() { Period = 26 };
		private EMA _longEma = new() { Period = 12 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Period", Order = 100)]
		[Range(2, 10000)]
		[PostValueMode(PostValueModes.OnLostFocus)]
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
        [Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Period", Order = 110)]
		[Range(2, 10000)]
		[PostValueMode(PostValueModes.OnLostFocus)]
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
        [Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Period", Order = 120)]
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