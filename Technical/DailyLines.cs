namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Utils.Common.Logging;

	using Color = System.Drawing.Color;

	[DisplayName("Daily Lines")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17029-daily-lines")]
	public class DailyLines : Indicator
	{
		#region Nested types

		[Serializable]
		[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
		public enum PeriodType
		{
			[Display(ResourceType = typeof(Resources), Name = "CurrentDay")]
			CurrentDay,

			[Display(ResourceType = typeof(Resources), Name = "PreviousDay")]
			PreviousDay,

			[Display(ResourceType = typeof(Resources), Name = "CurrentWeek")]
			CurrentWeek,

			[Display(ResourceType = typeof(Resources), Name = "PreviousWeek")]
			PreviousWeek,

			[Display(ResourceType = typeof(Resources), Name = "CurrentMonth")]
			CurrentMonth,

			[Display(ResourceType = typeof(Resources), Name = "PreviousMonth")]
			PreviousMonth
		}

		#endregion

		#region Static and constants

		private const string _defaultRegString = @"^(Curr|Prev)\.\s{1}(Month|Week|Day)\s{1}(High|Close|Open|Low)$";

		#endregion

		#region Fields

		private readonly RenderFont _font = new("Arial", 8);

		private readonly LineSeries _lsClose = new("Close") { Color = Colors.Red };
		private readonly LineSeries _lsHigh = new("High") { Color = Colors.Red };
		private readonly LineSeries _lsLow = new("Low") { Color = Colors.Red };
		private readonly LineSeries _lsOpen = new("Open") { Color = Colors.Red };

		private decimal _close;
		private DynamicLevels.DynamicCandle _currentCandle = new();
		private int _days;
		private decimal _high;
		private int _lastNewSessionBar;
		private decimal _low;

		private Color _lsCloseColor;
		private Color _lsHighColor;
		private Color _lsLowColor;
		private Color _lsOpenColor;
		private decimal _open;
		private PeriodType _per = PeriodType.PreviousDay;
		private DynamicLevels.DynamicCandle _previousCandle = new();
		private bool _showTest = true;
		private int _targetBar;
		private bool _tickBasedCalculation;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Filters", Order = 100)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters", Order = 100)]
		public PeriodType Period
		{
			get => _per;
			set
			{
				_per = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Show", Order = 200)]
		public bool ShowText
		{
			get => _showTest;
			set
			{
				_showTest = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "PriceLocation", GroupName = "Show", Order = 210)]
		public bool ShowPrice { get; set; } = true;

		private decimal Open
		{
			get => _open;
			set
			{
				if (_open != value)
					_lsOpen.Value = value;

				_open = value;
			}
		}

		private decimal High
		{
			get => _high;
			set
			{
				if (_high != value)
					_lsHigh.Value = value;
				_high = value;
			}
		}

		private decimal Low
		{
			get => _low;
			set
			{
				if (_low != value)
					_lsLow.Value = value;
				_low = value;
			}
		}

		private decimal Close
		{
			get => _close;
			set
			{
				if (_close != value)
					_lsClose.Value = value;
				_close = value;
			}
		}

		#endregion

		#region ctor

		public DailyLines()
			: base(true)
		{
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);

			DataSeries[0].IsHidden = true;
			_days = 20;
			((ValueDataSeries)DataSeries[0]).ScaleIt = false;
			((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

			_lsOpen.PropertyChanged += OpenChanged;
			_lsClose.PropertyChanged += CloseChanged;
			_lsHigh.PropertyChanged += HighChanged;
			_lsLow.PropertyChanged += LowChanged;

			LineSeries.Add(_lsOpen);
			LineSeries.Add(_lsHigh);
			LineSeries.Add(_lsLow);
			LineSeries.Add(_lsClose);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Daily Lines";
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (!ShowPrice || ChartInfo is null)
				return;

			var bounds = context.ClipBounds;
			context.ResetClip();
			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

			foreach (var line in LineSeries)
			{
				var y = ChartInfo.GetYByPrice(line.Value, false);

				var renderText = line.Value.ToString(CultureInfo.InvariantCulture);
				var textWidth = context.MeasureString(renderText, _font).Width;

				var polygon = new Point[]
				{
					new(Container.Region.Right, y),
					new(Container.Region.Right + 6, y - 7),
					new(Container.Region.Right + textWidth + 8, y - 7),
					new(Container.Region.Right + textWidth + 8, y + 8),
					new(Container.Region.Right + 6, y + 8)
				};

				Color color;

				if (line == _lsOpen)
					color = _lsOpenColor;
				else if (line == _lsClose)
					color = _lsCloseColor;
				else if (line == _lsHigh)
					color = _lsHighColor;
				else
					color = _lsLowColor;

				context.FillPolygon(color, polygon);
				context.DrawString(renderText, _font, Color.White, Container.Region.Right + 6, y - 6);
			}

			context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
			context.SetClip(bounds);
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			try
			{
				if (bar == 0)
				{
					_tickBasedCalculation = false;
					_currentCandle = new DynamicLevels.DynamicCandle();
					_previousCandle = new DynamicLevels.DynamicCandle();
					_lastNewSessionBar = -1;

					if (_days == 0)
						_targetBar = 0;
					else
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

					string periodStr;

					switch (Period)
					{
						case PeriodType.CurrentDay:
						{
							periodStr = "Curr. Day ";
							break;
						}
						case PeriodType.PreviousDay:
						{
							periodStr = "Prev. Day ";
							break;
						}
						case PeriodType.CurrentWeek:
						{
							periodStr = "Curr. Week ";
							break;
						}
						case PeriodType.PreviousWeek:
						{
							periodStr = "Prev. Week ";
							break;
						}
						case PeriodType.CurrentMonth:
						{
							periodStr = "Curr. Month ";
							break;
						}
						case PeriodType.PreviousMonth:
						{
							periodStr = "Prev. Month ";
							break;
						}
						default:
							throw new ArgumentOutOfRangeException();
					}

					foreach (var lineSeries in LineSeries)
					{
						if (ShowText)
						{
							if (lineSeries.Text == "" || Regex.IsMatch(periodStr, _defaultRegString))
								lineSeries.Text = periodStr + lineSeries.Name;
						}
						else
							lineSeries.Text = "";
					}

					return;
				}

				if (bar < _targetBar)
					return;

				if (bar != _lastNewSessionBar)
				{
					if (Period is PeriodType.CurrentDay or PeriodType.PreviousDay && IsNewSession(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if (Period is PeriodType.CurrentWeek or PeriodType.PreviousWeek && IsNewWeek(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
					else if (Period is PeriodType.CurrentMonth or PeriodType.PreviousMonth && IsNewMonth(bar))
					{
						_previousCandle = _currentCandle;
						_currentCandle = new DynamicLevels.DynamicCandle();
						_lastNewSessionBar = bar;
					}
				}

				if (!_tickBasedCalculation)
					_currentCandle.AddCandle(GetCandle(bar), InstrumentInfo.TickSize);

				var showedCandle = Period is PeriodType.CurrentDay or PeriodType.CurrentWeek or PeriodType.CurrentMonth
					? _currentCandle
					: _previousCandle;

				if (bar == CurrentBar - 1)
				{
					Open = showedCandle.Open;
					Close = showedCandle.Close;
					High = showedCandle.High;
					Low = showedCandle.Low;
					_tickBasedCalculation = true;
				}
			}
			catch (Exception e)
			{
				this.LogError("Daily lines error ", e);
			}
		}

		protected override void OnNewTrade(MarketDataArg arg)
		{
			if (_tickBasedCalculation)
				_currentCandle.AddTick(arg);
		}

		#endregion

		#region Private methods

		private void HighChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is "Color")
				_lsHighColor = _lsHigh.Color.Convert();
		}

		private void LowChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is "Color")
				_lsLowColor = _lsLow.Color.Convert();
		}

		private void CloseChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is "Color")
				_lsCloseColor = _lsClose.Color.Convert();
		}

		private void OpenChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is "Color")
				_lsOpenColor = _lsOpen.Color.Convert();
		}

		#endregion
	}
}