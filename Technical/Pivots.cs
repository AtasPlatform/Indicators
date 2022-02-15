namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	using Utils.Common.Logging;

	using Color = System.Drawing.Color;

	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/17002-pivots")]
	public class Pivots : Indicator
	{
		#region Nested types

		public enum Period
		{
			M1 = 1,
			M5 = 3,
			M10 = 4,
			M15 = 5,
			M30 = 6,

			[Display(ResourceType = typeof(Resources), Name = "Hourly")]
			Hourly = -1,

			[Display(ResourceType = typeof(Resources), Name = "H4")]
			H4 = 7,

			[Display(ResourceType = typeof(Resources), Name = "Daily")]
			Daily = 0,

			[Display(ResourceType = typeof(Resources), Name = "Weekly")]
			Weekly = 10,

			[Display(ResourceType = typeof(Resources), Name = "Monthly")]
			Monthly = 20
		}

		public enum TextLocation
		{
			[Display(ResourceType = typeof(Resources), Name = "Left")]
			Left = 0,

			[Display(ResourceType = typeof(Resources), Name = "Right")]
			Right = 1
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _m1Series = new("M1");
		private readonly ValueDataSeries _m2Series = new("M2");
		private readonly ValueDataSeries _m3Series = new("M3");
		private readonly ValueDataSeries _m4Series = new("M4");

		private readonly ValueDataSeries _ppSeries;
		private readonly ValueDataSeries _r1Series;
		private readonly ValueDataSeries _r2Series;
		private readonly ValueDataSeries _r3Series;
		private readonly ValueDataSeries _s1Series;
		private readonly ValueDataSeries _s2Series;
		private readonly ValueDataSeries _s3Series;

		private readonly Queue<int> _sessionStarts;

		private decimal _prevDayClose;
		private decimal _currentDayHigh;
		private decimal _currentDayLow;

		private int _fontSize = 12;
		private int _id;
		private int _lastBar;

		private int _lastNewSessionBar = -1;

		private decimal _m1;
		private decimal _m2;
		private decimal _m3;
		private decimal _m4;
		private bool _newSessionWasStarted;
		private Period _pivotRange;

		private decimal _pp;
		private decimal _r1;
		private decimal _r2;
		private decimal _r3;

		private decimal _s1;
		private decimal _s2;
		private decimal _s3;
		private TimeSpan _sessionBegin;
		private TimeSpan _sessionEnd;

		private bool _showText = true;

		private TextLocation _textLocation;
		private bool _useCustomSession;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RenderPeriods", Order = 10)]
		public Filter<int> RenderPeriodsFilter { get; set; } = new()
			{ Value = 3, Enabled = false };

		[Display(ResourceType = typeof(Resources), Name = "Enabled", GroupName = "CustomSession", Order = 12)]
		public bool UseCustomSession
		{
			get => _useCustomSession;
			set
			{
				_useCustomSession = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SessionBegin", GroupName = "CustomSession", Order = 13)]
		[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")]
		public TimeSpan SessionBegin
		{
			get => _sessionBegin;
			set
			{
				_sessionBegin = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SessionEnd", GroupName = "CustomSession", Order = 15)]
		[Mask(MaskTypes.DateTimeAdvancingCaret, "HH:mm:ss")]
		public TimeSpan SessionEnd
		{
			get => _sessionEnd;
			set
			{
				_sessionEnd = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PivotRange")]
		public Period PivotRange
		{
			get => _pivotRange;
			set
			{
				_pivotRange = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "Text")]
		public bool ShowText
		{
			get => _showText;
			set
			{
				_showText = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextSize", GroupName = "Text")]
		public int FontSize
		{
			get => _fontSize;
			set
			{
				_fontSize = Math.Max(9, value);
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextLocation", GroupName = "Text")]
		public TextLocation Location
		{
			get => _textLocation;
			set
			{
				_textLocation = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Pivots()
			: base(true)
		{
			_sessionBegin = new TimeSpan(0, 0, 0);
			_sessionEnd = new TimeSpan(23, 59, 59);
			_sessionStarts = new Queue<int>();

			_ppSeries = (ValueDataSeries)DataSeries[0];
			_ppSeries.VisualType = VisualMode.Hash;
			_ppSeries.Color = Colors.Goldenrod;
			_ppSeries.Name = "PP";

			_m1Series.Color = _m2Series.Color
				= _m3Series.Color = _m4Series.Color = Colors.Blue;

			_s1Series = new ValueDataSeries("S1")
			{
				Color = Colors.Crimson,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_s1Series);

			_s2Series = new ValueDataSeries("S2")
			{
				Color = Colors.Crimson,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_s2Series);

			_s3Series = new ValueDataSeries("S3")
			{
				Color = Colors.Crimson,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_s3Series);

			_r1Series = new ValueDataSeries("R1")
			{
				Color = Colors.DodgerBlue,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_r1Series);

			_r2Series = new ValueDataSeries("R2")
			{
				Color = Colors.DodgerBlue,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_r2Series);

			_r3Series = new ValueDataSeries("R3")
			{
				Color = Colors.DodgerBlue,
				VisualType = VisualMode.Hash
			};
			DataSeries.Add(_r3Series);

			_m1Series.VisualType = _m2Series.VisualType
				= _m3Series.VisualType = _m4Series.VisualType = VisualMode.Hash;

			DataSeries.Add(_m1Series);
			DataSeries.Add(_m2Series);
			DataSeries.Add(_m3Series);
			DataSeries.Add(_m4Series);

			_ppSeries.PropertyChanged += SeriesPropertyChanged;
			_s1Series.PropertyChanged += SeriesPropertyChanged;
			_s2Series.PropertyChanged += SeriesPropertyChanged;
			_s3Series.PropertyChanged += SeriesPropertyChanged;
			_r1Series.PropertyChanged += SeriesPropertyChanged;
			_r2Series.PropertyChanged += SeriesPropertyChanged;
			_r3Series.PropertyChanged += SeriesPropertyChanged;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_lastNewSessionBar = -1;
				_lastBar = 0;
				_sessionStarts.Clear();
				_newSessionWasStarted = false;
				DataSeries.ForEach(x => x.Clear());
				Labels.Clear();
				_currentDayHigh = _currentDayLow = _prevDayClose = 0;
				return;
			}

			if (RenderPeriodsFilter.Enabled && RenderPeriodsFilter.Value <= 0)
				return;

			_ppSeries[bar] = 0;
			_s1Series[bar] = 0;
			_s2Series[bar] = 0;
			_s3Series[bar] = 0;
			_r1Series[bar] = 0;
			_r2Series[bar] = 0;
			_r3Series[bar] = 0;

			var candle = GetCandle(bar);
			var inSession = InsideSession(bar) || !UseCustomSession;
			var isNewSession = IsNeSession(bar);

			if (isNewSession && _lastNewSessionBar != bar)
			{
				_sessionStarts.Enqueue(bar);

				if (RenderPeriodsFilter.Enabled)
				{
					while (_sessionStarts.Count > RenderPeriodsFilter.Value)
					{
						RemoveLabels(_sessionStarts.Peek());

						for (var i = _sessionStarts.Dequeue(); i < _sessionStarts.Peek(); i++)
						{
							_ppSeries[i] = 0;
							_s1Series[i] = 0;
							_s2Series[i] = 0;
							_s3Series[i] = 0;

							_r1Series[i] = 0;
							_r2Series[i] = 0;
							_r3Series[i] = 0;

							_m1Series[i] = 0;
							_m2Series[i] = 0;
							_m3Series[i] = 0;
							_m4Series[i] = 0;
						}
					}
				}

				_lastNewSessionBar = bar;
				_id = bar;
				_newSessionWasStarted = true;

				var close = _prevDayClose == 0 ? candle.Close : _prevDayClose;

				_pp = (_currentDayHigh + _currentDayLow + close) / 3;
				_s1 = 2 * _pp - _currentDayHigh;
				_r1 = 2 * _pp - _currentDayLow;
				_s2 = _pp - (_currentDayHigh - _currentDayLow);
				_r2 = _pp + (_currentDayHigh - _currentDayLow);
				_s3 = _currentDayLow - 2 * (_currentDayHigh - _pp);
				_r3 = _currentDayHigh + 2 * (_pp - _currentDayLow);

				_m1 = (_s1 + _s2) / 2;
				_m2 = (_s1 + _pp) / 2;
				_m3 = (_r1 + _pp) / 2;
				_m4 = (_r1 + _r2) / 2;

				_currentDayHigh = candle.High;
				_currentDayLow = candle.Low;
			}
			else
			{
				if (candle.High > _currentDayHigh)
					_currentDayHigh = candle.High;

				if (candle.Low < _currentDayLow || _currentDayLow == 0)
					_currentDayLow = candle.Low;

				_prevDayClose = candle.Close;
			}

			if (_newSessionWasStarted && inSession)
			{
				_ppSeries[bar] = _pp;
				_s1Series[bar] = _s1;
				_s2Series[bar] = _s2;
				_s3Series[bar] = _s3;

				_r1Series[bar] = _r1;
				_r2Series[bar] = _r2;
				_r3Series[bar] = _r3;

				_m1Series[bar] = _m1;
				_m2Series[bar] = _m2;
				_m3Series[bar] = _m3;
				_m4Series[bar] = _m4;

				if (_showText && Location == TextLocation.Right)
					SetLabels(bar, DrawingText.TextAlign.Left);
			}

			if (_showText
			    && Labels
				    .Select(x => x.Value.Bar)
				    .DefaultIfEmpty(0)
				    .Max() < _lastNewSessionBar)
				SetLabels(bar, DrawingText.TextAlign.Right);

			_lastBar = bar;
		}

		protected override void OnInitialize()
		{
			RenderPeriodsFilter.PropertyChanged += (a, b) =>
			{
				if (RenderPeriodsFilter.Value < 0)
				{
					RenderPeriodsFilter.Value = 0;
					return;
				}

				RecalculateValues();
			};
		}

		#endregion

		#region Private methods

		private bool InsideSession(int bar)
		{
			var diff = InstrumentInfo.TimeZone;
			var candle = GetCandle(bar);
			var time = candle.Time.AddHours(diff);

			if (_sessionBegin < _sessionEnd)
				return time.TimeOfDay <= _sessionEnd && time.TimeOfDay >= _sessionBegin;

			return time.TimeOfDay >= _sessionEnd && time.TimeOfDay >= _sessionBegin
				|| time.TimeOfDay <= _sessionBegin && time.TimeOfDay <= _sessionEnd;
		}

		private void SeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			try
			{
				foreach (var drawingText in Labels)
				{
					if (drawingText.Value.Text == "PP")
						drawingText.Value.Textcolor = ConvertColor(_ppSeries.Color);
					else if (drawingText.Value.Text == "S1")
						drawingText.Value.Textcolor = ConvertColor(_s1Series.Color);
					else if (drawingText.Value.Text == "S2")
						drawingText.Value.Textcolor = ConvertColor(_s2Series.Color);
					else if (drawingText.Value.Text == "S3")
						drawingText.Value.Textcolor = ConvertColor(_s3Series.Color);
					else if (drawingText.Value.Text == "R1")
						drawingText.Value.Textcolor = ConvertColor(_r1Series.Color);
					else if (drawingText.Value.Text == "R2")
						drawingText.Value.Textcolor = ConvertColor(_r2Series.Color);
					else if (drawingText.Value.Text == "R3")
						drawingText.Value.Textcolor = ConvertColor(_r3Series.Color);
					else if (drawingText.Value.Text == "M1")
						drawingText.Value.Textcolor = ConvertColor(_m1Series.Color);
					else if (drawingText.Value.Text == "M2")
						drawingText.Value.Textcolor = ConvertColor(_m2Series.Color);
					else if (drawingText.Value.Text == "M3")
						drawingText.Value.Textcolor = ConvertColor(_m3Series.Color);
					else if (drawingText.Value.Text == "M4")
						drawingText.Value.Textcolor = ConvertColor(_m4Series.Color);
				}
			}
			catch (Exception exception)
			{
				this.LogError("Pivots update colors error", exception);
			}
		}

		private void SetLabels(int bar, DrawingText.TextAlign align)
		{
			if (Labels is null)
				return;

			AddText("pp" + _id, "PP", true, bar, _pp, 0, 0, ConvertColor(_ppSeries.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s1" + _id, "S1", true, bar, _s1, 0, 0, ConvertColor(_s1Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s2" + _id, "S2", true, bar, _s2, 0, 0, ConvertColor(_s2Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s3" + _id, "S3", true, bar, _s3, 0, 0, ConvertColor(_s3Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r1" + _id, "R1", true, bar, _r1, 0, 0, ConvertColor(_r1Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r2" + _id, "R2", true, bar, _r2, 0, 0, ConvertColor(_r2Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r3" + _id, "R3", true, bar, _r3, 0, 0, ConvertColor(_r3Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);

			AddText("m1" + _id, "M1", true, bar, _m1, 0, 0, ConvertColor(_m1Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("m2" + _id, "M2", true, bar, _m2, 0, 0, ConvertColor(_m2Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("m3" + _id, "M3", true, bar, _m3, 0, 0, ConvertColor(_m3Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("m4" + _id, "M4", true, bar, _m4, 0, 0, ConvertColor(_m4Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
		}

		private void RemoveLabels(int id)
		{
			Labels.Remove("pp" + id);
			Labels.Remove("s1" + id);
			Labels.Remove("s2" + id);
			Labels.Remove("s3" + id);
			Labels.Remove("r1" + id);
			Labels.Remove("r2" + id);
			Labels.Remove("r3" + id);
			Labels.Remove("m1" + id);
			Labels.Remove("m2" + id);
			Labels.Remove("m3" + id);
			Labels.Remove("m4" + id);
		}

		private Color ConvertColor(System.Windows.Media.Color cl)
		{
			return Color.FromArgb(cl.A, cl.R, cl.G, cl.B);
		}

		private bool IsNeSession(int bar)
		{
			if (bar == 0)
				return true;

			switch (PivotRange)
			{
				case Period.M1:
					return isnewsession(1, bar);
				case Period.M5:
					return isnewsession(5, bar);
				case Period.M10:
					return isnewsession(10, bar);
				case Period.M15:
					return isnewsession(15, bar);
				case Period.M30:
					return isnewsession(30, bar);
				case Period.Hourly:
					return GetCandle(bar).Time.Hour != GetCandle(bar - 1).Time.Hour;
				case Period.H4:
					return isnewsession(240, bar);
				case Period.Daily:
					return UseCustomSession ? IsNewCustomSession(bar) : IsNewSession(bar);
				case Period.Weekly:
					return UseCustomSession ? IsNewCusomWeek(bar) : IsNewWeek(bar);
				case Period.Monthly:
					return UseCustomSession ? IsNewCusomMonth(bar) : IsNewMonth(bar);
			}

			return false;
		}

		private bool IsNewCusomMonth(int bar)
		{
			for (var i = _lastBar + 1; i <= bar; i++)
			{
				if (IsNewMonth(i))
					return true;
			}

			return false;
		}

		private bool IsNewCusomWeek(int bar)
		{
			for (var i = _lastBar + 1; i <= bar; i++)
			{
				if (IsNewWeek(i))
					return true;
			}

			return false;
		}

		private bool IsNewCustomSession(int bar)
		{
			var candle = GetCandle(bar);

			var candleStart = candle.Time
				.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			var candleEnd = candle.LastTime
				.AddHours(InstrumentInfo.TimeZone)
				.TimeOfDay;

			if (bar == 0)
			{
				if (_sessionBegin < _sessionEnd)
				{
					return candleStart <= _sessionBegin && candleEnd >= _sessionEnd
						|| candleStart >= _sessionBegin && candleEnd <= _sessionEnd
						|| candleStart < _sessionBegin && candleEnd > _sessionBegin && candleEnd <= _sessionEnd;
				}

				return candleStart >= _sessionBegin || candleStart <= _sessionEnd;
			}

			var diff = InstrumentInfo.TimeZone;

			var prevCandle = GetCandle(bar - 1);
			var prevTime = prevCandle.LastTime.AddHours(diff);

			var time = candle.LastTime.AddHours(diff);

			if (_sessionBegin < _sessionEnd)
			{
				return time.TimeOfDay >= _sessionBegin && time.TimeOfDay <= _sessionEnd &&
					!(prevTime.TimeOfDay >= _sessionBegin && prevTime.TimeOfDay <= _sessionEnd);
			}

			return time.TimeOfDay >= _sessionBegin && time.TimeOfDay >= _sessionEnd
				&& !(prevTime.TimeOfDay >= _sessionBegin && prevTime.TimeOfDay >= _sessionEnd
					||
					time.TimeOfDay <= _sessionBegin && time.TimeOfDay <= _sessionEnd)
				&& !(prevTime.TimeOfDay <= _sessionBegin && prevTime.TimeOfDay <= _sessionEnd);
		}

		private bool isnewsession(int tf, int bar)
		{
			return (GetBeginTime(GetCandle(bar).Time, tf) - GetBeginTime(GetCandle(bar - 1).Time, tf)).TotalMinutes >= tf;
		}

		private DateTime GetBeginTime(DateTime tim, int period)
		{
			var tim2 = tim;
			tim2 = tim2.AddMilliseconds(-tim2.Millisecond);
			tim2 = tim2.AddSeconds(-tim2.Second);
			var begin = Convert.ToInt32((tim2 - new DateTime()).TotalMinutes % period);
			var res = tim2.AddMinutes(-begin).AddMilliseconds(-tim2.Millisecond).AddSeconds(-tim2.Second);
			return res;
		}

		#endregion
	}
}