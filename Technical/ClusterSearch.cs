namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using OFT.Attributes;
using OFT.Localization;

using Utils.Common;
using Utils.Common.Collections;
using Utils.Common.Collections.Synchronized;

using static DynamicLevels;

[Category(IndicatorCategories.VolumeOrderFlow)]
[DisplayName("Cluster Search")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ClusterSearchDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602240")]
public partial class ClusterSearch : Indicator
{
	#region Fields

	private readonly PriceSelectionDataSeries _renderDataSeries = new("RenderDataSeries", "Price");
	private bool _autoFilter;
	private decimal _autoFilterValue;

	private HashSet<decimal> _alertPrices = [];

	private int _barsRange = 1;
	private CandleDirection _candleDirection = CandleDirection.Any;
	private CrossColor _clusterPriceColor;

	private Dictionary<(int Bar, decimal Price), PriceVolumeInfo> _clustersCache = new();

	private CrossColor _clusterTransColor;
	private int _days = 20;
	private decimal _deltaFilter;
	private decimal _deltaImbalance;
	private bool _fixedSizes;
	private bool _isFinishRecalculate;
	private int _lastBar = -1;
	private decimal _lastHigh;
	private decimal _lastLow;
	private decimal _lastPrice;

	private SyncList<PriceSelectionValue> _lastSeriesBar = [];
	private decimal _maxAverageTrade;
	private Filter _maxFilter = new() { Enabled = true, Value = 99999 };
	private decimal _maxPercent;
	private int _maxSize = 50;

	private MergedClusterDictionary _mergedLevels;
	private decimal _minAverageTrade;
	private Filter _minFilter = new() { Enabled = true, Value = 1000 };
	private decimal _minFilterValue;
	private decimal _minPercent;
	private int _minSize = 5;
	private bool _onlyOneSelectionPerBar;
	private Filter _pipsFromHigh = new() { Value = 100000000 };
	private Filter _pipsFromLow = new() { Value = 100000000 };
	private PriceLocation _priceLocation = PriceLocation.Any;
	private int _priceRange = 1;
	private bool _showPriceSelection = true;
	private int _size = 10;
	private int _targetBar;
	private TimeSpan _timeFrom = TimeSpan.Zero;
	private TimeSpan _timeTo = TimeSpan.Zero;
	private CalcMode _type = CalcMode.Volume;
	private bool _usePrevClose;
	private bool _useTimeFilter;

	private Dictionary<decimal, CustomVolumeInfo> _validVolumeLevels = new();
	private int _visualObjectsTransparency;
	private ObjectType _visualType = ObjectType.Rectangle;

	#endregion

	#region ctor

	public ClusterSearch()
		: base(true)
	{
		VisualObjectsTransparency = 70;
		PriceSelectionColor = ClusterColor = CrossColor.FromArgb(100, 255, 0, 255);

		VisualType = ObjectType.Rectangle;

		DenyToChangePanel = true;
		_renderDataSeries.IsHidden = true;
		DataSeries[0] = _renderDataSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnInitialize()
	{
		_maxFilter.PropertyChanged += MaxMinFilter_PropertyChanged;
		_minFilter.PropertyChanged += MaxMinFilter_PropertyChanged;
		PipsFromHigh.PropertyChanged += Filter_PropertyChanged;
		PipsFromLow.PropertyChanged += Filter_PropertyChanged;

		MinCandleHeight.PropertyChanged += Filter_PropertyChanged;
		MaxCandleHeight.PropertyChanged += Filter_PropertyChanged;
		MinCandleBodyHeight.PropertyChanged += Filter_PropertyChanged;
		MaxCandleBodyHeight.PropertyChanged += Filter_PropertyChanged;
	}

	protected override void OnNewTrade(MarketDataArg trade)
	{
		if (!_isFinishRecalculate || UsePrevClose)
			return;

		var curBar = CurrentBar - 1;

		var i = 0;

		while (i < curBar)
		{
			if (trade.Time < GetCandle(curBar - i).Time)
			{
				i++;
				continue;
			}

			break;
		}

		var targetBar = curBar - i;

		// Handle multiple bars created at once (e.g., Renko gap-filling)
		if (_lastBar != targetBar)
		{   
			// On certain chart types (Renko, Range XV, Range US, Range Z), when direction changes
			// Chart types that modify previous bar: Renko (Open/Close), Range XV (Close), Range US (Close/High/Low), Range Z (Close)
			if (_lastBar >= 2 && ChartInfo.ChartType is "Renko" or "RangeXV" or "RangeUS" or "RangeZ" && PriceLoc is not PriceLocation.Any)
			{
				_lastSeriesBar.Clear();
				CalculateBar(_lastBar);
			}
            
			// Process all skipped bars (for Renko charts that create multiple bars from single trade)
            var startBar = Math.Max(_lastBar + 1, _targetBar);

			for (var bar = startBar; bar <= targetBar; bar++)
			{
				OnNewBar(bar);
				CalculateBar(bar);
			}

			_lastBar = targetBar;
		}

		CalculateTick(targetBar, trade);
		_lastPrice = trade.Price;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar is 0 && UsePrevClose)
			return;

		if (!UsePrevClose)
		{
			if (_isFinishRecalculate)
				return;
		}
		else
			bar--;

		var newBar = _lastBar != bar;
		_lastBar = bar;

		if (bar < _targetBar || !newBar)
			return;

		if (bar is 0)
			_renderDataSeries[bar] = _lastSeriesBar;
		else
			OnNewBar(bar);

		CalculateBar(bar);
	}

	protected override void OnRecalculate()
	{
		if (InstrumentInfo is null)
			return;

		_lastBar = -1;
        _isFinishRecalculate = false;
		_mergedLevels = new MergedClusterDictionary(PriceRange, InstrumentInfo.TickSize);
		
		_autoFilterValue = 0;
		_targetBar = 0;

		if (Days is 0)
			return;

		var days = 0;

		for (var i = CurrentBar - 1; i >= 0; i--)
		{
			_targetBar = i;

			if (!IsNewSession(i))
				continue;

			days++;

			if (days == Days)
				break;
		}

		_lastSeriesBar.Clear();
		_renderDataSeries.Clear();

		if (!AutoFilter)
			_minFilterValue = MinimalFilter();
    }

	//Apply autofilter
	protected override void OnFinishRecalculate()
	{
		try
		{
			if (!AutoFilter)
				return;

			var valuesList = new List<PriceSelectionValue>();

			for (var i = 0; i < _renderDataSeries.Count; i++)
			{
				if (_renderDataSeries[i].Count is 0)
					continue;

				valuesList.AddRange(_renderDataSeries[i]);
			}

			if (valuesList.Count is 0)
				return;

			valuesList = valuesList.OrderByDescending(x => (decimal)x.Context).ToList();

			_autoFilterValue = valuesList.Count <= 10
				? (decimal)valuesList.Last().Context
				: (decimal)valuesList.Skip(10).First().Context;

			//Set autofilter value to see it in minimal filter value
			MinimumFilter.Value = _autoFilterValue;
			_minFilterValue     = MinimalFilter();

			for (var i = 0; i < _renderDataSeries.Count; i++)
			{
				if (_renderDataSeries[i].Count is 0)
					continue;

				_renderDataSeries[i].RemoveAll(x => (decimal)x.Context < _autoFilterValue);

				_renderDataSeries[i].ForEach(l =>
				{
					var clusterSize = FixedSizes ? _size : (int)((decimal)l.Context * _size / _minFilterValue);

					if (!FixedSizes)
					{
						clusterSize = Math.Min(clusterSize, MaxSize);
						clusterSize = Math.Max(clusterSize, MinSize);
					}

					l.Size = clusterSize;
				});
			}
			
			_validVolumeLevels.RemoveWhere(level =>
				CalcType switch
				{
					CalcMode.Bid => level.Value.Bid,
					CalcMode.Ask => level.Value.Ask,
					CalcMode.Delta => level.Value.Delta,
					CalcMode.Volume or CalcMode.MaxVolume => level.Value.Volume,
					CalcMode.Tick => level.Value.Ticks,
					_ => 0
				} < _autoFilterValue);
		}
		finally
		{
			OnChangeProperty(nameof(MinimumFilter));

			_isFinishRecalculate = true;
		}
	}

	#endregion

	#region Private methods

	private void CalculateTick(int bar, MarketDataArg trade)
	{
		var priceLevel = _clustersCache.GetOrAdd((bar, trade.Price), () => new CustomVolumeInfo(trade.Price));

		switch (trade.Direction)
		{
			case TradeDirection.Buy:
				priceLevel.Ask += trade.Volume;
				break;
			case TradeDirection.Sell:
				priceLevel.Bid += trade.Volume;
				break;
			case TradeDirection.Between:
			default:
				priceLevel.Between += trade.Volume;
				break;
		}

		priceLevel.Volume += trade.Volume;
		priceLevel.Ticks++;
		UpdateLevelCache(bar, trade);

		var candle = GetCandle(bar);

		var startPrice = Math.Min(_lastPrice, trade.Price);
		startPrice = Math.Max(candle.Low, startPrice - (PriceRange - 1) * InstrumentInfo.TickSize);

		var endPrice = Math.Max(_lastPrice, trade.Price);
		endPrice = Math.Min(candle.High - (PriceRange - 1) * InstrumentInfo.TickSize, endPrice);

		for (var price = startPrice; price <= endPrice; price += InstrumentInfo.TickSize)
		{
			if (CheckCluster(bar, price))
				continue;

			_validVolumeLevels.Remove(price);
			RemoveOldSelection(bar, trade.Price);
		}


		if (!CheckBarFormation(candle))
		{
			_renderDataSeries[bar] = new SyncList<PriceSelectionValue>();
			return;
		}

		_renderDataSeries[bar] = _lastSeriesBar;

		var ranges = GetPriceRanges(bar, endPrice);

		var changedDirection = false;

		if (_lastPrice != trade.Price)
		{
			changedDirection = _lastPrice >= candle.Open && trade.Price < candle.Open
				||
				_lastPrice <= candle.Open && trade.Price > candle.Open
				||
				_lastPrice == candle.Open;
		}

		// Detect if candle structure changed (high/low extended)
		var candleStructureChanged = candle.High != _lastHigh || candle.Low != _lastLow;

		// For wick location filters, remove markers and valid levels that are no longer in valid wick areas
		// (e.g., when body extends into previously marked wick area)
		if (PriceLoc is PriceLocation.UpperWick or PriceLocation.LowerWick or PriceLocation.AtUpperLowerWick)
		{
			var maxBody = Math.Max(candle.Close, candle.Open);
			var minBody = Math.Min(candle.Close, candle.Open);

			// Calculate the range for merged clusters that overlap with body
			// A cluster at price P represents levels [P, P + (PriceRange-1)*tick]
			// This overlaps with body [minBody, maxBody] if P + (PriceRange-1)*tick >= minBody AND P <= maxBody
			var mergeExtension = (PriceRange - 1) * InstrumentInfo.TickSize;
			var cleanFrom = minBody - mergeExtension;
			var cleanTo = maxBody;

			// Remove displayed markers whose merged range overlaps with body
			for (var i = _lastSeriesBar.Count - 1; i >= 0; i--)
			{
				var selection = _lastSeriesBar[i];
				var price = selection.MinimumPrice;

				// Remove if marker's merged range overlaps with body
				if (price >= cleanFrom && price <= cleanTo)
				{
					_lastSeriesBar.RemoveAt(i);
				}
			}

			// Also clean _validVolumeLevels to prevent body clusters from being checked
			_validVolumeLevels.RemoveWhere(kvp => kvp.Key >= cleanFrom && kvp.Key <= cleanTo);
		}

		foreach (var range in ranges)
		{
			// Determine if this range needs to be checked
			bool shouldCheckRange;

			if (PriceLoc is PriceLocation.Any)
			{
				// Original optimization: only check if trade is in range or direction changed
				shouldCheckRange = (trade.Price >= range.From && trade.Price <= range.To) || changedDirection;
			}
			else
			{
				// For location filters (wicks, extremes):
				// Check if candle structure changed OR if affected price range overlaps with this range
				shouldCheckRange = candleStructureChanged
					|| changedDirection
					|| (startPrice <= range.To && endPrice >= range.From);  // Ranges overlap
			}

			if (!shouldCheckRange)
				continue;

			RemoveOldSelection(bar, trade.Price);
			CheckPriceRange(bar, range.From, range.To);

			// Safety check: remove any markers whose merged range overlaps with body (immediately after CheckPriceRange)
			if (PriceLoc is PriceLocation.UpperWick or PriceLocation.LowerWick or PriceLocation.AtUpperLowerWick)
			{
				var maxBody = Math.Max(candle.Close, candle.Open);
				var minBody = Math.Min(candle.Close, candle.Open);

				var mergeExtension = (PriceRange - 1) * InstrumentInfo.TickSize;
				var cleanFrom = minBody - mergeExtension;
				var cleanTo = maxBody;

				for (var i = _lastSeriesBar.Count - 1; i >= 0; i--)
				{
					var selection = _lastSeriesBar[i];
					var price = selection.MinimumPrice;

					if (price >= cleanFrom && price <= cleanTo)
					{
						_lastSeriesBar.RemoveAt(i);
					}
				}
			}

			// Only break for PriceLocation.Any to preserve original performance
			if (PriceLoc is PriceLocation.Any)
				break;
		}

		// Update tracked high/low for next tick
		_lastHigh = candle.High;
		_lastLow = candle.Low;

		if (_lastPrice == trade.Price)
			return;

		if (PriceLoc is not PriceLocation.Any)
			UpdatePriceLocationValues(bar, trade);

		if (PipsFromHigh.Enabled)
		{
			var lowValue = candle.High - InstrumentInfo.TickSize * PipsFromHigh.Value;

			if (lowValue > candle.Low)
			{
				for (var i = _lastSeriesBar.Count - 1; i >= 0; i--)
				{
					var item = _lastSeriesBar[i];

					if (item.MinimumPrice >= lowValue)
						break;

					_lastSeriesBar.RemoveAt(i);
				}
			}
		}

		if (PipsFromLow.Enabled)
		{
			var highValue = candle.Low + InstrumentInfo.TickSize * PipsFromLow.Value;

			if (highValue < candle.High)
			{
				for (var i = _lastSeriesBar.Count - 1; i >= 0; i--)
				{
					var item = _lastSeriesBar[i];

					if (item.MinimumPrice <= highValue)
						break;

					_lastSeriesBar.RemoveAt(i);
				}
			}
		}
	}

	private void OnNewBar(int bar)
	{
		_mergedLevels.Clear();
		_validVolumeLevels.Clear();

		if (CheckBarFormation(GetCandle(bar - 1)))
		{
			var lastBar = _lastSeriesBar.Select(p => p.MemberwiseClone()).ToArray();
			_renderDataSeries[bar - 1] = new SyncList<PriceSelectionValue>(lastBar);
		}
		else
			_renderDataSeries[bar - 1] = [];

		_lastSeriesBar.Clear();
		_renderDataSeries[bar] = _lastSeriesBar;

		var candle = GetCandle(bar);
		_lastPrice = candle.Close;
		_lastHigh = candle.High;
		_lastLow = candle.Low;
		_alertPrices.Clear();
	}

	private void MaxMinFilter_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		Filter_PropertyChanged(sender, e);
	}

	//Calculate all clusters on current bar
	private void CalculateBar(int bar)
	{
		UpdateCumulativeCachePerBar(bar);

		var candle = GetCandle(bar);

		var endPrice = Math.Max(candle.Low, candle.High - (PriceRange - 1) * InstrumentInfo.TickSize);

		for (var price = candle.Low; price <= endPrice; price += InstrumentInfo.TickSize)
		{
			if (!CheckCluster(bar, price))
				_validVolumeLevels.Remove(price);
		}

		if (_validVolumeLevels.Count is 0)
			return;

		if (!CheckBarFormation(candle))
			return;

		var ranges = GetPriceRanges(bar, endPrice);

		// For wick location filters, clean body prices from valid levels cache
		// Also remove prices whose merged range overlaps with body
		if (PriceLoc is PriceLocation.UpperWick or PriceLocation.LowerWick or PriceLocation.AtUpperLowerWick)
		{
			var maxBody = Math.Max(candle.Close, candle.Open);
			var minBody = Math.Min(candle.Close, candle.Open);

			// A cluster at price P represents levels [P, P + (PriceRange-1)*tick]
			// Remove prices where this merged range overlaps with body [minBody, maxBody]
			var mergeExtension = (PriceRange - 1) * InstrumentInfo.TickSize;
			var cleanFrom = minBody - mergeExtension;
			var cleanTo = maxBody;

			_validVolumeLevels.RemoveWhere(kvp => kvp.Key >= cleanFrom && kvp.Key <= cleanTo);
		}

		foreach (var range in ranges)
			CheckPriceRange(bar, range.From, range.To);
	}

	/// <summary>
	///     Get price ranges on bar that are passed candle filters
	/// </summary>
	/// <param name="bar">Bar number</param>
	/// <param name="endPrice">High price minus price range value</param>
	/// <returns></returns>
	private List<(decimal From, decimal To)> GetPriceRanges(int bar, decimal endPrice)
	{
		var ranges = new List<(decimal From, decimal To)>();
		var candle = GetCandle(bar);

		var maxPrice = PipsFromLow.Enabled
			? candle.Low + PipsFromLow.Value * InstrumentInfo.TickSize
			: candle.High;

		var minPrice = PipsFromHigh.Enabled
			? candle.High - PipsFromHigh.Value * InstrumentInfo.TickSize
			: candle.Low;

		if (minPrice > maxPrice)
			return ranges;

		maxPrice = Math.Min(candle.High, maxPrice);
		minPrice = Math.Max(candle.Low, minPrice);

		switch (PriceLoc)
		{
			case PriceLocation.AtHigh when maxPrice != candle.High:
			case PriceLocation.AtLow when minPrice != candle.Low:
			case PriceLocation.AtHighOrLow when maxPrice != candle.High && minPrice != candle.Low:
				return ranges;

			case PriceLocation.Any:
				return [(minPrice, maxPrice)];

			case PriceLocation.AtHighOrLow:
			case PriceLocation.AtHigh:
			case PriceLocation.AtLow:
			{
				if (PriceLoc is PriceLocation.AtHighOrLow or PriceLocation.AtHigh)
				{
					if (maxPrice >= endPrice)
						ranges.Add((endPrice, endPrice));
				}

				if (PriceLoc is PriceLocation.AtHighOrLow or PriceLocation.AtLow)
				{
					if (minPrice <= candle.Low)
						ranges.Add((candle.Low, candle.Low));
				}

				return ranges;
			}
			case PriceLocation.AtUpperLowerWick or PriceLocation.UpperWick or PriceLocation.LowerWick or PriceLocation.Body:
			{
				var maxBody = Math.Max(candle.Close, candle.Open);
				var minBody = Math.Min(candle.Close, candle.Open);

				if (PriceLoc is PriceLocation.Body)
				{
					maxBody = Math.Min(maxBody, maxPrice);
					minBody = Math.Max(minBody, minPrice);
					return [(minBody, maxBody)];
				}

				if (PriceLoc is PriceLocation.UpperWick or PriceLocation.AtUpperLowerWick)
				{
					var upperWickFrom = maxBody + InstrumentInfo.TickSize;
					// Ensure the last checked price's merge range doesn't exceed maxPrice
					var upperWickTo = maxPrice - (PriceRange - 1) * InstrumentInfo.TickSize;

					// Only add range if valid (From <= To)
					if (upperWickTo >= upperWickFrom)
						ranges.Add((upperWickFrom, upperWickTo));
				}

				if (PriceLoc is PriceLocation.LowerWick or PriceLocation.AtUpperLowerWick)
				{
					var lowerWickFrom = minPrice;
					// Ensure the last checked price's merge range doesn't reach body
					var lowerWickTo = minBody - PriceRange * InstrumentInfo.TickSize;

					// Only add range if valid (From <= To)
					if (lowerWickTo >= lowerWickFrom)
						ranges.Add((lowerWickFrom, lowerWickTo));
				}

				return ranges;
			}
		}

		return ranges;
	}

	//Check valid clusters on filtered price range and draw it
	private void CheckPriceRange(int bar, decimal from, decimal to)
	{
		for (var price = from; price <= to; price += InstrumentInfo.TickSize)
			CheckPriceRange(bar, price);
	}

	private void CheckPriceRange(int bar, decimal price)
	{
		if (_validVolumeLevels.TryGetValue(price, out var info))
			PlaceToDataSeries(bar, info);
		else
			RemoveOldSelection(bar, price);
	}

	//Compare current candle with current candles filters
	private bool CheckBarFormation(IndicatorCandle candle)
	{
		if ((CandleDir is CandleDirection.Bearish && candle.Close >= candle.Open)
		    ||
		    (CandleDir is CandleDirection.Bullish && candle.Close <= candle.Open)
		    ||
		    (CandleDir is CandleDirection.Neutral && candle.Close != candle.Open))
			return false;

		if (UseTimeFilter)
		{
			var time = candle.Time.AddHours(InstrumentInfo.TimeZone);

			if (TimeFrom < TimeTo)
			{
				if (time < time.Date + TimeFrom)
					return false;

				if (time > time.Date + TimeTo)
					return false;
			}
			else
			{
				if (time < time.Date + TimeFrom && time > time.Date + TimeTo)
					return false;
			}
		}

		if (MinCandleHeight.Enabled || MaxCandleHeight.Enabled)
		{
			var height = (candle.High - candle.Low) / InstrumentInfo.TickSize + 1;

			if (MinCandleHeight.Enabled && height < MinCandleHeight.Value)
				return false;

			if (MaxCandleHeight.Enabled && height > MaxCandleHeight.Value)
				return false;
		}

		if (MinCandleBodyHeight.Enabled || MaxCandleBodyHeight.Enabled)
		{
			var bHeight = Math.Abs(candle.Close - candle.Open) / InstrumentInfo.TickSize + 1;

			if (MinCandleBodyHeight.Enabled && bHeight < MinCandleBodyHeight.Value)
				return false;

			if (MaxCandleBodyHeight.Enabled && bHeight > MaxCandleBodyHeight.Value)
				return false;
		}

		return true;
	}

	//Merge wide clusters if price levels is more then 1
	//Compare clusters values with volume filters
	private bool CheckCluster(int bar, decimal price)
	{
		var endPrice = price + (PriceRange - 1) * InstrumentInfo.TickSize;

		var ask = 0m;
		var bid = 0m;
		var between = 0m;
		var volume = 0m;
		var ticks = 0;

		for (var iPrice = price; iPrice <= endPrice; iPrice += InstrumentInfo.TickSize)
		{
			if (!_mergedLevels.TryGetValue(iPrice, out var level))
				continue;

			ask += level.Ask;
			bid += level.Bid;
			between += level.Between;
			volume += level.Volume;
			ticks += level.Ticks;
		}

		if (CalcType is CalcMode.MaxVolume && price != _mergedLevels.PocPrice)
			return false;

		var delta = ask - bid;
		var avgTrade = ticks is 0 ? 0 : volume / ticks;
		var value = CalcType switch
		{
			CalcMode.Bid => bid,
			CalcMode.Ask => ask,
			CalcMode.Delta => delta,
			CalcMode.Volume or CalcMode.MaxVolume => volume,
			CalcMode.Tick => ticks,
			_ => 0
		};

		if (AutoFilter)
		{
			if (_autoFilterValue is 0)
				return SaveLevel();

			if (value < _autoFilterValue)
				return false;
		}

		if (MinimumFilter.Enabled && value < MinimumFilter.Value)
			return false;

		if (MaximumFilter.Enabled && value > MaximumFilter.Value)
			return false;

		if (MinAverageTrade != 0 && avgTrade < MinAverageTrade)
			return false;

		if (MaxAverageTrade != 0 && avgTrade > MinAverageTrade)
			return false;

		if (MinPercent != 0 || MaxPercent != 0)
		{
			var curPerc = 100 * volume / _mergedLevels.TotalVolume;

			if (curPerc < MinPercent || MaxPercent is not 0 && curPerc > MaxPercent)
				return false;
		}

		if (DeltaImbalance != 0)
		{
			var vol = volume;
			var askImbalance = vol is not 0
				? ask * 100.0m / vol
				: 0;

			var bidImbalance = vol is not 0
				? bid * 100.0m / vol
				: 0;

			switch (DeltaImbalance)
			{
				case > 0 when askImbalance < DeltaImbalance:
				case < 0 when bidImbalance < Math.Abs(DeltaImbalance):
					return false;
			}
		}

		if (DeltaFilter != 0)
		{
			switch (DeltaFilter)
			{
				case > 0 when delta < DeltaFilter:
				case < 0 when delta > DeltaFilter:
					return false;
			}
		}

		if (CalcType is CalcMode.MaxVolume)
		{
			for (var i = 0; i < _lastSeriesBar.Count; i++)
			{
				var level = _lastSeriesBar[i];
				
				if(level.MinimumPrice == price)
					continue;

				_validVolumeLevels.Remove(level.MinimumPrice);
				_lastSeriesBar.RemoveAt(i);
				i--;
            }
		}

		return SaveLevel();

		bool SaveLevel()
		{
			if (!_validVolumeLevels.TryGetValue(price, out var info))
				_validVolumeLevels.Add(price, info = new CustomVolumeInfo(price));

			info.Ask = ask;
			info.Bid = bid;
			info.Between = between;
			info.Volume = volume;
			info.Ticks = ticks;

			return true;
		}
	}

	//Create horizontal merged clusters on all current bar prices
	private void UpdateCumulativeCachePerBar(int bar)
	{
		var candle = GetCandle(bar);
		
		for (var iPrice = candle.Low; iPrice <= candle.High; iPrice += InstrumentInfo.TickSize)
			CreateLevelCache(bar, iPrice);
	}

	//Create horizontal merged clusters
	private void CreateLevelCache(int bar, decimal price)
	{
		var level = new CustomVolumeInfo(price);
		var endBar = Math.Max(0, bar - (BarsRange - 1));

		for (var i = bar; i >= endBar; i--)
		{
			var iCandle = GetCandle(i);
			var cluster = _clustersCache.GetOrAdd((i, price), () => iCandle.GetPriceVolumeInfo(price), true);

			if (cluster is null)
				continue;

			level.Ask += cluster.Ask;
			level.Between += cluster.Between;
			level.Bid += cluster.Bid;
			level.Ticks += cluster.Ticks;
			level.Volume += cluster.Volume;
		}

		_mergedLevels[price] = level;
	}

	//Increment trade data to existing cluster
	private void UpdateLevelCache(int bar, MarketDataArg trade)
	{
		if (!_mergedLevels.TryGetValue(trade.Price, out var level))
		{
			level = new CustomVolumeInfo(trade.Price);
			var startBar = Math.Max(0, bar - 1);
			var endBar = Math.Max(0, bar - (BarsRange - 1));

			for (var i = startBar; i >= endBar; i--)
			{
				var iCandle = GetCandle(i);
				var cluster = _clustersCache.GetOrAdd((i, trade.Price), () => iCandle.GetPriceVolumeInfo(trade.Price), true);

				if (cluster is null)
					continue;

				level.Ask += cluster.Ask;
				level.Between += cluster.Between;
				level.Bid += cluster.Bid;
				level.Ticks += cluster.Ticks;
				level.Volume += cluster.Volume;
			}

			_mergedLevels[trade.Price] = level;
		}

		_mergedLevels.RemoveVolume(level);

		switch (trade.Direction)
		{
			case TradeDirection.Buy:
				level.Ask += trade.Volume;
				break;
			case TradeDirection.Sell:
				level.Bid += trade.Volume;
				break;
			case TradeDirection.Between:
			default:
				level.Between += trade.Volume;
				break;
		}
		
		level.Volume += trade.Volume;
		level.Ticks++;

        _mergedLevels[trade.Price] = level;
    }

	//Update data series values size on properties change
	private void SetSize()
	{
		if (_fixedSizes)
		{
			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => x.Size = _size);
		}
		else
		{
			var filterValue = MinimalFilter();

			for (var i = 0; i < _renderDataSeries.Count; i++)
			{
				_renderDataSeries[i].ForEach(x =>
				{
					x.Size = (int)((decimal)x.Context * _size / Math.Max(filterValue, 1));

					if (x.Size > MaxSize)
						x.Size = MaxSize;

					if (x.Size < MinSize)
						x.Size = MinSize;
				});
			}
		}
	}

	private void Filter_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		RecalculateValues();
		RedrawChart();
	}

	private void AddClusterAlert(string msg)
	{
		if (!UseAlerts)
			return;

		AddAlert(AlertFile, InstrumentInfo.Instrument, msg, AlertColor, ClusterColor);
	}

	private decimal MinimalFilter()
	{
		if (AutoFilter)
			return Math.Max(_autoFilterValue, 1);

        var minFilter = MinimumFilter.Enabled ? MinimumFilter.Value : 0;
		var maxFilter = MaximumFilter.Enabled ? MaximumFilter.Value : 0;

		if (MinimumFilter.Value >= 0 && MaximumFilter.Value >= 0)
			return minFilter;

		if (MinimumFilter.Value < 0 && MaximumFilter.Value >= 0)
			return Math.Min(Math.Abs(minFilter), maxFilter);

		return Math.Abs(maxFilter);
	}

	#endregion

	#region Filters

	[Browsable(false)]
	[Obsolete]
	public MiddleClusterType Type
	{
		get => CalcType switch
		{
			CalcMode.Bid => MiddleClusterType.Bid,
			CalcMode.Ask => MiddleClusterType.Ask,
			CalcMode.Delta => MiddleClusterType.Delta,
			CalcMode.Volume => MiddleClusterType.Volume,
			CalcMode.Tick => MiddleClusterType.Tick,
			_ => MiddleClusterType.Volume
		};
		set => CalcType = value switch
		{
			MiddleClusterType.Bid => CalcMode.Bid,
			MiddleClusterType.Ask => CalcMode.Ask,
			MiddleClusterType.Delta => CalcMode.Delta,
			MiddleClusterType.Volume or MiddleClusterType.Time => CalcMode.Volume,
			MiddleClusterType.Tick => CalcMode.Tick,
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
		};
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.CalculationMode),
		Description = nameof(Strings.CalculationModeDescription), Order = 200)]
	public CalcMode CalcType
	{
		get => _type;
		set
		{
			_type = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.AutoFilter),
		Description = nameof(Strings.ClusterSearchAutofilterDescription), Order = 215)]
	public bool AutoFilter
	{
		get => _autoFilter;
		set
		{
			_autoFilter = value;

			if (value)
			{
				MinimumFilter.Enabled = true;
				MaximumFilter.Enabled = false;
			}

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MinimumFilterDescription),
		Name = nameof(Strings.MinValue), Order = 220)]
	public Filter MinimumFilter
	{
		get => _minFilter;
		set => SetTrackedProperty(ref _minFilter, value, _ => RecalculateValues());
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MaximumFilterDescription),
		Name = nameof(Strings.MaxValue), Order = 230)]
	public Filter MaximumFilter
	{
		get => _maxFilter;
		set => SetTrackedProperty(ref _maxFilter, value, _ => RecalculateValues());
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.MinimumAverageTrade), Order = 470,
		Description = nameof(Strings.MinAvgTradeDescription))]
	[Range(0, 10000000)]
	public decimal MinAverageTrade
	{
		get => _minAverageTrade;
		set
		{
			_minAverageTrade = value;
			OnChangeProperty();

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.MaximumAverageTrade), Order = 480,
		Description = nameof(Strings.MaxAvgTradeDescription))]
	[Range(0, 10000000)]
	public decimal MaxAverageTrade
	{
		get => _maxAverageTrade;
		set
		{
			_maxAverageTrade = value;
			OnChangeProperty();

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.MinVolumePercent), Order = 490,
		Description = nameof(Strings.MinPercentDescription))]
	[Range(0, 100)]
	public decimal MinPercent
	{
		get => _minPercent;
		set
		{
			_minPercent = value;
			OnChangeProperty();

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Filters), Name = nameof(Strings.MaxVolumePercent), Order = 492,
		Description = nameof(Strings.MaxPercentDescription))]
	[Range(0, 100)]
	public decimal MaxPercent
	{
		get => _maxPercent;
		set
		{
			_maxPercent = value;
			OnChangeProperty();

			RecalculateValues();
		}
	}

	#endregion

	#region DeltaFilters

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.DeltaFilters), Name = nameof(Strings.DeltaImbalance), Order = 300,
		Description = nameof(Strings.DeltaImbalanceDescription))]
	[Range(-100, 100)]
	public decimal DeltaImbalance
	{
		get => _deltaImbalance;
		set
		{
			_deltaImbalance = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.DeltaFilters), Name = nameof(Strings.DeltaFilter), Order = 310,
		Description = nameof(Strings.DeltaFilterDescription))]
	public decimal DeltaFilter
	{
		get => _deltaFilter;
		set
		{
			_deltaFilter = value;
			RecalculateValues();
		}
	}

	#endregion

	#region Location filters

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.CandleDirection),
		Description = nameof(Strings.CandleDirectionDescription), Order = 400)]
	public CandleDirection CandleDir
	{
		get => _candleDirection;
		set
		{
			_candleDirection = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.BarsRange), Order = 410,
		Description = nameof(Strings.BarsRangeDescription))]
	[Range(1, 10000)]
	public int BarsRange
	{
		get => _barsRange;
		set
		{
			_barsRange = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.PriceRange), Order = 420,
		Description = nameof(Strings.PriceRangeDescription))]
	[Range(1, 100000)]
	public int PriceRange
	{
		get => _priceRange;
		set
		{
			_priceRange = value;
			RecalculateValues();
		}
	}

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.PipsFromHigh), Order = 430,
		Description = nameof(Strings.PipsFromHighDescription))]
	public Filter PipsFromHigh
	{
		get => _pipsFromHigh;
		set
		{
			_pipsFromHigh = value;
			RecalculateValues();
		}
	}

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.PipsFromLow), Order = 440,
		Description = nameof(Strings.PipsFromLowDescription))]
	public Filter PipsFromLow
	{
		get => _pipsFromLow;
		set
		{
			_pipsFromLow = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.LocationFilters), Name = nameof(Strings.PriceLocation), Order = 450,
		Description = nameof(Strings.PriceLocationDescription))]
	public PriceLocation PriceLoc
	{
		get => _priceLocation;
		set
		{
			_priceLocation = value;
			RecalculateValues();
		}
	}

	#endregion

	#region Candle size filters

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumCandleHeight), GroupName = nameof(Strings.CandleHeight), Order = 460,
		Description = nameof(Strings.MinCandleHeightDescription))]
	public FilterInt MinCandleHeight { get; set; } = new()
		{ Value = 1, Enabled = false };

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumCandleHeight), GroupName = nameof(Strings.CandleHeight), Order = 461,
		Description = nameof(Strings.MaxCandleHeightDescription))]
	public FilterInt MaxCandleHeight { get; set; } = new()
		{ Value = 1, Enabled = false };

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumCandleBodyHeight), GroupName = nameof(Strings.CandleHeight), Order = 470,
		Description = nameof(Strings.MinCandleBodyHeightDescription))]
	public FilterInt MinCandleBodyHeight { get; set; } = new()
		{ Value = 1, Enabled = false };

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumCandleBodyHeight), GroupName = nameof(Strings.CandleHeight), Order = 471,
		Description = nameof(Strings.MaxCandleBodyHeightDescription))]
	public FilterInt MaxCandleBodyHeight { get; set; } = new()
		{ Value = 1, Enabled = false };

	#endregion

	#region Time filtration

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFiltration), Name = nameof(Strings.UseTimeFilter), Order = 500,
		Description = nameof(Strings.UseTimeFilterDescription))]
	public bool UseTimeFilter
	{
		get => _useTimeFilter;
		set
		{
			_useTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFiltration), Name = nameof(Strings.TimeFrom), Order = 510,
		Description = nameof(Strings.TimeFromDescription))]
	public TimeSpan TimeFrom
	{
		get => _timeFrom;
		set
		{
			_timeFrom = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeFiltration), Name = nameof(Strings.TimeTo), Order = 520,
		Description = nameof(Strings.TimeToDescription))]
	public TimeSpan TimeTo
	{
		get => _timeTo;
		set
		{
			_timeTo = value;
			RecalculateValues();
		}
	}

	#endregion

	#region Visualization

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.OnlyOneSelectionPerBar), Order = 590,
		Description = nameof(Strings.OneSelectionPerBarDescription))]
	public bool OnlyOneSelectionPerBar
	{
		get => _onlyOneSelectionPerBar;
		set
		{
			_onlyOneSelectionPerBar = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.VisualMode), Order = 600,
		Description = nameof(Strings.VisualModeDescription))]
	public ObjectType VisualType
	{
		get => _visualType;
		set
		{
			_visualType = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.VisualObject = value; });
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.ObjectsColor), Order = 605,
		Description = nameof(Strings.VisualObjectsDescription))]
	public CrossColor ClusterColor
	{
		get => _clusterTransColor;
		set
		{
			_clusterTransColor = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.ObjectColor = _clusterTransColor; });
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.VisualObjectsTransparency), Order = 610,
		Description = nameof(Strings.VisualObjectsTransparencyDescription))]
	[Range(0, 100)]
	public int VisualObjectsTransparency
	{
		get => _visualObjectsTransparency;
		set
		{
			_visualObjectsTransparency = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
			{
				_renderDataSeries[i].ForEach(x =>
				{
					x.ObjectColor = _clusterTransColor;
					x.ObjectsTransparency = _visualObjectsTransparency;
				});
			}
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.ShowPriceSelection), Order = 615,
		Description = nameof(Strings.ShowPriceSelectionDescription))]
	public bool ShowPriceSelection
	{
		get => _showPriceSelection;
		set
		{
			_showPriceSelection = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.PriceSelectionColor = value ? _clusterPriceColor : CrossColors.Transparent; });
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.PriceSelectionColor), Order = 620,
		Description = nameof(Strings.PriceSelectionColorDescription))]
	public CrossColor PriceSelectionColor
	{
		get => _clusterPriceColor;
		set
		{
			_clusterPriceColor = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => x.PriceSelectionColor = ShowPriceSelection ? _clusterPriceColor : CrossColors.Transparent);
		}
	}

	[Browsable(false)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.ClusterSelectionTransparency), Order = 625,
		Description = nameof(Strings.PriceSelectionTransparencyDescription))]
	[Range(0, 100)]
	public int Transparency { get; set; }

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.FixedSizes), Order = 640,
		Description = nameof(Strings.FixedSizesDescription))]
	public bool FixedSizes
	{
		get => _fixedSizes;
		set
		{
			_fixedSizes = value;
			SetSize();
		}
	}

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.Size), Order = 650,
		Description = nameof(Strings.SizeDescription))]
	public int Size
	{
		get => _size;
		set
		{
			_size = value;
			SetSize();
		}
	}

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.MinimumSize), Order = 660,
		Description = nameof(Strings.MinimumSizeDescription))]
	public int MinSize
	{
		get => _minSize;
		set
		{
			_minSize = value;

			if (!_fixedSizes)
			{
				var filterValue = MinimalFilter();

				for (var i = 0; i < _renderDataSeries.Count; i++)
				{
					_renderDataSeries[i].ForEach(x =>
					{
						x.Size = (int)((decimal)x.Context * _size / Math.Max(filterValue, 1));

						if (x.Size > MaxSize)
							x.Size = MaxSize;

						if (x.Size < value)
							x.Size = value;
					});
				}
			}
		}
	}

	[Range(1, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visualization), Name = nameof(Strings.MaximumSize), Order = 670,
		Description = nameof(Strings.MaximumSizeDescription))]
	public int MaxSize
	{
		get => _maxSize;
		set
		{
			_maxSize = value;

			if (!_fixedSizes)
			{
				var filterValue = MinimalFilter();

				for (var i = 0; i < _renderDataSeries.Count; i++)
				{
					_renderDataSeries[i].ForEach(x =>
					{
						x.Size = (int)((decimal)x.Context * _size / Math.Max(filterValue, 1));

						if (x.Size > value)
							x.Size = value;

						if (x.Size < MinSize)
							x.Size = MinSize;
					});
				}
			}
		}
	}

	#endregion

	#region Alerts

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.UseAlerts), Order = 700,
		Description = nameof(Strings.UseAlertDescription))]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.AlertFile), Order = 720,
		Description = nameof(Strings.AlertFileDescription))]
	public string AlertFile { get; set; } = "alert2";

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Alerts), Name = nameof(Strings.BackGround), Order = 740,
		Description = nameof(Strings.AlertBackgroundDescription))]
	public CrossColor AlertColor { get; set; } = CrossColors.Black;

	#endregion

	#region Calculation

	[Range(0, int.MaxValue)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue,
		Description = nameof(Strings.DaysLookBackDescription))]
	public int Days
	{
		get => _days;
		set
		{
			_days = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.UsePreviousClose), Order = 800,
		Description = nameof(Strings.CalculateOnBarCloseDescription))]
	public bool UsePrevClose
	{
		get => _usePrevClose;
		set
		{
			_usePrevClose = value;
			RecalculateValues();
		}
	}

	#endregion
}