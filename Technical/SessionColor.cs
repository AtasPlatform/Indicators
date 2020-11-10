namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Reflection;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes.Editors;
	using OFT.Rendering.Context;

	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/3602-session-color")]
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
				if (time > End)
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

		private Color _areaColor = Color.FromArgb(63, 65, 105, 225);
		private Session _currentSession;
		private TimeSpan _endTime = new TimeSpan(12, 0, 0);
		private List<Session> _sessions = new List<Session>();
		private TimeSpan _startTime;
		private object _syncRoot = new object();

		#endregion

		#region Properties

		private Color FillBrush { get; set; }

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
				FillBrush = _areaColor;
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

		#region Overrides of BaseIndicator

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_syncRoot)
			{
				if (bar == 0)
				{
					_sessions.Clear();
					_currentSession = null;
				}

				var diff = InstrumentInfo.TimeZone;
				var time = GetCandle(bar).Time.AddHours(diff);

				DateTime start;
				DateTime end;

				if (EndTime >= StartTime)
				{
					start = time.Date + StartTime;
					end = time.Date + EndTime;
				}
				else
				{
					start = time.Date + StartTime;
					end = time.Date.AddDays(1) + EndTime;
				}

				if (_currentSession == null)
				{
					_currentSession = new Session(start, end, bar);
					_sessions.Add(_currentSession);
				}
				else
				{
					if (!_currentSession.TryAddCandle(bar, time))
					{
						if (time < start || time > end)
							return;

						_currentSession = new Session(start, end, bar);
						_sessions.Insert(0, _currentSession);
					}
				}
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (layout != DrawingLayouts.Historical)
				return;

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

					var rectangle = new Rectangle(x, 0, x2 - x, ChartArea.Height);
					context.FillRectangle(FillBrush, rectangle);
				}
			}
		}

		#endregion
	}
}