namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Price Momentum Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45343-price-momentum-oscillator")]
	public class MomentumOscillator : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private readonly ValueDataSeries _rateSeries = new("Rate");

		private readonly ValueDataSeries _signalSeries = new(Resources.Line)
		{
			Color = DefaultColors.Red.Convert(),
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _smoothSeries = new(Resources.EMA)
		{
			Color = DefaultColors.Blue.Convert(),
			UseMinimizedModeIfEnabled = true,
			IgnoredByAlerts = true
		};

		private int _period1 = 10;
		private int _period2 = 10;

		#endregion

		#region Properties
		
		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period1", GroupName = "Settings", Order = 120)]
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

		[Display(ResourceType = typeof(Resources), Name = "Period2", GroupName = "Settings", Order = 120)]
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