namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Extensions;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;
	using IndicatorFilterColor = ATAS.Indicators.FilterColor;

    [DisplayName("Bar Timer")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarTimerDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602327")]
	public class BarTimer : Indicator
	{
		#region Nested types

		public enum Format
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Auto))]
			Auto,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HHMMSS))]
			HHMMSS,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HHMMSSPM))]
			HHMMSSPM,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MMSS))]
			MMSS
		}

		public enum Location
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopLeft))]
			TopLeft,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopRight))]
			TopRight,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomLeft))]
			BottomLeft,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomRight))]
			BottomRight,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UnderCurrentPriceLabel))]
			Price
		}

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeToEndOfCandle))]
			TimeToEndOfCandle,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CurrentTime))]
			CurrentTime
		}

        #endregion

        #region Static and constants

        private static readonly TimeSpan _timerLength = TimeSpan.FromSeconds(1);
        private const int _daySeconds = 86400;
		private const int _weekSeconds = 604800;

        #endregion

        #region Fields

        private readonly RenderStringFormat _format = new()
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private int _barLength;
		private int _customOffset;
		private DateTime _endTime;
		private RenderFont _font = new("Arial", 15);
		private bool _isUnsupportedTimeFrame;
		private int _lastBar;
		private int _lastBeforeAlert;
#pragma warning disable CS0414
		private int _lastSecond = -1;
