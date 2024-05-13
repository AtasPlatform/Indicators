namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;

using ATAS.Indicators.Technical.Extensions;

using OFT.Attributes;
using OFT.Localization;
using Utils.Common;
using Utils.Common.Logging;

[Category("Order Flow")]
[DisplayName("Tape Patterns")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.TapePatternDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602248")]
public class TapePattern : Indicator
{
	#region Nested types

	public enum TicksType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Any))]
		Any,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bid))]
		Bid,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ask))]
		Ask,											
														
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Between))]
		Between,										
														
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidOrAsk))]
		BidOrAsk,										
														
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidAndAsk))]
		BidAndAsk
	}

	internal class CumTradeExtended
	{
		public CumulativeTrade Owner { get; }
        public decimal FirstPrice { get; set; }
        public decimal Lastprice { get; set; }
        public decimal Volume { get; set; }
        public DateTime FirstTime { get; set; }
        public DateTime LastTime { get; set; }
        public TradeDirection Direction { get; set; }
        public List<decimal> TicksVolumes { get; set; }

		public CumTradeExtended(CumulativeTrade owner)
		{
			Owner = owner;
		}

        public override string ToString()
        {
            return $"{FirstTime:yyyy-MM-dd HH:mm:ss,fff} {Direction} at {FirstPrice} vol {Volume}/";
        }
    }

    internal class MinimizedVolumeString
    {
        private int _digits;

		private string _format;
		internal bool Minimize { get; set; } = true;
        internal int Digits
        {
            get => _digits;
            set
            {
                if (value < 0)
                    return;

                _digits = value;
                var s = "{0:0";
                s = string.Concat(s, ".", string.Join("", Enumerable.Repeat("#", value)));
                _format = string.Concat(s, "}");
            }
        }

        internal string TryGetString(decimal value)
        {
            var absValue = Math.Abs(value);

            if (Minimize && absValue > 1000)
            {
                if (absValue < 1000000)
                {
                    return string.Format(_format + "K", value / 1000);
                }
                else
                {
                    return string.Format(_format + "M", value / 1000000);
                }
            }
            else
            {
                return string.Format(_format, value);
            }
        }
    }

    #endregion

    #region Static and constants

    private const decimal _clusterStepSize = 0.03m;

	#endregion

	#region Fields

	private readonly List<TradeDirection> _directions = new();
	private readonly PriceSelectionDataSeries _renderSeries = new("RenderSeries", "TapePrice");
	private readonly BlockingCollection<object> _tradesQueue = new();
	private readonly SortedDictionary<decimal, int> _volumesBySize = new();

	private readonly MinimizedVolumeString _minimizer = new() { Digits = 3 };

    private TicksType _calcMode;
	private CrossColor _clusterBetween;
	private CrossColor _clusterBuy;
	private CrossColor _clusterSell;
	private int _clusterTransparency;
	private int _count;
	private decimal _cumulativeVol;

	private PriceSelectionValue _currentTick;
	private decimal _delta;
	private DateTime _firstTime;
	private bool _fixedSizes;
	private bool _historyCalculated;
	private CumulativeTrade _lastRenderedTrade;
    private CumTradeExtended _lastRenderedTradeExt;
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
	private CrossColor _objectBetween;
	private CrossColor _objectBuy;
	private CrossColor _objectSell;
	private int _objectTransparency;
	private int _rangeFilter;
	private bool _requestFailed;
	private bool _requestWaiting;
	private bool _searchPrintsInsideTimeFilter;
	private CrossColor _sellColor;
    private CrossColor _betweenColor;
    private CrossColor _buyColor;
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseTimeFilter), GroupName = nameof(Strings.TimeFiltration), Description = nameof(Strings.UseTimeFilterDescription), Order = 100)]
	public bool UseTimeFilter
	{
		get => _useTimeFilter;
		set
		{
			_useTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrom), GroupName = nameof(Strings.TimeFiltration), Description = nameof(Strings.TimeFromDescription), Order = 110)]
	public TimeSpan TimeFrom
	{
		get => _timeFrom;
		set
		{
			_timeFrom = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeTo), GroupName = nameof(Strings.TimeFiltration), Description = nameof(Strings.TimeToDescription), Order = 120)]
	public TimeSpan TimeTo
	{
		get => _timeTo;
		set
		{
			_timeTo = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CumulativeTrades), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.CumulativeTradesModeDescription), Order = 200)]
	public bool CumulativeTrades
	{
		get => _useCumulativeTrades;
		set
		{
			_useCumulativeTrades = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinPrintVolume), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 210)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaxPrintVolume), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 220)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumCount), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MinimumTradesCountDescription), Order = 230)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumCount), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MaximumTradesCountDescription), Order = 240)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinCumulativeVolume), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MinCumulativeVolumeDescription), Order = 250)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaxCumulativeVolume), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MaxCumulativeVolumeDescription), Order = 260)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFilter), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MaxTimeFilterDescription), Order = 270)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SearchPrintsInsideTimeFilter), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.UseCumTradesTimeFilterDescription), Order = 280)]
	public bool SearchPrintsInsideTimeFilter
	{
		get => _searchPrintsInsideTimeFilter;
		set
		{
			_searchPrintsInsideTimeFilter = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.RangeFilter), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MaxPriceLevelsCountDescription), Order = 290)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.SourceTypeDescription), Order = 295)]
	public TicksType CalculationMode
	{
		get => _calcMode;
		set
		{
			_calcMode = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualObjectsTransparency), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisualObjectsTransparency), Order = 300)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClusterSelectionTransparency), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PriceSelectionTransparencyDescription), Order = 305)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FixedSizes), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.FixedSizesDescription), Order = 310)]
	public bool FixedSizes
	{
		get => _fixedSizes;
		set
		{
			_fixedSizes = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Size), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.SizeDescription), Order = 320)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumSize), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.MaximumSizeDescription), Order = 330)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumSize), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.MinimumSizeDescription), Order = 340)]
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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.VisualModeDescription), Order = 350)]
	public ObjectType VisualType
	{
		get => _visualType;
		set
		{
			_visualType = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BetweenColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.BetweenColorDescription), Order = 400)]
	public CrossColor BetweenColor
	{
		get => _betweenColor;
		set
		{
			_betweenColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Buys), GroupName = nameof(Strings.Colors), Description = nameof(Strings.BuySignalColorDescription), Order = 410)]
	public CrossColor BuyColor
	{
		get => _buyColor;
		set
		{
			_buyColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Sells), GroupName = nameof(Strings.Colors), Description = nameof(Strings.SellSignalColorDescription), Order = 420)]
	public CrossColor SellColor
	{
		get => _sellColor;
		set
		{
			_sellColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription), Order = 500)]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 510)]
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
		_betweenColor = CrossColorExtensions.FromRgb(128, 128, 128);
		_buyColor = System.Drawing.Color.Green.Convert();
		_sellColor = System.Drawing.Color.Red.Convert();
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

		_tradesQueue.TryAdd(GetCumTradeExtended(trade));

    }

	protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
	{
		var totalBars = ChartInfo.PriceChartContainer.TotalBars;

		if (totalBars < 0)
			return;

		if (!_useCumulativeTrades)
			return;

        _tradesQueue.TryAdd(GetCumTradeExtended(trade));
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
                    var isUpdate = false;

                    switch (item)
					{
						case CumTradeExtended cTadeExt:
							if (_lastRenderedTradeExt != null)
								isUpdate = _lastRenderedTradeExt.Owner.IsEqual(cTadeExt.Owner);

							ProcessCumulativeTickExtended(cTadeExt, CurrentBar - 1, isUpdate);

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
		ProcessTick(trade.Time.AddHours(InstrumentInfo.TimeZone), trade.Price, trade.Volume, trade.Direction, CurrentBar - 1);
	}

    private void SeriesUpdate(int bar)
	{
		RedrawChart();
	}

	private void SetClusterColors()
	{
		var alphaCluster = (byte)Math.Floor(255 * (1 - _clusterTransparency * 0.01m));
		var alphaObject = (byte)Math.Floor(255 * (1 - _objectTransparency * 0.01m));
		_clusterBuy = CrossColor.FromArgb(alphaCluster, BuyColor.R, BuyColor.G, BuyColor.B);
		_clusterSell = CrossColor.FromArgb(alphaCluster, SellColor.R, SellColor.G, SellColor.B);
		_clusterBetween = CrossColor.FromArgb(alphaCluster, BetweenColor.R, BetweenColor.G, BetweenColor.B);

		_objectBuy = CrossColor.FromArgb(alphaObject, BuyColor.R, BuyColor.G, BuyColor.B);
		_objectSell = CrossColor.FromArgb(alphaObject, SellColor.R, SellColor.G, SellColor.B);
		_objectBetween = CrossColor.FromArgb(alphaObject, BetweenColor.R, BetweenColor.G, BetweenColor.B);
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

		if (volume < _minVol || (volume > _maxVol && _maxVol != 0)) 
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
				AddAlert(AlertFile, InstrumentInfo.Instrument, $"{price} {direction.GetDisplayName()}", bgColor, CrossColorExtensions.FromRgb(0, 0, 0));
			}
		}

		var printList = () =>
		{
            foreach (var (key, value) in _volumesBySize.Reverse())
                _currentTick.Tooltip += $"{_minimizer.TryGetString(key)} lots x {value}{Environment.NewLine}";
        };

        SetCurrentTickTooltipHead(_delta, _cumulativeVol, _firstTime, printList);
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
				{
					// ProcessCumulativeTick(trade, i, false);

					var cumTradeExt = GetCumTradeExtended(trade);
					ProcessCumulativeTickExtended(cumTradeExt, i, false);
                }
				else
				{
					foreach (var tick in trade.Ticks)
						ProcessTick(tick.Time.AddHours(InstrumentInfo.TimeZone), tick.Price, tick.Volume, tick.Direction, i);
				}

				break;
			}
		}

		_historyCalculated = true;
		RedrawChart();
	}

    private void ProcessCumulativeTickExtended(CumTradeExtended cumTradeExt, int bar, bool isUpdate)
    {
        var time = cumTradeExt.FirstTime.AddHours(InstrumentInfo.TimeZone);
        var direction = cumTradeExt.Direction;
        var price = cumTradeExt.FirstPrice;

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

		List<decimal> tradeTicks = cumTradeExt.TicksVolumes;

        if (tradeTicks.Any(x => x < _minVol) || (_maxVol != 0 && tradeTicks.Any(x => x > _maxVol)))
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (_searchPrintsInsideTimeFilter)
        {
            if (_timeFilter == 0)
            {
                if (cumTradeExt.FirstTime.Second != cumTradeExt.LastTime.Second)
                {
                    TryRemoveCurrentTick(bar, isUpdate);

                    return;
                }
            }
            else if ((cumTradeExt.LastTime - cumTradeExt.FirstTime) > TimeSpan.FromMilliseconds(_timeFilter))
            {
                TryRemoveCurrentTick(bar, isUpdate);

                return;
            }
        }

        var min = Math.Min(cumTradeExt.FirstPrice, cumTradeExt.Lastprice);
        var max = Math.Max(cumTradeExt.FirstPrice, cumTradeExt.Lastprice);
        var tickSize = InstrumentInfo.TickSize;

        if (_rangeFilter > 0 && (Math.Abs(cumTradeExt.FirstPrice - cumTradeExt.Lastprice) / tickSize + 1) > _rangeFilter)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (cumTradeExt.Volume < _minCumVol)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (cumTradeExt.Volume > _maxCumVol && _maxCumVol != 0)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (cumTradeExt.TicksVolumes.Count < _minCount)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (_calcMode == TicksType.BidAndAsk && _count < 2)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        if (cumTradeExt.TicksVolumes.Count > _maxCount && _maxCount != 0)
        {
            TryRemoveCurrentTick(bar, isUpdate);

            return;
        }

        var clusterSize = _fixedSizes ? _size : (int)Math.Round(_clusterStepSize * _size * cumTradeExt.Volume);
        clusterSize = Math.Min(clusterSize, _maxSize);
        clusterSize = Math.Max(clusterSize, _minSize);

        var delta = cumTradeExt.Volume * (cumTradeExt.Direction is TradeDirection.Buy ? 1 : -1);

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

        if (isUpdate)
        {
            _currentTick.MinimumPrice = min;
            _currentTick.MaximumPrice = max;
        }
        else
        {
            _currentTick = new PriceSelectionValue(price)
            {
                Size = clusterSize,
                VisualObject = VisualType,
                ObjectColor = objectColor,
                PriceSelectionColor = clusterColor,
                MaximumPrice = max,
                MinimumPrice = min,
                Context = direction
            };

            lock (_renderSeries[bar].SyncRoot)
            {
                _renderSeries[bar].Add(_currentTick);
            }

            _lastRenderedTradeExt = cumTradeExt;
        }

        _currentTick.Tooltip = string.Empty;

        var printList = () =>
        {
            foreach (var volTick in tradeTicks.GroupBy(x => x))
                _currentTick.Tooltip += $"{_minimizer.TryGetString(volTick.Key)} lots x {volTick.Count()}{Environment.NewLine}";
        };

        SetCurrentTickTooltipHead(delta, cumTradeExt.Volume, time, printList);

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
            AddAlert(AlertFile, InstrumentInfo.Instrument, $"{price} {direction.GetDisplayName()}", bgColor, CrossColorExtensions.FromRgb(0, 0, 0));
        }
    }

    private void TryRemoveCurrentTick(int bar, bool isUpdate)
    {
        if (!isUpdate)
            return;

        var result = false;

        lock (_renderSeries[bar].SyncRoot)
        {
            result = _renderSeries[bar].Remove(_currentTick);
        }

        if (!result && bar > 0) // Если не получилось удалить _currentTick, значит он был добавлен на предыдущей свече.
            _ = _renderSeries[bar - 1].Remove(_currentTick); // пробуем удалить его из предыдущей свечи.

        _currentTick = null;
    }

    private void SetCurrentTickTooltipHead(decimal delta, decimal tradeVolume, DateTime time, Action action)
    {
        var newLine = Environment.NewLine;
        var deltaPerc = 0m;

        if (delta != 0)
            deltaPerc = delta * 100 / tradeVolume;
		
        _currentTick.Tooltip = "Tape Patterns" + newLine;
        _currentTick.Tooltip += $"Volume = {_minimizer.TryGetString(tradeVolume)}{newLine}";
        _currentTick.Tooltip += $"Delta = {_minimizer.TryGetString(delta)} [{deltaPerc:F}%]{newLine}";
        _currentTick.Tooltip += $"Time: {time}{newLine}";
        _currentTick.Tooltip += $"Ticks:{newLine}";
        action();
        _currentTick.Tooltip += $"{new string('-', 20)}{newLine}";
    }

    private CumTradeExtended GetCumTradeExtended(CumulativeTrade trade)
    {
		var cumTradeExt = new CumTradeExtended(trade)
		{
			FirstPrice = trade.FirstPrice,
			Lastprice = trade.Lastprice,
			FirstTime = trade.Time,
			LastTime = trade.Ticks.Last().Time,
			Volume = trade.Volume,
			Direction = trade.Direction,
			TicksVolumes = trade.Ticks.Select(t => t.Volume).ToList()
		};

		return cumTradeExt;
    }

    #endregion
}