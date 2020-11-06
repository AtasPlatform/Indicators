namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Bars Pattern")]
	public class BarsPattern : Indicator
	{
		#region Nested types

		public enum Direction
		{
			[Display(ResourceType = typeof(Resources), Name = "Disabled")]
			Disabled = 0,

			[Display(ResourceType = typeof(Resources), Name = "Bullish")]
			Bull = 1,

			[Display(ResourceType = typeof(Resources), Name = "Bearlish")]
			Bear = 2,

			[Display(ResourceType = typeof(Resources), Name = "Dodge")]
			Dodge = 3
		}

		public enum MaxVolumeLocation
		{
			[Display(ResourceType = typeof(Resources), Name = "Disabled")]
			Disabled = 0,

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

		private int _lastBar;
		private bool _lastBarCalculated;
		private MaxVolumeLocation _maxVolumeLocation;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Common")]
		public bool UseAlerts { get; set; } = false;

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

		[Display(ResourceType = typeof(Resources), Name = "MinimumVolume", GroupName = "Volume", Order = 10)]
		public Filter MinVolume { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "Volume", Order = 11)]
		public Filter MaxVolume { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MinimumBid", GroupName = "DepthMarket", Order = 20)]
		public Filter MinBid { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumBid", GroupName = "DepthMarket", Order = 21)]
		public Filter MaxBid { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MinimumAsk", GroupName = "DepthMarket", Order = 22)]
		public Filter MinAsk { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumAsk", GroupName = "DepthMarket", Order = 23)]
		public Filter MaxAsk { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MinimumDelta", GroupName = "DepthMarket", Order = 24)]
		public Filter MinDelta { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumDelta", GroupName = "DepthMarket", Order = 25)]
		public Filter MaxDelta { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MinimumTrades", GroupName = "Trades", Order = 30)]
		public Filter MinTrades { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumTrades", GroupName = "Trades", Order = 31)]
		public Filter MaxTrades { get; set; } = new Filter { Value = 0, Enabled = false };

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

		[Display(ResourceType = typeof(Resources), Name = "MaximumVolume", GroupName = "MaximumVolumeFilter", Order = 51)]
		public MaxVolumeLocation MaxVolLocation
		{
			get => _maxVolumeLocation;
			set
			{
				_maxVolumeLocation = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleHeight", GroupName = "CandleHeight", Order = 60)]
		public Filter MinCandleHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleHeight", GroupName = "CandleHeight", Order = 61)]
		public Filter MaxCandleHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MinimumCandleBodyHeight", GroupName = "CandleHeight", Order = 70)]
		public Filter MinCandleBodyHeight { get; set; } = new Filter { Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "MaximumCandleBodyHeight", GroupName = "CandleHeight", Order = 71)]
		public Filter MaxCandleBodyHeight { get; set; } = new Filter { Value = 0, Enabled = false };

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

			var candle = GetCandle(bar);

			if (_lastBar == bar)
			{
				_lastBarCalculated = true;
				_paintBars[bar] = null;
			}
			else
			{
				_lastBar = bar;

				if (_lastBarCalculated &&
					UseAlerts &&
					_paintBars[bar - 1] != null)
					AddAlert(AlertFile, "The bar is appropriate");
			}

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

			if (BarDirection != 0)
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

			if (MaxVolLocation != 0)
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

			_paintBars[bar] = _dataSeriesColor;
		}

		protected override void OnInitialize()
		{
			MaxVolume.PropertyChanged += Filter_PropertyChanged;
			MinVolume.PropertyChanged += Filter_PropertyChanged;

			MaxBid.PropertyChanged += Filter_PropertyChanged;
			MinBid.PropertyChanged += Filter_PropertyChanged;
			MaxAsk.PropertyChanged += Filter_PropertyChanged;
			MinAsk.PropertyChanged += Filter_PropertyChanged;

			MaxDelta.PropertyChanged += Filter_PropertyChanged;
			MinDelta.PropertyChanged += Filter_PropertyChanged;
			MaxTrades.PropertyChanged += Filter_PropertyChanged;
			MinTrades.PropertyChanged += Filter_PropertyChanged;

			MaxCandleHeight.PropertyChanged += Filter_PropertyChanged;
			MinCandleHeight.PropertyChanged += Filter_PropertyChanged;
			MaxCandleBodyHeight.PropertyChanged += Filter_PropertyChanged;
			MinCandleBodyHeight.PropertyChanged += Filter_PropertyChanged;
		}

		#endregion

		#region Private methods

		private void Filter_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			RecalculateValues();
		}

		#endregion
	}
}