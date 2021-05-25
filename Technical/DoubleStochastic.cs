namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Double Stochastic")]
	[FeatureId("NotReady")]
	public class DoubleStochastic : Indicator
	{
		#region Fields

		private readonly EMA _ema = new();
		private readonly EMA _emaSecond = new();

		private readonly Highest _max = new();
		private readonly Lowest _min = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _max.Period;
			set
			{
				if (value <= 0)
					return;

				_max.Period = _min.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		public int SmaPeriod
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

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
			_ema.Period = _emaSecond.Period = 10;
			_max.Period = _min.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var max = _max.Calculate(bar, candle.High);
			var min = _min.Calculate(bar, candle.Low);

			var fastK1 = 0m;

			if (max - min != 0)
				fastK1 = 100m * (candle.Close - min) / (max - min);
			else
				fastK1 = 100m * (candle.Close - min);

			var fastD1 = _ema.Calculate(bar, fastK1);

			var maxD1 = ((ValueDataSeries)_ema.DataSeries[0]).MAX(_max.Period, bar);
			var minD1 = ((ValueDataSeries)_ema.DataSeries[0]).MIN(_max.Period, bar);

			var fastK2 = 0m;

			if (maxD1 - minD1 != 0)
				fastK2 = 100m * (fastD1 - minD1) / (maxD1 - minD1);
			else
				fastK2 = 100m * (fastD1 - minD1);

			_renderSeries[bar] = _emaSecond.Calculate(bar, fastK2);
		}

		#endregion
	}
}