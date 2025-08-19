namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;

[DisplayName("Daily Lines")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DailyLinesDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602284")]
public class DailyLines : Indicator
{
	#region Nested types

	private class SessionRange
	{
		#region Properties

		public int OpenBar { get; private set; } = -1;

		public decimal OpenPrice { get; private set; }

		public int HighBar { get; private set; }

		public decimal HighPrice { get; private set; } = decimal.MinValue;

		public int LowBar { get; private set; }

		public decimal LowPrice { get; private set; } = decimal.MaxValue;

		public int CloseBar { get; private set; }

		public decimal ClosePrice { get; private set; }

		public bool IsFinished { get; set; }

		#endregion

		#region ctor

		public SessionRange()
		{
		}

		public SessionRange(IndicatorCandle candle, int bar)
		{
			OpenBar = CloseBar = HighBar = LowBar = bar;
			OpenPrice = candle.Open;
			HighPrice = candle.High;
			LowPrice = candle.Low;
			ClosePrice = candle.Close;
		}

		#endregion

		internal void IncCandle(IndicatorCandle candle, int bar)
		{
			if (OpenBar < 0)
			{
				OpenPrice = candle.Open;
				OpenBar = bar;
			}

			if (candle.High > HighPrice)
			{
				HighPrice = candle.High;
				HighBar = bar;
			}

			if (candle.Low < LowPrice)
			{
				LowPrice = candle.Low;
				LowBar = bar;
			}

			ClosePrice = candle.Close;
			CloseBar = bar;
		}
	}

	[Serializable]
	[Obfuscation(Feature = "renaming", ApplyToMembers = true, Exclude = true)]
	public enum PeriodType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CurrentDay))]
		CurrentDay,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PreviousDay))]
		PreviousDay,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CurrentWeek))]
		CurrenWeek,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PreviousWeek))]
		PreviousWeek,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CurrentMonth))]
		CurrentMonth,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PreviousMonth))]
		PreviousMonth
	}

	#endregion

	#region Fields

	private readonly RenderFont _axisFont = new("Arial", 9);
	private readonly FontSetting _fontSetting = new("Arial", 9);

	[Browsable(false)]
	public readonly RenderStringFormat _format = new()
	{
		Alignment = StringAlignment.Near,
		LineAlignment = StringAlignment.Center,
		Trimming = StringTrimming.EllipsisCharacter
	};

	private bool _customSession;
	private int _days = 60;
	private bool _drawOverChart;
	private bool _newWeekWait;
	private PeriodType _per = PeriodType.PreviousDay;
	private SessionRange _sessionRange;
	private bool _showText = true;
	private int _lastDefaultSession;

	// New: bounded session history to support previous periods across day/week/month
	private readonly Queue<SessionRange> _sessionHistory = new();
	private const int MaxSessions = 5;

	// New: Half Gap state (not used yet in behavior)
	private decimal? _halfGapPrice;
	private int _halfGapBar = -1;

    #endregion

    #region Properties

    #region Calculation

    [Browsable(false)]
	[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue,
		Description = nameof(Strings.DaysLookBackDescription))]
	[Range(1, 1000)]
	public int Days
	{
		get => _days;
		set
		{
			_days = value;
			RecalculateValues();
		}
	}

    #endregion

	#region Filters

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.PeriodDescription), Order = 110)]
    public PeriodType Period
    {
        get => _per;
        set
        {
            _per = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.IsCustomSessionDescription), Order = 120)]
    public bool CustomSession
    {
        get => _customSession;
        set
        {
            _customSession = value;
            FilterStartTime.Enabled = FilterEndTime.Enabled = _customSession;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionBegin), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.SessionBeginDescription), Order = 120)]
    public FilterTimeSpan FilterStartTime { get; set; } = new(false);

    [Browsable(false)]
    public TimeSpan StartTime
    {
        get => FilterStartTime.Value;
        set => FilterStartTime.Value = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionEnd), GroupName = nameof(Strings.Filters),
        Description = nameof(Strings.SessionEndDescription), Order = 120)]
    public FilterTimeSpan FilterEndTime { get; set; } = new(false);

    [Browsable(false)]
    public TimeSpan EndTime
    {
        get => FilterEndTime.Value;
        set => FilterEndTime.Value = value;
    }

    #endregion

    #region Show

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Show),
        Description = nameof(Strings.IsNeedShowLabelDescription), Order = 200)]
    public bool ShowText
    {
        get => _showText;
        set
        {
            _showText = value;
            TextSize.Enabled = _showText;
            RecalculateValues();
        }
    }

    [Range(5, 30)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize), GroupName = nameof(Strings.Show),
        Description = nameof(Strings.FontSizeDescription), Order = 205)]
    public FilterInt TextSize { get; set; } = new(false);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowPriceLabels), GroupName = nameof(Strings.Show),
        Description = nameof(Strings.ShowSelectedPriceOnPriceAxisDescription), Order = 210)]
    public bool ShowPrice { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FirstBar), GroupName = nameof(Strings.Show),
        Description = nameof(Strings.FirstBarDescription), Order = 220)]
    public bool DrawFromBar { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AbovePrice), GroupName = nameof(Strings.Show),
        Description = nameof(Strings.DrawAbovePriceDescription), Order = 230)]
    public bool DrawOverChart
    {
        get => _drawOverChart;
        set => _drawOverChart = DrawAbovePrice = value;
    }

    #endregion

    #region Open

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Open),
        Description = nameof(Strings.PenSettingsDescription), Order = 310)]
    public PenSettings OpenPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Open), Description = nameof(Strings.LabelTextDescription),
        Order = 315)]
    public string OpenText { get; set; }

    #endregion

    #region Close

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Close),
        Description = nameof(Strings.PenSettingsDescription), Order = 320)]
    public PenSettings ClosePen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Close), Description = nameof(Strings.LabelTextDescription),
        Order = 325)]
    public string CloseText { get; set; }

    #endregion

    #region High

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.High),
        Description = nameof(Strings.PenSettingsDescription), Order = 330)]
    public PenSettings HighPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.High), Description = nameof(Strings.LabelTextDescription),
        Order = 335)]
    public string HighText { get; set; }

    #endregion

    #region Low

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Low), Description = nameof(Strings.PenSettingsDescription),
        Order = 340)]
    public PenSettings LowPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Low), Description = nameof(Strings.LabelTextDescription),
        Order = 345)]
    public string LowText { get; set; }

    #endregion

    #region HalfGap
    // Optional Half Gap visualization (midpoint between previous close and current open)
    [Display(Name = "Half Gap Line", GroupName = "Half Gap", Description = "Line color and style for the Half Gap level", Order = 350)]
    public PenSettings HalfGapPen { get; set; } = new() { Color = DefaultColors.Blue.Convert(), Width = 2 };

    [Display(Name = "Text", GroupName = "Half Gap", Description = "Label text", Order = 355)]
    public string HalfGapText { get; set; } = "Half Gap";

    [Display(Name = "Show Half Gap", GroupName = "Half Gap", Description = "Toggle the display of the Half Gap line", Order = 349)]
    public bool ShowHalfGap { get; set; } = true;
    #endregion


    #endregion

    #region ctor

    public DailyLines()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical);
		DrawAbovePrice = true;

		DataSeries[0].IsHidden = true;
		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

		FilterStartTime.PropertyChanged += OnFilterPropertyChanged;
		FilterEndTime.PropertyChanged += OnFilterPropertyChanged;
		TextSize.PropertyChanged += OnFilterPropertyChanged;

		TextSize.Enabled = ShowText;
		TextSize.Value = _fontSetting.Size;
	}

	#endregion

	#region Protected methods

	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
		if (ChartInfo is null)
			return;

		var isCurrent = Period is PeriodType.CurrentDay or PeriodType.CurrenWeek or PeriodType.CurrentMonth;

        // Show message only when the current period is selected and we are clearly
		// outside the custom session window in the current viewport.
		// Avoid blocking rendering solely because the current session is marked as finished,
		// which may be legitimate in complex custom sessions (e.g., crossing midnight).
		if (isCurrent
           && CustomSession
           && _lastDefaultSession > _sessionRange.OpenBar
           && !InsideSession(LastVisibleBarNumber))
        {
			DrawMessage(context);
			return;
		}

        // Select range: current period uses _sessionRange; previous periods use last completed from history
        SessionRange range = null;
        switch (Period)
		{
			case PeriodType.CurrentDay:
            case PeriodType.CurrenWeek:
            case PeriodType.CurrentMonth:
				range = _sessionRange;
                break;
            case PeriodType.PreviousDay:
            case PeriodType.PreviousWeek:
            case PeriodType.PreviousMonth:
				range = GetLastCompletedSession();
                break;
			default:
				range = _sessionRange;
                break;
		}

        // Fallback: for PreviousDay + CustomSession, if there is no completed session yet
        // (e.g., after session end but before the next session starts), use the finished current range.
        if ((range is null || range.OpenBar < 0)
			&& Period == PeriodType.PreviousDay
            && CustomSession
			&& _sessionRange.OpenBar >= 0
            && _sessionRange.IsFinished)
        {
            range = _sessionRange;
        }

        // Guard: nothing to draw if no valid range available
        if (range is null || range.OpenBar < 0)
			return;

        // Compute Half Gap only for CurrentDay and only if a previous completed session exists
        if (ShowHalfGap && Period == PeriodType.CurrentDay)
		{
			var prev = GetLastCompletedSession();
            if (prev != null && _sessionRange.OpenBar >= 0)
			{
				var openPrice = _sessionRange.OpenPrice;
				var closePrice = prev.ClosePrice;
				_halfGapPrice = closePrice + (openPrice - closePrice) / 2m;
				_halfGapBar = _sessionRange.OpenBar;
            }
            else
			{
				_halfGapPrice = null;
				_halfGapBar = -1;
            }
        }
        

        var periodStr = Period switch
		{
			PeriodType.CurrentDay => "Curr. Day",
			PeriodType.PreviousDay => "Prev. Day",
			PeriodType.CurrenWeek => "Curr. Week",
			PeriodType.PreviousWeek => "Prev. Week",
			PeriodType.CurrentMonth => "Curr. Month",
			PeriodType.PreviousMonth => "Prev. Month",
			_ => throw new ArgumentOutOfRangeException()
		};

		var high = ChartInfo.PriceChartContainer.High;
		var low = ChartInfo.PriceChartContainer.Low;

        // Draw Half Gap (if computed) within visible price range
        if (ShowHalfGap && _halfGapPrice.HasValue && _halfGapBar >= 0 && _halfGapPrice.Value >= low && _halfGapPrice.Value <= high)
            DrawLevel(context, HalfGapPen, _halfGapBar, _halfGapPrice.Value, HalfGapText, "HalfGap", periodStr);

        if (range.OpenPrice >= low && range.OpenPrice <= high)
			DrawLevel(context, OpenPen, range.OpenBar, range.OpenPrice, OpenText, "Open", periodStr);

		if (range.HighPrice >= low && range.HighPrice <= high)
			DrawLevel(context, HighPen, range.HighBar, range.HighPrice, HighText, "High", periodStr);

		if (range.LowPrice >= low && range.LowPrice <= high)
			DrawLevel(context, LowPen, range.LowBar, range.LowPrice, LowText, "Low", periodStr);

		if (range.IsFinished && range.ClosePrice >= low && range.ClosePrice <= high)
			DrawLevel(context, ClosePen, range.CloseBar, range.ClosePrice, CloseText, "Close", periodStr);
	}

	protected override void OnRecalculate()
	{
		_sessionRange = new SessionRange();

        // Clear new state to avoid stale data after full recalculation
        _sessionHistory.Clear();
        _halfGapPrice = null;
        _halfGapBar = -1;
    }

	protected new bool IsNewSession(int bar)
	{
		if (!CustomSession)
			return base.IsNewSession(bar);

		var candle = GetCandle(bar);

		var startTime = candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
		var endTime = candle.LastTime.AddHours(InstrumentInfo.TimeZone).TimeOfDay;

		if (bar == 0)
		{
			if (startTime <= endTime)
				return FilterStartTime.Value >= startTime && FilterStartTime.Value <= endTime;

			return FilterStartTime.Value >= startTime || FilterStartTime.Value <= endTime;
		}

		var insideBar = (startTime <= endTime && FilterStartTime.Value >= startTime && FilterStartTime.Value <= endTime)
			||
			(startTime > endTime && (FilterStartTime.Value >= startTime || FilterStartTime.Value <= endTime));

		if (insideBar)
			return true;

		var prevCandle = GetCandle(bar - 1);
		startTime = prevCandle.LastTime.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
		endTime = candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay;

		if (startTime <= endTime)
			return FilterStartTime.Value >= startTime && FilterStartTime.Value <= endTime;

		return FilterStartTime.Value >= startTime || FilterStartTime.Value <= endTime;
	}

	protected new bool IsNewWeek(int bar)
	{
		var isNew = base.IsNewWeek(bar);

		if (!CustomSession)
			return isNew;

		if (isNew)
			_newWeekWait = true;

		if (!InsideSession(bar) || !_newWeekWait)
			return false;

		_newWeekWait = false;
		return true;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		if (base.IsNewSession(bar))
			_lastDefaultSession = bar;

        var isNewPeriod = IsNewPeriod(bar);
        
               if (isNewPeriod)
                   {
                       // Unified archival: finish and enqueue the previous range when a new period starts
                       if (_sessionRange.OpenBar >= 0 && !_sessionRange.IsFinished)
                           {
							 _sessionRange.IsFinished = true;
							 _sessionHistory.Enqueue(_sessionRange);
                             while (_sessionHistory.Count > MaxSessions)
								_sessionHistory.Dequeue();

							}
            
					_sessionRange = new SessionRange(candle, bar);
                   }
               else
                   {
                       if (bar != _sessionRange.OpenBar)
                           {
								if (Period is PeriodType.CurrentDay or PeriodType.PreviousDay)
								{
									if (InsideSession(bar))
									{
										_sessionRange.IncCandle(candle, bar);
									}
									else 
									{
										if (_sessionRange.OpenBar >= 0)
											_sessionRange.IsFinished = true;
									}
								}
								else
								{
									if (_sessionRange.OpenBar >= 0)
										_sessionRange.IncCandle(candle, bar);
								}
							}
						else
							{
                               if (Period is PeriodType.CurrentDay or PeriodType.PreviousDay)
                                   {
                                       if (InsideSession(bar))
										_sessionRange.IncCandle(candle, bar);
                                       else if (_sessionRange.OpenBar >= 0)
										_sessionRange.IsFinished = true;
                                   }
                               else if (_sessionRange.OpenBar >= 0)
                                   {
										_sessionRange.IncCandle(candle, bar);
                                   }
                           }
                   }
    }

	#endregion

	#region Private methods

	private bool InsideSession(int bar)
	{
		if (!CustomSession)
			return true;

		if (FilterStartTime.Value == FilterEndTime.Value)
			return true;

		var candle = GetCandle(bar);

		var sessionStart = FilterStartTime.Value;
		var sessionEnd = FilterEndTime.Value;

		var startTime = candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
		var endTime = candle.LastTime.AddHours(InstrumentInfo.TimeZone).TimeOfDay;

		if (sessionStart < sessionEnd)
		{
			return (startTime >= sessionStart && startTime <= sessionEnd) ||
				(endTime >= sessionStart && endTime <= sessionEnd) ||
				(startTime <= sessionStart && endTime >= sessionEnd);
		}

		return startTime >= sessionStart || endTime >= sessionStart ||
			startTime <= sessionEnd || endTime <= sessionEnd;
	}

	private bool IsNewPeriod(int bar)
	{
		return Period switch
		{
			PeriodType.CurrentDay or PeriodType.PreviousDay => IsNewSession(bar),
			PeriodType.CurrenWeek or PeriodType.PreviousWeek => IsNewWeek(bar),
			PeriodType.CurrentMonth or PeriodType.PreviousMonth => IsNewMonth(bar),
			_ => false
		};
	}

	private void OnFilterPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != "Value")
			return;

		if (sender.Equals(FilterStartTime))
		{
			RecalculateValues();
			RedrawChart();
		}
		else if (sender.Equals(FilterEndTime))
		{
			RecalculateValues();
			RedrawChart();
		}
		else if (sender.Equals(TextSize))
			_fontSetting.Size = TextSize.Value;
	}

	private void DrawString(RenderContext context, RenderFont font, string renderText, int yPrice, Color color)
	{
		var textSize = context.MeasureString(renderText, font);
		context.DrawString(renderText, font, color, Container.Region.Right - textSize.Width - 5, yPrice - textSize.Height);
	}

	private void DrawPrice(RenderContext context, decimal price, RenderPen pen)
	{
		var y = ChartInfo.GetYByPrice(price, false);

        // Guard: avoid drawing outside bounds
        if (y < 0)
           return;

        if (y + 8 > Container.Region.Height)
           return;

        var renderText = price.ToString(CultureInfo.InvariantCulture);
		var size = context.MeasureString(renderText, _axisFont);
		var priceHeight = size.Height / 2;
		var x = Container.Region.Right;

		var points = new Point[5];
		points[0] = new Point(x, y);
		points[1] = new Point(x + priceHeight, y - priceHeight);
		points[2] = new Point(x + size.Width + 2 * priceHeight, y - priceHeight);
		points[3] = new Point(points[2].X, y + priceHeight + 1);
		points[4] = new Point(x + priceHeight, y + priceHeight + 1);

		var textRect = new Rectangle(points[1], new Size(size.Width + priceHeight, 2 * priceHeight));
		context.FillPolygon(pen.Color, points);
		context.DrawString(renderText, _axisFont, Color.White, textRect, _format);
	}

	private void DrawLevel(RenderContext context, PenSettings pen, int bar, decimal price, string text, string ohlc, string periodStr)
	{
		if (DrawFromBar && bar > LastVisibleBarNumber)
			return;
		
		var x1 = DrawFromBar ? ChartInfo.GetXByBar(bar) : 0;
		var x2 = Container.Region.Right;
		var y = ChartInfo.GetYByPrice(price, false);
		context.DrawLine(pen.RenderObject, x1, y, x2, y);

		var offset = 3;
		var renderText = string.IsNullOrEmpty(text) ? $"{periodStr} {ohlc}" : text;

		if (ShowText)
			DrawString(context, _fontSetting.RenderObject, renderText, y - offset, pen.RenderObject.Color);

		if (ShowPrice)
		{
			var bounds = context.ClipBounds;
			context.ResetClip();
			context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);
			DrawPrice(context, price, pen.RenderObject);
			context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
			context.SetClip(bounds);
		}
	}

	private void DrawMessage(RenderContext g)
	{
		var text = Strings.CustomSessionInactive;

		var textSize = g.MeasureString(text, ChartInfo.PriceAxisFont);

		var rect = new Rectangle(Container.Region.X, Container.Region.Bottom - textSize.Height, textSize.Width, textSize.Height);
		g.DrawString(text, ChartInfo.PriceAxisFont, DefaultColors.Red, rect);
    }

	// Returns the most recent finished session from history (supports day/week/month)
	private SessionRange GetLastCompletedSession()
	{
		if (_sessionHistory.Count == 0)
			return null;

		// Iterate from newest to oldest
		foreach (var s in _sessionHistory.Reverse())
		{
			if (s.IsFinished && s.OpenBar >= 0)
				return s;
		}

		return null;
	}
	#endregion
}