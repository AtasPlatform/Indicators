namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Collections.Specialized;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;
	using System.Reflection;

	using Newtonsoft.Json;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
	using OFT.Localization;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	[Display(ResourceType = typeof(Strings), Description = nameof(Strings.SessionColorIndDescription))]
	[HelpLink("https://help.atas.net/support/solutions/articles/72000602465")]
	[DisplayName("Session Color")]
	public class SessionColor : Indicator
	{
		#region Nested types

		private sealed class SessionRange
		{
			#region Properties

			public SessionSettings Settings { get; }

			public int FirstBar { get; }

			public int LastBar { get; private set; }

			public decimal High { get; private set; }

			public decimal Low { get; private set; }

			public DateTime End { get; }

			public DateTime Start { get; }

			#endregion

			#region ctor

			public SessionRange(SessionSettings settings, DateTime start, DateTime end, int bar, decimal high, decimal low)
			{
				Settings = settings;
				Start = start;
				End = end;
				FirstBar = LastBar = bar;
				High = high;
				Low = low;
			}

			#endregion

			#region Public methods

			public bool TryAddCandle(int bar, DateTime time, decimal high, decimal low)
			{
				if (time >= End || time < Start)
					return false;

				if (bar > LastBar)
					LastBar = bar;

				if (high > High)
					High = high;

				if (low < Low)
					Low = low;

				return true;
			}

			#endregion
		}

		public sealed class SessionSettings : NotifyPropertyChangedBase
		{
			#region Fields

			private Color _areaColor = Color.FromArgb(63, 65, 105, 225);
			private FilterString _closeAlertFilter = new(true) { Value = "alert1" };
			private TimeSpan _endTime = new(12, 0, 0);
			private bool _fitToPriceRange;
			private FontSetting _labelFont = new() { FontFamily = "Arial", Size = 10 };
			private Color _labelColor = Color.White;
			private string _labelText = string.Empty;
			private FilterString _openAlertFilter = new(true) { Value = "alert1" };
			private bool _showArea = true;
			private TimeSpan _startTime;

			#endregion

			#region Properties

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.ShowArea),
				GroupName = nameof(Strings.Settings),
				Description = nameof(Strings.FillAreaDescription),
				Order = 20)]
			public bool ShowArea
			{
				get => _showArea;
				set => SetProperty(ref _showArea, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.FitToPriceRange),
				GroupName = nameof(Strings.Settings),
				Description = nameof(Strings.FitToPriceRangeDescription),
				Order = 25)]
			public bool FitToPriceRange
			{
				get => _fitToPriceRange;
				set => SetProperty(ref _fitToPriceRange, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.Text),
				GroupName = nameof(Strings.TextSettings),
				Description = nameof(Strings.LabelTextDescription),
				Order = 26)]
			public string LabelText
			{
				get => _labelText;
				set => SetProperty(ref _labelText, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.Font),
				GroupName = nameof(Strings.TextSettings),
				Description = nameof(Strings.FontSettingDescription),
				Order = 27)]
			public FontSetting LabelFont
			{
				get => _labelFont;
				set => SetProperty(ref _labelFont, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.Color),
				GroupName = nameof(Strings.TextSettings),
				Description = nameof(Strings.LabelTextColorDescription),
				Order = 28)]
			public CrossColor LabelColor
			{
				get => _labelColor.Convert();
				set => SetProperty(ref _labelColor, value.Convert());
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.AreaColor),
				GroupName = nameof(Strings.Settings),
				Description = nameof(Strings.AreaColorDescription),
				Order = 30)]
			public CrossColor AreaColor
			{
				get => _areaColor.Convert();
				set => SetProperty(ref _areaColor, value.Convert());
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.StartTime),
				GroupName = nameof(Strings.Settings),
				Description = nameof(Strings.StartTimeDescription),
				Order = 40)]
			public TimeSpan StartTime
			{
				get => _startTime;
				set => SetProperty(ref _startTime, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.EndTime),
				GroupName = nameof(Strings.Settings),
				Description = nameof(Strings.EndTimeDescription),
				Order = 50)]
			public TimeSpan EndTime
			{
				get => _endTime;
				set => SetProperty(ref _endTime, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.OpenSession),
				GroupName = nameof(Strings.Alerts),
				Description = nameof(Strings.OpenSessionAlertFilterDescription),
				Order = 10)]
			[SoundComboBoxEditor]
			public FilterString OpenAlertFilter
			{
				get => _openAlertFilter;
				set => SetProperty(ref _openAlertFilter, value);
			}

			[Display(ResourceType = typeof(Strings),
				Name = nameof(Strings.ClosingSession),
				GroupName = nameof(Strings.Alerts),
				Description = nameof(Strings.CloseSessionAlertFilterDescription),
				Order = 20)]
			[SoundComboBoxEditor]
			public FilterString CloseAlertFilter
			{
				get => _closeAlertFilter;
				set => SetProperty(ref _closeAlertFilter, value);
			}

			#endregion

			#region ctor

			public SessionSettings()
			{
				SubscribeNestedProperty(_labelFont, nameof(LabelFont));
				SubscribeNestedProperty(_openAlertFilter, nameof(OpenAlertFilter));
				SubscribeNestedProperty(_closeAlertFilter, nameof(CloseAlertFilter));
			}

			#endregion

			#region Public methods

			public override string ToString()
			{
				if (!string.IsNullOrWhiteSpace(LabelText))
					return LabelText;

				return $"{StartTime:hh\\:mm}-{EndTime:hh\\:mm}";
			}

			#endregion

			#region Private methods

			private void SubscribeNestedProperty(INotifyPropertyChanged nestedProperty, string propertyName)
			{
				nestedProperty.PropertyChanged += (_, _) => RaisePropertyChanged(propertyName);
			}

			#endregion
		}

		#endregion

		#region Fields

		private readonly Dictionary<SessionSettings, SessionRange> _currentSessions = new();
		private readonly Dictionary<SessionSettings, int> _lastEndAlerts = new();
		private readonly Dictionary<SessionSettings, int> _lastSessionBars = new();
		private readonly Dictionary<SessionSettings, int> _lastStartAlerts = new();
		private readonly List<SessionRange> _sessionRanges = new();
		private readonly object _syncRoot = new();

		private Color _legacyAreaColor = Color.FromArgb(63, 65, 105, 225);
		private FilterString _legacyCloseAlertFilter;
		private TimeSpan _legacyEndTime = new(12, 0, 0);
		private bool _legacyFitToPriceRange;
		private FontSetting _legacyLabelFont = new() { FontFamily = "Arial", Size = 10 };
		private Color _legacyLabelColor = Color.White;
		private string _legacyLabelText = string.Empty;
		private FilterString _legacyOpenAlertFilter;
		private bool _legacyShowArea = true;
		private TimeSpan _legacyStartTime;
		private bool _sessionSettingsInitialized;
		private bool _sessionsProvidedExplicitly;
		private ObservableCollection<SessionSettings> _sessions = new();

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

		[IsExpanded]
		[Display(Name = "Sessions", GroupName = nameof(Strings.Settings), Order = 20)]
		[JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
		public ObservableCollection<SessionSettings> Sessions
		{
			get => _sessions;
			set
			{
				if (ReferenceEquals(_sessions, value))
					return;

				DetachCollection(_sessions);
				_sessions = value ?? new ObservableCollection<SessionSettings>();
				AttachCollection(_sessions);
				_sessionsProvidedExplicitly = true;
				EnsureDefaultSessionExists();
				_sessionSettingsInitialized = true;
				RecalculateValues();
			}
		}

		#region Hidden legacy properties

		[Browsable(false)]
		[Obsolete]
		public bool ShowArea
		{
			get => _legacyShowArea;
			set
			{
				_legacyShowArea = value;
				SyncLegacySession(settings => settings.ShowArea = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public bool FitToPriceRange
		{
			get => _legacyFitToPriceRange;
			set
			{
				_legacyFitToPriceRange = value;
				SyncLegacySession(settings => settings.FitToPriceRange = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public string LabelText
		{
			get => _legacyLabelText;
			set
			{
				_legacyLabelText = value;
				SyncLegacySession(settings => settings.LabelText = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public FontSetting LabelFont
		{
			get => _legacyLabelFont;
			set
			{
				_legacyLabelFont = value;
				SyncLegacySession(settings => settings.LabelFont = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public CrossColor LabelColor
		{
			get => _legacyLabelColor.Convert();
			set
			{
				_legacyLabelColor = value.Convert();
				SyncLegacySession(settings => settings.LabelColor = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public CrossColor AreaColor
		{
			get => _legacyAreaColor.Convert();
			set
			{
				_legacyAreaColor = value.Convert();
				SyncLegacySession(settings => settings.AreaColor = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public TimeSpan StartTime
		{
			get => _legacyStartTime;
			set
			{
				_legacyStartTime = value;
				SyncLegacySession(settings => settings.StartTime = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public TimeSpan EndTime
		{
			get => _legacyEndTime;
			set
			{
				_legacyEndTime = value;
				SyncLegacySession(settings => settings.EndTime = value);
			}
		}

		[Browsable(false)]
		[Obsolete]
		public FilterString OpenAlertFilter
		{
			get => _legacyOpenAlertFilter;
			set
			{
				_legacyOpenAlertFilter = value;
				SyncLegacySession(settings => settings.OpenAlertFilter = CloneFilter(value));
			}
		}

		[Browsable(false)]
		[Obsolete]
		public FilterString CloseAlertFilter
		{
			get => _legacyCloseAlertFilter;
			set
			{
				_legacyCloseAlertFilter = value;
				SyncLegacySession(settings => settings.CloseAlertFilter = CloneFilter(value));
			}
		}

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

			_legacyOpenAlertFilter = new FilterString(true) { Value = "alert1" };
			_legacyCloseAlertFilter = new FilterString(true) { Value = "alert1" };

			AttachCollection(_sessions);
			EnsureDefaultSessionExists();
			_sessionSettingsInitialized = true;
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Session Color";
		}

		#endregion

		#region Protected methods

		protected override void OnDispose()
		{
			DetachCollection(_sessions);
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			lock (_syncRoot)
			{
				EnsureSessionSettingsInitialized();

				if (bar == 0)
				{
					_sessionRanges.Clear();
					_currentSessions.Clear();
					_lastSessionBars.Clear();
					_lastEndAlerts.Clear();
					_lastStartAlerts.Clear();
				}

				var candle = GetCandle(bar);
				var timeZone = InstrumentInfo.TimeZone;
				var time = candle.Time.AddHours(timeZone);
				var lastTime = candle.LastTime.AddHours(timeZone);

				foreach (var settings in Sessions)
					ProcessSession(bar, candle, time, lastTime, settings);
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_syncRoot)
			{
				var lastVisibleBar = LastVisibleBarNumber + 1;
				var firstVisibleBar = lastVisibleBar - VisibleBarsCount - 1;

				foreach (var session in _sessionRanges)
				{
					if (session.FirstBar > lastVisibleBar)
						continue;

					if (session.LastBar < firstVisibleBar)
						continue;

					var settings = session.Settings;
					var x = ChartInfo.GetXByBar(session.FirstBar);
					var x2 = ChartInfo.GetXByBar(session.LastBar + 1);

					if (x2 > ChartArea.Width)
						x2 = ChartArea.Width;

					int y;
					int height;

					if (settings.FitToPriceRange)
					{
						var yHigh = ChartInfo.GetYByPrice(session.High);
						var yLow = ChartInfo.GetYByPrice(session.Low);
						y = yHigh;
						height = yLow - yHigh;
					}
					else
					{
						y = 0;
						height = ChartArea.Height;
					}

					if (settings.ShowArea)
					{
						var rectangle = new Rectangle(x, y, x2 - x, height);
						context.FillRectangle(settings.AreaColor.Convert(), rectangle);
					}
					else
					{
						var pen = new RenderPen(settings.AreaColor.Convert(), 2);
						context.DrawLine(pen, x, y, x, y + height);
						context.DrawLine(pen, x2, y, x2, y + height);
					}

					if (!string.IsNullOrEmpty(settings.LabelText))
					{
						var labelSize = context.MeasureString(settings.LabelText, settings.LabelFont.RenderObject);
						var labelX = x + (x2 - x - labelSize.Width) / 2;
						var labelY = settings.FitToPriceRange
							? y - labelSize.Height - 2
							: y + 2;

						var labelRect = new Rectangle(labelX, labelY, labelSize.Width, labelSize.Height);
						context.DrawString(settings.LabelText, settings.LabelFont.RenderObject, settings.LabelColor.Convert(), labelRect);
					}
				}
			}
		}

		#endregion

		#region Private methods

		private void AttachCollection(ObservableCollection<SessionSettings> collection)
		{
			collection.CollectionChanged += SessionsChanged;

			foreach (var session in collection)
				session.PropertyChanged += SessionSettingsChanged;
		}

		private void DetachCollection(ObservableCollection<SessionSettings> collection)
		{
			collection.CollectionChanged -= SessionsChanged;

			foreach (var session in collection)
				session.PropertyChanged -= SessionSettingsChanged;
		}

		private bool CanSyncLegacySession()
		{
			return !_sessionsProvidedExplicitly && _sessions.Count == 1;
		}

		private SessionSettings CreateLegacySessionSettings()
		{
			return new SessionSettings
			{
				ShowArea = _legacyShowArea,
				FitToPriceRange = _legacyFitToPriceRange,
				LabelText = _legacyLabelText,
				LabelFont = _legacyLabelFont,
				LabelColor = _legacyLabelColor.Convert(),
				AreaColor = _legacyAreaColor.Convert(),
				StartTime = _legacyStartTime,
				EndTime = _legacyEndTime,
				OpenAlertFilter = CloneFilter(_legacyOpenAlertFilter),
				CloseAlertFilter = CloneFilter(_legacyCloseAlertFilter)
			};
		}

		private static FilterString CloneFilter(FilterString filter)
		{
			if (filter is null)
				return new FilterString(true);

			return new FilterString(true)
			{
				Enabled = filter.Enabled,
				Value = filter.Value
			};
		}

		private void EnsureDefaultSessionExists()
		{
			if (_sessions.Count == 0)
				_sessions.Add(CreateLegacySessionSettings());
		}

		private void EnsureSessionSettingsInitialized()
		{
			if (_sessionSettingsInitialized)
				return;

			_sessionSettingsInitialized = true;

			EnsureDefaultSessionExists();
		}

		private (DateTime Start, DateTime End) GetSessionBounds(DateTime time, int bar, SessionSettings settings)
		{
			if (settings.EndTime >= settings.StartTime)
				return (time.Date + settings.StartTime, time.Date + settings.EndTime);

			var start = bar > 0
				? time.Date + settings.StartTime
				: time.Date.AddDays(-1) + settings.StartTime;

			var end = bar > 0
				? time.Date.AddDays(1) + settings.EndTime
				: time.Date + settings.EndTime;

			return (start, end);
		}

		private string GetSessionDisplayName(SessionSettings settings)
		{
			return !string.IsNullOrWhiteSpace(settings.LabelText)
				? settings.LabelText
				: $"{settings.StartTime:hh\\:mm}-{settings.EndTime:hh\\:mm}";
		}

		private void ProcessSession(int bar, IndicatorCandle candle, DateTime time, DateTime lastTime, SessionSettings settings)
		{
			var (start, end) = GetSessionBounds(time, bar, settings);

			if (!_currentSessions.TryGetValue(settings, out var currentSession))
			{
				var startBar = StartSession(start, end, bar);

				if (startBar == -1)
					return;

				var startCandle = GetCandle(startBar);
				currentSession = new SessionRange(settings, start, end, startBar, startCandle.High, startCandle.Low);
				_currentSessions[settings] = currentSession;
				_sessionRanges.Add(currentSession);
				StartAlert(settings, currentSession, bar);
				return;
			}

			StartAlert(settings, currentSession, bar);

			var candleAdded = currentSession.TryAddCandle(bar, time, candle.High, candle.Low);

			if ((!_lastSessionBars.TryGetValue(settings, out var lastSessionBar) || lastSessionBar != currentSession.LastBar)
				&& lastTime >= end
				&& !candleAdded)
			{
				if (settings.CloseAlertFilter.Enabled
					&& (!_lastEndAlerts.TryGetValue(settings, out var lastEndAlert) || lastEndAlert != bar)
					&& bar == CurrentBar - 1)
				{
					AddAlert(settings.CloseAlertFilter.Value,
						InstrumentInfo.Instrument,
						$"{GetSessionDisplayName(settings)} end",
						Color.Black.Convert(),
						Color.White.Convert());

					_lastEndAlerts[settings] = bar;
				}

				_lastSessionBars[settings] = currentSession.LastBar;
			}

			if (candleAdded)
				return;

			if (time < start && lastTime < start || time >= end)
				return;

			var newSessionBar = StartSession(start, end, bar);

			if (currentSession.FirstBar == newSessionBar)
				return;

			var newSessionCandle = GetCandle(newSessionBar);
			_currentSessions[settings] = new SessionRange(settings, start, end, newSessionBar, newSessionCandle.High, newSessionCandle.Low);
			_sessionRanges.Add(_currentSessions[settings]);
		}

		private void ResetSessionState()
		{
			lock (_syncRoot)
			{
				_currentSessions.Clear();
				_sessionRanges.Clear();
				_lastSessionBars.Clear();
				_lastEndAlerts.Clear();
				_lastStartAlerts.Clear();
			}
		}

		private void SessionSettingsChanged(object sender, PropertyChangedEventArgs e)
		{
			ResetSessionState();
			RecalculateValues();
			RedrawChart();
		}

		private void SyncLegacySession(Action<SessionSettings> updateAction)
		{
			if (!CanSyncLegacySession())
				return;

			updateAction(_sessions[0]);
		}

		private void SessionsChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.NewItems != null)
			{
				foreach (SessionSettings session in e.NewItems)
					session.PropertyChanged += SessionSettingsChanged;
			}

			if (e.OldItems != null)
			{
				foreach (SessionSettings session in e.OldItems)
					session.PropertyChanged -= SessionSettingsChanged;
			}

			ResetSessionState();
			RecalculateValues();
			RedrawChart();
		}

		private void StartAlert(SessionSettings settings, SessionRange session, int bar)
		{
			if (!settings.OpenAlertFilter.Enabled
				|| bar != CurrentBar - 1
				|| bar != session.FirstBar
				|| _lastStartAlerts.TryGetValue(settings, out var lastStartAlert) && lastStartAlert == bar)
			{
				return;
			}

			AddAlert(settings.OpenAlertFilter.Value,
				InstrumentInfo.Instrument,
				$"{GetSessionDisplayName(settings)} start",
				Color.Black.Convert(),
				Color.White.Convert());

			_lastStartAlerts[settings] = bar;
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
				var searchTime = searchCandle.Time.AddHours(timeZone);

				if (searchTime <= endTime && searchTime >= startTime)
					return i;
			}

			return -1;
		}

		#endregion
	}
}
