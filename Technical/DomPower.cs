namespace ATAS.Indicators.Technical;

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[Category(IndicatorCategories.OrderBook)]
[DisplayName("DOM Power")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DomPowerDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602374")]
public class DomPower : Indicator
{
	#region Fields

	private readonly ValueDataSeries _asks = new("AsksId", "Asks")
	{
		UseMinimizedModeIfEnabled = true,
		DescriptionKey = nameof(Strings.AskVisualizationSettingsDescription)
	};

	private readonly ValueDataSeries _bids = new("BidsId", "Bids")
	{
		Color = System.Drawing.Color.Green.Convert(),
		UseMinimizedModeIfEnabled = true,
        DescriptionKey = nameof(Strings.BidVisualizationSettingsDescription)
    };

	private bool _first = true;
	private int _lastCalculatedBar;
	private Filter _levelDepth = new(true)
	{
		Value = 5,
		Enabled = false
	};
	private readonly object _locker = new();

	private ValueDataSeries _maxDelta = new("MaxDelta", "Max Delta")
	{
		Color = System.Drawing.Color.FromArgb(255, 27, 134, 198).Convert(),
		UseMinimizedModeIfEnabled = true,
        DescriptionKey = nameof(Strings.MaxDeltaSettingsDescription)
    };

	private SortedList<decimal, decimal> _mDepthAsk = new();
	private SortedList<decimal, decimal> _mDepthBid = new();

	private ValueDataSeries _minDelta = new("MinDelta", "Min Delta")
	{
		Color = System.Drawing.Color.FromArgb(255, 27, 134, 198).Convert(),
		UseMinimizedModeIfEnabled = true,
        DescriptionKey = nameof(Strings.MinDeltaSettingsDescription)
    };

	private int _lastBar = -1;
    private volatile bool _isLastDeltaCalc;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DepthMarketFilter), GroupName = nameof(Strings.Period), Description = nameof(Strings.DOMMaxFilterDescription), Order = 100)]
	[Range(1, 1000)]
	public Filter LevelDepth
	{
		get => _levelDepth;
		set
		{
			if (!ReferenceEquals(_levelDepth, value))
			{
				if (_levelDepth != null)
					_levelDepth.PropertyChanged -= DepthFilterChanged;

			_levelDepth = value;

				if (_levelDepth != null)
					_levelDepth.PropertyChanged += DepthFilterChanged;
			}

			DataSeries.ForEach(x => x.Clear());
            RedrawChart();
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
		if (bar is 0)
		{
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

		if (bar > 0 && bar != _lastBar) 
		{
			lock (_locker)
				_isLastDeltaCalc = false;

            _asks[bar] = _asks[bar - 1];
            _bids[bar] = _bids[bar - 1];
            _minDelta[bar] = _minDelta[bar - 1];
            _maxDelta[bar] = _maxDelta[bar - 1];
        }

		_lastBar = bar;
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
					cumAsks = MarketDepthInfo.CumulativeDomAsks;
				}
				else
				{
					cumAsks = 0;

					for (var i = 0; i < LevelDepth.Value; i++)
						cumAsks += _mDepthAsk.Values[i];
				}

				if (_mDepthBid.Count <= LevelDepth.Value)
				{
					cumBids = MarketDepthInfo.CumulativeDomBids;
                }
				else
				{
					cumBids = 0;
					var lastIdx = _mDepthBid.Values.Count - 1;

					for (var i = 0; i < LevelDepth.Value; i++)
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

			if (!_isLastDeltaCalc && i == lastCandle)
			{
                _maxDelta[i] = delta;
                _minDelta[i] = delta;

				lock (_locker)
					_isLastDeltaCalc = true;
            }

			if (delta > _maxDelta[i]) 
				_maxDelta[i] = delta;

			if (delta < _minDelta[i])
				_minDelta[i] = delta;			

            RaiseBarValueChanged(i);
		}

		_lastCalculatedBar = lastCandle;
	}

    protected override void OnDispose()
    {
        if (_levelDepth != null)
            _levelDepth.PropertyChanged -= DepthFilterChanged;
    }

    protected override void OnRecalculate()
    {
        _first = true;
        _lastCalculatedBar = 0;
        _lastBar = -1;

        lock (_locker)
        {
            _isLastDeltaCalc = false;
            _mDepthAsk.Clear();
            _mDepthBid.Clear();
        }
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