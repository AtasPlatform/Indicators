namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

using Utils.Common;
using Utils.Common.Logging;

[Category("Order Flow")]
[DisplayName("Tape Patterns")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/3047-tape-patterns")]
public class TapePattern : Indicator
{
	#region Nested types

	public enum TicksType
	{
		[Display(ResourceType = typeof(Resources), Name = "Any")]
		Any,

		[Display(ResourceType = typeof(Resources), Name = "Bid")]
		Bid,

		[Display(ResourceType = typeof(Resources), Name = "Ask")]
		Ask,

		[Display(ResourceType = typeof(Resources), Name = "Between")]
		Between,

		[Display(ResourceType = typeof(Resources), Name = "BidOrAsk")]
		BidOrAsk,

		[Display(ResourceType = typeof(Resources), Name = "BidAndAsk")]
		BidAndAsk
	}

	#endregion

	#region Static and constants

	private const decimal _clusterStepSize = 0.03m;

	#endregion

	#region Fields

	private readonly List<TradeDirection> _directions = new();
	private readonly PriceSelectionDataSeries _renderSeries = new("TapePrice");
	private readonly BlockingCollection<object> _tradesQueue = new();
	private readonly SortedDictionary<decimal, int> _volumesBySize = new();

	private Color _betweenColor;
	private Color _buyColor;

	private TicksType _calcMode;
	private Color _clusterBetween;
	private Color _clusterBuy;
	private Color _clusterSell;
	private int _clusterTransparency;
	private int _count;
	private decimal _cumulativeVol;

	private PriceSelectionValue _currentTick;
	private decimal _delta;
	private DateTime _firstTime;
	private bool _fixedSizes;
	private bool _historyCalculated;
	private CumulativeTrade _lastRenderedTrade;
	private int _lastSession;
	private List<PriceSelectionValue> _lastTick = new();
	private DateTime _lastTime;

	private int _maxCount;
	private decimal _maxCumVol;
	private decimal _maxPrice;
	private int _maxSize;
	private decimal _maxVol;
	private int _minCount;
	private decimal _minCumVol;
	private decimal _minPrice;
	private int _minSize;
	private decimal _minVol;
	private Color _objectBetween;
	private Color _objectBuy;
	private Color _objectSell;
	private int _objectTransparency;
	private int _rangeFilter;
	private bool _requestFailed;
	private bool _requestWaiting;
	private bool _searchPrintsInsideTimeFilter;
	private Color _sellColor;
	private int _size;
	private int _timeFilter;
	private TimeSpan _timeFrom;
	private TimeSpan _timeTo;
	private CancellationTokenSource _tokenSource;
	private Thread _tradesThread;
	private bool _useCumulativeTrades;
	private bool _useTimeFilter;
	private ObjectType _visualType;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "UseTimeFilter", GroupName = "TimeFiltration", Order = 100)]
	public bool UseTimeFilter
	{
		get => _useTimeFilter;
		set
		{
			_useTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "TimeFrom", GroupName = "TimeFiltration", Order = 110)]
	public TimeSpan TimeFrom
	{
		get => _timeFrom;
		set
		{
			_timeFrom = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "TimeTo", GroupName = "TimeFiltration", Order = 120)]
	public TimeSpan TimeTo
	{
		get => _timeTo;
		set
		{
			_timeTo = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "CumulativeTrades", GroupName = "Calculation", Order = 200)]
	public bool CumulativeTrades
	{
		get => _useCumulativeTrades;
		set
		{
			_useCumulativeTrades = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MinPrintVolume", GroupName = "Calculation", Order = 210)]
	public decimal MinVol
	{
		get => _minVol;
		set
		{
			if (value < 0)
				return;

			_minVol = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MaxPrintVolume", GroupName = "Calculation", Order = 220)]
	public decimal MaxVol
	{
		get => _maxVol;
		set
		{
			if (value < 0)
				return;

			_maxVol = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumCount", GroupName = "Calculation", Order = 230)]
	public int MinCount
	{
		get => _minCount;
		set
		{
			if (value < 0)
				return;

			_minCount = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MaximumCount", GroupName = "Calculation", Order = 240)]
	public int MaxCount
	{
		get => _maxCount;
		set
		{
			if (value < 0)
				return;

			_maxCount = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MinCumulativeVolume", GroupName = "Calculation", Order = 250)]
	public decimal MinCumulativeVolume
	{
		get => _minCumVol;
		set
		{
			if (value < 0)
				return;

			_minCumVol = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MaxCumulativeVolume", GroupName = "Calculation", Order = 260)]
	public decimal MaxCumulativeVolume
	{
		get => _maxCumVol;
		set
		{
			if (value < 0)
				return;

			_maxCumVol = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "TimeFilter", GroupName = "Calculation", Order = 270)]
	public int TimeFilter
	{
		get => _timeFilter;
		set
		{
			if (value < 0)
				return;

			_timeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "SearchPrintsInsideTimeFilter", GroupName = "Calculation", Order = 280)]
	public bool SearchPrintsInsideTimeFilter
	{
		get => _searchPrintsInsideTimeFilter;
		set
		{
			_searchPrintsInsideTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "RangeFilter", GroupName = "Calculation", Order = 290)]
	public int RangeFilter
	{
		get => _rangeFilter;
		set
		{
			if (value < 0)
				return;

			_rangeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Calculation", Order = 295)]
	public TicksType CalculationMode
	{
		get => _calcMode;
		set
		{
			_calcMode = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "VisualObjectsTransparency", GroupName = "Visualization", Order = 300)]
	public int ObjectTransparency
	{
		get => _objectTransparency;
		set
		{
			if (value < 0)
				return;

			_objectTransparency = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "ClusterSelectionTransparency", GroupName = "Visualization", Order = 305)]
	public int ClusterTransparency
	{
		get => _clusterTransparency;
		set
		{
			if (value < 0)
				return;

			_clusterTransparency = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "FixedSizes", GroupName = "Visualization", Order = 310)]
	public bool FixedSizes
	{
		get => _fixedSizes;
		set
		{
			_fixedSizes = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Size", GroupName = "Visualization", Order = 320)]
	public int Size
	{
		get => _size;
		set
		{
			if (value <= 0)
				return;

			_size = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MaximumSize", GroupName = "Visualization", Order = 330)]
	public int MaxSize
	{
		get => _maxSize;
		set
		{
			if (value <= 0)
				return;

			_maxSize = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "MinimumSize", GroupName = "Visualization", Order = 340)]
	public int MinSize
	{
		get => _minSize;
		set
		{
			if (value <= 0)
				return;

			_minSize = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 350)]
	public ObjectType VisualType
	{
		get => _visualType;
		set
		{
			_visualType = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "BetweenColor", GroupName = "Colors", Order = 400)]
	public Color BetweenColor
	{
		get => _betweenColor;
		set
		{
			_betweenColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Buys", GroupName = "Colors", Order = 410)]
	public Color BuyColor
	{
		get => _buyColor;
		set
		{
			_buyColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Sells", GroupName = "Colors", Order = 420)]
	public Color SellColor
	{
		get => _sellColor;
		set
		{
			_sellColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 500)]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 510)]
	public string AlertFile { get; set; } = "alert1";

	#endregion

	#region ctor

	public TapePattern()
		: base(true)

	{
		DenyToChangePanel = true;

		_minCumVol = 100;
		_timeFilter = 1000;
		_rangeFilter = 1;
		_calcMode = TicksType.Any;
		_objectTransparency = 70;
		_clusterTransparency = 40;
		_size = 10;
		_maxSize = 50;
		_minSize = 5;
		_visualType = ObjectType.Rectangle;
		_betweenColor = Color.FromRgb(128, 128, 128);
		_buyColor = Colors.Green;
		_sellColor = Colors.Red;
		_renderSeries.IsHidden = true;

		DataSeries[0] = _renderSeries;
		_renderSeries.Changed += SeriesUpdate;
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    BuyColor = ChartInfo.ColorsStore.FootprintAskColor.Convert();
	    SellColor = ChartInfo.ColorsStore.FootprintBidColor.Convert();
	    BetweenColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
    }

    protected override void OnDispose()
	{
		StopProcessQueueThread();
	}

	protected override void OnRecalculate()
	{
		while (_tradesQueue.TryTake(out _))
		{
		}
	}

	protected override void OnInitialize()
	{
		StartProcessQueueThread();
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (ChartInfo is null || InstrumentInfo is null)
			return;

		var totalBars = CurrentBar - 1;

		if (bar != 0 && bar != totalBars)
			return;

		if (bar == 0)
		{
			_historyCalculated = false;
			_renderSeries.Clear();
			_volumesBySize.Clear();
			_delta = 0;
			_minPrice = _maxPrice = 0;
			_cumulativeVol = 0;
			_count = 0;
			SetClusterColors();

			_lastSession = 0;

			try
			{
				if (IsNewSession(totalBars))
					_lastSession = totalBars;
				else
				{
					for (var i = totalBars; i >= 0; i--)
					{
						if (IsNewSession(i))
						{
							_lastSession = i;
							break;
						}
					}
				}
			}
			catch (ArgumentOutOfRangeException)
			{
				//Old instrument exception
				return;
			}

			if (!_requestWaiting)
			{
				_requestWaiting = true;
				RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_lastSession).Time));
			}
			else
				_requestFailed = true;
		}
		else
		{
			lock (_renderSeries[bar].SyncRoot)
				_lastTick = _renderSeries[bar].ToList();
		}
	}
	
	protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
	{
		_requestWaiting = false;

		if (!_requestFailed)
		{
			var trades = cumulativeTrades.ToList();

			GetTradesHistory(trades);
		}
		else
		{
			_requestFailed = false;
			Calculate(0, 0);
		}
	}

	protected override void OnCumulativeTrade(CumulativeTrade trade)
	{
		var totalBars = ChartInfo.PriceChartContainer.TotalBars;

		if (totalBars < 0)
			return;

		if (!_useCumulativeTrades)
			return;

		_tradesQueue.TryAdd(trade);
	}

	protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
	{
		var totalBars = ChartInfo.PriceChartContainer.TotalBars;

		if (totalBars < 0)
			return;

		if (!_useCumulativeTrades)
			return;

		_tradesQueue.TryAdd(trade);
	}

	protected override void OnNewTrade(MarketDataArg trade)
	{
		var totalBars = ChartInfo.PriceChartContainer.TotalBars;

		if (totalBars < 0)
			return;

		if (_useCumulativeTrades)
			return;

		_tradesQueue.TryAdd(trade);
	}

	#endregion

	#region Private methods

	private void StartProcessQueueThread()
	{
		if (_tradesThread != null)
			return;

		_tokenSource = new CancellationTokenSource();

		_tradesThread = new Thread(ProcessQueue)
		{
			Name = "TapePattern",
			IsBackground = true
		};
		_tradesThread.Start();
	}

	private void StopProcessQueueThread()
	{
		_tokenSource?.Cancel();
		_tradesThread = null;
	}

	private void ProcessQueue()
	{
		var token = _tokenSource.Token;

		while (!token.IsCancellationRequested)
		{
			try
			{
				if (ChartInfo == null || !_historyCalculated)
				{
					Thread.Sleep(10);
					continue;
				}

				if (_tradesQueue.TryTake(out var item, 200, token))
				{
					switch (item)
					{
						case CumulativeTrade cTrade:
							var isUpdate = false;

							if (_lastRenderedTrade != null)
								isUpdate = _lastRenderedTrade.IsEqual(cTrade);

							ProcessCumulative(cTrade, isUpdate);
							break;

						case MarketDataArg mdArg:
							ProcessTickTrade(mdArg);
							break;

						default:
							throw new ArgumentOutOfRangeException(nameof(item), item, null);
					}
				}
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception e)
			{
				this.LogError("Trades processing error.", e);
			}
		}
	}

	private void ProcessTickTrade(MarketDataArg trade)
	{
		ProcessTick(trade.Time, trade.Price, trade.Volume, trade.Direction, CurrentBar - 1);
	}

	private void ProcessCumulative(CumulativeTrade trade, bool isUpdate)
	{
		var barValues = _renderSeries[CurrentBar - 1];

		lock (barValues.SyncRoot)
		{
			if (isUpdate && barValues.Any())
			{
				var lastInd = barValues.Count - 1;
				barValues.RemoveAt(lastInd);

				ClearValues();
			}

			var lastObj = barValues.Count;
			ProcessCumulativeTick(trade, CurrentBar - 1);

			if (barValues.Count != lastObj)
				_lastRenderedTrade = trade;
		}
	}

	private void ProcessCumulativeTick(CumulativeTrade trade, int bar)
	{
		var time = trade.Time;
		var direction = trade.Direction;
		var price = trade.FirstPrice;

		if (_useTimeFilter)
		{
			if (_timeFrom < _timeTo)
			{
				if (time < time.Date + _timeFrom || time > time.Date + _timeTo)
					return;
			}
			else
			{
				var condition = time >= time.Date + _timeFrom || time <= time.Date + _timeTo;

				if (!condition)
					return;
			}
		}

		if (trade.Ticks.Any(x => x.Volume < _minVol)
		    ||
		    (trade.Ticks.Any(x => x.Volume > _maxVol) && _maxVol != 0))
			return;

		switch (_calcMode)
		{
			case TicksType.Bid:
				if (direction != TradeDirection.Sell)
					return;

				break;
			case TicksType.Ask:
				if (direction != TradeDirection.Buy)
					return;

				break;
			case TicksType.Between:
				if (direction != TradeDirection.Between)
					return;

				break;
			case TicksType.BidOrAsk:
				if (direction == TradeDirection.Between)
					return;

				break;
		}

		if (_volumesBySize.Count == 0)
			_firstTime = time;

		if (_searchPrintsInsideTimeFilter)
		{
			if (_timeFilter == 0)
			{
				if (time.Second != _firstTime.Second)
				{
					ClearValues();
					_firstTime = time;
					_minPrice = _maxPrice = price;
				}
			}
			else if (time - _firstTime > TimeSpan.FromMilliseconds(_timeFilter))
			{
				ClearValues();
				_firstTime = time;
				_minPrice = _maxPrice = price;
			}
		}
		else if (time - _lastTime > TimeSpan.FromMilliseconds(_timeFilter))
		{
			ClearValues();
			_firstTime = time;
			_minPrice = _maxPrice = price;
		}

		var min = _minPrice;
		var max = _maxPrice;

		if (min == 0 || price < min)
			min = price;

		if (price > max)
			max = price;

		var tickSize = InstrumentInfo.TickSize;

		if (Math.Abs(trade.FirstPrice - trade.Lastprice) / tickSize + 1 >= _rangeFilter)
		{
			ClearValues();
			_firstTime = time;
			_minPrice = _maxPrice = price;
		}
		else
		{
			_minPrice = min;
			_maxPrice = max;
		}

		_lastTime = time;

		if (trade.Volume < _minCumVol)
			return;

		if (trade.Volume > _maxCumVol && _maxCumVol != 0)
		{
			_firstTime = time;
			_minPrice = _maxPrice = price;
			return;
		}

		if (trade.Ticks.Count < _minCount)
			return;

		if (_calcMode == TicksType.BidAndAsk && _count < 2)
			return;

		if (trade.Ticks.Count > _maxCount && _maxCount != 0)
		{
			_firstTime = time;
			return;
		}

		var clusterSize = _fixedSizes ? _size : (int)Math.Round(_clusterStepSize * _size * _cumulativeVol);
		clusterSize = Math.Min(clusterSize, _maxSize);
		clusterSize = Math.Max(clusterSize, _minSize);

		var delta = trade.Volume * (trade.Direction is TradeDirection.Buy ? 1 : -1);

		var objectColor = delta > 0
			? _objectBuy
			: delta < 0
				? _objectSell
				: _objectBetween;

		var clusterColor = delta > 0
			? _clusterBuy
			: delta < 0
				? _clusterSell
				: _clusterBetween;

		_currentTick = new PriceSelectionValue(price)
		{
			Size = clusterSize,
			VisualObject = VisualType,
			ObjectColor = objectColor,
			PriceSelectionColor = clusterColor,
			MaximumPrice = _maxPrice,
			MinimumPrice = _minPrice,
			Context = direction
		};

		var deltaPerc = 0m;

		if (delta != 0)
			deltaPerc = delta * 100 / trade.Volume;

		_currentTick.Tooltip = "Tape Patterns" + Environment.NewLine;
		_currentTick.Tooltip += $"Volume={ChartInfo.TryGetMinimizedVolumeString(trade.Volume)}{Environment.NewLine}";
		_currentTick.Tooltip += $"Delta={ChartInfo.TryGetMinimizedVolumeString(delta)}[{deltaPerc:F}%]{Environment.NewLine}";
		_currentTick.Tooltip += string.Format("Time:{1}{0}", Environment.NewLine, trade.Time);
		_currentTick.Tooltip += $"Ticks:{Environment.NewLine}";

		foreach (var volTick in trade.Ticks.GroupBy(x => x.Volume))
			_currentTick.Tooltip += $"{ChartInfo.TryGetMinimizedVolumeString(volTick.Key)} lots x {volTick.Sum(x => x.Volume)}{Environment.NewLine}";

		_currentTick.Tooltip += "------------------" + Environment.NewLine;
		_renderSeries[bar].Add(_currentTick);

		if (bar == ChartInfo.PriceChartContainer.TotalBars && UseAlerts && _historyCalculated
		    &&
		    !_lastTick.Any(x =>
			    (x.MaximumPrice == price || x.MinimumPrice == price) && (TradeDirection)x.Context == direction)
		   )
		{
			var bgColor = _delta > 0
				? _buyColor
				: _delta < 0
					? _sellColor
					: _betweenColor;
			AddAlert(AlertFile, InstrumentInfo.Instrument, $"{price} {direction.GetDisplayName()}", bgColor, Color.FromRgb(0, 0, 0));
		}
	}

	private void SeriesUpdate(int bar)
	{
		RedrawChart();
	}

	private void SetClusterColors()
	{
		var alphaCluster = (byte)Math.Floor(255 * (1 - _clusterTransparency * 0.01m));
		var alphaObject = (byte)Math.Floor(255 * (1 - _objectTransparency * 0.01m));
		_clusterBuy = Color.FromArgb(alphaCluster, BuyColor.R, BuyColor.G, BuyColor.B);
		_clusterSell = Color.FromArgb(alphaCluster, SellColor.R, SellColor.G, SellColor.B);
		_clusterBetween = Color.FromArgb(alphaCluster, BetweenColor.R, BetweenColor.G, BetweenColor.B);

		_objectBuy = Color.FromArgb(alphaObject, BuyColor.R, BuyColor.G, BuyColor.B);
		_objectSell = Color.FromArgb(alphaObject, SellColor.R, SellColor.G, SellColor.B);
		_objectBetween = Color.FromArgb(alphaObject, BetweenColor.R, BetweenColor.G, BetweenColor.B);
	}

	private void ProcessTick(DateTime time, decimal price, decimal volume, TradeDirection direction, int bar)
	{
		if (_useTimeFilter)
		{
			if (_timeFrom < _timeTo)
			{
				if (time < time.Date + _timeFrom || time > time.Date + _timeTo)
					return;
			}
			else
			{
				var condition = time >= time.Date + _timeFrom || time <= time.Date + _timeTo;

				if (!condition)
					return;
			}
		}

		if (volume < _minVol
		    ||
		    (volume > _maxVol && _maxVol != 0))
			return;

		switch (_calcMode)
		{
			case TicksType.Bid:
				if (direction != TradeDirection.Sell)
					return;

				break;
			case TicksType.Ask:
				if (direction != TradeDirection.Buy)
					return;

				break;
			case TicksType.Between:
				if (direction != TradeDirection.Between)
					return;

				break;
			case TicksType.BidOrAsk:
				if (direction == TradeDirection.Between)
					return;

				break;
		}

		if (_volumesBySize.Count == 0)
			_firstTime = time;

		if (_searchPrintsInsideTimeFilter)
		{
			if (_timeFilter == 0)
			{
				if (time.Second != _firstTime.Second)
				{
					ClearValues();
					_firstTime = time;
					_minPrice = _maxPrice = price;
				}
			}
			else if (time - _firstTime > TimeSpan.FromMilliseconds(_timeFilter))
			{
				ClearValues();
				_firstTime = time;
				_minPrice = _maxPrice = price;
			}
		}
		else if (time - _lastTime > TimeSpan.FromMilliseconds(_timeFilter))

		{
			ClearValues();
			_firstTime = time;
			_minPrice = _maxPrice = price;
		}

		var min = _minPrice;
		var max = _maxPrice;

		if (min == 0 || price < min)
			min = price;

		if (price > max)
			max = price;

		var tickSize = InstrumentInfo.TickSize;

		if (max - min + tickSize > _rangeFilter * tickSize)
		{
			ClearValues();
			_firstTime = time;
			_minPrice = _maxPrice = price;
		}
		else
		{
			_minPrice = min;
			_maxPrice = max;
		}

		_lastTime = time;

		if (direction == TradeDirection.Buy)
			_delta += volume;

		if (direction == TradeDirection.Sell)
			_delta -= volume;

		_cumulativeVol += volume;

		if (!_directions.Contains(direction))
			_directions.Add(direction);

		_count++;

		if (!_volumesBySize.ContainsKey(volume))
			_volumesBySize.Add(volume, 1);
		else
			_volumesBySize[volume]++;

		if (_cumulativeVol < _minCumVol)
			return;

		if (_cumulativeVol > _maxCumVol && _maxCumVol != 0)
		{
			_firstTime = time;
			_minPrice = _maxPrice = price;
			return;
		}

		if (_count < _minCount)
			return;

		if (_calcMode == TicksType.BidAndAsk && _count < 2)
			return;

		if (_count > _maxCount && _maxCount != 0)
		{
			_firstTime = time;
			_minPrice = _maxPrice = price;
			return;
		}

		var clusterSize = _fixedSizes ? _size : (int)Math.Round(_clusterStepSize * _size * _cumulativeVol);
		clusterSize = Math.Min(clusterSize, _maxSize);
		clusterSize = Math.Max(clusterSize, _minSize);

		var objectColor = _delta > 0
			? _objectBuy
			: _delta < 0
				? _objectSell
				: _objectBetween;

		var clusterColor = _delta > 0
			? _clusterBuy
			: _delta < 0
				? _clusterSell
				: _clusterBetween;

		if (_currentTick == null)
		{
			_currentTick = new PriceSelectionValue(price)
			{
				Size = clusterSize,
				VisualObject = VisualType,
				ObjectColor = objectColor,
				PriceSelectionColor = clusterColor,
				MaximumPrice = _maxPrice,
				MinimumPrice = _minPrice,
				Context = direction
			};

			_renderSeries[bar].Add(_currentTick);

			if (bar == ChartInfo.PriceChartContainer.TotalBars && UseAlerts && _historyCalculated
			    &&
			    !_lastTick.Any(x =>
				    (x.MaximumPrice == price || x.MinimumPrice == price) && (TradeDirection)x.Context == direction)
			   )
			{
				var bgColor = _delta > 0
					? _buyColor
					: _delta < 0
						? _sellColor
						: _betweenColor;
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"{price} {direction.GetDisplayName()}", bgColor, Color.FromRgb(0, 0, 0));
			}
		}

		var deltaPerc = 0m;

		if (_delta != 0)
			deltaPerc = _delta * 100 / _cumulativeVol;

		_currentTick.Tooltip = "Tape Patterns" + Environment.NewLine;
		_currentTick.Tooltip += $"Volume={ChartInfo.TryGetMinimizedVolumeString(_cumulativeVol)}{Environment.NewLine}";
		_currentTick.Tooltip += $"Delta={ChartInfo.TryGetMinimizedVolumeString(_delta)}[{deltaPerc:F}%]{Environment.NewLine}";
		_currentTick.Tooltip += string.Format("Time:{1}{0}", Environment.NewLine, _firstTime);
		_currentTick.Tooltip += $"Ticks:{Environment.NewLine}";

		foreach (var (key, value) in _volumesBySize.Reverse())
			_currentTick.Tooltip += $"{ChartInfo.TryGetMinimizedVolumeString(key)} lots x {value}{Environment.NewLine}";

		_currentTick.Tooltip += "------------------" + Environment.NewLine;
	}

	private void ClearValues()
	{
		_directions.Clear();
		_cumulativeVol = 0;
		_delta = 0;
		_volumesBySize.Clear();
		_count = 0;
		_currentTick = null;
	}

	private void GetTradesHistory(List<CumulativeTrade> trades)
	{
		foreach (var trade in trades.OrderBy(x => x.Time))
		{
			var time = trade.Time;

			for (var i = _lastSession; i <= ChartInfo.PriceChartContainer.TotalBars; i++)
			{
				var candle = GetCandle(i);

				if (candle.Time > time || candle.LastTime < time)
					continue;

				if (_useCumulativeTrades)
					ProcessCumulativeTick(trade, i);
				else
				{
					foreach (var tick in trade.Ticks)
						ProcessTick(tick.Time, tick.Price, tick.Volume, tick.Direction, i);
				}

				break;
			}
		}

		_historyCalculated = true;
		RedrawChart();
	}

	#endregion
}