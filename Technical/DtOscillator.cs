namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("DT Oscillator")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43363-dt-oscillator")]
	public class DtOscillator : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _sdSeries = new(Resources.SMMA)
		{
			Color = DefaultColors.Blue.Convert(),
			IgnoredByAlerts = true
		};
		private readonly ValueDataSeries _skSeries = new(Resources.SMA);

		private readonly SMA _smaSd = new() { Period = 3 };
		private readonly SMA _smaSk = new() { Period = 3 };
		private readonly StochasticRsi _stRsi = new()
		{
			RsiPeriod = 8,
			Period = 5
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Stochastic", Order = 100)]
		[Range(1, 10000)]
		public int RsiPeriod
		{
			get => _stRsi.RsiPeriod;
			set
			{
				_stRsi.RsiPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Stochastic", Order = 110)]
		[Range(1, 10000)]
        public int Period
		{
			get => _stRsi.Period;
			set
			{
				_stRsi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "Smooth", Order = 200)]
		[Range(1, 10000)]
        public int SMAPeriod1
		{
			get => _smaSk.Period;
			set
			{
				_smaSk.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "Smooth", Order = 210)]
		[Range(1, 10000)]
        public int SMAPeriod2
		{
			get => _smaSd.Period;
			set
			{
				_smaSd.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DtOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
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