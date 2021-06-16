namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Historical Volatility Ratio")]
	public class HVR : Indicator
	{
		#region Fields

		private readonly StdDev _longDev = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly StdDev _shortDev = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _shortDev.Period;
			set
			{
				if (value <= 0)
					return;

				_shortDev.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		public int LongPeriod
		{
			get => _longDev.Period;
			set
			{
				if (value <= 0)
					return;

				_longDev.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HVR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_shortDev.Period = 6;
			_longDev.Period = 100;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var lr = Convert.ToDecimal(Math.Log(Convert.ToDouble(candle.Close / prevCandle.Close)));
			_renderSeries[bar] = _shortDev.Calculate(bar, lr) / _longDev.Calculate(bar, lr);
		}

		#endregion
	}
}