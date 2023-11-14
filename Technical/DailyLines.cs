namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using Color = System.Drawing.Color;

[DisplayName("Daily Lines")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/17029-daily-lines")]
public class DailyLines : Indicator
{
    #region Nested types

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

    internal class SessionRange
    {
        internal int OpenBar { get; set; }
        internal decimal OpenPrice { get; set; }
        internal int HighBar { get; set; }
        internal decimal HighPrice { get; set; }
        internal int LowBar { get; set; }
        internal decimal LowPrice { get; set; } = decimal.MaxValue;
        internal int CloseBar { get; set; }
        internal decimal ClosePrice { get; set; }
        internal bool IsFinished { get; set; } 

        internal void HighLowUpdate(IndicatorCandle candle, int bar)
        {
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
        }
    }

    #endregion

    #region Fields

    public readonly RenderStringFormat _format = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
    };
    private readonly RenderFont _axisFont = new("Arial", 9);
    private readonly FontSetting _fontSetting = new("Arial", 9);
    private SessionRange _sessionRange;
    private int _lastBar = -1;

    private bool _customSession;
    private int _days = 60;
    private PeriodType _per = PeriodType.PreviousDay;
    private bool _showText = true;
    private bool _drawOverChart;

    #endregion

    #region Properties

    #region Calculation

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
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
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Filters), Order = 110)]
    public PeriodType Period
    {
        get => _per;
        set
        {
            _per = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession), GroupName = nameof(Strings.Filters), Order = 120)]
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

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionBegin), GroupName = nameof(Strings.Filters), Order = 120)]
    public FilterTimeSpan FilterStartTime { get; set; } = new(false);

    [Browsable(false)]
    public TimeSpan StartTime
    {
        get => FilterStartTime.Value;
        set
        {
            FilterStartTime.Value = value;
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SessionEnd), GroupName = nameof(Strings.Filters), Order = 120)]
    public FilterTimeSpan FilterEndTime { get; set; } = new(false);

    [Browsable(false)]
    public TimeSpan EndTime
    {
        get => FilterEndTime.Value;
        set
        {
            FilterEndTime.Value = value;
        }
    }

    #endregion

    #region Show

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Show), Order = 200)]
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
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize), GroupName = nameof(Strings.Show), Order = 205)]
    public FilterInt TextSize { get; set; } = new(false);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PriceLocation), GroupName = nameof(Strings.Show), Order = 210)]
    public bool ShowPrice { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FirstBar), GroupName = nameof(Strings.Show), Order = 220)]
    public bool DrawFromBar { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AbovePrice), GroupName = nameof(Strings.Show), Order = 230)]
    public bool DrawOverChart 
    { 
        get => _drawOverChart;
        set
        {
            _drawOverChart = DrawAbovePrice = value;
        }
    }

    #endregion

    #region Open

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Open), Order = 310)]
    public PenSettings OpenPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Open), Order = 315)]
    public string OpenText { get; set; }

    #endregion

    #region Close

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Close), Order = 320)]
    public PenSettings ClosePen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Close), Order = 325)]
    public string CloseText { get; set; }

    #endregion

    #region High

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.High), Order = 330)]
    public PenSettings HighPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.High), Order = 335)]
    public string HighText { get; set; }

    #endregion

    #region Low

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line), GroupName = nameof(Strings.Low), Order = 340)]
    public PenSettings LowPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text), GroupName = nameof(Strings.Low), Order = 345)]
    public string LowText { get; set; }

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

        if (_sessionRange is null)
            return;

        string periodStr;

        switch (Period)
        {
            case PeriodType.CurrentDay:
                {
                    periodStr = "Curr. Day";
                    break;
                }
            case PeriodType.PreviousDay:
                {
                    periodStr = "Prev. Day";
                    break;
                }
            case PeriodType.CurrenWeek:
                {
                    periodStr = "Curr. Week";
                    break;
                }
            case PeriodType.PreviousWeek:
                {
                    periodStr = "Prev. Week";
                    break;
                }
            case PeriodType.CurrentMonth:
                {
                    periodStr = "Curr. Month";
                    break;
                }
            case PeriodType.PreviousMonth:
                {
                    periodStr = "Prev. Month";
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException();
        }

        DrawLevel(context, OpenPen, _sessionRange.OpenBar, _sessionRange.OpenPrice, OpenText, "Open", periodStr);
        DrawLevel(context, HighPen, _sessionRange.HighBar, _sessionRange.HighPrice, HighText, "High", periodStr);
        DrawLevel(context, LowPen, _sessionRange.LowBar, _sessionRange.LowPrice, LowText, "Low", periodStr);

        if (_sessionRange.IsFinished)
            DrawLevel(context, ClosePen, _sessionRange.CloseBar, _sessionRange.ClosePrice, CloseText, "Close", periodStr);
    }

    protected override void OnFinishRecalculate()
    {
        SessionRangeInit();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        OnCurrentBarCalculate(bar);

        _lastBar = bar; 
    }

    #endregion

    #region Private methods

    private void OnCurrentBarCalculate(int bar)
    {
        if (bar != CurrentBar - 1 || _sessionRange is null)
            return;

        var candle = GetCandle(bar);

        if (bar != _lastBar)
        {
            if (IsNewSession(bar))
            {
                SessionRangeInit();
            }

            _sessionRange.CloseBar = bar;

            if (CustomSession && !_sessionRange.IsFinished)
            {
                var time = candle.Time.AddHours(InstrumentInfo.TimeZone);

                if (time.TimeOfDay >= FilterEndTime.Value)
                {
                    _sessionRange.IsFinished = true;
                    _sessionRange.CloseBar = bar - 1;
                    _sessionRange.ClosePrice = GetCandle(bar - 1).Close;
                }
            }
        }

        if (!_sessionRange.IsFinished)
        {
            _sessionRange.ClosePrice = candle.Close;
            _sessionRange.HighLowUpdate(candle, bar);
        }
    }

    private void SessionRangeInit()
    {
        _sessionRange = null;
        var startEndState = GetStartEndBars();

        var start = startEndState.Item1;
        var end = startEndState.Item2;
        var isFinished = startEndState.Item3;

        if (start < 0 || end < 0)
            return;

        if (CustomSession)
        {
            var newStart = 0;
            var newEnd = 0;
            var isNewStart = false;
            var isNewEnd = false;
            var stopSearch = false;

            for ( var bar = start; bar <= end; bar++ )
            {
                if (!isNewStart)
                {
                    var time1 = GetCandle(bar).Time.AddHours(InstrumentInfo.TimeZone);

                    if (time1.TimeOfDay == StartTime)
                    {
                        newStart = bar;
                        isNewStart = true;
                    }
                    else if (bar != start)
                    {
                        var prevTime = GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
                        isNewStart = CheckIsNewStart(prevTime, time1, StartTime, bar, ref newStart);
                    }
                }

                if (!isNewEnd && !stopSearch)
                {
                    var i = end - (bar - start); // индекс для движения от конца к началу.
                    var time2 = GetCandle(i).Time.AddHours(InstrumentInfo.TimeZone);

                    if (time2.TimeOfDay == EndTime)
                    {
                        newEnd = i - 1;
                        isNewEnd = true;
                    }
                    else if (bar != start)
                    {
                        var prevTime = GetCandle(i + 1).Time.AddHours(InstrumentInfo.TimeZone);
                        isNewEnd = CheckIsNewEnd(prevTime, time2, EndTime, i, ref newEnd);

                        if (!isNewEnd)
                        {
                            stopSearch = CheckIsNewEnd(prevTime, time2, StartTime, i, ref newEnd, false);
                        }
                    }
                }

                if (isNewStart && isNewEnd)
                {
                    break;
                }
            }

            if (isNewStart && isNewEnd)
            {
                start = newStart;

                if (newEnd > newStart)
                {
                    end = newEnd;
                    isFinished = true;
                }
            }
            else if (isNewStart)
                start = newStart;
            else if (isNewEnd)
                return;
        }

        _sessionRange = new()
        {
            OpenBar = start,
            OpenPrice = GetCandle(start).Open,
            CloseBar = end,
            ClosePrice = GetCandle(end).Close,
            IsFinished = isFinished
        };

        for (int bar = start; bar <= end; bar++)
        {
            var candle = GetCandle(bar);
            _sessionRange.HighLowUpdate(candle, bar);
        }
    }

    private bool CheckIsNewEnd(DateTime prevTime, DateTime time, TimeSpan endTime, int bar, ref int newEnd, bool toSetNewEnd = true)
    {
        if (prevTime.TimeOfDay > time.TimeOfDay)
        {
            if (time.TimeOfDay < endTime && prevTime.TimeOfDay > endTime) 
            {
                if (toSetNewEnd)
                    newEnd = bar;

                return true;
            }
        }
        else if (prevTime.TimeOfDay < time.TimeOfDay)
        {
            if (prevTime.TimeOfDay > endTime && time.TimeOfDay > endTime
            || prevTime.TimeOfDay < endTime && time.TimeOfDay < endTime)
            {
                if (toSetNewEnd)
                    newEnd = bar;

                return true;
            }
        }

            return false;
    }

    private bool CheckIsNewStart(DateTime prevTime, DateTime time, TimeSpan startTime, int bar, ref int newStart)
    {
        if (prevTime.TimeOfDay > time.TimeOfDay)
        {
            if (prevTime.TimeOfDay > startTime && time.TimeOfDay > startTime
             || prevTime.TimeOfDay < startTime && time.TimeOfDay < startTime)
            {
                newStart = bar - 1;

                return true;
            }
        }
        else if (prevTime.TimeOfDay < time.TimeOfDay)
        {
            if (prevTime.TimeOfDay < startTime && time.TimeOfDay > startTime)
            {
                newStart = bar - 1;

                return true;
            }
        }

        return false;
    }

    private (int, int, bool) GetStartEndBars()
    {
        var isFinished = false;
        var startBar = 0;
        var endBar = 0;
        var count = 0;

        for (int bar = CurrentBar - 1; bar >= 0; bar--)
        {
            switch (_per)
            {
                case PeriodType.CurrentDay:
                    if (!IsNewSession(bar))
                        continue;

                    count++;

                    if (count == 1)
                        startBar = bar;

                    break;
                case PeriodType.PreviousDay:
                    if (!IsNewSession(bar))
                        continue;

                    count++;

                    if (count == 1)
                        endBar = bar - 1;
                    else if (count == 2)
                        startBar = bar;

                    isFinished = true;

                    break;
                case PeriodType.CurrenWeek:
                    if (!IsNewWeek(bar))
                        continue;

                    count++;

                    if (count == 1)
                        startBar = bar;

                    break;
                case PeriodType.PreviousWeek:
                    if (!IsNewWeek(bar))
                        continue;

                    count++;

                    if (count == 1)
                        endBar = bar - 1;
                    else if (count == 2)
                        startBar = bar;

                    isFinished = true;

                    break;
                case PeriodType.CurrentMonth:
                    if (!IsNewMonth(bar))
                        continue;

                    count++;

                    if (count == 1)
                        startBar = bar;

                    break;
                case PeriodType.PreviousMonth:
                    if (!IsNewMonth(bar))
                        continue;

                    count++;

                    if (count == 1)
                        endBar = bar - 1;
                    else if (count == 2)
                        startBar = bar;

                    isFinished = true;

                    break;
            }

            if (startBar > 0)
                break;
        }

        if (endBar == 0)
            endBar = CurrentBar - 1;

        return (startBar, endBar, isFinished);
    }

    private void OnFilterPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Value")
            return;

        if (sender.Equals(FilterStartTime))
        {
            RecalculateValues();
        }
        else if (sender.Equals(FilterEndTime))
        {
            RecalculateValues();
        }
        else if (sender.Equals(TextSize))
        {
            _fontSetting.Size = TextSize.Value;
        }
    }

    private void DrawString(RenderContext context, RenderFont font, string renderText, int yPrice, Color color)
    {
        var textSize = context.MeasureString(renderText, font);
        context.DrawString(renderText, font, color, Container.Region.Right - textSize.Width - 5, yPrice - textSize.Height);
    }

    private void DrawPrice(RenderContext context, decimal price, RenderPen pen)
    {
        var y = ChartInfo.GetYByPrice(price, false);

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

    #endregion
}