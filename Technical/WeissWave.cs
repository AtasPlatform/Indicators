namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Weis Wave")]
	[Description("Weis Wave")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17943-weis-wave")]
	public class WeissWave : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downValueSeries = new ValueDataSeries("Down DataSeries");

		private readonly ValueDataSeries _filterValueSeries = new ValueDataSeries("Filter DataSeries")
			{ Color = Colors.LightBlue, VisualType = VisualMode.Histogram, ShowZeroValue = false };

		private readonly ValueDataSeries _upValueSeries;
		private int _filter;

		#endregion

		#region Properties

		[DisplayName("Filter")]
		public int Filter
		{
			get => _filter;
			set
			{
				_filter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WeissWave()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_upValueSeries = (ValueDataSeries)DataSeries[0];
			_upValueSeries.Name = "Up DataSeries";
			_upValueSeries.Color = Colors.Green;
			_upValueSeries.VisualType = VisualMode.Histogram;
			_upValueSeries.ShowZeroValue = false;

			_downValueSeries.Color = Colors.Red;
			_downValueSeries.VisualType = VisualMode.Histogram;
			_downValueSeries.ShowZeroValue = false;

			DataSeries.Add(_downValueSeries);
			DataSeries.Add(_filterValueSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_upValueSeries[bar] = _downValueSeries[bar] = 0;
			if (bar == 0)
			{
				var candle = GetCandle(bar);
				if (candle.Open < candle.Close) // bullish
					_upValueSeries[bar] = candle.Volume;
				else if (candle.Open > candle.Close) // bearish
					_downValueSeries[bar] = candle.Volume;
			}
			else
			{
				var candle = GetCandle(bar);
				if (candle.Open < candle.Close) // bullish
					_upValueSeries[bar] = _upValueSeries[bar - 1] + candle.Volume;
				else if (candle.Open > candle.Close) // bearish
					_downValueSeries[bar] = _downValueSeries[bar - 1] + candle.Volume;
				else
				{
					if (_upValueSeries[bar - 1] > 0)
						_upValueSeries[bar] = _upValueSeries[bar - 1] + candle.Volume;
					else if (_downValueSeries[bar - 1] > 0)
						_downValueSeries[bar] = _downValueSeries[bar - 1] + candle.Volume;
				}
			}

			if (_filter > 0)
			{
				var volume = Math.Max(_upValueSeries[bar], _downValueSeries[bar]);
				if (volume > _filter)
					_filterValueSeries[bar] = volume;
			}
		}

		#endregion
	}
}