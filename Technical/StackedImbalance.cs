namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Drawing;

	using Newtonsoft.Json;

	using OFT.Attributes;
    using OFT.Localization;
    using Pen = CrossPen;
	
    [DisplayName("Stacked Imbalance")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StackedImbalanceDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602474")]
	public class StackedImbalance : Indicator, IBidAskVolumeDependent
	{
		#region Readonly initialized fields

		private readonly List<(decimal Price, int Count)> _bidStacks = new();
		private readonly PriceVolumeInfo _volumeInfo = new();

		#endregion

		#region Fields

		private readonly CrossPen _askBidPen;
		private readonly CrossPen _bidAskPen;

		private CrossColor _askBidImbalanceColor = DefaultColors.Green.Convert();
		private CrossColor _bidAskImbalanceColor = DefaultColors.DarkRed.Convert();
		private int _days = 20;
        private int _drawBarsLength = 10;
		private bool _ignoreZeroValues;
		private int _imbalanceRange = 3;

		private int _imbalanceRatio = 300;
		// PLAT-5080: the level volume threshold may be set in money; persisted as the old number plus the exact value and the money scalar
		private VolumeFilter _imbalanceVolume;
		private int _lastCalculatedBar = -1;
		private decimal _lastClose;
		private int _lineWidth = 10;
		private List<decimal> _priceAlerts = new();

		private bool _readyToAlert;
		private int _targetBar;
		private bool _tillTouch;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.IgnoreZeroValues), GroupName = nameof(Strings.Settings), Description = nameof(Strings.IgnoreZeroValuesDescription))]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        public bool IgnoreZeroValues
        {
            get => _ignoreZeroValues;
            set
            {
                _ignoreZeroValues = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceRatio), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinRatioDescription))]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        [Range(0, 100000)]
        public int ImbalanceRatio
        {
            get => _imbalanceRatio;
            set
            {
                _imbalanceRatio = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceRange), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinLevelsRangeSizeDescription))]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        [Range(0, 100000)]
        public int ImbalanceRange
        {
            get => _imbalanceRange;
            set
            {
                _imbalanceRange = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceVolume), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinVolumeFilterCommonDescription))]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        [JsonIgnore]
        public VolumeFilter ImbalanceVolumeFilter
        {
            get => _imbalanceVolume;
            set => SetTrackedProperty(ref _imbalanceVolume, value, OnFilterChanged);
        }

        [Parameter]
        [Browsable(false)]
        [Range(0, 10000000)]
        public int ImbalanceVolume
        {
            get => (int)Math.Round(_imbalanceVolume.Value);
            set => _imbalanceVolume.Value = value;
        }

        // declared after ImbalanceVolume: loaded last, it restores a fractional threshold; older versions ignore it
        [Browsable(false)]
        public decimal ImbalanceVolumeExact
        {
            get => _imbalanceVolume.Value;
            set => _imbalanceVolume.Value = value;
        }

        [Browsable(false)]
        public string? ImbalanceVolumeMoney
        {
            get => _imbalanceVolume.MoneyScalar;
            set => _imbalanceVolume.MoneyScalar = value;
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        [Range(0, 1000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineTillTouch), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.IsLineTillTouchDescription))]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public bool TillTouch
		{
			get => _tillTouch;
			set
			{
				_tillTouch = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskBidImbalanceColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.BullishColorDescription))]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public CrossColor AskBidImbalanceColor
		{
			get => _askBidImbalanceColor;
			set
			{
				_askBidImbalanceColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BidAskImbalanceColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.BearishColorDescription))]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public CrossColor BidAskImbalanceColor
		{
			get => _bidAskImbalanceColor;
			set
			{
				_bidAskImbalanceColor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LineWidthDescription))]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		[Range(1, 100)]
		public int LineWidth
		{
			get => _lineWidth;
			set
			{
				_lineWidth = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PrintLineForXBars), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.LineLengthDescription))]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		[Range(0, 10000)]
		public int DrawBarsLength
		{
			get => _drawBarsLength;
			set
			{
				_drawBarsLength = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SingalAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.IsSingalAlertDescription))]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.IsApproximationAlertDescription))]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public bool UseCrossAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public string AlertFile { get; set; } = "alert1";

		#endregion

		#region ctor

		BidAskVolumeUsage IBidAskVolumeDependent.BidAskVolumeUsage => BidAskVolumeUsage.Full;

		public StackedImbalance()
			: base(true)
		{
			ImbalanceVolumeFilter = new VolumeFilter(false) { Enabled = true, Value = 30 };

			_askBidPen = new CrossPen(GetDrawingColor(_askBidImbalanceColor));
			_bidAskPen = new CrossPen(GetDrawingColor(_bidAskImbalanceColor));

			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		// a money filter selects other levels when the rates or the display currency change
		protected override void OnValuationChanged()
		{
			if (_imbalanceVolume.IsMoney)
				DoActionInGuiThread(RecalculateValues);
		}

		protected override void OnInitialize()
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DataSeries[0].IsHidden = true;
        }

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_priceAlerts.Clear();
				_askBidPen.Width = _lineWidth;
				_bidAskPen.Width = _lineWidth;
				_askBidPen.Color = GetDrawingColor(_askBidImbalanceColor);
				_bidAskPen.Color = GetDrawingColor(_bidAskImbalanceColor);
				_readyToAlert = false;

				_targetBar = 0;
				_lastClose = 0;

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

			if (bar < _targetBar)
				return;

			if (bar != _lastCalculatedBar)
			{
				_priceAlerts.Clear();
				_lastCalculatedBar = bar;

				CalculateImbalances(bar - 1);

				if (_readyToAlert && UseAlerts &&
					ContainsCurrentLines(bar - 1))
				{
					AddAlert(AlertFile, "StackedImbalance was triggered");
					_readyToAlert = false;
				}

				return;
			}

			_readyToAlert = true;

			var closePrice = GetCandle(bar).Close;

			if (UseCrossAlerts && _lastClose != 0)
			{
				foreach (var line in HorizontalLinesTillTouch.Where(x => x.SecondBar >= bar))
				{
					var price = line.FirstPrice;

					if (_priceAlerts.Contains(price))
						continue;

					if (_lastClose < price && closePrice >= price
					    ||
					    _lastClose > price && closePrice <= price)
					{
						AddAlert(AlertFile, $"Price reached {price} level");
						_priceAlerts.Add(price);
					}
				}
			}

			_lastClose = closePrice;
		}

		private bool ContainsCurrentLines(int bar)
		{
			for (var i = HorizontalLinesTillTouch.Count - 1; i >= 0; i--)
			{
				var item = HorizontalLinesTillTouch[i];

				if (item.FirstBar == bar)
					return true;

				if (item.FirstBar < bar)
					break;
			}

			return false;
		}
		
		#endregion

		#region Private methods

		private void OnFilterChanged(string property)
		{
			// binding the instrument valuation is not an edit
			if (property != nameof(VolumeFilter.Valuation))
				RecalculateValues();
		}

		private void CalculateImbalances(int bar)
		{
			var candle = GetCandle(bar);
			var tickSize = InstrumentInfo.TickSize;
			var previousBid = 0m;
			var firstAskPrice = 0m;
			var firstBidPrice = 0m;
			var askCount = 0;
			var bidCount = 0;
			_bidStacks.Clear();

			// Visit empty ticks too: diagonal comparisons must stay exactly one tick apart.
			for (var price = candle.Low; price <= candle.High; price += tickSize)
			{
				var volumeInfo = candle.GetPriceVolumeInfo(price, _volumeInfo);
				var bid = volumeInfo?.Bid ?? 0;
				var ask = volumeInfo?.Ask ?? 0;

				if (price > candle.Low)
				{
					var askFilterValue = previousBid * _imbalanceRatio / 100;

					if ((!_ignoreZeroValues || askFilterValue != 0)
						&& ask > askFilterValue && _imbalanceVolume.Compare(ask, price) > 0)
					{
						if (askCount == 0)
							firstAskPrice = price;

						askCount++;
					}
					else
					{
						AddImbalanceLines(bar, firstAskPrice, askCount, tickSize, _askBidPen);
						askCount = 0;
					}

					var bidFilterValue = ask * _imbalanceRatio / 100;
					var bidPrice = price - tickSize;

					if ((!_ignoreZeroValues || bidFilterValue != 0)
						&& previousBid > bidFilterValue && _imbalanceVolume.Compare(previousBid, bidPrice) > 0)
					{
						if (bidCount == 0)
							firstBidPrice = bidPrice;

						bidCount++;
					}
					else
					{
						QueueBidStack(firstBidPrice, bidCount);
						bidCount = 0;
					}
				}

				// Copy the value before the next lookup overwrites the reusable price-volume record.
				previousBid = bid;
			}

			AddImbalanceLines(bar, firstAskPrice, askCount, tickSize, _askBidPen);
			QueueBidStack(firstBidPrice, bidCount);

			// Preserve the original drawing order: every Ask stack precedes every Bid stack.
			foreach (var stack in _bidStacks)
				AddImbalanceLines(bar, stack.Price, stack.Count, tickSize, _bidAskPen);
		}

		private void QueueBidStack(decimal firstPrice, int count)
		{
			if (count > 0 && count >= _imbalanceRange)
				_bidStacks.Add((firstPrice, count));
		}

		private void AddImbalanceLines(int bar, decimal firstPrice, int count, decimal tickSize, CrossPen pen)
		{
			if (count < _imbalanceRange)
				return;

			for (var i = 0; i < count; i++)
			{
				var price = firstPrice + i * tickSize;
				AddHorizontalLineTillTouch(_tillTouch || _drawBarsLength == 0
					? new LineTillTouch(bar, price, pen)
					: new LineTillTouch(bar, price, pen, _drawBarsLength));
			}
		}

		private System.Drawing.Color GetDrawingColor(CrossColor color)
		{
			var drawingColor = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
			return drawingColor;
		}

		#endregion
	}
}
