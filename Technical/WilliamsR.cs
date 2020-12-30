namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Williams' %R")]
	public class WilliamsR : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private readonly Highest _highest = new Highest();
		private bool _invertOutput;
		private readonly Lowest _lowest = new Lowest();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
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

		[Display(ResourceType = typeof(Resources), Name = "InvertOutput", GroupName = "Settings", Order = 110)]
		public bool InvertOutput
		{
			get => _invertOutput;
			set
			{
				_invertOutput = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WilliamsR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_highest.Period = _lowest.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highest.Calculate(bar, candle.High);
			_lowest.Calculate(bar, candle.Low);

			var renderValue = 0m;

			if (_highest[bar] != _lowest[bar])
				renderValue = 100 * (_highest[bar] - candle.Close) / (_highest[bar] - _lowest[bar]);

			if (_invertOutput)
				_renderSeries[bar] = -renderValue;
			else
				_renderSeries[bar] = renderValue;
		}

		#endregion
	}
}