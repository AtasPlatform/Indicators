namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Price Momentum Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45343-price-momentum-oscillator")]
	public class MomentumOscillator : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _csfSeries = new("CSF");
		private readonly EMA _ema = new();
		private readonly ValueDataSeries _pmoSeries = new("Pmo");
		private readonly ValueDataSeries _rateSeries = new("Rate");

		private readonly ValueDataSeries _signalSeries = new(Resources.Line);
		private readonly ValueDataSeries _smoothSeries = new(Resources.EMA);
		private int _period;
		private int _period1;
		private int _period2;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 110)]
		public int SignalPeriod
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

				_ema.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period1", GroupName = "Settings", Order = 120)]
		public int Period1
		{
			get => _period1;
			set
			{
				if (value <= 0)
					return;

				_period1 = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period2", GroupName = "Settings", Order = 120)]
		public int Period2
		{
			get => _period2;
			set
			{
				if (value <= 0)
					return;

				_period2 = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MomentumOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_ema.Period = 10;
			_period = 10;
			_period1 = _period2 = 10;
			_signalSeries.Color = Colors.Red;
			_smoothSeries.Color = Colors.Blue;

			DataSeries[0] = _signalSeries;
			DataSeries.Add(_smoothSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_csfSeries[bar] = value;
				return;
			}

			var rate = 100m * (value - (decimal)SourceDataSeries[bar - 1]) / (decimal)SourceDataSeries[bar - 1];
			_csfSeries[bar] = 2m / _period * (value - _csfSeries[bar - 1]) + _csfSeries[bar - 1];
			_rateSeries[bar] = 2m / _period1 * (rate - _rateSeries[bar - 1]) + _rateSeries[bar - 1];
			_signalSeries[bar] = 2m / _period2 * (_rateSeries[bar] - _pmoSeries[bar - 1]) + _pmoSeries[bar - 1];

			_smoothSeries[bar] = _ema.Calculate(bar, 10 * _signalSeries[bar]);
		}

		#endregion
	}
}