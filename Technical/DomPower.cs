namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Dom Power")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/369-dom-power")]
	public class DomPower : Indicator
	{
		#region Fields

		private ValueDataSeries _asks = new("Asks") { UseMinimizedModeIfEnabled = true };
		private ValueDataSeries _bids = new("Bids")
		{
			Color = Colors.Green,
			UseMinimizedModeIfEnabled = true
		};

		private ValueDataSeries _maxDelta = new("Max Delta")
		{
			Color = Color.FromArgb(255, 27, 134, 198),
			UseMinimizedModeIfEnabled = true
		};

        private ValueDataSeries _minDelta = new("Min Delta")
        {
	        Color = Color.FromArgb(255, 27, 134, 198),
	        UseMinimizedModeIfEnabled = true
        };
		
        private bool _first = true;
        private int _lastCalculatedBar;

        #endregion

        #region ctor

        public DomPower()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _asks;
			DataSeries.Add(_bids);
			DataSeries.Add(_maxDelta);
			DataSeries.Add(_minDelta);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void MarketDepthChanged(MarketDataArg arg)
		{
			if (_first)
			{
				_first = false;
				_lastCalculatedBar = CurrentBar - 1;
			}

			var lastCandle = CurrentBar - 1;
			var cumAsks = MarketDepthInfo.CumulativeDomAsks;
			var cumBids = MarketDepthInfo.CumulativeDomBids;
			var delta = cumBids - cumAsks;
			var calcDelta = cumAsks != 0 && cumBids != 0;

			if (!calcDelta)
				return;

			for (var i = _lastCalculatedBar; i <= lastCandle; i++)
			{
				_asks[i] = -cumAsks;
				_bids[i] = cumBids;
				var max = _maxDelta[i];

				if (delta > max || max == 0)
					_maxDelta[i] = delta;
				var min = _minDelta[i];

				if (delta < min || min == 0)
					_minDelta[i] = delta;

				RaiseBarValueChanged(i);
			}

			_lastCalculatedBar = lastCandle;
		}

		#endregion
	}
}