namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	public class BarsPattern : Indicator
	{
		#region Nested types

		public enum Direction
		{
			[Display(ResourceType = typeof(Resources), Name = "Bullish")]
			Bull = 1,

			[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
			Bear = 2,

			[Display(ResourceType = typeof(Resources), Name = "Dodge")]
			Dodge = 3
		}

		public enum MaxVolumeLocation
		{
			[Display(ResourceType = typeof(Resources), Name = "UpperWick")]
			UpperWick = 1,

			[Display(ResourceType = typeof(Resources), Name = "LowerWick")]
			LowerWick = 2,

			[Display(ResourceType = typeof(Resources), Name = "Body")]
			Body = 3
		}

		#endregion

		#region Fields

		private readonly PaintbarsDataSeries _paintBars = new PaintbarsDataSeries("ColoredSeries");
		private Direction _barDirection;
		private bool _candleBodyHeightFilter;
		private bool _candleHeightFilter;

		private Color _dataSeriesColor;

		private bool _depthFilter;
		private bool _directionFilter;

		private int _lastBar;
		private bool _lastBarCalculated;
		private decimal _maxAsk;
		private decimal _maxBid;
		private decimal _maxCandleBodyHeight;
		private decimal _maxCandleHeight;
		private decimal _maxDelta;
		private decimal _maxTrade;
		private decimal _maxVolume;
		private bool _maxVolumeFilter;
		private MaxVolumeLocation _maxVolumeLocation;
		private decimal _minAsk;
		private decimal _minBid;
		private decimal _minCandleBodyHeight;
		private decimal _minCandleHeight;
		private decimal _minDelta;
		private decimal _minTrade;
		private decimal _minVolume;
		private bool _tradeFilter;

		private bool _volumeFilter;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Common")]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Common", Order = 0)]
		public Color Color
		{
			get => _dataSeriesColor;
			set
			{
				_dataSeriesColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "VolumeFilter", GroupName = "Volume", Order = 10)]
		public bool VolumeFilter
		{
			get => _volumeFilter;
			set
			{
				_volumeFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Volume", Order = 11)]
		public decimal MinVolume
		{
			get => _minVolume;
			set
			{
				if (value < 0)
					return;

				if (value > _maxVolume && _maxVolume != 0)
					_maxVolume = value;

				_minVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Volume", Order = 12)]
		public decimal MaxVolume
		{
			get => _maxVolume;
			set
			{
				if (value < 0)
					return;

				if (value < _minVolume)
					_minVolume = value;

				_maxVolume = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DepthMarketFilter", GroupName = "DepthMarket", Order = 20)]
		public bool DepthMarketFilter
		{
			get => _depthFilter;
			set
			{
				_depthFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumBid", GroupName = "DepthMarket", Order = 21)]
		public decimal MinBid
		{
			get => _minBid;
			set
			{
				if (value < 0)
					return;

				if (value > _maxBid && _maxBid != 0)
					_maxBid = value;

				_minBid = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumBid", GroupName = "DepthMarket", Order = 22)]
		public decimal MaxBid
		{
			get => _maxBid;
			set
			{
				if (value < 0)
					return;

				if (value < _minBid)
					_minBid = value;

				_maxBid = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumAsk", GroupName = "DepthMarket", Order = 23)]
		public decimal MinAsk
		{
			get => _minAsk;
			set
			{
				if (value < 0)
					return;

				if (value > _maxAsk && _maxAsk != 0)
					_maxAsk = value;

				_minAsk = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumAsk", GroupName = "DepthMarket", Order = 24)]
		public decimal MaxAsk
		{
			get => _maxAsk;
			set
			{
				if (value < 0)
					return;

				if (value < _minAsk)
					_minAsk = value;

				_maxAsk = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumDelta", GroupName = "DepthMarket", Order = 25)]
		public decimal MinDelta
		{
			get => _minDelta;
			set
			{
				if (value > _maxDelta && _maxDelta != 0)
					_maxDelta = value;

				_minDelta = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumDelta", GroupName = "DepthMarket", Order = 26)]
		public decimal MaxDelta
		{
			get => _maxDelta;
			set
			{
				if (value < _minDelta)
					_minDelta = value;

				_maxDelta = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TradesFilter", GroupName = "Trades", Order = 30)]
		public bool TradesFilter
		{
			get => _tradeFilter;
			set
			{
				_tradeFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumTrades", GroupName = "Trades", Order = 31)]
		public decimal MaxTrades
		{
			get => _maxTrade;
			set
			{
				if (value < 0)
					return;

				if (value < _minTrade)
					_minTrade = value;

				_minTrade = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumTrades", GroupName = "Trades", Order = 32)]
		public decimal MinTrades
		{
			get => _minTrade;
			set
			{
				if (value < 0)
					return;

				if (value < _maxTrade && _maxTrade != 0)
					_maxTrade = value;

				_minTrade = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "DirectionFilter", GroupName = "BarsDirection", Order = 40)]
		public bool DirectionFilter
		{
			get => _directionFilter;
			set
			{
				_directionFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BarsDirection", GroupName = "BarsDirection", Order = 41)]
		public Direction BarDirection
		{
			get => _barDirection;
			set
			{
				_barDirection = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolumeFilter", GroupName = "MaximumVolume", Order = 50)]
		public bool MaxVolumeFilter
		{
			get => _maxVolumeFilter;
			set
			{
				_maxVolumeFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "MaximumVolume", Order = 51)]
		public MaxVolumeLocation MaxVolLocation
		{
			get => _maxVolumeLocation;
			set
			{
				_maxVolumeLocation = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CandleHeightFilter", GroupName = "CandleHeight", Order = 60)]
		public bool CandleHeightFilter
		{
			get => _candleHeightFilter;
			set
			{
				_candleHeightFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleHeight", GroupName = "CandleHeight", Order = 61)]
		public decimal MinCandleHeight
		{
			get => _minCandleHeight;
			set
			{
				if (value < 0)
					return;

				if (value > _maxCandleHeight && _maxCandleHeight != 0)
					_maxCandleHeight = value;

				_minCandleHeight = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleHeight", GroupName = "CandleHeight", Order = 62)]
		public decimal MaxCandleHeight
		{
			get => _maxCandleHeight;
			set
			{
				if (value < 0)
					return;

				if (value < _minCandleHeight)
					_minCandleHeight = value;

				_maxCandleHeight = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CandleBodyHeightFilter", GroupName = "CandleBodyHeight", Order = 70)]
		public bool CandleBodyHeightFilter
		{
			get => _candleBodyHeightFilter;
			set
			{
				_candleBodyHeightFilter = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleBodyHeight", GroupName = "CandleBodyHeight", Order = 71)]
		public decimal MinCandleBodyHeight
		{
			get => _minCandleBodyHeight;
			set
			{
				if (value < 0)
					return;

				if (value > _maxCandleBodyHeight && _maxCandleBodyHeight != 0)
					_maxCandleBodyHeight = value;

				_minCandleBodyHeight = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleBodyHeight", GroupName = "CandleBodyHeight", Order = 72)]
		public decimal MaxCandleBodyHeight
		{
			get => _maxCandleBodyHeight;
			set
			{
				if (value < 0)
					return;

				if (value < _minCandleBodyHeight)
					_minCandleBodyHeight = value;

				_maxCandleBodyHeight = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BarsPattern()
			: base(true)
		{
			_lastBar = 0;
			_dataSeriesColor = Color.FromRgb(0, 0, 255);
			_paintBars.IsHidden = true;
			DenyToChangePanel = true;
			DataSeries[0] = _paintBars;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_lastBarCalculated = false;
				return;
			}

			if (bar != _lastBar)
			{
				var candle = GetCandle(bar - 1);
				_lastBar = bar;

				if (VolumeFilter
					&&
					(candle.Volume > MaxVolume && MaxVolume != 0 || candle.Volume < MinVolume && MinVolume != 0)
				)
					return;

				if (DepthMarketFilter &&
					(candle.Bid > MaxBid && MaxBid != 0 || candle.Bid < MinBid && MinBid != 0
						||
						candle.Ask > MaxAsk && MaxAsk != 0 || candle.Ask < MinAsk && MinAsk != 0
						||
						candle.Delta > MaxDelta && MaxDelta != 0 || candle.Delta < MinDelta && MinDelta != 0
					)
				)
					return;

				if (TradesFilter
					&&
					(candle.Ticks > MaxTrades && MaxTrades != 0 || candle.Ticks < MinTrades && MinTrades != 0)
				)
					return;

				if (DirectionFilter)
				{
					switch (BarDirection)
					{
						case Direction.Bear:
							if (candle.Open >= candle.Close)
								return;

							break;

						case Direction.Bull:
							if (candle.Open <= candle.Close)
								return;

							break;

						case Direction.Dodge:
							if (candle.Open != candle.Close)
								return;

							break;
					}
				}

				if (MaxVolumeFilter)
				{
					var maxVolPrice = candle.MaxVolumePriceInfo.Price;
					var maxBody = Math.Max(candle.Open, candle.Close);
					var minBody = Math.Min(candle.Open, candle.Close);

					switch (MaxVolLocation)
					{
						case MaxVolumeLocation.Body:
							if (maxVolPrice < minBody || maxVolPrice > maxBody)
								return;

							break;

						case MaxVolumeLocation.UpperWick:
							if (maxVolPrice < maxBody)
								return;

							break;

						case MaxVolumeLocation.LowerWick:
							if (maxVolPrice > minBody)
								return;

							break;
					}
				}

				if (CandleHeightFilter)
				{
					var height = candle.High - candle.Low;

					if (height < MinCandleHeight && MinCandleHeight != 0 || height > MaxCandleHeight && MaxCandleHeight != 0)
						return;
				}

				if (CandleBodyHeightFilter)
				{
					var bodyHeight = Math.Abs(candle.Open - candle.Close);

					if (bodyHeight < MinCandleBodyHeight && MinCandleBodyHeight != 0 || bodyHeight > MaxCandleBodyHeight && MaxCandleBodyHeight != 0)
						return;
				}

				_paintBars[bar - 1] = _dataSeriesColor;

				if (_lastBarCalculated)
					AddAlert(AlertFile, "The bar is appropriate");
			}
			else
				_lastBarCalculated = true;
		}

		#endregion
	}
}