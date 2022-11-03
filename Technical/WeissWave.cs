namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Weis Wave")]
	[Description("Weis Wave")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/17943-weis-wave")]
	public class WeissWave : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downValueSeries = new("Down DataSeries")
		{
			Color = Colors.Red,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _filterValueSeries = new("Filter DataSeries")
		{
			Color = Colors.LightBlue, 
			VisualType = VisualMode.Histogram, 
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _upValueSeries = new("Up DataSeries")
		{
			Color = Colors.Green,
			VisualType = VisualMode.Histogram,
			ShowZeroValue = false,
			UseMinimizedModeIfEnabled = true
		};

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
			DataSeries[0] = _upValueSeries;
			
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