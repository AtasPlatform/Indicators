namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using Pen = System.Drawing.Pen;
	
    [DisplayName("Stacked Imbalance")]
	[Description("Stacked Imbalance")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StackedImbalanceDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602474")]
	public class StackedImbalance : Indicator
	{
		#region Fields

		private readonly Pen _askBidPen;
		private readonly Pen _bidAskPen;

		private CrossColor _askBidImbalanceColor = DefaultColors.Green.Convert();
		private CrossColor _bidAskImbalanceColor = DefaultColors.DarkRed.Convert();
		private int _days = 20;
        private int _drawBarsLength = 10;
		private bool _ignoreZeroValues;
		private int _imbalanceRange = 3;

		private int _imbalanceRatio = 300;
		private int _imbalanceVolume = 30;
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

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ImbalanceVolume), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MinVolumeFilterCommonDescription))]
        [Range(0, 10000000)]
        public int ImbalanceVolume
        {
            get => _imbalanceVolume;
            set
            {
                _imbalanceVolume = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
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
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.IsApproximationAlertDescription))]
		public bool UseCrossAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
		public string AlertFile { get; set; } = "alert1";

		#endregion

		#region ctor

		public StackedImbalance()
			: base(true)
		{
			_askBidPen = new Pen(GetDrawingColor(_askBidImbalanceColor));
			_bidAskPen = new Pen(GetDrawingColor(_bidAskImbalanceColor));

			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

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
				HorizontalLinesTillTouch.RemoveAll(t => t.FirstBar == bar - 1);
				var volumes = GetVolumes(bar - 1);
				CalculateAskBid(bar - 1, volumes);
				CalculateBidAsk(bar - 1, volumes);

				if (_readyToAlert &&
				    UseAlerts &&
				    HorizontalLinesTillTouch.Any(x => x.FirstBar == bar - 1)
				   )

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

		#endregion

		#region Private methods

		private List<decimal[]> GetVolumes(int bar)
		{
			var candle = GetCandle(bar);
			var volumes = new List<decimal[]>();

			for (var price = candle.Low; price <= candle.High; price += InstrumentInfo.TickSize)
			{
				var volumeInfo = candle.GetPriceVolumeInfo(price);

				if (volumeInfo == null)
					continue;

				var volume = new decimal[3];
				volume[0] = price;
				volume[1] = volumeInfo.Bid;
				volume[2] = volumeInfo.Ask;
				volumes.Add(volume);
			}

			return volumes;
		}

		private void CalculateAskBid(int bar, List<decimal[]> volumes) // Ask/Bid
		{
			var imbalance = new bool[volumes.Count];

			for (var i = 0; i < volumes.Count - 1; i++)
			{
				var lowVolume = volumes[i];
				var highVolume = volumes[i + 1];
				var askFilterValue = lowVolume[1] * _imbalanceRatio / 100;

				if (_ignoreZeroValues && askFilterValue == 0)
					continue;

				if (highVolume[2] > askFilterValue && highVolume[2] > _imbalanceVolume) // Ask > volume
					imbalance[i] = true;
			}

			var count = 0;

			for (var i = 0; i < volumes.Count; i++)
			{
				if (imbalance[i])
					count++;
				else
				{
					if (count >= _imbalanceRange)
					{
						if (_drawBarsLength != 0)
						{
							for (var k = i - count + 1; k <= i; k++)
							{
								HorizontalLinesTillTouch.Add(_tillTouch
									? new LineTillTouch(bar, volumes[k][0], _askBidPen)
									: new LineTillTouch(bar, volumes[k][0], _askBidPen, _drawBarsLength));
							}
						}
						else
						{
							for (var k = i - count + 1; k <= i; k++)
								HorizontalLinesTillTouch.Add(new LineTillTouch(bar, volumes[k][0], _askBidPen));
						}
					}

					count = 0;
				}
			}
		}

		private void CalculateBidAsk(int bar, List<decimal[]> volumes) // Bid/Ask
		{
			var imbalance = new bool[volumes.Count];

			for (var i = 0; i < volumes.Count - 1; i++)
			{
				var lowVolume = volumes[i];
				var highVolume = volumes[i + 1];
				var bidFilterValue = highVolume[2] * _imbalanceRatio / 100;

				if (_ignoreZeroValues && bidFilterValue == 0)
					continue;

				if (lowVolume[1] > bidFilterValue && lowVolume[1] > _imbalanceVolume) // Bid
					imbalance[i] = true;
			}

			var count = 0;

			for (var i = 0; i < volumes.Count; i++)
			{
				if (imbalance[i])
					count++;
				else
				{
					if (count >= _imbalanceRange)
					{
						if (_drawBarsLength != 0)
						{
							for (var k = i - count + 1; k <= i; k++)
							{
								HorizontalLinesTillTouch.Add(_tillTouch
									? new LineTillTouch(bar, volumes[k - 1][0], _bidAskPen)
									: new LineTillTouch(bar, volumes[k - 1][0], _bidAskPen, _drawBarsLength));
							}
						}
						else
						{
							for (var k = i - count + 1; k <= i; k++)
								HorizontalLinesTillTouch.Add(new LineTillTouch(bar, volumes[k - 1][0], _bidAskPen));
						}
					}

					count = 0;
				}
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