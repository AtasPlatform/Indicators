namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Stochastic Momentum")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45341-stochastic-momentum")]
	public class StochasticMomentum : Indicator
	{
		#region Fields

		private readonly EMA _emaCloseRange1 = new() { Period = 10 };
		private readonly EMA _emaCloseRange2 = new() { Period = 10 };
        private readonly EMA _emaRange1 = new() { Period = 10 };
        private readonly EMA _emaRange2 = new() { Period = 10 };
        private readonly EMA _emaSmi = new() { Period = 15 };
        private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };

        private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "PeriodK", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int PeriodK
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "PeriodD", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int PeriodD
		{
			get => _emaRange1.Period;
			set
			{
				_emaRange1.Period = _emaRange2.Period = _emaCloseRange1.Period = _emaCloseRange2.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "EMA", GroupName = "Settings", Order = 120)]
		[Range(1, 10000)]
		public int EmaPeriod
		{
			get => _emaSmi.Period;
			set
			{
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
			LineSeries.Add(new LineSeries("ZeroVal", Strings.ZeroValue) { Color = Colors.Gray, Value = 0, Width = 2 });
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