#pragma warning restore CS0414
		private bool _offsetIsSet;
		private Location _timeLocation;
		private CrossColor _textBeforeColor = DefaultColors.Red.Convert();
		private CrossColor _areaBeforeColor = DefaultColors.Yellow.Convert();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeSettings), Name = nameof(Strings.TimeFormat), Description = nameof(Strings.TimeFormatDescription), Order = 100)]
		[Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
		public Format TimeFormat { get; set; }

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeSettings), Name = nameof(Strings.Mode), Description = nameof(Strings.TimeModeDescription), Order = 110)]
		[Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
		public Mode TimeMode { get; set; }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.TimeSettings), Name = nameof(Strings.TimeZone), Description = nameof(Strings.TimeZoneDescription), Order = 120)]
        [Tab(TabName = nameof(Strings.Data), TabOrder = 0, ResourceType = typeof(Strings))]
        [Range(-23, 23)]
        public int CustomTimeZone { get; set; }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.OffsetX), Description = nameof(Strings.LabelOffsetXDescription), Order = 200)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		[Range(-10000, 10000)]
		public FilterInt OffsetX { get; set; } = new(false, true) { Value = 10 };

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.OffsetY), Description = nameof(Strings.LabelOffsetYDescription), Order = 210)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		[Range(-10000, 10000)]
		public FilterInt OffsetY { get; set; } = new(false, true) { Value = 15 };

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Size), Description = nameof(Strings.FontSizeDescription), Order = 220)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		[Range(1, 100)]
		public FilterInt Size { get; set; } = new(false, true) { Value = 15 };

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Settings), Name = nameof(Strings.Location), Description = nameof(Strings.LabelLocationDescription), Order = 195)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public Location TimeLocation
		{
			get => _timeLocation;
			set
			{
				_timeLocation = value;
				UpdateLocationDependentProperties();
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.Color), Description = nameof(Strings.LabelTextColorDescription), Order = 300)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public IndicatorFilterColor TextColor { get; set; } = new(false, true) { Value = CrossColors.White };

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Colors), Name = nameof(Strings.BackGround), Description = nameof(Strings.LabelFillColorDescription), Order = 310)]
		[Tab(TabName = nameof(Strings.Visualization), TabOrder = 1, ResourceType = typeof(Strings))]
		public IndicatorFilterColor BackGroundColor { get; set; } = new(false, true) { Value = DefaultColors.Green.Convert() };

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.AlertNewCandle), Name = nameof(Strings.UseAlerts), Description = nameof(Strings.UseAlertsDescription), Order = 400)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public bool UseAlert { get; set; }

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.AlertNewCandle), Name = nameof(Strings.AlertFile), Description = nameof(Strings.AlertFileDescription), Order = 410)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.AlertNewCandle), Name = nameof(Strings.TextColor), Description = nameof(Strings.AlertTextColorDescription), Order = 420)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public CrossColor AlertTextColor { get; set; } = CrossColors.White;

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.AlertNewCandle), Name = nameof(Strings.AreaColor), Description = nameof(Strings.AlertFillColorDescription), Order = 430)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public CrossColor AlertBackgroundColor { get; set; } = CrossColors.Black;

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.UseAlerts), Description = nameof(Strings.UseAlertBeforeDescription), Order = 500)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public bool UseAlertBefore { get; set; }

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.AlertFile), Description = nameof(Strings.AlertBeforeFileDescription), Order = 510)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		[OFT.Attributes.Editors.SoundComboBoxEditor]
		public string AlertBeforeFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.Seconds), Description = nameof(Strings.AlertBeforeSecondsDescription), Order = 520)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		[Range(1, 10000)]
		public int AlertBeforeSeconds { get; set; } = 5;

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.ShowArea), Description = nameof(Strings.ShowAlertAreaDescription), Order = 530)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public bool ShowAlertArea { get; set; }

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.AreaColor), Description = nameof(Strings.LabelFillColorDescription), Order = 540)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public Color AreaBeforeColor
		{
			get => _areaBeforeColor.Convert();
			set => _areaBeforeColor = value.Convert();
		} 

		[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.ColorBeforeCandle), Name = nameof(Strings.TextColor), Description = nameof(Strings.LabelTextColorDescription), Order = 550)]
		[Tab(TabName = nameof(Strings.Alerts), TabOrder = 2, ResourceType = typeof(Strings))]
		public Color TextBeforeColor
		{
			get => _textBeforeColor.Convert();
			set => _textBeforeColor = value.Convert();
		}

		#endregion

		#region ctor

		public BarTimer()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			_lastBar = -1;
			OffsetX.ValueOnChanged(_ => RedrawChart());
			OffsetY.ValueOnChanged(_ => RedrawChart());
			Size.ValueOnChanged(value =>
			{
				_font = new RenderFont("Arial", value);
				RedrawChart();
			});
			TextColor.ValueOnChanged(_ => RedrawChart());
			BackGroundColor.ValueOnChanged(_ => RedrawChart());
			TimeLocation = Location.BottomRight;

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
			var frameType = ChartInfo.ChartType;
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_lastBeforeAlert = -1;
				_customOffset = 0;
				_offsetIsSet = false;
				_barLength = CalculateBarLength();

				_isUnsupportedTimeFrame = frameType != "Seconds"
				    && frameType != "Tick"
				    && frameType != "Volume"
				    && frameType != "TimeFrame"
				    && !IsPriceBasedTimeFrame(frameType);
				
				_lastBar = CurrentBar - 1;

				return;
			}

			if (bar != CurrentBar - 1)
				return;

			if (!_offsetIsSet && _lastBar == bar)
				_offsetIsSet = true;

            if (UseAlert && _lastBar != bar && bar == CurrentBar - 1 && _offsetIsSet)
	            AddAlert(AlertFile, InstrumentInfo.Instrument, "New bar", AlertBackgroundColor, AlertTextColor);

            if (_isUnsupportedTimeFrame)
            {
	            _lastBar = bar;
	            return;
            }

            if (frameType is "Seconds" or "TimeFrame")
                _endTime = candle.Time.AddSeconds(_barLength);

            _lastBar = bar;
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (ChartInfo is null)
				return;

			if (CurrentBar < 0)
				return;

			var candle = GetCandle(CurrentBar - 1);

			var isBarTimerMode = TimeMode == Mode.TimeToEndOfCandle;

			var format = TimeFormat switch
			{
				Format.HHMMSS => isBarTimerMode ? @"hh\:mm\:ss" : "HH:mm:ss",
				Format.HHMMSSPM => isBarTimerMode ? @"hh\:mm\:ss" : "hh:mm:ss tt",
				Format.MMSS => @"mm\:ss",
				_ => ""
			};

			var renderText = "";

			if (isBarTimerMode)
			{
				if (!_offsetIsSet)
					renderText = Strings.WaitingForNewTick;

				if (_isUnsupportedTimeFrame)
					renderText = Strings.OnlyAlertsSupported;

				switch (ChartInfo.ChartType)
				{
					case "Tick":
						renderText = $"{_barLength - candle.Ticks:0.##} ticks";
						break;

					case "Volume":
						renderText = $"{_barLength - candle.Volume:0.##} lots";
						break;
					case "Range":
					case "RangeX":
					case "RangeXV":
					case "RangeZ":
					case "RangeUS":
					case "Renko":
					case "ReversalX":
						if (TryGetPriceBasedTicksToClose(CurrentBar - 1, candle, out var ticksToClose))
							renderText = $"{ticksToClose:0.##} ticks";
						else
							renderText = ChartInfo.ChartType == "ReversalX"
								? "N/A"
								: Strings.OnlyAlertsSupported;
						break;

					case "Seconds":
					case "TimeFrame":
						if (string.IsNullOrEmpty(renderText))
						{
							var rolledOver = _endTime <= MarketTime;
							var diff = CurrentDifference();

							var (newLastBeforeAlert, fireAlert, _) = EvaluateAlertBeforeState(
								rolledOver,
								diff.TotalSeconds,
								AlertBeforeSeconds,
								CurrentBar - 1,
								_lastBeforeAlert,
								UseAlertBefore,
								ShowAlertArea);

							_lastBeforeAlert = newLastBeforeAlert;

							if (fireAlert)
								AddAlert(AlertBeforeFile, InstrumentInfo.Instrument, $"New bar incoming: {diff.TotalSeconds:0.} seconds",
									_areaBeforeColor, _textBeforeColor);

							if (diff.TotalSeconds < 0)
								diff = new TimeSpan();

							renderText = diff.ToString(
								format != ""
									? format
									: diff.Hours == 0
										? @"mm\:ss"
										: @"hh\:mm\:ss"
								, CultureInfo.InvariantCulture);
						}

						break;
				}
			}

			if (!isBarTimerMode)
			{
				var time = MarketTime.AddHours(_customOffset + CustomTimeZone).Add(InstrumentInfo.TimeZoneOffset);

				renderText = time.ToString(
					format != ""
						? format
						: @"HH\:mm\:ss"
					, CultureInfo.InvariantCulture);
			}

			var size = context.MeasureString(renderText, _font);
			var height = size.Height;
			var width = size.Width + 10;
			var rect = new Rectangle();

			var x0 = Container.Region.X;
			var y0 = Container.Region.Y;
			var timeLocation = TimeLocation == Location.Price && _isUnsupportedTimeFrame
				? Location.BottomLeft
				: TimeLocation;

			switch (timeLocation)
			{
				case Location.TopLeft:
					rect = new Rectangle(x0 + OffsetX.Value, y0 + OffsetY.Value, width, height);
					break;
				case Location.TopRight:
					rect = new Rectangle(x0 + Container.Region.Width - width - OffsetX.Value, y0 + OffsetY.Value, width, height);
					break;
				case Location.BottomLeft:
					rect = new Rectangle(x0 + OffsetX.Value, y0 + Container.Region.Height - OffsetY.Value - height, width, height);
					break;
				case Location.BottomRight:
					rect = new Rectangle(x0 + Container.Region.Width - width - OffsetX.Value, y0 + Container.Region.Height - OffsetY.Value - height, width, height);
					break;
			}

			var drawAlertArea = ShowAlertArea && _lastBeforeAlert == CurrentBar - 1;

			var bgColor = drawAlertArea
				? AreaBeforeColor
				: GetBackgroundColor(timeLocation, candle);

			var textColor = drawAlertArea
				? TextBeforeColor
				: GetTextColor(timeLocation);

			if (timeLocation == Location.Price)
			{
				DrawPriceScaleMarker(context, candle.Close, renderText, bgColor, textColor);
				return;
			}

			context.FillRectangle(bgColor, rect);
			context.DrawString(renderText, _font, textColor, rect, _format);
		}

		protected override void OnInitialize()
		{
			SubscribeToTimer(_timerLength, Redraw);
        }
		
		protected override void OnDispose()
		{
			UnsubscribeFromTimer(_timerLength, Redraw);
		}

		#endregion

        #region Private methods
		
        private void Redraw()
        {
	        RedrawChart();
        }

        private void UpdateLocationDependentProperties()
        {
	        var enabled = TimeLocation != Location.Price;

	        OffsetX.Enabled = enabled;
	        OffsetY.Enabled = enabled;
	        Size.Enabled = enabled;
	        TextColor.Enabled = enabled;
	        BackGroundColor.Enabled = enabled;
        }

        private Color GetBackgroundColor(Location timeLocation, IndicatorCandle candle)
        {
	        if (timeLocation != Location.Price || ChartInfo is null)
		        return BackGroundColor.Value.Convert();

	        return candle.Close < candle.Open
		        ? ChartInfo.ColorsStore.DownCandleColor
		        : ChartInfo.ColorsStore.UpCandleColor;
        }

        private Color GetTextColor(Location timeLocation)
        {
	        return timeLocation == Location.Price && ChartInfo is not null
		        ? ChartInfo.ColorsStore.MouseTextColor
		        : TextColor.Value.Convert();
        }

        private TimeSpan CurrentDifference()
		{
			var timeleft = _endTime - MarketTime;

			// when the bar is done but no new candles arrived
			if (timeleft <= TimeSpan.Zero)
				timeleft += TimeSpan.FromSeconds(_barLength);

			return timeleft;
		}

		private int CalculateBarLength()
		{
			if (ChartInfo.ChartType == "Seconds")
				return int.Parse(ChartInfo.TimeFrame);

			if (ChartInfo.ChartType == "TimeFrame")
			{
				if (ChartInfo.TimeFrame.Contains("M"))
					return int.Parse(ChartInfo.TimeFrame.Replace("M", "")) * 60;

				if (ChartInfo.TimeFrame.Contains("H"))
					return int.Parse(ChartInfo.TimeFrame.Replace("H", "")) * 3600;

				if (ChartInfo.TimeFrame == "Daily")
					return _daySeconds;

				if (ChartInfo.TimeFrame == "Weekly")
					return _weekSeconds;
			}

			if (ChartInfo.ChartType is "Tick" or "Volume")
				return int.Parse(ChartInfo.TimeFrame);

			return 0;
		}

		private bool TryGetPriceBasedTicksToClose(int bar, IndicatorCandle candle, out decimal ticksToClose)
		{
			ticksToClose = 0;

			if (ChartInfo is null)
				return false;

			var step = ChartInfo.PriceChartContainer.Step;

			if (step <= 0)
				return false;

			var close = ToPriceLevel(candle.Close, step);
			var open = ToPriceLevel(candle.Open, step);
			var high = ToPriceLevel(candle.High, step);
			var low = ToPriceLevel(candle.Low, step);
			var frameType = ChartInfo.ChartType;

			if (frameType == "RangeUS")
			{
				if (!TryParseTimeFrame(ChartInfo.TimeFrame, out var openOffset, out var tickTrend, out var tickReversal))
					return false;

				var isFirstSessionBar = bar <= 0 || DataProvider.IsNewSession(GetCandle(bar - 1).Time, candle.Time);
				decimal upper;
				decimal lower;

				if (isFirstSessionBar)
				{
					if (openOffset > 0 && open % openOffset != 0)
						return false;

					upper = open + tickTrend;
					lower = open - tickTrend;
				}
				else
				{
					var previousCandle = GetCandle(bar - 1);
					var previousOpen = ToPriceLevel(previousCandle.Open, step);
					var previousClose = ToPriceLevel(previousCandle.Close, step);

					if (previousClose == previousOpen)
						return false;

					if (previousClose > previousOpen)
					{
						upper = open + tickTrend;
						lower = open - tickReversal;
					}
					else
					{
						upper = open + tickReversal;
						lower = open - tickTrend;
					}
				}

				ticksToClose = GetClosestDistance(close, upper, lower);
				return true;
			}

			if (frameType == "ReversalX")
			{
				if (!TryParseTimeFrame(ChartInfo.TimeFrame, out var probe, out var value))
					return false;

				var direction = close - open;

				if (high - low < probe || direction == 0)
					return false;

				ticksToClose = direction > 0
					? Math.Max(0, close - (high - value))
					: Math.Max(0, low + value - close);

				return true;
			}

			if (!TryParseTimeFrame(ChartInfo.TimeFrame, out var period))
				return false;

			decimal upperLevel;
			decimal lowerLevel;

			switch (frameType)
			{
				case "Range":
					upperLevel = low + period + 1;
					lowerLevel = high - period - 1;
					break;
				case "RangeX":
					var previousOpen = GetPreviousOpen(bar, open, step);
					upperLevel = Math.Max(open + period, previousOpen + period) + 1;
					lowerLevel = Math.Min(open - period, previousOpen - period) - 1;
					break;
				case "RangeXV":
					previousOpen = GetRangeXvPreviousOpen(bar, open, step);
					upperLevel = Math.Max(open + period, previousOpen + period);
					lowerLevel = Math.Min(open - period, previousOpen - period);
					break;
				case "RangeZ":
					upperLevel = open + period;
					lowerLevel = open - period;
					break;
				case "Renko":
					var renkoPeriod = Math.Max(period, 2) - 1;

					if (bar <= 0)
					{
						upperLevel = open + renkoPeriod;
						lowerLevel = open - renkoPeriod;
					}
					else
					{
						var previousCandle = GetCandle(bar - 1);
						var previousRenkoOpen = ToPriceLevel(previousCandle.Open, step);
						var previousClose = ToPriceLevel(previousCandle.Close, step);

						if (previousClose > previousRenkoOpen)
						{
							upperLevel = previousClose + renkoPeriod;
							lowerLevel = previousClose - renkoPeriod * 2;
						}
						else
						{
							upperLevel = previousClose + renkoPeriod * 2;
							lowerLevel = previousClose - renkoPeriod;
						}
					}

					break;
				default:
					return false;
			}

			ticksToClose = GetClosestDistance(close, upperLevel, lowerLevel);
			return true;
		}

		private decimal GetPreviousOpen(int bar, decimal currentOpen, decimal step)
		{
			if (bar <= 1)
				return currentOpen;

			return ToPriceLevel(GetCandle(bar - 1).Open, step);
		}

		private decimal GetRangeXvPreviousOpen(int bar, decimal currentOpen, decimal step)
		{
			if (bar <= 1)
				return currentOpen;

			var previousCandle = GetCandle(bar - 1);

			if (DataProvider.IsNewSession(GetCandle(bar - 2).Time, previousCandle.Time))
				return currentOpen;

			return ToPriceLevel(previousCandle.Open, step);
		}

		private static decimal GetClosestDistance(decimal price, decimal upperLevel, decimal lowerLevel)
		{
			var upperDistance = Math.Max(0, upperLevel - price);
			var lowerDistance = Math.Max(0, price - lowerLevel);

			return Math.Min(upperDistance, lowerDistance);
		}

		private static bool IsPriceBasedTimeFrame(string frameType)
		{
			return frameType is "Range"
				or "RangeX"
				or "RangeXV"
				or "RangeZ"
				or "RangeUS"
				or "Renko"
				or "ReversalX";
		}

		private static decimal ToPriceLevel(decimal price, decimal step)
		{
			return Math.Round(price / step, 8, MidpointRounding.AwayFromZero);
		}

		private static bool TryParseTimeFrame(string timeFrame, out int value)
		{
			value = 0;
			return int.TryParse(timeFrame, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
		}

		private static bool TryParseTimeFrame(string timeFrame, out int first, out int second)
		{
			first = 0;
			second = 0;

			var values = timeFrame.Split('/');

			return values.Length == 2
				&& int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first)
				&& int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out second);
		}

		private static bool TryParseTimeFrame(string timeFrame, out int first, out int second, out int third)
		{
			first = 0;
			second = 0;
			third = 0;

			var values = timeFrame.Split('/');

			return values.Length == 3
				&& int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out first)
				&& int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out second)
				&& int.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out third);
		}

		private void DrawPriceScaleMarker(RenderContext context, decimal price, string indicatorText, Color backgroundColor, Color textColor)
		{
			var bounds = context.ClipBounds;
			context.ResetClip();

			try
			{
				var priceText = ChartInfo.GetPriceString(price);
				var font = ChartInfo.PriceAxisFont;
				var priceTextSize = context.MeasureString(priceText, font);
				var indicatorTextSize = context.MeasureString(indicatorText, font);

				var lineHeight = Math.Max(priceTextSize.Height, indicatorTextSize.Height);
				var labelHeight = lineHeight * 2;
				var x = ChartInfo.PriceChartContainer.Region.Right;
				var y = ChartInfo.GetYByPrice(price, false);
				var arrowWidth = Math.Max(3, lineHeight / 4);
				var rightX = ChartInfo.ChartContainer.Region.Right;
				var leftX = x + arrowWidth;

				var upperY = y - labelHeight / 2;
				var lowerY = upperY + labelHeight;

				var axis = ChartInfo.PriceChartContainer.Region with
				{
					X = x,
					Width = rightX - x
				};

				context.SetClip(axis);

				var points = new[]
				{
					new Point(x, y),
					new Point(leftX, upperY),
					new Point(rightX, upperY),
					new Point(rightX, lowerY),
					new Point(leftX, lowerY)
				};

				context.FillPolygon(backgroundColor, points);

				var textRect = new Rectangle(leftX, upperY, Math.Max(1, rightX - leftX - 2), lineHeight);
				context.DrawString(priceText, font, textColor, textRect, _format);

				textRect.Y += lineHeight;
				context.DrawString(indicatorText, font, textColor, textRect, _format);
			}
			finally
			{
				context.SetClip(bounds);
			}
		}

		#endregion

		#region Private static methods

		// Pure transition used by OnRender for the "color/alert before next candle" logic.
		// Returns the next value of _lastBeforeAlert, whether an alert must fire this tick, and
		// whether the alert area should currently be painted.
		// rolledOver means "_endTime has already passed but OnCalculate for the new bar has not
		// arrived yet" — without this reset the alert color would stay on after the bar closed.
		private static (int lastBeforeAlert, bool fireAlert, bool drawAlertArea) EvaluateAlertBeforeState(
			bool rolledOver,
			double seconds,
			int alertBeforeSeconds,
			int currentBarIndex,
			int lastBeforeAlert,
			bool useAlertBefore,
			bool showAlertArea)
		{
			if (rolledOver)
				lastBeforeAlert = -1;

			var fireAlert = false;

			if ((useAlertBefore || showAlertArea) && !rolledOver
				&& seconds <= alertBeforeSeconds && lastBeforeAlert != currentBarIndex)
			{
				if (useAlertBefore)
					fireAlert = true;

				lastBeforeAlert = currentBarIndex;
			}

			var drawAlertArea = showAlertArea && lastBeforeAlert == currentBarIndex;
			return (lastBeforeAlert, fireAlert, drawAlertArea);
		}

		#endregion
	}
}
