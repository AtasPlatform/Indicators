namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Pen = System.Drawing.Pen;

#if CROSS_PLATFORM
    using Color = System.Drawing.Color;
#else
    using Color = System.Windows.Media.Color;
#endif

    [DisplayName("Unfinished Auction")]
	[Description("Unfinished Auction")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.UnfinishedAuctionModDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602495")]
	public class UnfinishedAuctionMod : Indicator
	{
		#region Fields

		private readonly PriceSelectionDataSeries _priceSelectionSeries = new("PriceSelectionSeries", "Clusters Selection");
		private int _askFilter = 20;

		private int _bidFilter = 20;
		private int _days;
		private Color _highColor = System.Drawing.Color.Red.Convert();
		private Color _highLineColor = System.Drawing.Color.Crimson.Convert();
		private int _lastAlert;
		private int _lastBar;
		private int _lineWidth;
		private Color _lowColor = System.Drawing.Color.Blue.Convert();

		private Color _lowLineColor = System.Drawing.Color.Aqua.Convert();
		private int _targetBar;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.BidFilter), Description = nameof(Strings.MinBidVolumeFilterCommonDescription))]
        [Range(0, 1000000)]
        public int BidFilter
        {
            get => _bidFilter;
            set
            {
                _bidFilter = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.AskFilter), Description = nameof(Strings.MinAskVolumeFilterCommonDescription))]
        [Range(0, 1000000)]
        public int AskFilter
        {
            get => _askFilter;
            set
            {
                _askFilter = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LineWidthDescription))]
		[Range(1, 1000)]
		public int LineWidth
		{
			get => _lineWidth;
			set
			{
				_lineWidth = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowLineColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LineColorDescription))]

		public Color LowLineColor
		{
			get => _lowLineColor;
			set
			{
				_lowLineColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLineColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.LineColorDescription))]
		public Color HighLineColor
		{
			get => _highLineColor;
			set
			{
				_highLineColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LowColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PriceSelectionColorDescription))]
		public Color LowColor
		{
			get => _lowColor;
			set
			{
				_lowColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighColor), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.PriceSelectionColorDescription))]
		public Color HighColor
		{
			get => _highColor;
			set
			{
				_highColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription), Order = 300)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription),  Order = 310)]
		public string AlertFile { get; set; } = "alert1";

		#endregion

		#region ctor

		public UnfinishedAuctionMod()
			: base(true)
		{
			DataSeries[0] = _priceSelectionSeries;
			DataSeries[0].IsHidden = true;
			_highLineColor.A = _highColor.A = _lowLineColor.A = _lowColor.A = 150;
			_lineWidth = 10;
			_days = 20;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				TrendLines.Clear();
				DataSeries.ForEach(x => x.Clear());
				_targetBar = 0;

				if (_days <= 0)
					return;

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

				return;
			}

			if (bar - 1 < _targetBar || _lastBar == bar)
				return;

			if (bar == CurrentBar - 1 && HorizontalLinesTillTouch.Any())
			{
				for (var i = HorizontalLinesTillTouch.Count - 1; i >= 0; i--)
				{
					var line = HorizontalLinesTillTouch[i];

					if (line.FirstBar < bar - 1)
						break;

					HorizontalLinesTillTouch.RemoveAt(i);
				}
			}

			CalculateAuctionAt(bar - 1);
			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private void SendAlert(TradeDirection dir, decimal price)
		{
			if (_lastAlert == CurrentBar - 1)
				return;

			var alertText = dir != TradeDirection.Between
				? $"Unfinished Auction ({(dir == TradeDirection.Buy ? "Low" : "High")} Zone on {price:0.######})"
				: $"Unfinished Auction Zone closed on {price:0.######}";

			AddAlert(AlertFile, alertText);
			_lastAlert = CurrentBar - 1;
		}

		private void CalculateAuctionAt(int bar)
		{
			var candle = GetCandle(bar);

			var priceSelectionValues = _priceSelectionSeries[bar];
			priceSelectionValues.Clear();

			for (var i = HorizontalLinesTillTouch.Count - 1; i >= 0; i--)
			{
				var line = HorizontalLinesTillTouch[i];

				if (candle.High >= line.FirstPrice && candle.Low <= line.FirstPrice)
				{
					line.SecondBar = bar;
					line.IsRay = false;

					if (UseAlerts && bar == CurrentBar - 2)
						SendAlert(TradeDirection.Between, line.FirstPrice);

					TrendLines.Add(line);
					HorizontalLinesTillTouch.RemoveAt(i);

					var value = line.FirstPrice;
					var cl = System.Drawing.Color.Black.Convert();

					if (line.FirstBar == bar)
						cl = value == candle.Low ? _lowColor : _highColor;
					else
					{
						var val = _priceSelectionSeries[line.FirstBar].FirstOrDefault(t => t.MinimumPrice == value);

						if (val != null)
							cl = val.ObjectColor;
					}

					priceSelectionValues.Add(new PriceSelectionValue(value)
					{
						VisualObject = ObjectType.OnlyCluster,
						ObjectColor = cl,
						Size = 100,
						PriceSelectionColor = cl
					});
				}
			}

			//Ищем новые начала трендовых
			var candlePvLow = candle.GetPriceVolumeInfo(candle.Low);
			var candlePvHigh = candle.GetPriceVolumeInfo(candle.High);

			if ((candlePvLow?.Ask ?? 0) > 0 && candlePvLow.Bid > _bidFilter)
			{
				var lowPenColor = System.Drawing.Color.FromArgb(_lowLineColor.A, _lowLineColor.R, _lowLineColor.G, _lowLineColor.B);

				var lowPen = new Pen(lowPenColor)
				{
					Width = _lineWidth
				};

				var tt = new LineTillTouch(bar, candle.Low, lowPen)
				{
					IsRay = true
				};

				HorizontalLinesTillTouch.Add(tt);

				if (UseAlerts && bar == CurrentBar - 2)
					SendAlert(TradeDirection.Buy, candle.Low);
			}

			if (candlePvHigh != null && candlePvHigh.Ask > _askFilter && candlePvHigh.Bid > 0)
			{
				var highPenColor = System.Drawing.Color.FromArgb(_highLineColor.A, _highLineColor.R, _highLineColor.G, _highLineColor.B);

				var highPen = new Pen(highPenColor)
				{
					Width = _lineWidth
				};

				var tt = new LineTillTouch(bar, candle.High, highPen)
				{
					IsRay = true
				};

				HorizontalLinesTillTouch.Add(tt);

				if (UseAlerts && bar == CurrentBar - 2)
					SendAlert(TradeDirection.Sell, candle.High);
			}

			for (var i = HorizontalLinesTillTouch.Count - 1; i >= 0; i--)
			{
				var trendLine = HorizontalLinesTillTouch[i];

				if (trendLine.FirstBar < bar)
					break;

				var value = trendLine.FirstPrice;
				var cl = value == candle.Low ? _lowColor : _highColor;

				priceSelectionValues.Add(new PriceSelectionValue(value)
				{
					VisualObject = ObjectType.OnlyCluster,
					ObjectColor = cl,
					Size = 100,
					PriceSelectionColor = cl
				});
			}
		}

		#endregion
	}
}