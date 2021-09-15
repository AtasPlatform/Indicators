namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("Stochastic Momentum")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45341-stochastic-momentum")]
	public class StochasticMomentum : Indicator
	{
		#region Fields

		private readonly EMA _emaCloseRange1 = new();
		private readonly EMA _emaCloseRange2 = new();
		private readonly EMA _emaRange1 = new();
		private readonly EMA _emaRange2 = new();
		private readonly EMA _emaSmi = new();
		private readonly Highest _highest = new();
		private readonly Lowest _lowest = new();

		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "PeriodK", GroupName = "Settings", Order = 100)]
		public int PeriodK
		{
			get => _highest.Period;
			set
			{
				if (value <= 0)
					return;

				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "PeriodD", GroupName = "Settings", Order = 110)]
		public int PeriodD
		{
			get => _emaRange1.Period;
			set
			{
				if (value <= 0)
					return;

				_emaRange1.Period = _emaRange2.Period = _emaCloseRange1.Period = _emaCloseRange2.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "EMA", GroupName = "Settings", Order = 120)]
		public int EmaPeriod
		{
			get => _emaSmi.Period;
			set
			{
				if (value <= 0)
					return;

				_emaSmi.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StochasticMomentum()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_highest.Period = _lowest.Period = 10;
			_emaRange1.Period = _emaRange2.Period = _emaCloseRange1.Period = _emaCloseRange2.Period = 10;
			_emaSmi.Period = 15;
			LineSeries.Add(new LineSeries(Strings.ZeroValue) { Color = Colors.Gray, Value = 0, Width = 2 });
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);
			var range = _highest[bar] - _lowest[bar];
			var closeRange = candle.Close - (_highest[bar] + _lowest[bar]) / 2;

			_emaRange1.Calculate(bar, range);
			_emaRange2.Calculate(bar, _emaRange1[bar]);
			_emaCloseRange1.Calculate(bar, closeRange);
			_emaCloseRange2.Calculate(bar, _emaCloseRange1[bar]);

			var smi = 200 * _emaCloseRange2[bar] / _emaRange2[bar];
			_renderSeries[bar] = _emaSmi.Calculate(bar, smi);
		}

		#endregion
	}
}