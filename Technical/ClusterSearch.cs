namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using static DynamicLevels;

	[Category("Clusters, Profiles, Levels")]
	[DisplayName("Cluster Search")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/365-cluster-search")]
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

		public enum CandleDirection
		{
			[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
			Bearish,

			[Display(ResourceType = typeof(Resources), Name = "Bullish")]
			Bullish,

			[Display(ResourceType = typeof(Resources), Name = "Any")]
			Any
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

		private readonly List<decimal> _alertPrices = new List<decimal>();

		private readonly List<Pair> _pairs = new List<Pair>();

		private readonly PriceSelectionDataSeries _renderDataSeries = new PriceSelectionDataSeries("Price");

		private int _barsRange;
		private CandleDirection _candleDirection;
		private Color _clusterPriceTransColor;

		private Color _clusterTransColor;
		private decimal _deltaFilter;
		private decimal _deltaImbalance;
		private bool _fixedSizes;
		private bool _isLastBar;
		private decimal _maxAverageTrade;
		private Filter _maxFilter = new Filter();
		private int _maxSize;
		private decimal _minAverageTrade;
		private Filter _minFilter = new Filter();
		private int _minSize;
		private bool _onlyOneSelectionPerBar;
		private int _pipsFromHigh;
		private int _pipsFromLow;
		private PriceLocation _priceLocation;
		private int _priceRange;
		private int _size;
		private decimal _tickSize;
		private TimeSpan _timeFrom;
		private TimeSpan _timeTo;
		private int _transparency;

		private MiddleClusterType _type;
		private bool _useTimeFilter;
		private int _visualObjectsTransparency;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "CalculationMode", Order = 12)]
		public MiddleClusterType Type
		{
			get => _type;
			set
			{
				_type = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "MinValue", Order = 14)]
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

		[Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "MaxValue", Order = 16)]
		public Filter MaximumFilter
		{
			get => _maxFilter;
			set
			{
				if (value.Value <= 0)
					return;

				_maxFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "DeltaFilters", Name = "DeltaImbalance", Order = 20)]
		public decimal DeltaImbalance
		{
			get => _deltaImbalance;
			set
			{
				_deltaImbalance = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "DeltaFilters", Name = "DeltaFilter", Order = 22)]
		public decimal DeltaFilter
		{
			get => _deltaFilter;
			set
			{
				_deltaFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "CandleDirection", Order = 30)]
		public CandleDirection CandleDir
		{
			get => _candleDirection;
			set
			{
				_candleDirection = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "BarsRange", Order = 32)]
		public int BarsRange
		{
			get => _barsRange;
			set
			{
				if (value <= 0)
					return;

				_barsRange = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "PriceRange", Order = 34)]
		public int PriceRange
		{
			get => _priceRange;
			set
			{
				if (value <= 0)
					return;

				_priceRange = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "PipsFromHigh", Order = 35)]
		public int PipsFromHigh
		{
			get => _pipsFromHigh;
			set
			{
				if (value <= 0)
					return;

				_pipsFromHigh = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "PipsFromLow", Order = 36)]
		public int PipsFromLow
		{
			get => _pipsFromLow;
			set
			{
				if (value <= 0)
					return;

				_pipsFromLow = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "PriceLocation", Order = 37)]
		public PriceLocation PriceLoc
		{
			get => _priceLocation;
			set
			{
				_priceLocation = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "OnlyOneSelectionPerBar", Order = 38)]
		public bool OnlyOneSelectionPerBar
		{
			get => _onlyOneSelectionPerBar;
			set
			{
				_onlyOneSelectionPerBar = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MinimumAverageTrade", Order = 39)]
		public decimal MinAverageTrade
		{
			get => _minAverageTrade;
			set
			{
				if (value < 0)
					return;

				_minAverageTrade = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Filters", Name = "MaximumAverageTrade", Order = 40)]
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

		[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "UseTimeFilter", Order = 50)]
		public bool UseTimeFilter
		{
			get => _useTimeFilter;
			set
			{
				_useTimeFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "TimeFrom", Order = 52)]
		public TimeSpan TimeFrom
		{
			get => _timeFrom;
			set
			{
				_timeFrom = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "TimeFiltration", Name = "TimeTo", Order = 54)]
		public TimeSpan TimeTo
		{
			get => _timeTo;
			set
			{
				_timeTo = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "Color", Order = 60)]
		public Color ClusterColor { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "VisualMode", Order = 61)]
		public ObjectType VisualType { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "VisualObjectsTransparency", Order = 62)]
		public int VisualObjectsTransparency
		{
			get => _visualObjectsTransparency;
			set
			{
				if (value < 0 || value > 100)
					return;

				_visualObjectsTransparency = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "ClusterSelectionTransparency", Order = 63)]
		public int Transparency
		{
			get => _transparency;
			set
			{
				if (value < 0 || value > 100)
					return;

				_transparency = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "FixedSizes", Order = 64)]
		public bool FixedSizes
		{
			get => _fixedSizes;
			set
			{
				_fixedSizes = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "Size", Order = 65)]
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

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "MinimumSize", Order = 66)]
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

		[Display(ResourceType = typeof(Resources), GroupName = "Visualization", Name = "MaximumSize", Order = 67)]
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

		[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts", Order = 70)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile", Order = 72)]
		public string AlertFile { get; set; } = "alert2";

		[Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "BackGround", Order = 74)]
		public Color AlertColor { get; set; }

		#endregion

		#region ctor

		public ClusterSearch()
			: base(true)
		{
			_type = MiddleClusterType.Volume;
			_maxFilter.Enabled = true;
			_maxFilter.Value = 99999;
			_minFilter.Enabled = true;
			_minFilter.Value = 1000;

			_candleDirection = CandleDirection.Any;
			_barsRange = 1;
			_priceRange = 1;
			_pipsFromHigh = 100000000;
			_pipsFromLow = 100000000;
			_priceLocation = PriceLocation.Any;

			_timeFrom = TimeSpan.Zero;
			_timeTo = TimeSpan.Zero;

			ClusterColor = Color.FromArgb(255, 255, 0, 255);
			VisualType = ObjectType.Rectangle;
			_visualObjectsTransparency = 70;
			_transparency = 20;

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
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_renderDataSeries.Clear();
				_tickSize = InstrumentInfo.TickSize;

				_clusterTransColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - VisualObjectsTransparency * 0.01m)), ClusterColor.R, ClusterColor.G,
					ClusterColor.B);
				_clusterPriceTransColor = Color.FromArgb((byte)Math.Ceiling(255 * (1 - Transparency * 0.01m)), ClusterColor.R, ClusterColor.G, ClusterColor.B);
			}

			var candle = GetCandle(bar);
			_pairs.Clear();
			var time = candle.Time;

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

			_isLastBar = ChartInfo.PriceChartContainer.TotalBars == bar;

			if (!_isLastBar)
				_alertPrices.Clear();

			var toolTip = "";

			var candles = new List<IndicatorCandle>();

			if (_barsRange == 1)
				candles.Add(candle);
			else
			{
				for (var i = bar; i >= Math.Max(0, bar - _barsRange); i--)
					candles.Add(GetCandle(i));
			}

			var maxBody = Math.Max(candle.Open, candle.Close);
			var minBody = Math.Min(candle.Open, candle.Close);

			if (CandleDir == CandleDirection.Any
				||
				CandleDir == CandleDirection.Bullish && candle.Close > candle.Open
				||
				CandleDir == CandleDirection.Bearish && candle.Close < candle.Open)
			{
				var candlesHigh = candles.Max(x => x.High);
				var candlesLow = candles.Min(x => x.Low);

				for (var price = candlesLow; price <= candlesHigh; price += _tickSize)
				{
					var sumInfo = new List<PriceVolumeInfo>();
					var isApproach = true;

					for (var i = price; i < price + PriceRange * _tickSize; i += _tickSize)
					{
						switch (PriceLoc)
						{
							case PriceLocation.AtHigh when i != candle.High:
							case PriceLocation.AtLow when i != candle.Low:
							case PriceLocation.AtHighOrLow when i != candle.High && i != candle.Low:
							case PriceLocation.LowerWick when i > minBody:
							case PriceLocation.UpperWick when i < maxBody:
							case PriceLocation.Body when i > maxBody || i < minBody:
								continue;
						}

						if (i > candle.High || i < candle.Low)
						{
							isApproach = false;
							break;
						}

						if ((candle.High - i) / _tickSize > PipsFromHigh)
						{
							isApproach = false;
							break;
						}

						if ((i - candle.Low) / _tickSize > PipsFromLow)
						{
							isApproach = false;
							break;
						}

						var cumulativeInfo = new PriceVolumeInfo
						{
							Price = price
						};

						foreach (var candleItem in candles)
						{
							var priceInfo = candleItem.GetPriceVolumeInfo(i);

							if (priceInfo == null)
								continue;

							cumulativeInfo.Ask += priceInfo.Ask;
							cumulativeInfo.Between += priceInfo.Between;
							cumulativeInfo.Bid += priceInfo.Bid;
							cumulativeInfo.Ticks += priceInfo.Ticks;
							cumulativeInfo.Time += priceInfo.Time;
							cumulativeInfo.Volume += priceInfo.Volume;
						}

						if (!IsInfoEmpty(cumulativeInfo))
							sumInfo.Add(cumulativeInfo);
					}

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

					if (DeltaFilter > 0 && sumInfo.Sum(x => x.Ask - x.Bid) < DeltaFilter)
						continue;

					if (DeltaFilter < 0 && sumInfo.Sum(x => x.Ask - x.Bid) > DeltaFilter)
						continue;

					if (DeltaImbalance != 0)
					{
						var sumAsk = sumInfo.Sum(x => x.Ask);
						var sumBid = sumInfo.Sum(x => x.Bid);
						var sumVol = sumInfo.Sum(x => x.Volume);

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

					switch (Type)
					{
						case MiddleClusterType.Volume:
							sum = sumInfo.Sum(x => x.Volume);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Lots";
							}

							break;

						case MiddleClusterType.Tick:
							sum = sumInfo.Sum(x => x.Ticks);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Trades";
							}

							break;

						case MiddleClusterType.Time:
							sum = sumInfo.Sum(x => x.Time);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Seconds";
							}

							break;

						case MiddleClusterType.Delta:
							sum = sumInfo.Sum(x => x.Ask - x.Bid);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Delta";
							}

							break;

						case MiddleClusterType.Bid:
							sum = sumInfo.Sum(x => x.Bid);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Bids";
							}

							break;

						case MiddleClusterType.Ask:
							sum = sumInfo.Sum(x => x.Ask);

							if (IsApproach(sum))
							{
								val = sum;
								toolTip = sum.ToString(CultureInfo.InvariantCulture) + " Bids";
							}

							break;
					}

					if (val != null)
					{
						var avgTrade = sumInfo.Sum(x => x.Volume) / sumInfo.Sum(x => x.Ticks);

						if ((MaxAverageTrade == 0 || avgTrade < MaxAverageTrade)
							&&
							(MinAverageTrade == 0 || avgTrade > MinAverageTrade))
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

			if (_isLastBar)
			{
				foreach (var pair in _pairs)
				{
					if (!_alertPrices.Contains(pair.Price))
					{
						_alertPrices.Add(pair.Price);
						AddClusterAlert(toolTip);
					}
				}
			}

			var selectionSide = SelectionType.Full;

			if (Type == MiddleClusterType.Ask)
				selectionSide = SelectionType.Ask;

			if (Type == MiddleClusterType.Bid)
				selectionSide = SelectionType.Bid;

			if (_isLastBar)
				_renderDataSeries[bar].Clear();

			foreach (var pair in _pairs.OrderBy(x => x.Price))
			{
				var clusterSize = FixedSizes ? _size : (int)Math.Round(_clusterStepSize * _size * pair.Vol);
				clusterSize = Math.Min(clusterSize, MaxSize);
				clusterSize = Math.Max(clusterSize, MinSize);

				var priceValue = new PriceSelectionValue(pair.Price)
				{
					VisualObject = VisualType,
					Size = clusterSize,
					SelectionSide = selectionSide,
					ObjectColor = _clusterTransColor,
					PriceSelectionColor = _clusterPriceTransColor,
					Tooltip = pair.ToolTip
				};
				_renderDataSeries[bar].Add(priceValue);
			}
		}

		#endregion

		#region Private methods

		private void Filter_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			RecalculateValues();
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
			var isApproach = MaximumFilter.Enabled && MaximumFilter.Value >= value;

			if (MinimumFilter.Enabled && MinimumFilter.Value > value)
				isApproach = false;

			return isApproach;
		}

		private void AddClusterAlert(string msg)
		{
			if (!UseAlerts)
				return;

			AddAlert(AlertFile, InstrumentInfo.Instrument, msg, AlertColor, ClusterColor);
		}

		#endregion
	}
}