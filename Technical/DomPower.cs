namespace ATAS.Indicators.Technical;

using OFT.Attributes;
using OFT.Localization;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

[Category(IndicatorCategories.OrderBook)]
[DisplayName("DOM Power")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DomPowerDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602374")]
public class DomPower : Indicator
{

	#region Enums
	public enum VisualizationMode
	{
       SeparateLines,
       HistogramDom
	}
	#endregion

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

	// New extrema series based on DOM Imbalance (per-bar)
	private readonly ValueDataSeries _maxDomImbalance = new("MaxDomImbalance", "Max DOM Imbalance")
	{
       UseMinimizedModeIfEnabled = true,
       DescriptionKey = nameof(Strings.MaxDeltaSettingsDescription)
	};
 
	private readonly ValueDataSeries _minDomImbalance = new("MinDomImbalance", "Min DOM Imbalance")
	{
       UseMinimizedModeIfEnabled = true,
       DescriptionKey = nameof(Strings.MinDeltaSettingsDescription)
	};

	// DOM Imbalance = Cumulative DOM Bids - Cumulative DOM Asks (optionally limited by LevelDepth)
	private readonly ValueDataSeries _domImbalanceSeries = new("DomImbalance", "DOM Imbalance")
	{
		VisualType = VisualMode.Histogram,
		UseMinimizedModeIfEnabled = true
	};


	// Per-bar range of DOM Imbalance = max_intra_bar - min_intra_bar
	private readonly ValueDataSeries _domImbalanceRangeSeries = new("DomImbalanceRange", "DOM Imbalance Range")
	{
		Color = System.Drawing.Color.MediumVioletRed.Convert(),
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

	private SortedList<decimal, decimal> _mDepthAsk = new();
	private SortedList<decimal, decimal> _mDepthBid = new();

	private int _lastBar = -1;

	// State for new visualization and caches
	private VisualizationMode _visualMode = VisualizationMode.SeparateLines;
	private readonly Dictionary<int, decimal> _maxDomImbalanceCache = new();
	private readonly Dictionary<int, decimal> _minDomImbalanceCache = new();

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DepthMarketFilter), GroupName = nameof(Strings.Period), Description = nameof(Strings.DOMMaxFilterDescription), Order = 100)]
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

    [Display(Name = "Visualization Mode", GroupName = "View", Order = 110)]
	public VisualizationMode Mode
	{
       get => _visualMode;
       set
       {
           _visualMode = value;
           // Apply visibility change and fully reset series/state to avoid artifacts
           ApplyModeVisibility(_visualMode);
           ClearAllSeriesAndState();
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
        DataSeries.Add(_maxDomImbalance);
        DataSeries.Add(_minDomImbalance);
        DataSeries.Add(_domImbalanceSeries);
        DataSeries.Add(_domImbalanceRangeSeries);

        // Ensure correct default visibility on startup
        ApplyModeVisibility(_visualMode);
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

            _asks[bar] = _asks[bar - 1];
            _bids[bar] = _bids[bar - 1];
            _minDomImbalance[bar] = _minDomImbalance[bar - 1];
            _maxDomImbalance[bar] = _maxDomImbalance[bar - 1];
            _domImbalanceSeries[bar] = _domImbalanceSeries[bar - 1];
            _domImbalanceRangeSeries[bar] = _domImbalanceRangeSeries[bar - 1];

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

        var domImbalance = cumBids - cumAsks;
        var canCalc = cumAsks != 0 && cumBids != 0;

        if (!canCalc)
            return;

		for (var i = _lastCalculatedBar; i <= lastCandle; i++)
		{
            // update per-bar extrema caches
            if (!_maxDomImbalanceCache.ContainsKey(i))
            {
                _maxDomImbalanceCache[i] = domImbalance;
                _minDomImbalanceCache[i] = domImbalance;
            }
            else
            {
				if (domImbalance > _maxDomImbalanceCache[i])
					_maxDomImbalanceCache[i] = domImbalance;
                if (domImbalance < _minDomImbalanceCache[i])
					_minDomImbalanceCache[i] = domImbalance;
            }

			if (_visualMode == VisualizationMode.SeparateLines)
			{
				_asks[i] = -cumAsks;
				_bids[i] = cumBids;
				_maxDomImbalance[i] = _maxDomImbalanceCache[i];
				_minDomImbalance[i] = _minDomImbalanceCache[i];
			}
			else // HistogramDom
			{
				_domImbalanceSeries[i] = domImbalance;
				// color set in later commit to ensure RenderColor conversion
				var max = _maxDomImbalanceCache[i];
				var min = _minDomImbalanceCache[i];
				_domImbalanceRangeSeries[i] = max - min;
			}

            RaiseBarValueChanged(i);
		}

		_lastCalculatedBar = lastCandle;
	}

    #endregion

    #region Private methods

    /// <summary>
    /// Apply visibility for each series depending on the selected visualization mode.
    /// SeparateLines: show asks/bids + extrema; hide histogram/range.
    /// HistogramDom:  show histogram/range;  hide asks/bids + extrema.
    /// </summary>
    private void ApplyModeVisibility(VisualizationMode mode)
    {
        var showSeparate = mode == VisualizationMode.SeparateLines;
        var showHistogram = mode == VisualizationMode.HistogramDom;

        _asks.IsHidden = !showSeparate;
        _bids.IsHidden = !showSeparate;
        _maxDomImbalance.IsHidden = !showSeparate;
        _minDomImbalance.IsHidden = !showSeparate;

        _domImbalanceSeries.IsHidden = !showHistogram;
        _domImbalanceRangeSeries.IsHidden = !showHistogram;
    }

    /// <summary>
    /// Clears all series data and internal state caches to avoid artifacts on mode/filter changes.
    /// </summary>
    private void ClearAllSeriesAndState()
    {
        foreach (var ds in DataSeries)
        {
            ds.Clear();
        }
        
        _maxDomImbalanceCache.Clear();
        _minDomImbalanceCache.Clear();
        _lastCalculatedBar = System.Math.Max(0, CurrentBar - 1);
    }

	private void DepthFilterChanged(object sender, PropertyChangedEventArgs e)
	{
        ClearAllSeriesAndState();
        RedrawChart();
    }

    #endregion
}