namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Threading;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[DisplayName("Bar Timer")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/9196-bar-timer")]
	public class BarTimer : Indicator
	{
		#region Nested types

		public enum Format
		{
			[Display(ResourceType = typeof(Resources), Name = "Auto")]
			Auto,

			[Display(ResourceType = typeof(Resources), Name = "HHMMSS")]
			HHMMSS,

			[Display(ResourceType = typeof(Resources), Name = "HHMMSSPM")]
			HHMMSSPM,

			[Display(ResourceType = typeof(Resources), Name = "MMSS")]
			MMSS
		}

		public enum Location
		{
			[Display(ResourceType = typeof(Resources), Name = "TopLeft")]
			TopLeft,

			[Display(ResourceType = typeof(Resources), Name = "TopRight")]
			TopRight,

			[Display(ResourceType = typeof(Resources), Name = "BottomLeft")]
			BottomLeft,

			[Display(ResourceType = typeof(Resources), Name = "BottomRight")]
			BottomRight
		}

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "TimeToEndOfCandle")]
			TimeToEndOfCandle,

			[Display(ResourceType = typeof(Resources), Name = "CurrentTime")]
			CurrentTime
		}

		#endregion

		#region Static and constants

		private const int _daySeconds = 86400;
		private const int _weekSeconds = 604800;

		#endregion

		#region Fields

		private readonly RenderStringFormat _format = new()
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private Color _backGroundColor;
		private int _barLength;
		private int _customOffset;
		private DateTime _endTime;
		private RenderFont _font = new("Arial", 15);
		private bool _isUnsupportedTimeFrame;
		private int _lastBar;
		private int _lastBeforeAlert;
		private int _lastSecond = -1;
		private bool _offsetIsSet;
		private Color _textColor;
		private TimeSpan _timeDiff;
		private Location _timeLocation;
		private Timer _timer;
		private System.Windows.Media.Color _textBeforeColor = DefaultColors.Red.Convert();
		private System.Windows.Media.Color _areaBeforeColor = DefaultColors.Yellow.Convert();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "CustomTimeZone", Name = "TimeFormat", Order = 100)]
		[Range(-23, 23)]
		public int CustomTimeZone { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "TimeSettings", Name = "TimeFormat", Order = 100)]
		public Format TimeFormat { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "TimeSettings", Name = "Mode", Order = 110)]
		public Mode TimeMode { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "OffsetX", Order = 200)]
		[Range(-10000, 10000)]
		public int OffsetX { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "OffsetY", Order = 210)]
		[Range(-10000, 10000)]
		public int OffsetY { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Size", Order = 220)]
		[Range(1, 100)]
		public int Size
		{
			get => (int)Math.Floor(_font.Size);
			set => _font = new RenderFont("Arial", value);
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Location", Order = 230)]
		public Location TimeLocation
		{
			get => _timeLocation;
			set
			{
				_timeLocation = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Colors", Name = "Color", Order = 300)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = Color.FromArgb(value.A, value.R, value.G, value.B);
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Colors", Name = "BackGround", Order = 310)]
		public System.Windows.Media.Color BackGroundColor
		{
			get => _backGroundColor.Convert();
			set => _backGroundColor = Color.FromArgb(value.A, value.R, value.G, value.B);
		}

		[Display(ResourceType = typeof(Resources), GroupName = "AlertNewCandle", Name = "UseAlerts", Order = 400)]
		public bool UseAlert { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "AlertNewCandle", Name = "AlertFile", Order = 410)]
		public string AlertFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), GroupName = "AlertNewCandle", Name = "TextColor", Order = 420)]
		public System.Windows.Media.Color AlertTextColor { get; set; } = Colors.White;

		[Display(ResourceType = typeof(Resources), GroupName = "AlertNewCandle", Name = "AreaColor", Order = 430)]
		public System.Windows.Media.Color AlertBackgroundColor { get; set; } = Colors.Black;

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "UseAlerts", Order = 500)]
		public bool UseAlertBefore { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "AlertFile", Order = 510)]
		public string AlertBeforeFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "Seconds", Order = 520)]
		[Range(1, 10000)]
		public int AlertBeforeSeconds { get; set; } = 5;

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "ShowArea", Order = 530)]
		public bool ShowAlertArea { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "AreaColor", Order = 540)]
		public Color AreaBeforeColor
		{
			get => _areaBeforeColor.Convert();
			set => _areaBeforeColor = value.Convert();
		} 

		[Display(ResourceType = typeof(Resources), GroupName = "ColorBeforeCandle", Name = "TextColor", Order = 550)]
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
			OffsetX = 10;
			OffsetY = 15;
			Size = 15;
			TimeLocation = Location.BottomRight;
			TextColor = System.Windows.Media.Color.FromArgb(218, 0, 128, 0);
			BackGroundColor = System.Windows.Media.Color.FromRgb(220, 220, 220);

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
				_timeDiff = TimeSpan.Zero;
				_offsetIsSet = false;
				_barLength = CalculateBarLength();

				if (frameType != "Seconds"
				    && frameType != "Tick"
				    && frameType != "Volume"
				    && frameType != "TimeFrame")
					_isUnsupportedTimeFrame = true;
				
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

			

			var lastCandle = GetCandle(bar);

			_timeDiff = InstrumentInfo.Exchange is "FORTS" or "TQBR" or "CETS"
				? DateTime.UtcNow - lastCandle.LastTime.AddHours(-3)
				: DateTime.UtcNow - lastCandle.LastTime;

			_lastBar = bar;

			if (InstrumentInfo.Exchange is "FORTS" or "TQBR" or "CETS")
			{
				_customOffset = 3;

				_endTime = _endTime == DateTime.MinValue
					? _endTime
					: _endTime.AddHours(-3);
			}
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
					renderText = Resources.WaitingForNewTick;

				if (_isUnsupportedTimeFrame)
					renderText = Resources.OnlyAlertsSupported;

				switch (ChartInfo.ChartType)
				{
					case "Tick":
						renderText = $"{_barLength - candle.Ticks:0.##} ticks";
						break;

					case "Volume":
						renderText = $"{_barLength - candle.Volume:0.##} lots";
						break;
					case "Seconds":
					case "TimeFrame":
						if (string.IsNullOrEmpty(renderText))
						{
							var diff = CurrentDifference();

							if (UseAlertBefore || ShowAlertArea)
							{
								var seconds = diff.TotalSeconds;

								if (seconds <= AlertBeforeSeconds && _lastBeforeAlert != CurrentBar - 1)
								{
									if (UseAlertBefore && _lastBeforeAlert != CurrentBar - 1)
										AddAlert(AlertBeforeFile, InstrumentInfo.Instrument, $"New bar incoming: {seconds:0.} seconds", _areaBeforeColor,
											_textBeforeColor);

									_lastBeforeAlert = CurrentBar - 1;
								}
							}

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
				var time = DateTime.UtcNow.AddHours(_customOffset + InstrumentInfo.TimeZone + CustomTimeZone);

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

			switch (TimeLocation)
			{
				case Location.TopLeft:
					rect = new Rectangle(x0 + OffsetX, y0 + OffsetY, width, height);
					break;
				case Location.TopRight:
					rect = new Rectangle(x0 + Container.Region.Width - width - OffsetX, y0 + OffsetY, width, height);
					break;
				case Location.BottomLeft:
					rect = new Rectangle(x0 + OffsetX, y0 + Container.Region.Height - OffsetY - height, width, height);
					break;
				case Location.BottomRight:
					rect = new Rectangle(x0 + Container.Region.Width - width - OffsetX, y0 + Container.Region.Height - OffsetY - height, width, height);
					break;
			}

			var drawAlertArea = ShowAlertArea && _lastBeforeAlert == CurrentBar - 1;

			var bgColor = drawAlertArea
				? AreaBeforeColor
				: _backGroundColor;

			var textColor = drawAlertArea
				? TextBeforeColor
				: _textColor;

			context.FillRectangle(bgColor, rect);
			context.DrawString(renderText, _font, textColor, rect, _format);
		}

		protected override void OnInitialize()
		{
			_timer = new Timer(
				e =>
				{
					if (DateTime.Now.Second != _lastSecond)
					{
						_lastSecond = DateTime.Now.Second;
						RedrawChart();
					}
				},
				null,
				TimeSpan.Zero,
				TimeSpan.FromMilliseconds(10));
		}

		protected override void OnDispose()
		{
			_timer?.Dispose();
		}

		#endregion

		#region Private methods

		private TimeSpan CurrentDifference()
		{
			return _endTime - DateTime.UtcNow + _timeDiff;
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

		#endregion
	}
}