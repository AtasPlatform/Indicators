namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Threading;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[DisplayName("Bar Timer")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/9196-bar-timer")]
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
		private bool _offsetIsSetted;
		private Color _textColor;
		private Location _timeLocation;
		private Timer _timer;
		private int customOffset;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "TimeSettings", Name = "TimeFormat", Order = 100)]
		public Format TimeFormat { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "TimeSettings", Name = "Mode", Order = 110)]
		public Mode TimeMode { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "OffsetX", Order = 200)]
		public int OffsetX { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "OffsetY", Order = 210)]
		public int OffsetY { get; set; }

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Size", Order = 210)]
		public int Size
		{
			get => (int)Math.Floor(_font.Size);
			set
			{
				if (value <= 0)
					return;

				_font = new RenderFont("Arial", value);
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Location", Order = 210)]
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
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var frameType = ChartInfo.ChartType;
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				_barLength = CalculateBarLength();
				_offsetIsSetted = false;

				if (frameType != "Seconds"
					&& frameType != "Tick"
					&& frameType != "Volume"
					&& frameType != "TimeFrame")
					_isUnsupportedTimeFrame = true;

				if (frameType == "Tick" || frameType == "Volume")
					_offsetIsSetted = true;
				return;
			}

			if (bar != ChartInfo.PriceChartContainer.TotalBars)
				return;

			if (frameType == "Seconds" || frameType == "TimeFrame")
				_endTime = candle.Time.AddSeconds(_barLength);

			if (!_offsetIsSetted && _lastBar == bar)
				_offsetIsSetted = true;

			_lastBar = bar;

			if (InstrumentInfo.Exchange == "FORTS" || InstrumentInfo.Exchange == "TQBR" || InstrumentInfo.Exchange == "CETS")
			{
				_customOffset = 3;

				_endTime = _endTime == DateTime.MinValue
					? _endTime
					: _endTime.AddHours(-3);
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var totalBars = ChartInfo.PriceChartContainer.TotalBars;

			if (totalBars < 0)
				return;

			var candle = GetCandle(totalBars);

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
				if (!_offsetIsSetted)
					renderText = Resources.WaitingForNewTick;

				if (_isUnsupportedTimeFrame)
					renderText = Resources.UnsupportedTimeFrame;
			}

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
						var diff = _endTime - DateTime.UtcNow;

						if (diff.TotalSeconds < 0)
							diff = new TimeSpan();

						renderText = diff.ToString(
							format != ""
								? format
								: diff.Hours == 0
									? @"mm\:ss"
									: @"hh\:mm\:ss");
					}

					break;
			}

			if (!isBarTimerMode)
			{
				var time = DateTime.UtcNow.AddHours(_customOffset + InstrumentInfo.TimeZone);

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

			context.FillRectangle(_backGroundColor, rect);
			context.DrawString(renderText, _font, _textColor, rect, _format);
		}

		protected override void OnInitialize()
		{
			_timer = new Timer(
				e => { RedrawChart(); },
				null,
				TimeSpan.Zero,
				TimeSpan.FromSeconds(1));
		}

		protected override void OnDispose()
		{
			_timer?.Dispose();
		}

		#endregion

		#region Private methods

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

			if (ChartInfo.ChartType == "Tick"
				|| ChartInfo.ChartType == "Volume")
				return int.Parse(ChartInfo.TimeFrame);

			return 0;
		}

		#endregion
	}
}