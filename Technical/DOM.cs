namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

using OFT.Attributes;
using OFT.Attributes.Editors;
using OFT.Localization;
using OFT.Rendering;
using OFT.Rendering.Context;
using OFT.Rendering.Helpers;
using OFT.Rendering.Tools;
using ATAS.DataFeedsCore.Dom;
using Utils.Common.Logging;

using Color = System.Drawing.Color;

[Category(IndicatorCategories.OrderBook)]
[DisplayName("Depth Of Market")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DOMDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602367")]
public class DOM : Indicator
{
	#region Nested types

	public class VolumeInfo
	{
		#region Properties

		public decimal Volume { get; set; }

		public decimal Price { get; set; }

		#endregion
	}

	public enum Mode
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Levels))]
		Common,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Cumulative))]
		Cumulative,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Both))]
		Combined
	}

	public enum DepthChangesMode
	{
		[Display(Name = "Net")]
		Net,

		[Display(Name = "Stacked only")]
		StackedOnly,

		[Display(Name = "Pulled only")]
		PulledOnly,

		[Display(Name = "Both")]
		Both
	}

	private sealed class DepthChangeInfo
	{
		public decimal Price { get; init; }

		public MarketDataType DataType { get; init; }

		public decimal StackedVolume { get; set; }

		public decimal PulledVolume { get; set; }

		public decimal NetVolume => StackedVolume - PulledVolume;
	}

	#endregion

	#region Static and constants

	private const int _fontSize = 10;
	private const int _minFontHeight = 10;
	private const int _heightToSolidMode = 4;

    #endregion

    #region Fields

    private readonly ValueDataSeries _upScale = new("UpScale", "Up") { ScaleIt = false };
    private readonly ValueDataSeries _downScale = new("DownScale", "Down") { ScaleIt = false };

    private readonly RedrawArg _emptyRedrawArg = new(new Rectangle(0, 0, 0, 0));

	private readonly RenderStringFormat _stringLeftFormat = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private readonly RenderStringFormat _stringRightFormat = new()
	{
		Alignment = StringAlignment.Far,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter,
		FormatFlags = StringFormatFlags.NoWrap
	};

	private Color _askBackGround;
	private Color _askColor;
	private HistogramRender _asksHistogram;
	private Color _bestAskBackGround;
	private Color _bestBidBackGround;
	private Color _bidBackGround;
	private Color _bidColor;
	private HistogramRender _bidsHistogram;

	private SortedList<decimal, decimal> _cumulativeAsk = new();
	private SortedList<decimal, decimal> _cumulativeBid = new();

	private MultiColorsHistogramRender _cumulativeHistogram;
	private readonly Dictionary<(decimal Price, MarketDataType DataType), MarketDataArg> _depthChangeBaseline = new();
	private readonly Dictionary<(decimal Price, MarketDataType DataType), DepthChangeInfo> _depthChanges = new();
	private Dictionary<decimal, Color> _filteredColors = new();

	private RenderFont _font = new("Arial", _fontSize); 
	private RenderFont _cumulativeFont = new("Arial", 9);

    private readonly object _locker = new();

	private decimal _maxPrice = decimal.MinValue;

	private VolumeInfo _maxVolume = new();
	private SortedDictionary<decimal, MarketDataArg> _asks = new();
	private SortedDictionary<decimal, MarketDataArg> _bids = new();
	private decimal _minPrice = decimal.MaxValue;

	// Cached min/max with lazy invalidation
	private decimal? _cachedMinAsk;
	private decimal? _cachedMaxAsk;
	private decimal? _cachedMaxBid;
	private decimal? _cachedMinBid;

	private int _priceLevelsHeight;
	private int _lastRenderedHeight;
	private int _scale;
	private int _fontHeight;

	private Dictionary<int, (RenderFont Font, int Height)> _fontCache = new();
	private string _cachedFontFamily;
	private float _cachedMaxFontSize;

	private (decimal Volume, decimal VolumePrice, decimal MinPrice, decimal MaxPrice)? _visibleVolumeCache;

	private List<FilterColor> _sortedFilters = new();
	private Color _textColor;
	private Mode _visualMode = Mode.Common;
	private Color _volumeAskColor;
	private Color _volumeBidColor;
	private bool _showDepthChanges;
	private int _lastDepthChangesBar = -1;
	private DepthChangesMode _depthChangesMode = DepthChangesMode.Net;
	private Filter _depthChangesFilter = new(true) { Value = 0, Enabled = true };
	private Color _pullingColor = Color.FromArgb(170, 242, 56, 90);
	private Color _stackingColor = Color.FromArgb(170, 8, 153, 129);
	private Color _depthChangesTextColor = Color.White;

	#endregion

	#region Computed properties

	private decimal MinAsk
	{
		get
		{
			if (_asks.Count == 0)
				return decimal.MaxValue;

			_cachedMinAsk ??= _asks.Keys.First();
			return _cachedMinAsk.Value;
		}
	}

	private decimal MaxBid
	{
		get
		{
			if (_bids.Count == 0)
				return decimal.MinValue;

			_cachedMaxBid ??= _bids.Keys.Last();
			return _cachedMaxBid.Value;
		}
	}

	private decimal MinBid
	{
		get
		{
			if (_bids.Count == 0)
				return decimal.MaxValue;

			_cachedMinBid ??= _bids.Keys.First();
			return _cachedMinBid.Value;
		}
	}

	private decimal MaxAsk
	{
		get
		{
			if (_asks.Count == 0)
				return decimal.MinValue;

			_cachedMaxAsk ??= _asks.Keys.Last();
			return _cachedMaxAsk.Value;
		}
	}

	private decimal MinDepthPrice => _bids.Count > 0 ? MinBid : (_asks.Count > 0 ? MinAsk : 0);
	private decimal MaxDepthPrice => _asks.Count > 0 ? MaxAsk : (_bids.Count > 0 ? MaxBid : 0);
	private int TotalDepthCount => _asks.Count + _bids.Count;
	private bool HasVisibleDepthChanges => IsDepthChangesEnabled && _depthChanges.Values.Any(HasDepthChangeValue);

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.VisualMode), GroupName = nameof(Strings.HistogramSize), Description = nameof(Strings.ElementDisplayModeDescription), Order = 100)]
	public Mode VisualMode
	{
		get => _visualMode;
		set
		{
			_visualMode = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAutoSize), GroupName = nameof(Strings.HistogramSize), Description = nameof(Strings.UseAutoMaxSizeDetectionDescription), Order = 105)]
	public bool UseAutoSize { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ProportionVolume), GroupName = nameof(Strings.HistogramSize), Description = nameof(Strings.MaxSizeProportionVolumeDescription), Order = 110)]
	[Range(0, 1000000000000)]
	public decimal ProportionVolume { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.HistogramSize), Description = nameof(Strings.HistogramWidthDescription), Order = 120)]
	[Range(0, 4000)]
	public int Width { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.RightToLeft), GroupName = nameof(Strings.HistogramSize), Description = nameof(Strings.RightToLeftDescription), Order = 130)]
	public bool RightToLeft { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidRows), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.ProfileBidValueColorDescription), Order = 200)]
	public CrossColor BidRows
	{
		get => _bidColor.Convert();
		set
		{
			_bidColor = value.Convert();
			_volumeBidColor = Color.FromArgb(50, value.R, value.G, value.B);
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.LabelTextColorDescription), Order = 210)]
	public CrossColor TextColor
	{
		get => _textColor.Convert();
		set => _textColor = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskRows), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.ProfileAskValueColorDescription), Order = 220)]
	public CrossColor AskRows
	{
		get => _askColor.Convert();
		set
		{
			_askColor = value.Convert();
			_volumeAskColor = Color.FromArgb(50, value.R, value.G, value.B);
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidsBackGround), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.BidBGColorDescription), Order = 230)]
	public CrossColor BidsBackGround
	{
		get => _bidBackGround.Convert();
		set => _bidBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AsksBackGround), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.AskBGColorDescription), Order = 240)]
	public CrossColor AsksBackGround
	{
		get => _askBackGround.Convert();
		set => _askBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BestBidBackGround), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.BestBidBGColorDescription), Order = 250)]
	public CrossColor BestBidBackGround
	{
		get => _bestBidBackGround.Convert();
		set => _bestBidBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BestAskBackGround), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.BestAskBGColorDescription), Order = 260)]
	public CrossColor BestAskBackGround
	{
		get => _bestAskBackGround.Convert();
		set => _bestAskBackGround = value.Convert();
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filters), GroupName = nameof(Strings.LevelsMode), Description = nameof(Strings.FiltersMaxVolumeDecimalColorDescription), Order = 270)]
	[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Reuse)]
	public ObservableCollection<FilterColor> FilterColors { get; set; } = new();

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskColor), GroupName = nameof(Strings.CumulativeMode), Description = nameof(Strings.CumulativeModeAskColorDescription), Order = 280)]
	public Color CumulativeAskColor { get; set; } = Color.FromArgb(255, 100, 100);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidColor), GroupName = nameof(Strings.CumulativeMode), Description = nameof(Strings.CumulativeModeBidColorDescription), Order = 285)]
	public Color CumulativeBidColor { get; set; } = Color.FromArgb(100, 255, 100);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowCumulativeValues), GroupName = nameof(Strings.Other), Description = nameof(Strings.ShowCumulativeValuesDescription), Order = 300)]
	public bool ShowCumulativeValues { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDepthChanges), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.ShowDepthChangesDescription), Order = 320)]
	public bool ShowDepthChanges
	{
		get => _showDepthChanges;
		set
		{
			if (_showDepthChanges == value)
				return;

			_showDepthChanges = value;
			ResetDepthChanges();
			RedrawChart(_emptyRedrawArg);
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.DepthChangesDisplayModeDescription), Order = 325)]
	public DepthChangesMode DepthChangesDisplayMode
	{
		get => _depthChangesMode;
		set
		{
			if (_depthChangesMode == value)
				return;

			_depthChangesMode = value;
			RedrawChart(_emptyRedrawArg);
		}
	}

	[Range(0, int.MaxValue)]
	[PostValueMode(PostValueModes.Delayed, DelayMilliseconds = 500)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.DepthChangesFilterDescription), Order = 330)]
	public Filter DepthChangesFilter
	{
		get => _depthChangesFilter;
		set
		{
			if (_depthChangesFilter == value)
				return;

			if (_depthChangesFilter is not null)
				_depthChangesFilter.PropertyChanged -= DepthChangesFilterPropertyChanged;

			_depthChangesFilter = value;

			if (_depthChangesFilter is not null)
				_depthChangesFilter.PropertyChanged += DepthChangesFilterPropertyChanged;

			RedrawChart(_emptyRedrawArg);
		}
	}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Pulling), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.PullingDescription), Order = 340)]
	public Color PullingColor
	{
		get => _pullingColor;
		set => _pullingColor = value;
	}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Stacking), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.StackingDescription), Order = 350)]
	public Color StackingColor
	{
		get => _stackingColor;
		set => _stackingColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), GroupName = nameof(Strings.PullingAndStacking), Description = nameof(Strings.DepthChangesTextColorDescription), Order = 360)]
	public Color DepthChangesTextColor
	{
		get => _depthChangesTextColor;
		set => _depthChangesTextColor = value;
	}

	[Range(0, 1000)]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomPriceLevelsHeight), GroupName = nameof(Strings.Other), Description = nameof(Strings.CustomPriceLevelsHeightDescription), Order = 310)]
	public int PriceLevelsHeight
	{
		get => _priceLevelsHeight;
		set => _priceLevelsHeight = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseScale), GroupName = nameof(Strings.Scale), Description = nameof(Strings.UseScaleDescription), Order = 400)]
	public bool UseScale
	{
		get => _upScale.ScaleIt;
		set => _upScale.ScaleIt = _downScale.ScaleIt = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomScale), GroupName = nameof(Strings.Scale), Description = nameof(Strings.ElementScaleValueDescription), Order = 410)]
	[Range(0, 1000)]
	public int Scale
	{
		get => _scale;
		set
		{
			_scale = value;
			_upScale.Clear();
			_downScale.Clear();
		}
	}

	#endregion

	#region ctor

	public DOM()
		: base(true)
	{
		DrawAbovePrice = true;
		DenyToChangePanel = true;
		_upScale.IsHidden = _downScale.IsHidden = true;
		_upScale.ShowCurrentValue = _downScale.ShowCurrentValue = false;
		_upScale.Color = _downScale.Color = Color.Transparent.Convert();

		DataSeries[0] = _upScale;
		DataSeries.Add(_downScale);

		IgnoreHistoryScale = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Final);

		UseAutoSize = true;

		ProportionVolume = 100;
		Width = 200;
		RightToLeft = true;

		BidRows = Color.FromArgb(153, 0, 128, 0).Convert();
		TextColor = Color.White.Convert();
		AskRows = Color.FromArgb(153, 255, 0, 0).Convert();

		ShowCumulativeValues = true;
		Scale = 20;

		DepthChangesFilter.PropertyChanged += DepthChangesFilterPropertyChanged;
		FilterColors.CollectionChanged += FiltersChanged;
	}

	#endregion

	#region Protected methods

	protected override void OnDispose()
	{
		_asks?.Clear();
		_bids?.Clear();
		_depthChangeBaseline.Clear();
		_depthChanges.Clear();
	}
	
	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			lock (_locker)
			{
				_cumulativeAsk = new SortedList<decimal, decimal>();
				_cumulativeBid = new SortedList<decimal, decimal>();
				_visibleVolumeCache = null;

				_asks = new SortedDictionary<decimal, MarketDataArg>();
				_bids = new SortedDictionary<decimal, MarketDataArg>();
				_lastDepthChangesBar = -1;
				_depthChangeBaseline.Clear();
				_depthChanges.Clear();
				_cachedMinAsk = _cachedMaxAsk = _cachedMaxBid = _cachedMinBid = null;

				DataSeries.ForEach(x => x.Clear());

				var depths = MarketDepthInfo.GetMarketDepthSnapshot();

				foreach (var depth in depths)
				{
					try
					{
						if (depth.DataType == MarketDataType.Ask)
							_asks.Add(depth.Price, depth);
						else
							_bids.Add(depth.Price, depth);

						if (IsDepthChangesEnabled)
							_depthChangeBaseline[(depth.Price, depth.DataType)] = depth;
					}
					catch (ArgumentException)
					{
						// catch duplicates in snapshot
					}
				}

				if (TotalDepthCount == 0)
				{
					_maxPrice = _minPrice = GetCandle(CurrentBar - 1).Close;
					return;
				}

				ResetColors();

				var anchor = _bids.Count > 0
					? MaxBid
					: (_asks.Count > 0 ? MinAsk : GetCandle(CurrentBar - 1).Close);
				_maxPrice = Math.Min(MaxDepthPrice, anchor * 1.3m);
				_minPrice = Math.Max(MinDepthPrice, anchor * 0.7m);

				var maxLevel = FindMaxVolume();
				_maxVolume = new VolumeInfo
				{
					Price = maxLevel.Price,
					Volume = maxLevel.Volume
				};

				if (VisualMode is not Mode.Common)
				{
					var sum = 0m;

					foreach (var (price, level) in _asks)
					{
						sum += level.Volume;
						_cumulativeAsk[price] = sum;
					}

					sum = 0m;

					// Accumulate from best bid (highest price) down
					foreach (var (price, level) in _bids.Reverse())
					{
						sum += level.Volume;
						_cumulativeBid[price] = sum;
					}
				}
			}

			return;
		}

		if (IsDepthChangesEnabled && bar != _lastDepthChangesBar)
		{
			lock (_locker)
			{
				if (bar != _lastDepthChangesBar)
				{
					ResetDepthChanges();
					_lastDepthChangesBar = bar;
				}
			}
		}

		if (UseScale)
		{
			_upScale[CurrentBar - 2] = 0;
			_downScale[CurrentBar - 2] = 0;

			if (_maxPrice != 0)
				_upScale[CurrentBar - 1] = _maxPrice + InstrumentInfo.TickSize * (_scale + 3);

			if (_minPrice != 0)
				_downScale[CurrentBar - 1] = _minPrice - InstrumentInfo.TickSize * (_scale + 3);
		}
	}

	private (decimal Price, decimal Volume) FindMaxVolume(decimal? minPrice = null, decimal? maxPrice = null)
	{
		var maxVolume = 0m;
		var maxVolumePrice = 0m;

		foreach (var (price, depth) in _asks)
		{
			if (price < minPrice)
				continue;

			if (price > maxPrice)
				break;

			if (depth.Volume > maxVolume)
			{
				maxVolume = depth.Volume;
				maxVolumePrice = price;
			}
		}

		foreach (var (price, depth) in _bids)
		{
			if (price < minPrice)
				continue;

			if (price > maxPrice)
				break;

			if (depth.Volume > maxVolume)
			{
				maxVolume = depth.Volume;
				maxVolumePrice = price;
			}
		}

		return (maxVolumePrice, maxVolume);
	}

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		var chartInfo = ChartInfo;
		var instrumentInfo = InstrumentInfo;

		if (chartInfo is null || instrumentInfo is null)
			return;

		if (chartInfo.PriceChartContainer.TotalBars == -1)
			return;

		var xOfLastBar = ChartInfo.PriceChartContainer.GetXByBar(CurrentBar - 1);

		if (xOfLastBar > ChartInfo.PriceChartContainer.Region.Right)
			return;

		if (CurrentBar <= 0)
			return;

		// UI calculations - don't require lock
		var height = (int)Math.Floor(chartInfo.PriceChartContainer.PriceRowHeight) - 1;

		if (PriceLevelsHeight != 0)
			height = PriceLevelsHeight - 2;

		height = height < 1 ? 1 : height;

		if (_lastRenderedHeight != height && _font.Size < ChartInfo.PriceAxisFont.Size && height >= _minFontHeight
		    || _lastRenderedHeight is 0 || _fontHeight > height)
		{
			SetTextSize(context, height);
			_lastRenderedHeight = height;
		}

		var maxVisiblePrice = chartInfo.PriceChartContainer.High;
		var minVisiblePrice = chartInfo.PriceChartContainer.Low;

		decimal currentPrice;

		try
		{
			currentPrice = GetCandle(CurrentBar - 1).Close;
		}
		catch (Exception e)
		{
			this.LogDebug("Chart does not contains bars", e);
			return;
		}

		var currentPriceY = chartInfo.GetYByPrice(currentPrice);

		// Single lock for all data operations
		lock (_locker)
		{
			if (TotalDepthCount == 0 && !HasVisibleDepthChanges)
				return;

			var maxVolume = _maxVolume.Volume;

			if (VisualMode is not Mode.Common)
				DrawCumulative(context);

			if (VisualMode is not Mode.Cumulative)
			{
				if (UseAutoSize)
					maxVolume = GetMaxVisibleVolume(minVisiblePrice, maxVisiblePrice);
				else
					maxVolume = ProportionVolume;

				var levelWidthKoeff = Width / (maxVolume == 0 ? 1 : maxVolume);
				var maxDepthChange = GetMaxVisibleDepthChange(minVisiblePrice, maxVisiblePrice);
				var depthChangeMaxWidth = DepthChangesDisplayMode == DepthChangesMode.Both
					? Width / 2m
					: Width;
				var depthChangeWidthKoeff = depthChangeMaxWidth / (maxDepthChange == 0 ? 1 : maxDepthChange);

				DrawBackGround(context, currentPriceY);

				var stringRects = new List<(string Text, Rectangle Rect)>();
				MultiColorsHistogramRender asksFiltersHistogram = null;
				MultiColorsHistogramRender bidsFiltersHistogram = null;

				var levelHeight = PriceLevelsHeight == 0
					? Math.Max(1, Math.Abs(chartInfo.GetYByPrice(currentPrice) - chartInfo.GetYByPrice(currentPrice - instrumentInfo.TickSize)) - 1)
					: Math.Max(1, PriceLevelsHeight - 1);

				if (_asks.Count > 0 || HasDepthChanges(MarketDataType.Ask))
				{
					_asksHistogram = new HistogramRender(!RightToLeft);
					var minAsk = _asks.Count > 0
						? MinAsk
						: GetDepthChangeEdgePrice(MarketDataType.Ask, true);

					foreach (var priceDepth in GetVisibleRenderDepths(_asks, MarketDataType.Ask, minVisiblePrice, maxVisiblePrice))
					{
						int y;

						if (PriceLevelsHeight == 0)
							y = chartInfo.GetYByPrice(priceDepth.Price, true);
						else
						{
							var diff = (priceDepth.Price - minAsk) / instrumentInfo.TickSize;
							y = currentPriceY - levelHeight * ((int)diff + 1) - (int)diff - 15;
						}

						var currentLevelHeight = PriceLevelsHeight == 0
							? Math.Max(1, chartInfo.GetYByPrice(priceDepth.Price - instrumentInfo.TickSize, true) - y)
							: levelHeight;

						if (y + currentLevelHeight < chartInfo.Region.Top)
							continue;

						var width = GetLevelWidth(priceDepth.Volume, levelWidthKoeff);

						if (!UseAutoSize)
							width = Math.Min(width, Width);

						if (priceDepth.Volume != 0 && _asks.ContainsKey(priceDepth.Price) && priceDepth.Price == minAsk)
						{
							var bestRect = new Rectangle(new Point(chartInfo.Region.Width - Width, y),
								new Size(Width, currentLevelHeight));
							context.FillRectangle(_bestAskBackGround, bestRect);
						}

						var x1 = RightToLeft
							? chartInfo.Region.Width - width
							: chartInfo.Region.Width - Width;

						var x2 = x1 + width;
						var botY = y + currentLevelHeight;

						var rect = RightToLeft
							? new Rectangle(chartInfo.Region.Width - width, y, width, currentLevelHeight)
							: new Rectangle(new Point(chartInfo.Region.Width - Width, y), new Size(width, currentLevelHeight));

						var fillColor = _filteredColors.GetValueOrDefault(priceDepth.Price, _askColor);
						var depthChanges = IsDepthChangesEnabled
							? GetDepthChange(priceDepth.Price, priceDepth.DataType)
							: null;

						if (_fontHeight >= _heightToSolidMode)
						{
							context.FillRectangle(fillColor, rect);
							DrawDepthChanges(context, depthChanges, y, currentLevelHeight, depthChangeWidthKoeff);

							if (_fontHeight > _minFontHeight)
							{
								if (priceDepth.Volume != 0)
								{
									var renderText = chartInfo.TryGetMinimizedVolumeString(priceDepth.Volume, priceDepth.Price);
									var textWidth = context.MeasureString(renderText, _font).Width + 5;

									var textRect = RightToLeft
										? new Rectangle(new Point(chartInfo.Region.Width - textWidth, y), new Size(textWidth, currentLevelHeight))
										: new Rectangle(new Point(chartInfo.Region.Width - Width, y), new Size(textWidth, currentLevelHeight));

									stringRects.Add((renderText, textRect));
								}
							}
						}
						else
						{
							_asksHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y - 1);
							DrawDepthChanges(context, depthChanges, y, currentLevelHeight, depthChangeWidthKoeff);

							if (_filteredColors.TryGetValue(priceDepth.Price, out var filteredColor))
							{
								asksFiltersHistogram ??= new MultiColorsHistogramRender(_askColor, !RightToLeft);
								asksFiltersHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y - 1, filteredColor);
							}
						}
					}
				}

				if (_bids.Count > 0 || HasDepthChanges(MarketDataType.Bid))
				{
					_bidsHistogram = new HistogramRender(!RightToLeft);
					var minAsk = _asks.Count > 0
						? MinAsk
						: GetDepthChangeEdgePrice(MarketDataType.Ask, true);
					var maxBid = _bids.Count > 0
						? MaxBid
						: GetDepthChangeEdgePrice(MarketDataType.Bid, false);
					var spread = 0;

					if (_asks.Count > 0)
						spread = (int)((minAsk - maxBid) / instrumentInfo.TickSize);

					foreach (var priceDepth in GetVisibleRenderDepths(_bids, MarketDataType.Bid, minVisiblePrice, maxVisiblePrice))
					{
						int y;

						if (PriceLevelsHeight == 0)
							y = chartInfo.GetYByPrice(priceDepth.Price, true);
						else
						{
							var diff = (maxBid - priceDepth.Price) / instrumentInfo.TickSize;
							y = currentPriceY + levelHeight * ((int)diff + spread - 1) + (int)diff - 15;
						}

						var currentLevelHeight = PriceLevelsHeight == 0
							? Math.Max(1, chartInfo.GetYByPrice(priceDepth.Price - instrumentInfo.TickSize, true) - y)
							: levelHeight;

						if (y > chartInfo.Region.Bottom)
							continue;

						var width = GetLevelWidth(priceDepth.Volume, levelWidthKoeff);

						if (!UseAutoSize)
							width = Math.Min(width, Width);

						if (priceDepth.Volume != 0 && _bids.ContainsKey(priceDepth.Price) && priceDepth.Price == maxBid)
						{
							var bestRect = new Rectangle(new Point(chartInfo.Region.Width - Width, y),
								new Size(Width, currentLevelHeight));
							context.FillRectangle(_bestBidBackGround, bestRect);
						}

						var x1 = RightToLeft
							? chartInfo.Region.Width - width
							: chartInfo.Region.Width - Width;

						var x2 = x1 + width;
						var botY = y + currentLevelHeight;

						var rect = RightToLeft
							? new Rectangle(chartInfo.Region.Width - width, y, width, currentLevelHeight)
							: new Rectangle(new Point(chartInfo.Region.Width - Width, y), new Size(width, currentLevelHeight));

						var fillColor = _filteredColors.GetValueOrDefault(priceDepth.Price, _bidColor);
						var depthChanges = IsDepthChangesEnabled
							? GetDepthChange(priceDepth.Price, priceDepth.DataType)
							: null;

						if (_fontHeight >= _heightToSolidMode)
						{
							if (_fontHeight > _minFontHeight)
							{
								if (priceDepth.Volume != 0)
								{
									var renderText = chartInfo.TryGetMinimizedVolumeString(priceDepth.Volume, priceDepth.Price);
									var textWidth = context.MeasureString(renderText, _font).Width + 5;

									var textRect = RightToLeft
										? new Rectangle(new Point(chartInfo.Region.Width - textWidth, y), new Size(textWidth, currentLevelHeight))
										: new Rectangle(new Point(chartInfo.Region.Width - Width, y), new Size(textWidth, currentLevelHeight));

									stringRects.Add((renderText, textRect));
								}
							}

							context.FillRectangle(fillColor, rect);
							DrawDepthChanges(context, depthChanges, y, currentLevelHeight, depthChangeWidthKoeff);
						}
						else
						{
							_bidsHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y - 1);
							DrawDepthChanges(context, depthChanges, y, currentLevelHeight, depthChangeWidthKoeff);

							if (_filteredColors.TryGetValue(priceDepth.Price, out var filteredColor))
							{
								bidsFiltersHistogram ??= new MultiColorsHistogramRender(_bidColor, !RightToLeft);
								bidsFiltersHistogram.AddPrice(RightToLeft ? x2 : x1, RightToLeft ? x1 : x2, botY, y - 1, filteredColor);
							}
						}
					}
				}

				if (_fontHeight < _heightToSolidMode)
				{
					_asksHistogram?.Draw(context, _askColor, true);
					_bidsHistogram?.Draw(context, _bidColor, true);
					asksFiltersHistogram?.Draw(context, true);
					bidsFiltersHistogram?.Draw(context, true);
				}

				foreach (var (text, rect) in stringRects)
				{
					context.DrawString(text,
						_font,
						_textColor,
						rect,
						RightToLeft ? _stringRightFormat : _stringLeftFormat);
				}
			}

			if (ShowCumulativeValues)
				DrawCumulativeValues(context);
		}
	}

	protected override void OnBestBidAskChanged(MarketDataArg depth)
	{
		// MinAsk and MaxBid are now calculated automatically with lazy invalidation
		RedrawChart(_emptyRedrawArg);
	}

	protected override void MarketDepthsChanged(IEnumerable<MarketDataArg> depths)
	{
		var hasChanges = false;

		lock (_locker)
		{
			foreach (var depth in depths)
			{
				ProcessMarketDepthChange(depth);
				hasChanges = true;
			}
		}

		if (hasChanges)
			RedrawChart(_emptyRedrawArg);
	}

	protected override void MarketDepthChanged(MarketDataArg depth)
	{
		lock (_locker)
			ProcessMarketDepthChange(depth);

		RedrawChart(_emptyRedrawArg);
	}

	protected override void OnApplyDefaultColors()
	{
		if (ChartInfo is null)
			return;

		_bidColor = ChartInfo.ColorsStore.UpCandleColor;
		_askColor = ChartInfo.ColorsStore.DownCandleColor;
		_textColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor;

		var cumulativeBid = Color.FromArgb(
			_bidColor.R / 4 * 3,
			_bidColor.G / 4 * 3,
			_bidColor.B / 4 * 3).SetTransparency(0.4m);

		var cumulativeAsk = Color.FromArgb(
			_askColor.R / 4 * 3,
			_askColor.G / 4 * 3,
			_askColor.B / 4 * 3).SetTransparency(0.4m);

		CumulativeBidColor = cumulativeBid;
		CumulativeAskColor = cumulativeAsk;
	}
	
	#endregion

	#region Private methods

    private void DrawCumulativeValues(RenderContext g)
	{
		var maxWidth = (int)Math.Round(ChartInfo.Region.Width * 0.2m);
		var totalVolume = MarketDepthInfo.CumulativeDomAsks + MarketDepthInfo.CumulativeDomBids;

		if (totalVolume == 0)
			return;

		var height = g.MeasureString("12", _cumulativeFont).Height;
		var askRowWidth = (int)Math.Round(MarketDepthInfo.CumulativeDomAsks * (maxWidth - 1) / totalVolume);
		var bidRowWidth = maxWidth - askRowWidth;
		var yRect = ChartInfo.Region.Bottom - height;
		var bidStr = ChartInfo.TryGetMinimizedVolumeString(MarketDepthInfo.CumulativeDomBids);
        var askStr = ChartInfo.TryGetMinimizedVolumeString(MarketDepthInfo.CumulativeDomAsks);

		var askWidth = g.MeasureString(askStr, _cumulativeFont).Width;
		var bidWidth = g.MeasureString(bidStr, _cumulativeFont).Width;

		if (askWidth > askRowWidth && MarketDepthInfo.CumulativeDomAsks != 0)
		{
			askRowWidth = askWidth;
			maxWidth = (int)Math.Round(Math.Min(ChartInfo.Region.Width * 0.3m, totalVolume * askRowWidth / MarketDepthInfo.CumulativeDomAsks + 1));
			bidRowWidth = maxWidth - askRowWidth;
		}

		if (bidWidth > bidRowWidth && MarketDepthInfo.CumulativeDomBids != 0)
		{
			bidRowWidth = bidWidth;
			maxWidth = (int)Math.Round(Math.Min(ChartInfo.Region.Width * 0.3m, totalVolume * bidRowWidth / MarketDepthInfo.CumulativeDomBids + 1));
			askRowWidth = maxWidth - bidRowWidth;
		}

		if (askRowWidth > 0)
		{
			var askRect = new Rectangle(new Point(ChartInfo.Region.Width - askRowWidth, yRect),
				new Size(askRowWidth, height));
			g.FillRectangle(_volumeAskColor, askRect);
			g.DrawString(askStr, _cumulativeFont, _bidColor, askRect, _stringLeftFormat);
		}

		if (bidRowWidth > 0)
		{
			var bidRect = new Rectangle(new Point(ChartInfo.Region.Width - maxWidth, yRect),
				new Size(bidRowWidth, height));
			g.FillRectangle(_volumeBidColor, bidRect);
			g.DrawString(bidStr, _cumulativeFont, _askColor, bidRect, _stringRightFormat);
		}
	}

	private void DrawCumulative(RenderContext context)
	{
		_cumulativeHistogram = new MultiColorsHistogramRender(CumulativeAskColor, !RightToLeft);

		var hasAsks = _cumulativeAsk.Count > 0;
		var hasBids = _cumulativeBid.Count > 0;

		var maxVolume = Math.Max(
			hasAsks ? _cumulativeAsk.Values[^1] : 0m,
			hasBids ? _cumulativeBid.Values[0] : 0m);

		if (maxVolume <= 0)
			return;

		var startX = RightToLeft ? Container.Region.Width : Container.Region.Width - Width;

		if (hasAsks)
		{
			var curIdx = 0;
			var lastIdx = _cumulativeAsk.Count - 1;

			foreach (var (price, volume) in _cumulativeAsk)
			{
				var levelWidth = (int)(Width * volume / maxVolume);

				var x = RightToLeft
					? Container.Region.Width - levelWidth
					: Container.Region.Width - Width + levelWidth;

				var y1 = ChartInfo.GetYByPrice(price - InstrumentInfo.TickSize);

				var y2 = curIdx == lastIdx
					? ChartInfo.GetYByPrice(price)
					: ChartInfo.GetYByPrice(_cumulativeAsk.Keys[curIdx + 1] - InstrumentInfo.TickSize);

				_cumulativeHistogram.AddPrice(startX, x, y1, y2, CumulativeAskColor);
				curIdx++;
			}
		}

		if (hasBids)
		{
			var curIdx = 0;
			var lastIdx = _cumulativeBid.Count - 1;

			foreach (var (price, volume) in _cumulativeBid)
			{
				var levelWidth = (int)(Width * volume / maxVolume);

				var x = RightToLeft
					? Container.Region.Width - levelWidth
					: Container.Region.Width - Width + levelWidth;

				var y1 = ChartInfo.GetYByPrice(price - InstrumentInfo.TickSize);

				var y2 = curIdx == lastIdx
					? ChartInfo.GetYByPrice(price)
					: ChartInfo.GetYByPrice(_cumulativeBid.Keys[curIdx + 1] - InstrumentInfo.TickSize);

				_cumulativeHistogram.AddPrice(startX, x, y1, y2, CumulativeBidColor);
				curIdx++;
			}
		}

		_cumulativeHistogram.Draw(context, true);

		if (VisualMode is Mode.Cumulative && _fontHeight >= _minFontHeight)
		{
			foreach (var (price, volume) in _cumulativeBid)
				DrawText(context, price, volume);

			foreach (var (price, volume) in _cumulativeAsk)
				DrawText(context, price, volume);
		}
	}

	private void DrawText(RenderContext context, decimal price, decimal volume)
	{
		var y = ChartInfo.GetYByPrice(price);
		var renderText = ChartInfo.TryGetMinimizedVolumeString(volume, price);
		var textWidth = context.MeasureString(renderText, _font).Width + 5;
		var rowHeight = (int)ChartInfo.PriceChartContainer.PriceRowHeight;

		var textRect = RightToLeft
			? new Rectangle(ChartInfo.Region.Width - textWidth, y, textWidth, rowHeight)
			: new Rectangle(ChartInfo.Region.Width - Width, y, textWidth, rowHeight);

		context.DrawString(renderText, _font, _textColor, textRect, RightToLeft ? _stringRightFormat : _stringLeftFormat);
	}

	private void DrawDepthChanges(RenderContext context, DepthChangeInfo depthChange, int y, int height, decimal widthKoeff)
	{
		if (depthChange is null || _fontHeight <= _minFontHeight)
			return;

		if (DepthChangesDisplayMode == DepthChangesMode.Both)
		{
			DrawDepthChangeBoth(context, depthChange, y, height, widthKoeff);
			return;
		}

		DrawDepthChangeValue(context, GetDepthChangeValue(depthChange), y, height, widthKoeff);
	}

	private void DrawDepthChangeBoth(RenderContext context, DepthChangeInfo depthChange, int y, int height, decimal widthKoeff)
	{
		var columnX = Math.Max(0, ChartInfo.Region.Width - Width * 2);
		var halfWidth = Math.Max(1, Width / 2);
		var centerX = columnX + halfWidth;

		DrawDepthChangeValue(context,
			depthChange.PulledVolume == 0 ? 0 : -depthChange.PulledVolume,
			y,
			height,
			widthKoeff,
			centerX - halfWidth,
			halfWidth,
			true);

		DrawDepthChangeValue(context,
			depthChange.StackedVolume,
			y,
			height,
			widthKoeff,
			centerX,
			halfWidth,
			false);
	}

	private void DrawDepthChangeValue(RenderContext context, decimal value, int y, int height, decimal widthKoeff)
	{
		DrawDepthChangeValue(context,
			value,
			y,
			height,
			widthKoeff,
			Math.Max(0, ChartInfo.Region.Width - Width * 2),
			Width,
			true);
	}

	private void DrawDepthChangeValue(RenderContext context, decimal value, int y, int height, decimal widthKoeff, int columnX, int columnWidth, bool alignRight)
	{
		var absValue = Math.Abs(value);
		var filter = DepthChangesFilter.Enabled ? DepthChangesFilter.Value : 0m;

		if (absValue <= filter)
			return;

		var text = value > 0
			? $"+{ChartInfo.TryGetMinimizedVolumeString(value)}"
			: $"-{ChartInfo.TryGetMinimizedVolumeString(absValue)}";

		var color = value > 0 ? StackingColor : PullingColor;
		var width = Math.Max(1, GetLevelWidth(absValue, widthKoeff));
		var x = alignRight
			? columnX + columnWidth - width
			: columnX;
		var textRect = new Rectangle(columnX, y, columnWidth, height);

		var rect = new Rectangle(x, y, width, height);

		context.FillRectangle(color, rect);
		context.DrawString(text, _font, DepthChangesTextColor, textRect, alignRight ? _stringRightFormat : _stringLeftFormat);
	}

	private DepthChangeInfo GetDepthChange(decimal price, MarketDataType dataType)
	{
		_depthChanges.TryGetValue((price, dataType), out var result);

		return result;
	}

	private void ProcessDepthChange(MarketDataArg depth)
	{
		if (!IsDepthChangesEnabled)
			return;

		var key = (depth.Price, depth.DataType);
		var oppositeKey = (depth.Price, depth.DataType == MarketDataType.Ask ? MarketDataType.Bid : MarketDataType.Ask);

		if (depth.Volume != 0)
		{
			_depthChangeBaseline.Remove(oppositeKey);
			_depthChanges.Remove(oppositeKey);
		}

		var previousVolume = _depthChangeBaseline.TryGetValue(key, out var previous)
			? previous.Volume
			: 0m;
		var delta = depth.Volume - previousVolume;

		if (delta != 0)
		{
			if (!_depthChanges.TryGetValue(key, out var changes))
			{
				_depthChanges[key] = changes = new DepthChangeInfo
				{
					Price = depth.Price,
					DataType = depth.DataType
				};
			}

			if (delta > 0)
				changes.StackedVolume += delta;
			else
				changes.PulledVolume += Math.Abs(delta);
		}

		if (depth.Volume == 0)
			_depthChangeBaseline.Remove(key);
		else
			_depthChangeBaseline[key] = depth;
	}

	private void ResetDepthChanges()
	{
		_depthChangeBaseline.Clear();
		_depthChanges.Clear();

		foreach (var depth in _asks.Values)
			_depthChangeBaseline[(depth.Price, depth.DataType)] = depth;

		foreach (var depth in _bids.Values)
			_depthChangeBaseline[(depth.Price, depth.DataType)] = depth;
	}

	private bool IsDepthChangesEnabled => _showDepthChanges;

	private bool HasDepthChanges(MarketDataType dataType)
	{
		return IsDepthChangesEnabled && _depthChanges.Values.Any(x => x.DataType == dataType && HasDepthChangeValue(x));
	}

	private decimal GetMaxVisibleDepthChange(decimal minPrice, decimal maxPrice)
	{
		if (!IsDepthChangesEnabled)
			return 0;

		return _depthChanges.Values
			.Where(x => IsInChart(x.Price, maxPrice, minPrice))
			.Select(GetDepthChangeMagnitude)
			.DefaultIfEmpty(0)
			.Max();
	}

	private decimal GetDepthChangeValue(DepthChangeInfo depthChange)
	{
		return DepthChangesDisplayMode switch
		{
			DepthChangesMode.StackedOnly => depthChange.StackedVolume,
			DepthChangesMode.PulledOnly => -depthChange.PulledVolume,
			_ => depthChange.NetVolume
		};
	}

	private decimal GetDepthChangeMagnitude(DepthChangeInfo depthChange)
	{
		return DepthChangesDisplayMode switch
		{
			DepthChangesMode.StackedOnly => depthChange.StackedVolume,
			DepthChangesMode.PulledOnly => depthChange.PulledVolume,
			DepthChangesMode.Both => Math.Max(depthChange.StackedVolume, depthChange.PulledVolume),
			_ => Math.Abs(depthChange.NetVolume)
		};
	}

	private bool HasDepthChangeValue(DepthChangeInfo depthChange)
	{
		return GetDepthChangeMagnitude(depthChange) != 0;
	}

	private decimal GetDepthChangeEdgePrice(MarketDataType dataType, bool min)
	{
		var prices = _depthChanges.Values
			.Where(x => x.DataType == dataType)
			.Select(x => x.Price);

		return min
			? prices.DefaultIfEmpty(0).Min()
			: prices.DefaultIfEmpty(0).Max();
	}

	private IEnumerable<MarketDataArg> GetRenderDepths(SortedDictionary<decimal, MarketDataArg> depths, MarketDataType dataType)
	{
		if (!IsDepthChangesEnabled)
			return depths.Values;

		var currentDepths = depths.Values.Select(x => new MarketDataArg
		{
			Price = x.Price,
			OriginPrice = x.OriginPrice,
			Volume = x.Volume,
			Time = x.Time,
			Direction = x.Direction,
			DataType = x.DataType,
			OpenInterest = x.OpenInterest,
			AggressorExchangeOrderId = x.AggressorExchangeOrderId,
			ExchangeOrderId = x.ExchangeOrderId
		});

		var staleChanges = _depthChanges.Values
			.Where(x => x.DataType == dataType && HasDepthChangeValue(x) && !HasCurrentDepth(x.Price))
			.Select(x => new MarketDataArg
			{
				Price = x.Price,
				OriginPrice = x.Price,
				Volume = 0,
				DataType = x.DataType
			});

		var renderDepths = currentDepths.Concat(staleChanges);

		return renderDepths.OrderBy(x => x.Price);
	}

	private IEnumerable<MarketDataArg> GetVisibleRenderDepths(SortedDictionary<decimal, MarketDataArg> depths, MarketDataType dataType, decimal minPrice, decimal maxPrice)
	{
		foreach (var depth in GetRenderDepths(depths, dataType))
		{
			if (depth.Price < minPrice)
				continue;

			if (depth.Price > maxPrice)
				break;

			yield return depth;
		}
	}

	private bool HasCurrentDepth(decimal price)
	{
		return _asks.ContainsKey(price) || _bids.ContainsKey(price);
	}

	private void DepthChangesFilterPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		RedrawChart(_emptyRedrawArg);
	}

	private void DrawBackGround(RenderContext context, int priceY)
	{
		var minAsk = MinAsk;
		var maxBid = MaxBid;

		if (PriceLevelsHeight == 0)
		{
			var y2 = ChartInfo.GetYByPrice(minAsk - InstrumentInfo.TickSize);
			var y3 = ChartInfo.GetYByPrice(maxBid);
			var y4 = ChartInfo.Region.Height;

			var fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, 0), new Size(Width, y2));

			context.FillRectangle(_askBackGround, fullRect);

			fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y3),
				new Size(Width, y4 - y3));

			context.FillRectangle(_bidBackGround, fullRect);
		}
		else
		{
			var spread = (int)((minAsk - maxBid) / InstrumentInfo.TickSize);
			var y = priceY - 15;

			var fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, 0), new Size(Width, y));
			context.FillRectangle(_askBackGround, fullRect);

			y = priceY + (PriceLevelsHeight - 1) * (spread - 1) - 15;
			fullRect = new Rectangle(new Point(ChartInfo.Region.Width - Width, y), new Size(Width, ChartInfo.Region.Height - y));
			context.FillRectangle(_bidBackGround, fullRect);
		}
	}

	private void FiltersChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.NewItems != null)
		{
			foreach (var item in e.NewItems)
				((INotifyPropertyChanged)item).PropertyChanged += ItemPropertyChanged;
		}

		if (e.OldItems != null)
		{
			foreach (var item in e.OldItems)
				((INotifyPropertyChanged)item).PropertyChanged -= ItemPropertyChanged;
		}

		lock (_locker)
		{
			_sortedFilters = new List<FilterColor>(FilterColors.OrderByDescending(x => x.Value));
			ResetColors();
		}
	}

	private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		lock (_locker)
		{
			_sortedFilters = new List<FilterColor>(FilterColors.OrderByDescending(x => x.Value));
			ResetColors();
		}
	}

	private void ResetColors()
	{
		_filteredColors.Clear();
		ApplyFilters(_asks.Values);
		ApplyFilters(_bids.Values);
	}

	private void ProcessMarketDepthChange(MarketDataArg depth)
	{
		var isCumulative = VisualMode is not Mode.Common;
		var isAsk = depth.DataType == MarketDataType.Ask;
		var list = isAsk ? _asks : _bids;

		ProcessDepthChange(depth);

		InvalidateMaxVisibleVolumeCache(depth.Price, depth.Volume);
		_filteredColors.Remove(depth.Price);

		if (depth.Volume != 0)
		{
			// Update min/max cache when adding
			if (isAsk)
			{
				if (depth.Price < _cachedMinAsk)
					_cachedMinAsk = depth.Price;

				if (depth.Price > _cachedMaxAsk)
					_cachedMaxAsk = depth.Price;
			}
			else
			{
				if (depth.Price > _cachedMaxBid)
					_cachedMaxBid = depth.Price;

				if (depth.Price < _cachedMinBid)
					_cachedMinBid = depth.Price;
			}

			list[depth.Price] = depth;

			foreach (var filterColor in _sortedFilters)
			{
				if (depth.Volume < filterColor.Value)
					continue;

				_filteredColors[depth.Price] = filterColor.Color;
				break;
			}
		}
		else
		{
			// Invalidate cache when removing current min/max
			if (isAsk)
			{
				if (depth.Price == _cachedMinAsk)
					_cachedMinAsk = null;

				if (depth.Price == _cachedMaxAsk)
					_cachedMaxAsk = null;
			}
			else
			{
				if (depth.Price == _cachedMaxBid)
					_cachedMaxBid = null;

				if (depth.Price == _cachedMinBid)
					_cachedMinBid = null;
			}

			list.Remove(depth.Price);
		}

		if (TotalDepthCount == 0)
		{
			if (isCumulative)
			{
				_cumulativeAsk = new SortedList<decimal, decimal>();
				_cumulativeBid = new SortedList<decimal, decimal>();
			}

			return;
		}

		if (UseScale || isCumulative)
		{
			if (depth.Price >= _maxPrice || depth.Volume == 0)
			{
				if (depth.Price >= _maxPrice && depth.Volume != 0)
					_maxPrice = depth.Price;
				else if (depth.Price >= _maxPrice && depth.Volume == 0)
					_maxPrice = MaxDepthPrice;

				if (UseScale)
					_upScale[CurrentBar - 1] = _maxPrice + InstrumentInfo.TickSize * (_scale + 3);
			}

			if (depth.Price <= _minPrice || depth.Volume == 0)
			{
				if (depth.Price <= _minPrice && depth.Volume != 0)
					_minPrice = depth.Price;
				else if (depth.Price <= _minPrice && depth.Volume == 0)
					_minPrice = MinDepthPrice;

				if (UseScale)
					_downScale[CurrentBar - 1] = _minPrice - InstrumentInfo.TickSize * (_scale + 3);
			}
		}

		if (depth.Price == _maxVolume.Price)
		{
			if (depth.Volume >= _maxVolume.Volume)
				_maxVolume.Volume = depth.Volume;
			else
			{
				var maxLevel = FindMaxVolume();
				_maxVolume.Price = maxLevel.Price;
				_maxVolume.Volume = maxLevel.Volume;
			}
		}
		else
		{
			if (depth.Volume > _maxVolume.Volume)
			{
				_maxVolume.Price = depth.Price;
				_maxVolume.Volume = depth.Volume;
			}
		}

		if (isCumulative)
			UpdateCumulative(depth);
	}

	private void ApplyFilters(IEnumerable<MarketDataArg> depths)
	{
		foreach (var arg in depths)
		{
			foreach (var filterColor in _sortedFilters)
			{
				if (arg.Volume < filterColor.Value)
					continue;

				_filteredColors[arg.Price] = filterColor.Color;
				break;
			}
		}
	}

	private void SetTextSize(RenderContext context, int height)
	{
		var fontFamily = ChartInfo.PriceAxisFont.FontFamily;
		var maxFontSize = ChartInfo.PriceAxisFont.Size;

		if (_cachedFontFamily != fontFamily || _cachedMaxFontSize != maxFontSize)
		{
			_fontCache.Clear();
			_cachedFontFamily = fontFamily;
			_cachedMaxFontSize = maxFontSize;

			for (var i = (int)maxFontSize; i > 0; i--)
			{
				var font = new RenderFont(fontFamily, i);
				var size = context.MeasureString("12", font);

				_fontCache[i] = (font, size.Height);
			}
		}

		for (var i = (int)maxFontSize; i > 0; i--)
		{
			if (!_fontCache.TryGetValue(i, out var cached) || cached.Height >= height + 4)
				continue;

			_font = cached.Font;
			_fontHeight = cached.Height;
			return;
		}

		_font = new RenderFont(fontFamily, 0);
		_fontHeight = 0;
	}

	private void UpdateCumulative(MarketDataArg depth)
	{
		if (depth.DataType is MarketDataType.Ask)
			UpdateCumulativeAsk(depth);
		else
			UpdateCumulativeBid(depth);
	}

	private void UpdateCumulativeAsk(MarketDataArg depth)
	{
		var price = depth.Price;
		var hasLevel = depth.Volume > 0;

		var list = _cumulativeAsk;
		var existingIndex = list.IndexOfKey(price);

		var insertIndex = existingIndex >= 0 
			? existingIndex 
			: GetInsertIndex(list, price);
		
		var previousSum = insertIndex > 0 
			? list.Values[insertIndex - 1] 
			: 0m;
		
		var oldVolume = existingIndex >= 0 
			? list.Values[existingIndex] - previousSum
			: 0m;
		
		var newVolume = hasLevel 
			? depth.Volume 
			: 0m;

		var delta = newVolume - oldVolume;

		if (delta == 0m)
			return;

		if (existingIndex < 0)
		{
			if (!hasLevel)
				return;

			list[price] = previousSum;
			existingIndex = list.IndexOfKey(price);
		}

		for (var i = existingIndex; i < list.Count; i++)
		{
			var newSum = list.Values[i] + delta;
			list.SetValueAtIndex(i, newSum);
		}

		if (!hasLevel)
			list.RemoveAt(existingIndex);
	}

	private void UpdateCumulativeBid(MarketDataArg depth)
	{
		var price = depth.Price;
		var hasLevel = depth.Volume > 0;

		var list = _cumulativeBid;
		var existingIndex = list.IndexOfKey(price);

		if (!hasLevel && existingIndex < 0)
			return;

		var insertIndex = existingIndex >= 0 
			? existingIndex
			: GetInsertIndex(list, price);

		decimal nextSum;
		decimal oldVolume;

		if (existingIndex >= 0)
		{
			nextSum = existingIndex + 1 < list.Count 
				? list.Values[existingIndex + 1] 
				: 0m;

			oldVolume = list.Values[existingIndex] - nextSum;
		}
		else
		{
			nextSum = insertIndex < list.Count 
				? list.Values[insertIndex] 
				: 0m;

			oldVolume = 0m;
		}

		var newVolume = hasLevel 
			? depth.Volume
			: 0m;

		var delta = newVolume - oldVolume;

		if (delta == 0m)
			return;

		if (existingIndex < 0)
		{
			list[price] = nextSum;
			existingIndex = list.IndexOfKey(price);
		}

		for (var i = 0; i <= existingIndex; i++)
		{
			var newSum = list.Values[i] + delta;
			list.SetValueAtIndex(i, newSum);
		}

		if (!hasLevel)
			list.RemoveAt(existingIndex);
	}

	private bool IsInChart(decimal price, decimal high, decimal low)
	{
		return price <= high && price >= low;
	}

	private decimal GetMaxVisibleVolume(decimal minPrice, decimal maxPrice)
	{
		if (_visibleVolumeCache is { } cache && cache.MinPrice == minPrice && cache.MaxPrice == maxPrice)
			return cache.Volume;

		var (maxVolumePrice, maxVolume) = FindMaxVolume(minPrice, maxPrice);

		_visibleVolumeCache = (maxVolume, maxVolumePrice, minPrice, maxPrice);

		return maxVolume;
	}

	private void InvalidateMaxVisibleVolumeCache(decimal price, decimal newVolume)
	{
		if (_visibleVolumeCache is not { } cache)
			return;

		if (price < cache.MinPrice || price > cache.MaxPrice)
			return;

		if (newVolume > cache.Volume)
		{
			_visibleVolumeCache = (newVolume, price, cache.MinPrice, cache.MaxPrice);
			return;
		}

		if (price == cache.VolumePrice)
			_visibleVolumeCache = null;
	}

	private int GetLevelWidth(decimal curVolume, decimal levelWidthKoeff)
	{
		var width = Math.Floor(curVolume * levelWidthKoeff);

		return (int)Math.Min(Width, width);
	}

	private static int GetInsertIndex(SortedList<decimal, decimal> list, decimal price)
	{
		var keys = list.Keys;
		var left = 0;
		var right = keys.Count - 1;

		while (left <= right)
		{
			var mid = (left + right) >> 1;
			var midKey = keys[mid];

			if (midKey < price)
				left = mid + 1;
			else
				right = mid - 1;
		}

		return left;
	}

	#endregion
}

public class FilterColor : INotifyPropertyChanged
{
	#region Fields

	private Color _color = Color.LightBlue;
	private decimal _value;

	#endregion

	#region Properties

	public decimal Value
	{
		get => _value;
		set
		{
			_value = value;
			OnPropertyChanged();
		}
	}

	public Color Color
	{
		get => _color;
		set
		{
			_color = value;
			OnPropertyChanged();
		}
	}

	#endregion

	#region Events

	public event PropertyChangedEventHandler PropertyChanged;

	#endregion

	#region Protected methods

	protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	#endregion
}
