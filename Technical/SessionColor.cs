namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Reflection;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Tools;
	
    [Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.SessionColorIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602465")]
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

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.ShowAboveChart),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.DrawAbovePriceDescription),
            Order = 10)]
		public bool ShowAboveChart
		{
			get => DrawAbovePrice;
			set => DrawAbovePrice = value;
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.ShowArea),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.FillAreaDescription),
            Order = 20)]
		public bool ShowArea { get; set; } = true;

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.AreaColor),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.AreaColorDescription),
            Order = 30)]
		public CrossColor AreaColor
		{
			get => _areaColor.Convert();
			set
			{
				_areaColor = value.Convert();
				_fillBrush = _areaColor;
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.StartTime),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.StartTimeDescription),
            Order = 40)]
		public TimeSpan StartTime
		{
			get => _startTime;
			set
			{
				_startTime = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.EndTime),
			GroupName = nameof(Strings.Settings),
            Description = nameof(Strings.EndTimeDescription),
            Order = 50)]
		public TimeSpan EndTime
		{
			get => _endTime;
			set
			{
				_endTime = value;
				RecalculateValues();
			}
		}

        [Display(ResourceType = typeof(Strings),
           Name = nameof(Strings.OpenSession),
           GroupName = nameof(Strings.Alerts),
           Description = nameof(Strings.OpenSessionAlertFilterDescription),
           Order = 10)]
        public FilterString OpenAlertFilter { get; set; }

        [Display(ResourceType = typeof(Strings),
        Name = nameof(Strings.ClosingSession),
        GroupName = nameof(Strings.Alerts),
        Description = nameof(Strings.CloseSessionAlertFilterDescription),
        Order = 20)]
        public FilterString CloseAlertFilter { get; set; }

        #region Hidden

        [Browsable(false)]
		[Obsolete]
		public bool UseOpenAlert
		{
			get => OpenAlertFilter.Enabled;
			set => OpenAlertFilter.Enabled = value;
        }

        [Browsable(false)]
        [Obsolete]
        public string AlertOpenFile
        {
            get => OpenAlertFilter.Value;
            set => OpenAlertFilter.Value = value;
        }

        [Browsable(false)]
        [Obsolete] 
		public bool UseCloseAlert
        {
            get => CloseAlertFilter.Enabled;
            set => CloseAlertFilter.Enabled = value;
        }

        [Browsable(false)]
        [Obsolete]
        public string AlertCloseFile
        {
            get => CloseAlertFilter.Value;
            set => CloseAlertFilter.Value = value;
        }

        #endregion

        #endregion

        #region ctor

        public SessionColor()
			: base(true)
		{
			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Historical);

			OpenAlertFilter = new(true) { Value = "alert1" };
            CloseAlertFilter = new(true) { Value = "alert1" };
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
						if (CloseAlertFilter.Enabled && _lastEndAlert != bar && bar == CurrentBar - 1)
						{
							AddAlert(CloseAlertFilter.Value, InstrumentInfo.Instrument, "Session end", Color.Black.Convert(), Color.White.Convert());
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
			if (OpenAlertFilter.Enabled && _lastStartAlert != bar && bar == CurrentBar - 1 && bar == _currentSession.FirstBar)
			{
				AddAlert(OpenAlertFilter.Value, InstrumentInfo.Instrument, "Session start", Color.Black.Convert(), Color.White.Convert());
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