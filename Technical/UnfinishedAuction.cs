namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Pen = System.Drawing.Pen;

	[DisplayName("Unfinished Auction")]
	[Description("Unfinished Auction")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/17466-unfinished-auction")]
	public class UnfinishedAuctionMod : Indicator
	{
		#region Fields

		private readonly PriceSelectionDataSeries _priceSelectionSeries = new("Clusters Selection");
		private int _askFilter = 20;

		private int _bidFilter = 20;
		private int _days;
		private Color _highColor = Colors.Red;
		private Color _highLineColor = Colors.Crimson;
		private int _lastAlert;
		private int _lastBar;
		private int _lineWidth;
		private Color _lowColor = Colors.Blue;

		private Color _lowLineColor = Colors.Aqua;
		private int _targetBar;

        #endregion

        #region Properties

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

		[Display(ResourceType = typeof(Resources), Name = "BidFilter", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "AskFilter", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "LineWidth", Order = 120)]
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

		[Display(ResourceType = typeof(Resources), Name = "LowLineColor", GroupName = "Colors", Order = 200)]

		public Color LowLineColor
		{
			get => _lowLineColor;
			set
			{
				_lowLineColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HighLineColor", GroupName = "Colors", Order = 210)]
		public Color HighLineColor
		{
			get => _highLineColor;
			set
			{
				_highLineColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LowColor", GroupName = "Colors", Order = 220)]
		public Color LowColor
		{
			get => _lowColor;
			set
			{
				_lowColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HighColor", GroupName = "Colors", Order = 220)]
		public Color HighColor
		{
			get => _highColor;
			set
			{
				_highColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 300)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 310)]
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
					var cl = Colors.Black;

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