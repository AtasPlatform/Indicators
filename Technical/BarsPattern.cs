namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Editors;
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

		private Color _dataSeriesColor;

		private bool _directionFilter;

		private int _lastBar;
		private bool _lastBarCalculated;
		private bool _maxVolumeFilter;
		private MaxVolumeLocation _maxVolumeLocation;

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

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Volume", Order = 10)]
		public Filter MaxVolume { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Volume", Order = 11)]
		public Filter MinVolume { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumBid", GroupName = "DepthMarket", Order = 20)]
		public Filter MaxBid { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumBid", GroupName = "DepthMarket", Order = 21)]
		public Filter MinBid { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumAsk", GroupName = "DepthMarket", Order = 22)]
		public Filter MaxAsk { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumAsk", GroupName = "DepthMarket", Order = 23)]
		public Filter MinAsk { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumDelta", GroupName = "DepthMarket", Order = 24)]
		public Filter MaxDelta { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumDelta", GroupName = "DepthMarket", Order = 25)]
		public Filter MinDelta { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumTrades", GroupName = "Trades", Order = 30)]
		public Filter MaxTrades { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumTrades", GroupName = "Trades", Order = 31)]
		public Filter MinTrades { get; set; } = new Filter { Value = 0, Enabled = false };

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

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleHeight", GroupName = "CandleHeight", Order = 60)]
		public Filter MaxCandleHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleHeight", GroupName = "CandleHeight", Order = 61)]
		public Filter MinCandleHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleBodyHeight", GroupName = "CandleHeight", Order = 60)]
		public Filter MaxCandleBodyHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Editor(typeof(FilterEditor), typeof(FilterEditor))]
		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleBodyHeight", GroupName = "CandleHeight", Order = 61)]
		public Filter MinCandleBodyHeight { get; set; } = new Filter { Value = 0, Enabled = false };

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

				if (MaxVolume.Enabled && candle.Volume > MaxVolume.Value)
					return;

				if (MinVolume.Enabled && candle.Volume < MinVolume.Value)
					return;

				if (MaxBid.Enabled && candle.Bid > MaxBid.Value)
					return;

				if (MinBid.Enabled && candle.Bid < MinBid.Value)
					return;

				if (MaxAsk.Enabled && candle.Ask > MaxAsk.Value)
					return;

				if (MinAsk.Enabled && candle.Ask < MinAsk.Value)
					return;

				if (MaxDelta.Enabled && candle.Delta > MaxDelta.Value)
					return;

				if (MinDelta.Enabled && candle.Delta < MinDelta.Value)
					return;

				if (MaxTrades.Enabled && candle.Ticks > MaxTrades.Value)
					return;

				if (MinTrades.Enabled && candle.Ticks < MinTrades.Value)
					return;

				if (DirectionFilter)
				{
					switch (BarDirection)
					{
						case Direction.Bear:
							if (candle.Open <= candle.Close)
								return;

							break;

						case Direction.Bull:
							if (candle.Open >= candle.Close)
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

				if (MinCandleHeight.Enabled)
				{
					var height = (candle.High - candle.Low) / ChartInfo.PriceChartContainer.Step;

					if (height < MinCandleHeight.Value)
						return;
				}

				if (MaxCandleHeight.Enabled)
				{
					var height = (candle.High - candle.Low) / ChartInfo.PriceChartContainer.Step;

					if (height > MaxCandleHeight.Value)
						return;
				}

				if (MinCandleBodyHeight.Enabled)
				{
					var bodyHeight = Math.Abs(candle.Open - candle.Close) / ChartInfo.PriceChartContainer.Step;

					if (bodyHeight < MinCandleBodyHeight.Value)
						return;
				}

				if (MaxCandleBodyHeight.Enabled)
				{
					var bodyHeight = Math.Abs(candle.Open - candle.Close) / ChartInfo.PriceChartContainer.Step;

					if (bodyHeight > MaxCandleBodyHeight.Value)
						return;
				}

				_paintBars[bar - 1] = _dataSeriesColor;

				if (_lastBarCalculated)
					AddAlert(AlertFile, "The bar is appropriate");
			}
			else
				_lastBarCalculated = true;
		}

		protected override void OnInitialize()
		{
			MaxVolume.PropertyChanged += (a, b) =>

			{
				if (MaxVolume.Value < 0)
					return;

				if (MaxVolume.Value < MinVolume.Value)
					MinVolume.Value = MaxVolume.Value;

				RecalculateValues();
			};

			MinVolume.PropertyChanged += (a, b) =>
			{
				if (MinVolume.Value < 0)
					return;

				if (MinVolume.Value > MaxVolume.Value && MaxVolume.Value != 0)
					MaxVolume.Value = MinVolume.Value;

				RecalculateValues();
			};

			MaxBid.PropertyChanged += (a, b) =>
			{
				if (MaxBid.Value < 0)
					return;

				if (MaxBid.Value < MinBid.Value)
					MinBid.Value = MaxBid.Value;

				RecalculateValues();
			};

			MinBid.PropertyChanged += (a, b) =>
			{
				if (MinBid.Value < 0)
					return;

				if (MinBid.Value > MaxBid.Value && MaxBid.Value != 0)
					MaxBid.Value = MinBid.Value;

				RecalculateValues();
			};

			MaxAsk.PropertyChanged += (a, b) =>
			{
				if (MaxAsk.Value < 0)
					return;

				if (MaxAsk.Value < MinAsk.Value)
					MinAsk.Value = MaxAsk.Value;

				RecalculateValues();
			};

			MinAsk.PropertyChanged += (a, b) =>
			{
				if (MinAsk.Value < 0)
					return;

				if (MinAsk.Value > MaxAsk.Value && MaxAsk.Value != 0)
					MaxAsk.Value = MinAsk.Value;

				RecalculateValues();
			};

			MaxDelta.PropertyChanged += (a, b) =>
			{
				if (MaxDelta.Value < 0)
					return;

				if (MaxDelta.Value < MinDelta.Value)
					MinDelta.Value = MaxDelta.Value;

				RecalculateValues();
			};

			MinDelta.PropertyChanged += (a, b) =>
			{
				if (MinDelta.Value < 0)
					return;

				if (MinDelta.Value > MaxDelta.Value && MaxDelta.Value != 0)
					MaxDelta.Value = MinDelta.Value;

				RecalculateValues();
			};

			MaxTrades.PropertyChanged += (a, b) =>
			{
				if (MaxTrades.Value < 0)
					return;

				if (MaxTrades.Value < MinTrades.Value)
					MinTrades.Value = MaxTrades.Value;

				RecalculateValues();
			};

			MinTrades.PropertyChanged += (a, b) =>
			{
				if (MinTrades.Value < 0)
					return;

				if (MinTrades.Value > MaxTrades.Value && MaxTrades.Value != 0)
					MaxTrades.Value = MinTrades.Value;

				RecalculateValues();
			};

			MaxCandleHeight.PropertyChanged += (a, b) =>
			{
				if (MaxCandleHeight.Value < 0)
					return;

				if (MaxCandleHeight.Value < MinCandleHeight.Value)
					MinCandleHeight.Value = MaxCandleHeight.Value;

				RecalculateValues();
			};

			MinCandleHeight.PropertyChanged += (a, b) =>
			{
				if (MinCandleHeight.Value < 0)
					return;

				if (MinCandleHeight.Value > MaxCandleHeight.Value && MaxCandleHeight.Value != 0)
					MaxCandleHeight.Value = MinCandleHeight.Value;

				RecalculateValues();
			};

			MaxCandleBodyHeight.PropertyChanged += (a, b) =>
			{
				if (MaxCandleBodyHeight.Value < 0)
					return;

				if (MaxCandleBodyHeight.Value < MinCandleBodyHeight.Value)
					MinCandleBodyHeight.Value = MaxCandleBodyHeight.Value;

				RecalculateValues();
			};

			MinCandleBodyHeight.PropertyChanged += (a, b) =>
			{
				if (MinCandleBodyHeight.Value < 0)
					return;

				if (MinCandleBodyHeight.Value > MaxCandleBodyHeight.Value && MaxCandleBodyHeight.Value != 0)
					MaxCandleBodyHeight.Value = MinCandleBodyHeight.Value;

				RecalculateValues();
			};
		}

		#endregion
	}
}