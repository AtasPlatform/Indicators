namespace ATAS.Indicators.Technical;

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

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

	private bool _first = true;
	private int _lastCalculatedBar;
	private Filter _levelDepth = new(true)
	{
		Value = 5,
		Enabled = false
	};
	private object _locker = new();

	private ValueDataSeries _maxDelta = new("Max Delta")
	{
		Color = Color.FromArgb(255, 27, 134, 198),
		UseMinimizedModeIfEnabled = true
	};

	private SortedList<decimal, decimal> _mDepthAsk = new();
	private SortedList<decimal, decimal> _mDepthBid = new();

	private ValueDataSeries _minDelta = new("Min Delta")
	{
		Color = Color.FromArgb(255, 27, 134, 198),
		UseMinimizedModeIfEnabled = true
	};

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "DepthMarketFilter", GroupName = "Period", Order = 100)]
	[Range(1, 1000)]
	public Filter LevelDepth
	{
		get => _levelDepth;
		set
		{
			_levelDepth = value;
			DataSeries.ForEach(x => x.Clear());
		}
	}

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

		_levelDepth.PropertyChanged += DepthFilterChanged;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar != 0)
			return;

		lock (_locker)
		{
			_mDepthAsk.Clear();
			_mDepthBid.Clear();
			var depths = MarketDepthInfo.GetMarketDepthSnapshot();

			foreach (var depth in depths)
			{
				if (depth.DataType is MarketDataType.Ask)
					_mDepthAsk[depth.Price] = depth.Volume;
				else
					_mDepthBid[depth.Price] = depth.Volume;
			}
		}
	}

	protected override void MarketDepthChanged(MarketDataArg depth)
	{
		if (_first)
		{
			_first = false;
			_lastCalculatedBar = CurrentBar - 1;
		}

		if (LevelDepth.Enabled)
		{
			lock (_locker)
			{
				if (depth.Volume is 0)
				{
					if (depth.DataType is MarketDataType.Ask)
						_mDepthAsk.Remove(depth.Price);
					else
						_mDepthBid.Remove(depth.Price);
				}
				else
				{
					if (depth.DataType is MarketDataType.Ask)
						_mDepthAsk[depth.Price] = depth.Volume;
					else
						_mDepthBid[depth.Price] = depth.Volume;
				}
			}
		}

		var lastCandle = CurrentBar - 1;

		var cumAsks = MarketDepthInfo.CumulativeDomAsks;
		var cumBids = MarketDepthInfo.CumulativeDomBids;

		if (LevelDepth.Enabled)
		{
			lock (_locker)
			{
				if (_mDepthAsk.Count <= LevelDepth.Value)
				{
					cumAsks = _mDepthAsk.Values
						.DefaultIfEmpty(0)
						.Sum();
				}
				else
				{
					cumAsks = 0;

					for (var i = 0; i <= LevelDepth.Value; i++)
						cumAsks += _mDepthAsk.Values[i];
				}

				if (_mDepthBid.Count <= LevelDepth.Value)
				{
					cumBids = _mDepthAsk.Values
						.DefaultIfEmpty(0)
						.Sum();
				}
				else
				{
					cumBids = 0;
					var lastIdx = _mDepthBid.Values.Count - 1;

					for (var i = 0; i <= LevelDepth.Value; i++)
						cumBids += _mDepthBid.Values[lastIdx - i];
				}
			}
		}

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

	#region Private methods

	private void DepthFilterChanged(object sender, PropertyChangedEventArgs e)
	{
		DataSeries.ForEach(x => x.Clear());
		RedrawChart();
	}

	#endregion
}