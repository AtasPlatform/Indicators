namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bars Pattern")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarsPatternDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602328")]
	public class BarsPattern : Indicator
	{
		#region Nested types

		public enum Direction
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Disabled))]
			Disabled = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bullish))]
			Bull = 1,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bearlish))]
			Bear = 2,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Dodge))]
			Dodge = 3
		}

		public enum MaxVolumeLocation
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Disabled))]
			Disabled = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UpperWick))]
			UpperWick = 1,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowerWick))]
			LowerWick = 2,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Body))]
			Body = 3
		}

		#endregion

		#region Fields

		private readonly PaintbarsDataSeries _paintBars = new("PaintBars", "ColoredSeries");

		private Direction _barDirection;
		private Color _dataSeriesColor;
		private int _lastBar;
		private MaxVolumeLocation _maxVolumeLocation;

        #endregion

        #region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumVolume), GroupName = nameof(Strings.Volume), Description = nameof(Strings.MinVolumeFilterCommonDescription), Order = 10)]
		public Filter MinVolume { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumVolume), GroupName = nameof(Strings.Volume), Description = nameof(Strings.MaxVolumeFilterCommonDescription), Order = 11)]
		public Filter MaxVolume { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumBid), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MinBidVolumeFilterCommonDescription), Order = 20)]
		public Filter MinBid { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumBid), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MaxBidVolumeFilterCommonDescription), Order = 21)]
		public Filter MaxBid { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumAsk), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MinAskVolumeFilterCommonDescription), Order = 22)]
		public Filter MinAsk { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumAsk), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MaxAskVolumeFilterCommonDescription), Order = 23)]
		public Filter MaxAsk { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumDelta), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MinDeltaVolumeFilterCommonDescription), Order = 24)]
		public Filter MinDelta { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumDelta), GroupName = nameof(Strings.DepthMarket), Description = nameof(Strings.MaxDeltaVolumeFilterCommonDescription), Order = 25)]
		public Filter MaxDelta { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumTrades), GroupName = nameof(Strings.Trades), Description = nameof(Strings.MinTickVolumeFilterCommonDescription), Order = 30)]
		public Filter MinTrades { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumTrades), GroupName = nameof(Strings.Trades), Description = nameof(Strings.MaxTickVolumeFilterCommonDescription), Order = 31)]
		public Filter MaxTrades { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BarsDirection), GroupName = nameof(Strings.BarsDirection), Description = nameof(Strings.BarDirectionDescription), Order = 41)]
		public Direction BarDirection
		{
			get => _barDirection;
			set
			{
				_barDirection = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaxVolumeLocation), GroupName = nameof(Strings.MaximumVolume), Description = nameof(Strings.MaxVolumeLocationDescription), Order = 51)]
		public MaxVolumeLocation MaxVolLocation
		{
			get => _maxVolumeLocation;
			set
			{
				_maxVolumeLocation = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumCandleHeight), GroupName = nameof(Strings.CandleHeight), Description = nameof(Strings.MinCandleHeightFilterDescription), Order = 60)]
		public Filter MinCandleHeight { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumCandleHeight), GroupName = nameof(Strings.CandleHeight), Description = nameof(Strings.MaxCandleHeightFilterDescription), Order = 61)]
		public Filter MaxCandleHeight { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimumCandleBodyHeight), GroupName = nameof(Strings.CandleHeight), Description = nameof(Strings.MinCandleBodyHeightFilterDescription), Order = 70)]
		public Filter MinCandleBodyHeight { get; set; } = new()
			{ Value = 0, Enabled = false };

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MaximumCandleBodyHeight), GroupName = nameof(Strings.CandleHeight), Description = nameof(Strings.MaxCandleBodyHeightFilterDescription), Order = 71)]
		public Filter MaxCandleBodyHeight { get; set; } = new()
			{ Value = 0, Enabled = false };

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription), Order = 101)]
        public bool UseAlerts { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 102)]
        public string AlertFile { get; set; } = "alert1";

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.FilterCandleColorDescription))]
        public Color Color
        {
            get => _dataSeriesColor;
            set
            {
                _dataSeriesColor = value;
                RecalculateValues();
            }
        }

        #endregion

        #region ctor

        public BarsPattern()
			: base(true)
		{
			_lastBar = 0;
			_dataSeriesColor = DefaultColors.Blue.Convert();
			_paintBars.IsHidden = true;
			DenyToChangePanel = true;
			DataSeries[0] = _paintBars;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (_lastBar == bar)
			{
				_paintBars[bar] = null;
			}
			else
			{
				_lastBar = bar;

				if (bar > 0 && bar == CurrentBar - 1 && UseAlerts && _paintBars[bar - 1] != null) 
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
						if (maxVolPrice <= maxBody)
							return;

						break;

					case MaxVolumeLocation.LowerWick:
						if (maxVolPrice >= minBody)
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