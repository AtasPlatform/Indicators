namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Double Stochastic")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45191-double-stochastic")]
	public class DoubleStochastic : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private readonly EMA _emaSecond = new() { Period = 10 };

		private readonly Highest _max = new() { Period = 10 };
		private readonly Lowest _min = new() { Period = 10 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _max.Period;
			set
			{
				_max.Period = _min.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EMAPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _ema.Period;
			set
			{
				_ema.Period = _emaSecond.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DoubleStochastic()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var max = _max.Calculate(bar, candle.High);
			var min = _min.Calculate(bar, candle.Low);

			var fastK1 = max - min != 0
				? 100m * (candle.Close - min) / (max - min)
				: 100m * (candle.Close - min);

            var fastD1 = _ema.Calculate(bar, fastK1);

			var maxD1 = ((ValueDataSeries)_ema.DataSeries[0]).MAX(_max.Period, bar);
			var minD1 = ((ValueDataSeries)_ema.DataSeries[0]).MIN(_max.Period, bar);

			var fastK2 = maxD1 - minD1 != 0
				? 100m * (fastD1 - minD1) / (maxD1 - minD1)
				: 100m * (fastD1 - minD1);
			
			_renderSeries[bar] = _emaSecond.Calculate(bar, fastK2);
		}

		#endregion
	}
}