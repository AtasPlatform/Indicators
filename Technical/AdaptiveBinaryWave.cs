namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Adaptive Binary Wave")]
	public class AdaptiveBinaryWave : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _amaHigh = new ValueDataSeries("High");
		private readonly ValueDataSeries _amaLow = new ValueDataSeries("Low");

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private readonly AMA _ama = new AMA();
		private decimal _percent;
		private readonly StdDev _stdDev = new StdDev();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _ama.Period;
			set
			{
				if (value <= 0)
					return;

				_ama.Period = _stdDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 110)]
		public decimal ShortPeriod
		{
			get => _ama.FastConstant;
			set
			{
				if (value <= 0)
					return;

				_ama.FastConstant = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 120)]
		public decimal LongPeriod
		{
			get => _ama.SlowConstant;
			set
			{
				if (value <= 0)
					return;

				_ama.SlowConstant = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Percent", GroupName = "Settings", Order = 130)]
		public decimal Percent
		{
			get => _percent;
			set
			{
				if (value <= 0 || value > 100)
					return;

				_percent = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AdaptiveBinaryWave()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_stdDev.Period = _ama.Period;
			_percent = 30;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_ama.Calculate(bar, value);
			_stdDev.Calculate(bar, _ama[bar]);

			if (bar == 0)
			{
				_renderSeries.Clear();
				_amaHigh[bar] = _amaLow[bar] = _ama[bar];
				return;
			}

			if (_ama[bar] < _ama[bar - 1])
				_amaLow[bar] = _ama[bar];
			else
				_amaLow[bar] = _amaLow[bar - 1];

			if (_ama[bar] > _ama[bar - 1])
				_amaHigh[bar] = _ama[bar];
			else
				_amaHigh[bar] = _amaHigh[bar - 1];

			var deviation = _percent * 0.01m * _stdDev[bar];

			if (_ama[bar] - _amaLow[bar] > deviation)
			{
				_renderSeries[bar] = 1;
				return;
			}

			if (_amaHigh[bar] - _ama[bar] > deviation)
			{
				_renderSeries[bar] = -1;
				return;
			}

			_renderSeries[bar] = 0;
		}

		#endregion
	}
}