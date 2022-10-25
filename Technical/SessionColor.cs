namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Reflection;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/3602-session-color")]
	[DisplayName("Session Color")]
	public class SessionColor : Indicator
	{
		#region Nested types

		private class Session
		{
			#region Properties

			public int FirstBar { get; }

			public int LastBar { get; private set; }

			private DateTime End { get; }

			private DateTime Start { get; }

			#endregion

			#region ctor

			public Session(DateTime start, DateTime end, int bar)
			{
				Start = start;
				End = end;
				FirstBar = LastBar = bar;
			}

			#endregion

			#region Public methods

			public bool TryAddCandle(int i, DateTime time)
			{
				if (time >= End)
					return false;

				if (time < Start)
					return false;

				if (i > LastBar)
					LastBar = i;

				return true;
			}

			#endregion
		}

		#endregion

		#region Fields

		private readonly List<Session> _sessions = new();
		private readonly object _syncRoot = new();

		private Color _areaColor = Color.FromArgb(63, 65, 105, 225);
		private Session _currentSession;
		private TimeSpan _endTime = new(12, 0, 0);
		private Color _fillBrush;
		private int _lastEndAlert;
		private int _lastSessionBar;

		private int _lastStartAlert;
		private TimeSpan _startTime;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources),
			Name = "ShowAboveChart",
			GroupName = "Settings",
			Order = 10)]
		public bool ShowAboveChart
		{
			get => DrawAbovePrice;
			set => DrawAbovePrice = value;
		}

		[Display(ResourceType = typeof(Resources),
			Name = "ShowArea",
			GroupName = "Settings",
			Order = 20)]
		public bool ShowArea { get; set; } = true;

		[Display(ResourceType = typeof(Resources),
			Name = "AreaColor",
			GroupName = "Settings",
			Order = 30)]
		public System.Windows.Media.Color AreaColor
		{
			get => _areaColor.Convert();
			set
			{
				_areaColor = value.Convert();
				_fillBrush = _areaColor;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "StartTime",
			GroupName = "Settings",
			Order = 10)]
		public TimeSpan StartTime
		{
			get => _startTime;
			set
			{
				_startTime = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "EndTime",
			GroupName = "Settings",
			Order = 20)]
		public TimeSpan EndTime
		{
			get => _endTime;
			set
			{
				_endTime = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "Open",
			Order = 30)]
		public bool UseOpenAlert { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "Open",
			Order = 40)]
		public string AlertOpenFile { get; set; } = "alert1";

		[Display(ResourceType = typeof(Resources),
			Name = "UseAlerts",
			GroupName = "Close",
			Order = 30)]
		public bool UseCloseAlert { get; set; }

		[Display(ResourceType = typeof(Resources),
			Name = "AlertFile",
			GroupName = "Close",
			Order = 40)]
		public string AlertCloseFile { get; set; } = "alert1";

		#endregion

		#region ctor

		public SessionColor()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Historical);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Session Color";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_syncRoot)
			{
				if (bar == 0)
				{
					_sessions.Clear();
					_currentSession = null;
					_lastSessionBar = -1;
					_lastEndAlert = _lastStartAlert = -1;
				}
				var candle = GetCandle(bar);

				var diff = InstrumentInfo.TimeZone;
				var time = candle.Time.AddHours(diff);
				var lastTime = candle.LastTime.AddHours(diff);

				DateTime start;
				DateTime end;

				if (EndTime >= StartTime)
				{
					start = time.Date + StartTime;
					end = time.Date + EndTime;
				}
				else
				{
					start = bar > 0
						? time.Date + StartTime
						: time.Date.AddDays(-1) + StartTime;

					end = bar > 0
						? time.Date.AddDays(1) + EndTime
						: time.Date + EndTime;
				}

				if (_currentSession == null)
				{
					var startBar = StartSession(start, end, bar);

					if (startBar == -1)
						return;

					_currentSession = new Session(start, end, startBar);
					_sessions.Add(_currentSession);
					StartAlert(bar);
				}
				else
				{
					StartAlert(bar);

					var candleAdded = _currentSession.TryAddCandle(bar, time);

					if (_lastSessionBar != _currentSession.LastBar && lastTime >= end && !candleAdded)
					{
						if (UseCloseAlert && _lastEndAlert != bar && bar == CurrentBar - 1)
						{
							AddAlert(AlertCloseFile, InstrumentInfo.Instrument, "Session end", Colors.Black, Colors.White);
							_lastEndAlert = bar;
						}

						_lastSessionBar = _currentSession.LastBar;
					}

					if (!candleAdded)
					{
						if (time < start && lastTime < start || time >= end)
							return;

						var startBar = StartSession(start, end, bar);

						if (_currentSession.FirstBar != startBar)
						{
							_currentSession = new Session(start, end, startBar);
							_sessions.Insert(0, _currentSession);
						}
					}
				}
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_syncRoot)
			{
				var lastVisibleBar = LastVisibleBarNumber + 1;
				var firstVisibleBar = lastVisibleBar - VisibleBarsCount - 1;

				foreach (var session in _sessions)
				{
					if (session.FirstBar > lastVisibleBar)
						continue;

					if (session.LastBar < firstVisibleBar)
						return;

					var x = ChartInfo.GetXByBar(session.FirstBar);
					var x2 = ChartInfo.GetXByBar(session.LastBar + 1);

					if (x2 > ChartArea.Width)
						x2 = ChartArea.Width;

					if (ShowArea)
					{
						var rectangle = new Rectangle(x, 0, x2 - x, ChartArea.Height);
						context.FillRectangle(_fillBrush, rectangle);
					}
					else
					{
						var pen = new RenderPen(_areaColor, 2);
						context.DrawLine(pen, x, 0, x, ChartArea.Height);
						context.DrawLine(pen, x2, 0, x2, ChartArea.Height);
					}
				}
			}
		}

		#endregion

		#region Private methods

		private void StartAlert(int bar)
		{
			if (UseOpenAlert && _lastStartAlert != bar && bar == CurrentBar - 1 && bar == _currentSession.FirstBar)
			{
				AddAlert(AlertOpenFile, InstrumentInfo.Instrument, "Session start", Colors.Black, Colors.White);
				_lastStartAlert = bar;
			}
		}

		private int StartSession(DateTime startTime, DateTime endTime, int bar)
		{
			var candle = GetCandle(bar);
			var timeZone = InstrumentInfo.TimeZone;

			var time = candle.Time.AddHours(timeZone);
			var lastTime = candle.LastTime.AddHours(timeZone);

			if (time <= endTime && (time >= startTime || lastTime >= startTime))
				return bar;

			for (var i = bar; i < CurrentBar; i++)
			{
				var searchCandle = GetCandle(i);

				if (searchCandle.Time.AddHours(timeZone) <= endTime && searchCandle.Time.AddHours(timeZone) >= startTime)
					return i;
			}

			return -1;
		}

		#endregion
	}
}