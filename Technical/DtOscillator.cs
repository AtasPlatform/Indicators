namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("DT Oscillator")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/43363-dt-oscillator")]
	public class DtOscillator : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _sdSeries = new(Resources.SMMA);

		private readonly ValueDataSeries _skSeries = new(Resources.SMA);
		private readonly SMA _smaSd = new();
		private readonly SMA _smaSk = new();
		private readonly StochasticRsi _stRsi = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Stochastic", Order = 100)]
		public int RsiPeriod
		{
			get => _stRsi.RsiPeriod;
			set
			{
				if (value <= 0)
					return;

				_stRsi.RsiPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Stochastic", Order = 110)]
		public int Period
		{
			get => _stRsi.Period;
			set
			{
				if (value <= 0)
					return;

				_stRsi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "Smooth", Order = 200)]
		public int SMAPeriod1
		{
			get => _smaSk.Period;
			set
			{
				if (value <= 0)
					return;

				_smaSk.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "Smooth", Order = 210)]
		public int SMAPeriod2
		{
			get => _smaSd.Period;
			set
			{
				if (value <= 0)
					return;

				_smaSd.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DtOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_stRsi.RsiPeriod = 8;
			_stRsi.Period = 5;

			_smaSk.Period = 3;
			_smaSd.Period = 3;

			_skSeries.Color = Colors.Red;
			_sdSeries.Color = Colors.Blue;

			DataSeries[0] = _skSeries;
			DataSeries.Add(_sdSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var stochRsi = _stRsi.Calculate(bar, value);
			_skSeries[bar] = _smaSk.Calculate(bar, 100 * stochRsi);
			_sdSeries[bar] = _smaSd.Calculate(bar, _skSeries[bar]);
		}

		#endregion
	}
}