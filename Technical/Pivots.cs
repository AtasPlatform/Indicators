namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes.Editors;

	using Utils.Common.Logging;

	using Color = System.Drawing.Color;

	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17002-pivots")]
	public class Pivots : Indicator
	{
		#region Nested types

		public enum Period
		{
			M5 = 3,
			M10 = 4,
			M15 = 5,
			M30 = 6,
			Hourly = -1,
			H4 = 7,
			Daily = 0,
			Weekly = 1,
			Monthly = 2
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

		private readonly ValueDataSeries _ppSeries;
		private readonly ValueDataSeries _r1Series;
		private readonly ValueDataSeries _r2Series;
		private readonly ValueDataSeries _r3Series;
		private readonly ValueDataSeries _s1Series;
		private readonly ValueDataSeries _s2Series;
		private readonly ValueDataSeries _s3Series;
		private readonly Queue<int> _sessionStarts;

		private decimal _currentDayClose;
		private decimal _currentDayHigh;
		private decimal _currentDayLow;

		private int _fontSize = 12;
		private int _id;

		private int _lastNewSessionBar = -1;
		private bool _newSessionWasStarted;
		private Period _pivotRange;

		private decimal _pp;
		private decimal _r1;
		private decimal _r2;
		private decimal _r3;

		private decimal _s1;
		private decimal _s2;
		private decimal _s3;

		private bool _showText = true;

		private TextLocation _textlocation;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RenderPeriods", Order = 10)]
		public Filter RenderPeriodsFilter { get; set; } = new Filter { Value = 3, Enabled = false };

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
			get => _textlocation;
			set
			{
				_textlocation = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Pivots()
			: base(true)
		{
			_sessionStarts = new Queue<int>();

			_ppSeries = (ValueDataSeries)DataSeries[0];
			_ppSeries.VisualType = VisualMode.Hash;
			_ppSeries.Color = Colors.Goldenrod;
			_ppSeries.Name = "PP";

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
				_sessionStarts.Clear();
				_newSessionWasStarted = false;
				return;
			}

			_ppSeries[bar] = 0;
			_s1Series[bar] = 0;
			_s2Series[bar] = 0;
			_s3Series[bar] = 0;
			_r1Series[bar] = 0;
			_r2Series[bar] = 0;
			_r3Series[bar] = 0;

			var candle = GetCandle(bar);
			var isNewSession = IsNeSession(bar);

			if (isNewSession && _lastNewSessionBar != bar)
			{
				if (RenderPeriodsFilter.Enabled)
				{
					if (_sessionStarts.Count == RenderPeriodsFilter.Value)
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
						}
					}
				}

				_sessionStarts.Enqueue(bar);

				_lastNewSessionBar = bar;
				_id = bar;
				_newSessionWasStarted = true;
				_pp = (_currentDayHigh + _currentDayLow + _currentDayClose) / 3;
				_s1 = 2 * _pp - _currentDayHigh;
				_r1 = 2 * _pp - _currentDayLow;
				_s2 = _pp - (_currentDayHigh - _currentDayLow);
				_r2 = _pp + (_currentDayHigh - _currentDayLow);
				_s3 = _pp - 2 * (_currentDayHigh - _currentDayLow);
				_r3 = _pp + 2 * (_currentDayHigh - _currentDayLow);

				_currentDayHigh = _currentDayLow = _currentDayClose = 0;

				if (_showText)
					SetLabels(bar, DrawingText.TextAlign.Right);
			}

			if (candle.High > _currentDayHigh)
				_currentDayHigh = candle.High;

			if (candle.Low < _currentDayLow || _currentDayLow == 0)
				_currentDayLow = candle.Low;
			_currentDayClose = candle.Close;

			if (_newSessionWasStarted)
			{
				_ppSeries[bar] = _pp;
				_s1Series[bar] = _s1;
				_s2Series[bar] = _s2;
				_s3Series[bar] = _s3;

				_r1Series[bar] = _r1;
				_r2Series[bar] = _r2;
				_r3Series[bar] = _r3;

				if (_showText && Location == TextLocation.Right)
					SetLabels(bar, DrawingText.TextAlign.Left);
			}
		}

		protected override void OnInitialize()
		{
			RenderPeriodsFilter.PropertyChanged += (a, b) => { RecalculateValues(); };
		}

		#endregion

		#region Private methods

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
				}
			}
			catch (Exception exception)
			{
				this.LogError("Pivots update colors error", exception);
			}
		}

		private void SetLabels(int bar, DrawingText.TextAlign align)
		{
			AddText("pp" + _id, "PP", true, bar, _pp, 0, 0, ConvertColor(_ppSeries.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s1" + _id, "S1", true, bar, _s1, 0, 0, ConvertColor(_s1Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s2" + _id, "S2", true, bar, _s2, 0, 0, ConvertColor(_s2Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("s3" + _id, "S3", true, bar, _s3, 0, 0, ConvertColor(_s3Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r1" + _id, "R1", true, bar, _r1, 0, 0, ConvertColor(_r1Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r2" + _id, "R2", true, bar, _r2, 0, 0, ConvertColor(_r2Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
			AddText("r3" + _id, "R3", true, bar, _r3, 0, 0, ConvertColor(_r3Series.Color), Color.Transparent, Color.Transparent, _fontSize, align);
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
					return IsNewSession(bar);
				case Period.Weekly:
					return IsNewWeek(bar);
				case Period.Monthly:
					return IsNewMonth(bar);
			}

			return false;
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