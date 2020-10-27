namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Fisher Transform")]
	public class FisherTransform : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _fisher = new ValueDataSeries("Fisher");

		private readonly Highest _highest = new Highest();
		private readonly Lowest _lowest = new Lowest();
		private readonly ValueDataSeries _triggers = new ValueDataSeries("Triggers");

		private readonly ValueDataSeries _values = new ValueDataSeries("Values");
		private readonly decimal _lastbar;
		private decimal _lastFisher;
		private decimal _lastValue;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common")]
		public int Period
		{
			get => _highest.Period;
			set
			{
				if (value <= 0)
					return;

				_highest.Period = value;
				_lowest.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public FisherTransform()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_lastbar = -1;
			_highest.Period = _lowest.Period = 10;

			_triggers.Color = Colors.Red;
			_fisher.Color = Colors.DodgerBlue;

			DataSeries[0] = _triggers;
			DataSeries.Add(_fisher);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar > Period)
			{
				if (bar != _lastbar && bar > 0)
				{
					_lastValue = _values[bar - 1];
					_lastFisher = _fisher[bar - 1];
				}

				var candle = GetCandle(bar);

				var smax = _highest.Calculate(bar, candle.High);
				var smin = _lowest.Calculate(bar, candle.Close);

				if (smax == smin)
					smax += ChartInfo.PriceChartContainer.Step;

				var wpr = (candle.Close - smin) / (smax - smin);

				var valueSeries = 0.66m * (wpr - 0.5m) + 0.67m * _lastValue;

				if (valueSeries >= 1 || valueSeries <= -1)
					valueSeries = Math.Sign(valueSeries) * 0.999m;

				var fisherSeries = 0.5 * Math.Log((1.0 + Convert.ToDouble(valueSeries)) / (1.0 - Convert.ToDouble(valueSeries))) +
					0.5 * Convert.ToDouble(_lastFisher);

				fisherSeries = Math.Round(fisherSeries, 5);

				_values[bar] = valueSeries;
				_fisher[bar] = Convert.ToDecimal(fisherSeries);
				_triggers[bar] = _lastFisher;
			}
		}

		#endregion
	}
}