namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Moving Averages of Oscillator")]
	public class OSMA : Indicator
	{
		#region Fields

		private EMA _fastEma = new();
		private SMA _signalSma = new();
		private EMA _slowEma = new();

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
			_fastEma.Period = 12;
			_slowEma.Period = 26;
			_signalSma.Period = 9;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Histogram;
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