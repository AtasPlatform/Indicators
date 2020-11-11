namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Dom Power")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/369-dom-power")]
	public class DomPower : Indicator
	{
		private ValueDataSeries _asks;
		private ValueDataSeries _bids=new ValueDataSeries("Bids");
		private ValueDataSeries _maxDelta = new ValueDataSeries("Max Delta");
		private ValueDataSeries _minDelta = new ValueDataSeries("Min Delta");

		private int _lastCalculatedBar = 0;
		bool _first = true;
		public DomPower():base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_asks = (ValueDataSeries)DataSeries[0];
			_asks.Name = "Asks";
			_bids.Color = Colors.Green;
			DataSeries.Add(_bids);
			DataSeries.Add(_maxDelta);
			DataSeries.Add(_minDelta);
			_maxDelta.Color = Color.FromArgb(255, 27, 134, 198);
			_minDelta.Color = Color.FromArgb(255, 27, 134, 198);
		}

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
			int lastCandle = CurrentBar - 1;
			var cumAsks = MarketDepthInfo.CumulativeDomAsks;
			var cumBids = MarketDepthInfo.CumulativeDomBids;
			var delta = cumBids - cumAsks;
			var calcDelta = cumAsks != 0 && cumBids != 0;
			if(!calcDelta) return;

			for (int i = _lastCalculatedBar; i <= lastCandle; i++)
			{
				_asks[i] = -cumAsks;
				_bids[i] = cumBids;
				var max = _maxDelta[i];
				if (delta > max || max == 0)
					_maxDelta[i] = delta;
				var min = _minDelta[i];
				if (delta < min || min == 0)
					_minDelta[i] = delta;


			}

			_lastCalculatedBar = lastCandle;
		}
	}
}