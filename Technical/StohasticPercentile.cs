namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Stochastic - Percentile")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45493-stochastic-percentile")]
	public class StohasticPercentile : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		private readonly SMA _sma = new() { Period = 10 };
		private readonly List<decimal> _values = new();
		private int _lastBar = -1;
		private int _period = 10;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(2, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMA", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StohasticPercentile()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_values.Clear();
				_renderSeries.Clear();
			}

			if (_values.Count > _period)
				_values.RemoveAt(0);

			if (bar == _lastBar)
				_values.RemoveAt(_values.Count - 1);

			_values.Add(value);

			var rankedValues = _values.OrderBy(x => x).ToList();

			var sp = 100m * rankedValues.IndexOf(value) / (_period - 1);

			_sma.Calculate(bar, sp);

			_renderSeries[bar] = (decimal)Math.Max(0.000001, (double)_sma[bar]);

			_lastBar = bar;
		}

		#endregion
	}
}