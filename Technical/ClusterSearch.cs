namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using MoreLinq;

using OFT.Attributes;

using Utils.Common.Collections;

using static DynamicLevels;

[Category("Clusters, Profiles, Levels")]
[DisplayName("Cluster Search")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/365-cluster-search")]
public class ClusterSearch : Indicator
{
	#region Nested types

	private class Pair
	{
		#region Properties

		public decimal Vol { get; set; }

		public decimal Price { get; set; }

		public string ToolTip { get; set; }

		#endregion
	}
	
	public enum CalcMode
	{
		[Display(ResourceType = typeof(Resources), Name = "Bid")]
		Bid,

		[Display(ResourceType = typeof(Resources), Name = "Ask")]
		Ask,

		[Display(ResourceType = typeof(Resources), Name = "Delta")]
		Delta,

		[Display(ResourceType = typeof(Resources), Name = "Volume")]
		Volume,

		[Display(ResourceType = typeof(Resources), Name = "Ticks")]
		Tick,

		[Display(ResourceType = typeof(Resources), Name = "MaxCumulativeVolume")]
		MaxVolume,

		[Browsable(false)]
		[Obsolete]
		[Display(ResourceType = typeof(Resources), Name = "Time")]
		Time
    }

    public enum CandleDirection
	{
		[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
		Bearish,

		[Display(ResourceType = typeof(Resources), Name = "Bullish")]
		Bullish,

		[Display(ResourceType = typeof(Resources), Name = "Any")]
		Any,

		[Display(ResourceType = typeof(Resources), Name = "Neutral")]
		Neutral
	}

	public enum PriceLocation
	{
		[Display(ResourceType = typeof(Resources), Name = "AtHigh")]
		AtHigh,

		[Display(ResourceType = typeof(Resources), Name = "AtLow")]
		AtLow,

		[Display(ResourceType = typeof(Resources), Name = "Any")]
		Any,

		[Display(ResourceType = typeof(Resources), Name = "Body")]
		Body,

		[Display(ResourceType = typeof(Resources), Name = "UpperWick")]
		UpperWick,

		[Display(ResourceType = typeof(Resources), Name = "LowerWick")]
		LowerWick,

		[Display(ResourceType = typeof(Resources), Name = "AtHighOrLow")]
		AtHighOrLow
	}

	#endregion

	#region Static and constants

	private const decimal _clusterStepSize = 0.001m;

	#endregion

	#region Fields

	private readonly HashSet<decimal> _alertPrices = new();
	private readonly PriceVolumeInfo _cacheItem = new();
	private readonly Dictionary<decimal, PriceVolumeInfo> _levels = new();

	private readonly List<Pair> _pairs = new();

	private readonly Queue<PriceVolumeInfo> _priceVolumeCache = new(1024);

	private readonly Dictionary<int, IEnumerable<PriceVolumeInfo>> _priceVolumeInfoCache = new();

	private readonly PriceSelectionDataSeries _renderDataSeries = new("Price");
	private readonly List<PriceVolumeInfo> _sumInfo = new();
	private bool _autoFilter;
	private decimal _autoFilterValue;

	private int _barsRange;
	private CandleDirection _candleDirection;
	private Color _clusterPriceTransColor;

	private Color _clusterTransColor;
	private int _days;
	private decimal _deltaFilter;
	private decimal _deltaImbalance;
	private bool _fixedSizes;
	private int _lastBar = -1;
	private decimal _maxAverageTrade;
	private Filter _maxFilter = new();
	private decimal _maxPercent;
	private int _maxSize;
	private decimal _minAverageTrade;
	private Filter _minFilter = new();
	private decimal _minPercent;
	private int _minSize;
	private bool _onlyOneSelectionPerBar;
	private Filter _pipsFromHigh = new();
	private Filter _pipsFromLow = new();
	private PriceLocation _priceLocation;
	private int _priceRange;
	private bool _showPriceSelection = true;
	private int _size;
	private int _targetBar;
	private decimal _tickSize;
	private TimeSpan _timeFrom;
	private TimeSpan _timeTo;
	private int _transparency;
	private CalcMode _type;
	private bool _usePrevClose;
	private bool _useTimeFilter;
	private int _visualObjectsTransparency;
	private ObjectType _visualType = ObjectType.Rectangle;

	#endregion

	#region ctor

	public ClusterSearch()
		: base(true)
	{
		_days = 20;

		_type = CalcMode.Volume;
		_maxFilter.Enabled = true;
		_maxFilter.Value = 99999;
		_minFilter.Enabled = true;
		_minFilter.Value = 1000;

		_candleDirection = CandleDirection.Any;
		_barsRange = 1;
		_priceRange = 1;
		_pipsFromHigh.Value = 100000000;
		_pipsFromLow.Value = 100000000;
		_priceLocation = PriceLocation.Any;

		_timeFrom = TimeSpan.Zero;
		_timeTo = TimeSpan.Zero;

		VisualObjectsTransparency = 70;
		Transparency = 20;
		ClusterColor = Color.FromArgb(255, 255, 0, 255);
		PriceSelectionColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - Transparency * 0.01m)), 255, 0, 255);
		VisualType = ObjectType.Rectangle;

		_size = 10;
		_minSize = 5;
		_maxSize = 50;

		AlertColor = Colors.Black;

		DenyToChangePanel = true;
		_renderDataSeries.IsHidden = true;
		DataSeries[0] = _renderDataSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnInitialize()
	{
		_maxFilter.PropertyChanged += Filter_PropertyChanged;
		_minFilter.PropertyChanged += Filter_PropertyChanged;
		PipsFromHigh.PropertyChanged += Filter_PropertyChanged;
		PipsFromLow.PropertyChanged += Filter_PropertyChanged;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_renderDataSeries.Clear();
			_tickSize = InstrumentInfo.TickSize;

			_autoFilterValue = 0;
			_targetBar = 0;

			if (_days > 0)
			{
				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}
			}

			if (UsePrevClose)
				return;
		}

		if (UsePrevClose)
			bar--;

		if (bar < _targetBar || (UsePrevClose && _lastBar == bar))
			return;

		var candle = GetCandle(bar);
		_pairs.Clear();
		var time = candle.Time.AddHours(InstrumentInfo.TimeZone);

		if (UseTimeFilter)
		{
			if (TimeFrom < TimeTo)
			{
				if (time < time.Date + TimeFrom)
					return;

				if (time > time.Date + TimeTo)
					return;
			}
			else
			{
				if (time < time.Date + TimeFrom && time > time.Date + TimeTo)
					return;
			}
		}

		if (_lastBar != bar)
		{
			_alertPrices.Clear();
			_priceVolumeInfoCache.Remove(bar - BarsRange);
		}

		var toolTip = "";

		var maxBody = Math.Max(candle.Open, candle.Close);
		var minBody = Math.Min(candle.Open, candle.Close);

		var candlesHigh = candle.High;
		var candlesLow = candle.Low;

		if (CandleDir == CandleDirection.Any
		    ||
		    (CandleDir == CandleDirection.Bullish && candle.Close > candle.Open)
		    ||
		    (CandleDir == CandleDirection.Bearish && candle.Close < candle.Open)
		    ||
		    (CandleDir == CandleDirection.Neutral && candle.Close == candle.Open))
		{
			for (var i = bar; i >= Math.Max(0, bar - _barsRange + 1); i--)
			{
				var lCandle = GetCandle(i);

				var candleLevels = i != CurrentBar - 1
					? _priceVolumeInfoCache.GetOrAdd(i, _ => lCandle.GetAllPriceLevels())
					: lCandle.GetAllPriceLevels(_cacheItem);

				if (lCandle.High > candlesHigh)
					candlesHigh = lCandle.High;

				if (lCandle.Low < candlesLow)
					candlesLow = lCandle.Low;

				foreach (var level in candleLevels)
				{
					var price = level.Price;

					if (!_levels.TryGetValue(price, out var currentLevel))
					{
						if (_priceVolumeCache.Count != 0)
						{
							currentLevel = _priceVolumeCache.Dequeue();

							currentLevel.Ask = 0;
							currentLevel.Between = 0;
							currentLevel.Bid = 0;
							currentLevel.Ticks = 0;
							currentLevel.Time = 0;
							currentLevel.Volume = 0;
						}
						else
							currentLevel = new PriceVolumeInfo();

						_levels.Add(price, currentLevel);
					}

					currentLevel.Ask += level.Ask;
					currentLevel.Between += level.Between;
					currentLevel.Bid += level.Bid;
					currentLevel.Ticks += level.Ticks;
					currentLevel.Time += level.Time;
					currentLevel.Volume += level.Volume;
				}
			}

			HashSet<decimal> maxVolPrice = new();

			if (CalcType is CalcMode.MaxVolume)
				maxVolPrice = CalcMaxVol(candle);

			foreach (var (price, _) in _levels)
			{
				var isApproach = true;

				if (price + (PriceRange - 1) * _tickSize > candle.High)
					continue;

				_sumInfo.Clear();

				for (var i = price; i < price + PriceRange * _tickSize; i += _tickSize)
				{
					var isLevel = _levels.TryGetValue(i, out var level);

					if (!isLevel)
						continue;

					switch (PriceLoc)
					{
						case PriceLocation.AtHigh when price != candle.High:
						case PriceLocation.AtLow when price != candle.Low:
						case PriceLocation.AtHighOrLow when price != candle.High && price != candle.Low:
						case PriceLocation.LowerWick when price > minBody:
						case PriceLocation.UpperWick when price < maxBody:
						case PriceLocation.Body when price > maxBody || price < minBody:
							continue;
					}

					if (i > candle.High || i < candle.Low)
					{
						isApproach = false;
						break;
					}

					if ((candle.High - i) / _tickSize > PipsFromHigh.Value && PipsFromHigh.Enabled)
					{
						isApproach = false;
						break;
					}

					if ((i - candle.Low) / _tickSize > PipsFromLow.Value && PipsFromLow.Enabled)
					{
						isApproach = false;
						break;
					}

					_sumInfo.Add(level);
				}

				if (_sumInfo.Count == 0)
					continue;

				if (price > candle.High || price < candle.Low || !isApproach)
					continue;

				switch (PriceLoc)
				{
					case PriceLocation.LowerWick when price >= minBody:
					case PriceLocation.UpperWick when price <= maxBody:
					case PriceLocation.AtHigh when price != candle.High:
					case PriceLocation.AtLow when price != candle.Low:
					case PriceLocation.AtHighOrLow when !(price == candle.Low || price == candle.High):
					case PriceLocation.Body when price > maxBody || price < minBody:
						continue;
				}

				var sumBid = 0m;
				var sumAsk = 0m;
				var sumVol = 0m;
				var sumTicks = 0;
				var sumTime = 0;

				for (var i = 0; i < _sumInfo.Count; i++)
				{
					var item = _sumInfo[i];

					sumBid += item.Bid;
					sumAsk += item.Ask;
					sumVol += item.Volume;
					sumTicks += item.Ticks;
					sumTime += item.Time;
				}

				if (DeltaFilter > 0 && sumAsk - sumBid < DeltaFilter)
					continue;

				if (DeltaFilter < 0 && sumAsk - sumBid > DeltaFilter)
					continue;

				if (DeltaImbalance != 0)
				{
					var askImbalance = sumAsk > 0
						? sumAsk * 100.0m / sumVol
						: 0;

					var bidImbalance = sumBid > 0
						? sumBid * 100.0m / sumVol
						: 0;

					if (DeltaImbalance > 0 && askImbalance < _deltaImbalance)
						continue;

					if (DeltaImbalance < 0 && bidImbalance < Math.Abs(DeltaImbalance))
						continue;
				}

				decimal? val = null;
				decimal sum;

				switch (CalcType)
				{
					case CalcMode.Volume:
						sum = sumVol;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Lots";
						}

						break;

					case CalcMode.Tick:
						sum = sumTicks;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Trades";
						}

						break;

					case CalcMode.Time:
						sum = sumTime;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Seconds";
						}

						break;

					case CalcMode.Delta:
						sum = sumAsk - sumBid;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Delta";
						}

						break;

					case CalcMode.Bid:
						sum = sumBid;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Bids";
						}

						break;

					case CalcMode.Ask:
						sum = sumAsk;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Asks";
						}

						break;
					case CalcMode.MaxVolume:
						sum = sumVol;

						if (IsApproach(sum))
						{
							val = sum;
							toolTip = ChartInfo.TryGetMinimizedVolumeString(sum) + " Asks";
						}

						break;
				}

				if (val != null)
				{
					var avgTrade = sumTicks == 0
						? 0
						: sumVol / sumTicks;

					if (MaxPercent != 0 || MinPercent != 0)
					{
						var volume = sumVol;
						var volPercent = 100m * volume / candle.Volume;

						if (volPercent < MinPercent || (volPercent > MaxPercent && MaxPercent != 0))
							continue;
					}

					if (CalcType is CalcMode.MaxVolume && !maxVolPrice.Contains(price))
						continue;

					if ((MaxAverageTrade == 0 || avgTrade <= MaxAverageTrade)
					    &&
					    (MinAverageTrade == 0 || avgTrade >= MinAverageTrade))
					{
						_pairs.Add(new Pair
						{
							Vol = val ?? 0,
							ToolTip = "Cluster Search" + Environment.NewLine + toolTip + Environment.NewLine,
							Price = price
						});
					}
				}
			}

			foreach (var pair in _levels)
				_priceVolumeCache.Enqueue(pair.Value);

			_levels.Clear();
		}

		if (OnlyOneSelectionPerBar)
		{
			var maxPair = _pairs.OrderByDescending(x => x.Vol).FirstOrDefault();

			if (maxPair != default)
			{
				_pairs.Clear();
				_pairs.Add(maxPair);
			}
		}

		if (bar == CurrentBar - 1 || (UsePrevClose && bar == CurrentBar - 2))
		{
			foreach (var pair in _pairs)
			{
				var isNew = _alertPrices.Add(pair.Price);

				if (isNew)
					AddClusterAlert(pair.ToolTip);
			}
		}

		var selectionSide = SelectionType.Full;

		if (CalcType == CalcMode.Ask)
			selectionSide = SelectionType.Ask;

		if (CalcType == CalcMode.Bid)
			selectionSide = SelectionType.Bid;

		if (bar == CurrentBar - 1)
			_renderDataSeries[bar].Clear();

		foreach (var pair in _pairs.OrderBy(x => x.Price))
		{
			var filterValue = MinimalFilter();
			var absValue = Math.Abs(pair.Vol);
			var clusterSize = FixedSizes ? _size : (int)(absValue * _size / Math.Max(filterValue, 1));

			if (!FixedSizes)
			{
				clusterSize = Math.Min(clusterSize, MaxSize);
				clusterSize = Math.Max(clusterSize, MinSize);
			}

			var priceValue = new PriceSelectionValue(pair.Price)
			{
				VisualObject = VisualType,
				Size = clusterSize,
				SelectionSide = selectionSide,
				ObjectColor = _clusterTransColor,
				PriceSelectionColor = ShowPriceSelection ? _clusterPriceTransColor : Colors.Transparent,
				Tooltip = pair.ToolTip,
				Context = absValue,
				MinimumPrice = Math.Max(pair.Price, candlesLow),
				MaximumPrice = Math.Min(candlesHigh, pair.Price + InstrumentInfo.TickSize * (_priceRange - 1))
			};
			_renderDataSeries[bar].Add(priceValue);
		}

		_lastBar = bar;
	}

	protected override void OnRecalculate()
	{
		_priceVolumeInfoCache.Clear();

		base.OnRecalculate();
	}

	protected override void OnFinishRecalculate()
	{
		if (AutoFilter)
		{
			var valuesList = new List<PriceSelectionValue>();

			for (var i = 0; i <= CurrentBar - 1; i++)
			{
				if (!_renderDataSeries[i].Any())
					continue;

				valuesList.AddRange(_renderDataSeries[i]);
			}

			if (!valuesList.Any())
				return;

			valuesList = valuesList.OrderByDescending(x =>
					CalcType is CalcMode.Delta
						? Math.Abs((decimal)x.Context)
						: (decimal)x.Context)
				.ToList();

			if (valuesList.Count <= 10)
			{
				_autoFilterValue = CalcType is CalcMode.Delta
					? Math.Abs((decimal)valuesList.Last().Context)
					: (decimal)valuesList.Last().Context;
			}
			else
			{
				_autoFilterValue = CalcType is CalcMode.Delta
					? Math.Abs((decimal)valuesList.Skip(10).First().Context)
					: (decimal)valuesList.Skip(10).First().Context;
			}

			for (var i = 0; i <= CurrentBar - 1; i++)
			{
				if (!_renderDataSeries[i].Any())
					continue;

				_renderDataSeries[i].RemoveAll(x => CalcType is CalcMode.Delta
					? Math.Abs((decimal)x.Context) < _autoFilterValue
					: (decimal)x.Context < _autoFilterValue);
			}
		}
	}

	#endregion

	#region Private methods

	private HashSet<decimal> CalcMaxVol(IndicatorCandle candle)
	{
		HashSet<decimal> maxVolPrice = new();
		var maxVol = 0m;

		foreach (var (price, _) in _levels)
		{
			var volume = 0m;

			for (var i = price; i < price + PriceRange * _tickSize; i += _tickSize)
			{
				var isLevel = _levels.TryGetValue(i, out var level);

				if (!isLevel)
					continue;

				if (i > candle.High || i < candle.Low)
					break;

				volume += level.Volume;
			}

			if (volume < maxVol)
				continue;

			if (volume > maxVol)
			{
				maxVolPrice.Clear();
				maxVol = volume;
			}

			maxVolPrice.Add(price);
		}

		return maxVolPrice;
	}

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

	private bool IsInfoEmpty(PriceVolumeInfo info)
	{
		return info.Ask == 0 &&
			info.Bid == 0 &&
			info.Ticks == 0 &&
			info.Time == 0 &&
			info.Volume == 0;
	}

	private bool IsApproach(decimal value)
	{
		if (!AutoFilter)
		{
			var isApproach = (MaximumFilter.Enabled && MaximumFilter.Value >= value) || !MaximumFilter.Enabled;

			if (MinimumFilter.Enabled && MinimumFilter.Value > value)
				isApproach = false;

			return isApproach;
		}
		else
		{
			var isApproach = CalcType is CalcMode.Delta
				? Math.Abs(value) >= _autoFilterValue
				: value >= _autoFilterValue;

			return isApproach;
		}
	}

	private void AddClusterAlert(string msg)
	{
		if (!UseAlerts)
			return;

		AddAlert(AlertFile, InstrumentInfo.Instrument, msg, AlertColor, ClusterColor);
	}

	private decimal MinimalFilter()
	{
		var minFilter = MinimumFilter.Enabled ? MinimumFilter.Value : 0;
		var maxFilter = MaximumFilter.Enabled ? MaximumFilter.Value : 0;

		if (MinimumFilter.Value >= 0 && MaximumFilter.Value >= 0)
			return minFilter;

		if (MinimumFilter.Value < 0 && MaximumFilter.Value >= 0)
			return Math.Min(Math.Abs(minFilter), maxFilter);

		return Math.Abs(maxFilter);
	}

	#endregion

	#region Calculation

	[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "DaysLookBack", Order = int.MaxValue, Description = "DaysLookBackDescription")]
	public int Days
	{
		get => _days;
		set
		{
			if (value < 0)
				return;

			_days = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "UsePreviousClose", Order = 110)]
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
			CalcMode.Volume or CalcMode.MaxVolume or _ => MiddleClusterType.Volume
		};
		set => CalcType = value switch
		{
			MiddleClusterType.Bid => CalcMode.Bid,
			MiddleClusterType.Ask => CalcMode.Ask,
			MiddleClusterType.Delta => CalcMode.Delta,
			MiddleClusterType.Volume => CalcMode.Volume,
			MiddleClusterType.Tick => CalcMode.Tick,
			MiddleClusterType.Time => CalcMode.Volume,
			_ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
		};
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "CalculationMode", Order = 200)]
	public CalcMode CalcType
	{
		get => _type;
		set
		{
			_type = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "AutoFilter", Order = 215)]
	public bool AutoFilter
	{
		get => _autoFilter;
		set
		{
			_autoFilter = value;

			MinimumFilter.Enabled = MaximumFilter.Enabled = !value;

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MinValue", Order = 220)]
	public Filter MinimumFilter
	{
		get => _minFilter;
		set
		{
			if (value.Value < 0)
				return;

			_minFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MaxValue", Order = 230)]
	public Filter MaximumFilter
	{
		get => _maxFilter;
		set
		{
			if (value.Value < 0)
				return;

			_maxFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MinimumAverageTrade", Order = 470, Description = "MinAvgTradeDescription")]
	[Range(0, 10000000)]
	public decimal MinAverageTrade
	{
		get => _minAverageTrade;
		set
		{
			_minAverageTrade = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MaximumAverageTrade", Order = 480, Description = "MaxAvgTradeDescription")]
	[Range(0, 10000000)]
	public decimal MaxAverageTrade
	{
		get => _maxAverageTrade;
		set
		{
			if (value < 0)
				return;

			_maxAverageTrade = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MinVolPercent", Order = 490, Description = "MinPercentDescription")]
	[Range(0, 100)]
	public decimal MinPercent
	{
		get => _minPercent;
		set
		{
			_minPercent = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MaxVolPercent", Order = 492, Description = "MaxPercentDescription")]
	[Range(0, 100)]
	public decimal MaxPercent
	{
		get => _maxPercent;
		set
		{
			_maxPercent = value;
			RecalculateValues();
		}
	}

	#endregion

	#region DeltaFilters

	[Display(ResourceType = typeof(Resources), GroupName = "DeltaFilters", Name = "DeltaImbalance", Order = 300, Description = "DeltaImbalanceDescription")]
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

	[Display(ResourceType = typeof(Resources), GroupName = "DeltaFilters", Name = "DeltaFilter", Order = 310, Description = "DeltaFilterDescription")]
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

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "CandleDirection", Order = 400)]
	public CandleDirection CandleDir
	{
		get => _candleDirection;
		set
		{
			_candleDirection = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "BarsRange", Order = 410, Description = "BarsRangeDescription")]
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

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "PriceRange", Order = 420, Description = "PriceRangeDescription")]
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

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "PipsFromHigh", Order = 430, Description = "PipsFromHighDescription")]
	public Filter PipsFromHigh
	{
		get => _pipsFromHigh;
		set
		{
			if (value.Value < 0)
				return;

			_pipsFromHigh = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "PipsFromLow", Order = 440, Description = "PipsFromLowDescription")]
	public Filter PipsFromLow
	{
		get => _pipsFromLow;
		set
		{
			if (value.Value < 0)
				return;

			_pipsFromLow = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "LocationFilters", Name = "PriceLocation", Order = 450)]
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

	#region Time filtration

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "UseTimeFilter", Order = 500)]
	public bool UseTimeFilter
	{
		get => _useTimeFilter;
		set
		{
			_useTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "TimeFrom", Order = 510)]
	public TimeSpan TimeFrom
	{
		get => _timeFrom;
		set
		{
			_timeFrom = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "TimeTo", Order = 520)]
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

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "OnlyOneSelectionPerBar", Order = 590)]
	public bool OnlyOneSelectionPerBar
	{
		get => _onlyOneSelectionPerBar;
		set
		{
			_onlyOneSelectionPerBar = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "VisualMode", Order = 600)]
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

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "ObjectsColor", Order = 605)]
	public Color ClusterColor
	{
		get => Color.FromRgb(_clusterTransColor.R, _clusterTransColor.G, _clusterTransColor.B);
		set
		{
			_clusterTransColor = Color.FromArgb(_clusterTransColor.A, value.R, value.G, value.B);

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.ObjectColor = _clusterTransColor; });
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "VisualObjectsTransparency", Order = 610)]
	[Range(0, 100)]
	public int VisualObjectsTransparency
	{
		get => _visualObjectsTransparency;
		set
		{
			_visualObjectsTransparency = value;

			_clusterTransColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - value * 0.01m)), _clusterTransColor.R, _clusterTransColor.G,
				_clusterTransColor.B);

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => x.ObjectColor = _clusterTransColor);
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "ShowPriceSelection", Order = 615)]
	public bool ShowPriceSelection
	{
		get => _showPriceSelection;
		set
		{
			_showPriceSelection = value;

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.PriceSelectionColor = value ? _clusterPriceTransColor : Colors.Transparent; });
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "PriceSelectionColor", Order = 620)]
	public Color PriceSelectionColor
	{
		get => Color.FromRgb(_clusterPriceTransColor.R, _clusterPriceTransColor.G, _clusterPriceTransColor.B);
		set
		{
			_clusterPriceTransColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - Transparency * 0.01m)), value.R, value.G, value.B);

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => { x.PriceSelectionColor = _clusterPriceTransColor; });
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "ClusterSelectionTransparency", Order = 625)]
	[Range(0, 100)]
	public int Transparency
	{
		get => _transparency;
		set
		{
			_transparency = value;

			_clusterPriceTransColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - value * 0.01m)), _clusterPriceTransColor.R, _clusterPriceTransColor.G,
				_clusterPriceTransColor.B);

			for (var i = 0; i < _renderDataSeries.Count; i++)
				_renderDataSeries[i].ForEach(x => x.PriceSelectionColor = ShowPriceSelection ? _clusterPriceTransColor : Colors.Transparent);
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "FixedSizes", Order = 640)]
	public bool FixedSizes
	{
		get => _fixedSizes;
		set
		{
			_fixedSizes = value;
			SetSize();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "Size", Order = 650)]
	public int Size
	{
		get => _size;
		set
		{
			if (value <= 0)
				return;

			_size = value;

			SetSize();
		}
	}

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "MinimumSize", Order = 660)]
	public int MinSize
	{
		get => _minSize;
		set
		{
			if (value <= 0)
				return;

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

	[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "MaximumSize", Order = 670)]
	public int MaxSize
	{
		get => _maxSize;
		set
		{
			if (value <= 0)
				return;

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

	[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts", Order = 700)]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile", Order = 720)]
	public string AlertFile { get; set; } = "alert2";

	[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "BackGround", Order = 740)]
	public Color AlertColor { get; set; }

	#endregion
}