namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	[DisplayName("Open Line")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/23629-open-line")]
	public class OpenLine : Indicator
	{
		#region Nested types

		public class Session
		{
			#region Properties

			public int StartBar { get; set; }

			public int EndBar { get; set; }

			public decimal OpenPrice { get; set; }

			public bool Touched { get; set; }

			#endregion
		}

		#endregion

		#region Fields

		private bool _customSessionStart;
		private int _days = 5;

        private RenderFont _font = new("Arial", 8);
		private int _fontSize = 8;
		private int _lastBar;

		private object _locker = new();
		private List<Session> _sessions = new();

		private TimeSpan _startDate = new(9, 0, 0);
		private int _targetBar;
		private bool _tillTouch;
		private string _openCandleText = "Open Line";

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Days",
			GroupName = "Common",
			Order = 5)]
        [Range(0, 10000)]
		public int Days
		{
			get => _days;
			set
			{
				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "CustomSessionStart",
			GroupName = "SessionTime",
			Order = 10)]
		public bool CustomSessionStart
		{
			get => _customSessionStart;
			set
			{
				_customSessionStart = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "StartTimeGmt",
			GroupName = "SessionTime",
			Order = 20)]
		public TimeSpan StartDate
		{
			get => _startDate;
			set
			{
				_startDate = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Text",
			GroupName = "TextSettings",
			Order = 30)]
		public string OpenCandleText
		{
			get => _openCandleText;
			set
			{
				if(value.Length > 1000)
					return;

				_openCandleText = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextSize",
			GroupName = "TextSettings",
			Order = 40)]
		[Range(1, 200)]
		public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = value;
				_font = new RenderFont("Arial", value);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "OffsetY",
			GroupName = "TextSettings",
			Order = 50)]
		public int Offset { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "OpenLine",
			GroupName = "Drawing",
			Order = 60)]
		public PenSettings LinePen { get; set; } = new() { Color = Colors.SkyBlue, Width = 2 };

		[Display(ResourceType = typeof(Resources), Name = "LineTillTouch",
			GroupName = "Drawing",
			Order = 62)]
		public bool TillTouch
		{
			get => _tillTouch;
			set
			{
				_tillTouch = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OpenLine()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_locker)
			{
				foreach (var session in _sessions)
				{
					if (session.StartBar > LastVisibleBarNumber)
						continue;

					var x1 = ChartInfo.GetXByBar(session.StartBar, false);

					var x2 = ChartInfo.GetXByBar(session.EndBar, false);

					if (x2 < 0)
						continue;

					var y = ChartInfo.GetYByPrice(session.OpenPrice, false);

					context.DrawLine(LinePen.RenderObject, x1, y, x2, y);

					var stringSize = context.MeasureString(OpenCandleText, _font);
					context.DrawString(OpenCandleText, _font, LinePen.RenderObject.Color, x2 - stringSize.Width, y - stringSize.Height - Offset);
				}
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_targetBar = 0;

				if (_days > 0)
				{
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
				}

				lock (_locker)
				{
					_sessions = new List<Session>
					{
						new()
						{
							StartBar = _targetBar,
							EndBar = _targetBar,
							OpenPrice = GetCandle(_targetBar).Open
						}
					};
				}
			}

			if (bar < _targetBar)
				return;

			lock (_locker)
			{
				var lastSession = _sessions[_sessions.Count - 1];

				if (_lastBar != bar || lastSession.StartBar != bar)
				{
					var isStart = _customSessionStart
						? IsNewCustomSession(bar)
						: IsNewSession(bar);

					var newSession = lastSession.StartBar != bar;

					if (isStart && newSession)
					{
						var candle = GetCandle(bar);

						_sessions.Add(new Session
						{
							StartBar = bar,
							EndBar = bar,
							OpenPrice = candle.Open
						});
					}
					else
					{
						if (!lastSession.Touched)
							lastSession.EndBar = bar;
					}
				}

				_lastBar = bar;

				if (bar == lastSession.StartBar)
					return;

				if (TillTouch && !lastSession.Touched)
				{
					var candle = GetCandle(bar);
					var open = lastSession.OpenPrice;

					if (candle.High >= open && candle.Low <= open)
					{
						lastSession.Touched = true;
						lastSession.EndBar = bar;
					}
				}
			}
		}

		#endregion

		#region Private methods

		private bool IsNewCustomSession(int bar)
		{
			if (bar == 0)
				return true;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var candleEnd = candle
				.LastTime.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			var prevCandleEnd = prevCandle
				.LastTime.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			return prevCandleEnd < _startDate && candleEnd >= _startDate;
		}

		#endregion
	}
}