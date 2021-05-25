namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Bollinger Squeeze 3")]
	[FeatureId("NotReady")]
	public class BollingerSqueezeV3 : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();

		private readonly ValueDataSeries _downRatio = new(Resources.LowRatio);
		private readonly StdDev _stdDev = new();
		private readonly ValueDataSeries _upRatio = new(Resources.HighRatio);
		private decimal _atrMultiplier;
		private decimal _stdMultiplier;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "ATR", Order = 100)]
		public int AtrPeriod
		{
			get => _atr.Period;
			set
			{
				if (value <= 0)
					return;

				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "ATR", Order = 110)]
		public decimal AtrMultiplier
		{
			get => _atrMultiplier;
			set
			{
				if (value <= 0)
					return;

				_atrMultiplier = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 200)]
		public int StdDevPeriod
		{
			get => _stdDev.Period;
			set
			{
				if (value <= 0)
					return;

				_stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "StdDev", Order = 210)]
		public decimal StdMultiplier
		{
			get => _stdMultiplier;
			set
			{
				if (value <= 0)
					return;

				_stdMultiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerSqueezeV3()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_atr.Period = 10;
			_stdDev.Period = 10;

			_upRatio.VisualType = _downRatio.VisualType = VisualMode.Histogram;

			_upRatio.Color = Colors.Green;
			_downRatio.Color = Colors.Red;
			_stdMultiplier = 1;
			_atrMultiplier = 1;
			Add(_atr);

			DataSeries[0] = _upRatio;
			DataSeries.Add(_downRatio);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			var ratio = 0m;
			var stdValue = _stdDev.Calculate(bar, value);

			if (_atr[bar] != 0)
				ratio = _stdMultiplier * stdValue / (_atrMultiplier * _atr[bar]);

			if (ratio >= 1)
				_upRatio[bar] = ratio;
			else
				_downRatio[bar] = ratio;
		}

		#endregion
	}
}