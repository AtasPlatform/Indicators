namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/53395-moving-average-of-oscillator")]
	[DisplayName("Moving Averages of Oscillator")]
	public class OSMA : Indicator
	{
		#region Fields

		private EMA _fastEma = new() { Period = 12 };
		private SMA _signalSma = new() { Period = 26 };
		private EMA _slowEma = new() { Period = 9 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Period", Order = 100)]
		[Range(2, 10000)]
		public int ShortPeriod
		{
			get => _fastEma.Period;
			set
			{
				if (value >= _slowEma.Period)
					return;

				_fastEma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Period", Order = 110)]
		[Range(2, 10000)]
		public int LongPeriod
		{
			get => _slowEma.Period;
			set
			{
				if (_fastEma.Period >= value)
					return;

				_slowEma.Period = value;
				RecalculateValues();
			}
		}

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
			_fastEma.Calculate(bar, value);
			_slowEma.Calculate(bar, value);

			var macd = _fastEma[bar] - _slowEma[bar];

			_signalSma.Calculate(bar, macd);

			this[bar] = macd - _signalSma[bar];
		}

		#endregion
	}
}