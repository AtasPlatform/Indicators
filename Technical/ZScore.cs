namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Z-Score")]
	[FeatureId("NotReady")]
	public class ZScore : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		private readonly SMA _sma = new();
		private readonly StdDev _stdDev = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SMA", GroupName = "Period", Order = 100)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StdDev", GroupName = "Period", Order = 110)]
		public int StdPeriod
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

		#endregion

		#region ctor

		public ZScore()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_stdDev.Period = 10;
			_sma.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_stdDev.Calculate(bar, value);

			if (_stdDev[bar] != 0)
				_renderSeries[bar] = (value - _sma[bar]) / _stdDev[bar];
			else
				_renderSeries[bar] = 0;
		}

		#endregion
	}
}