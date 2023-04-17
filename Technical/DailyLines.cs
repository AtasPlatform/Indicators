namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

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
        [Display(ResourceType = typeof(Resources), Name = "CurrentDay")]
        CurrentDay,

        [Display(ResourceType = typeof(Resources), Name = "PreviousDay")]
        PreviousDay,

        [Display(ResourceType = typeof(Resources), Name = "CurrentWeek")]
        CurrenWeek,

        [Display(ResourceType = typeof(Resources), Name = "PreviousWeek")]
        PreviousWeek,

        [Display(ResourceType = typeof(Resources), Name = "CurrentMonth")]
        CurrentMonth,

        [Display(ResourceType = typeof(Resources), Name = "PreviousMonth")]
        PreviousMonth
    }

    #endregion

    #region Fields

    private readonly RenderFont _font = new("Arial", 8);

    private int _closeBar;
    private decimal _currentClose;
    private decimal _currentHigh;
    private decimal _currentLow;

    private decimal _currentOpen;
    private bool _customSession;
    private int _days = 60;
    private bool _drawFromBar;
    private TimeSpan _endTime;
    private int _highBar;
    private int _lastSession;
    private int _lowBar;
    private int _openBar;
    private PeriodType _per = PeriodType.PreviousDay;
    private decimal _prevClose;
    private int _prevCloseBar;
    private decimal _prevHigh;
    private int _prevHighBar;
    private decimal _prevLow;
    private int _prevLowBar;
    private decimal _prevOpen;
    private int _prevOpenBar;
    private bool _showText = true;
    private TimeSpan _startTime;
    private int _targetBar;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "DaysLookBack", Order = int.MaxValue, Description = "DaysLookBackDescription")]
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

    [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Filters", Order = 110)]
    public PeriodType Period
    {
        get => _per;
        set
        {
            _per = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "CustomSession", GroupName = "Filters", Order = 120)]
    public bool CustomSession
    {
        get => _customSession;
        set
        {
            _customSession = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "SessionBegin", GroupName = "Filters", Order = 120)]
    public TimeSpan StartTime
    {
        get => _startTime;
        set
        {
            _startTime = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "SessionEnd", GroupName = "Filters", Order = 120)]
    public TimeSpan EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Show", Order = 200)]
    public bool ShowText
    {
        get => _showText;
        set
        {
            _showText = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "PriceLocation", GroupName = "Show", Order = 210)]
    public bool ShowPrice { get; set; } = true;

    [Display(ResourceType = typeof(Resources), Name = "FirstBar", GroupName = "Drawing", Order = 300)]
    public bool DrawFromBar
    {
        get => _drawFromBar;
        set
        {
            _drawFromBar = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Open", Order = 310)]
    public PenSettings OpenPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Open", Order = 315)]
    public string OpenText { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Close", Order = 320)]
    public PenSettings ClosePen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Close", Order = 325)]
    public string CloseText { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "High", Order = 330)]
    public PenSettings HighPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "High", Order = 335)]
    public string HighText { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "Line", GroupName = "Low", Order = 340)]
    public PenSettings LowPen { get; set; } = new() { Color = DefaultColors.Red.Convert(), Width = 2 };

    [Display(ResourceType = typeof(Resources), Name = "Text", GroupName = "Low", Order = 345)]
    public string LowText { get; set; }

    #endregion

    #region ctor

    public DailyLines()
        : base(true)
    {
        DenyToChangePanel = true;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
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
        if (ChartInfo is null)
            return;

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
            case PeriodType.CurrenWeek:
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

        var isLastPeriod = Period is PeriodType.CurrentDay or PeriodType.CurrentMonth or PeriodType.CurrenWeek;

        var open = isLastPeriod
            ? _currentOpen
            : _prevOpen;

        var close = isLastPeriod
            ? _currentClose
            : _prevClose;

        var high = isLastPeriod
            ? _currentHigh
            : _prevHigh;

        var low = isLastPeriod
            ? _currentLow
            : _prevLow;

        var isCurrentPeriod = Period is PeriodType.CurrentDay or PeriodType.CurrenWeek or PeriodType.CurrentMonth;

        if (DrawFromBar)
        {
            var openBar = isLastPeriod
                ? _openBar
                : _prevOpenBar;

            var closeBar = isLastPeriod
                ? _closeBar
                : _prevCloseBar;

            var highBar = isLastPeriod
                ? _highBar
                : _prevHighBar;

            var lowBar = isLastPeriod
                ? _lowBar
                : _prevLowBar;

            if (openBar >= 0 && openBar <= LastVisibleBarNumber)
            {
                var x = ChartInfo.PriceChartContainer.GetXByBar(openBar, false);
                var y = ChartInfo.PriceChartContainer.GetYByPrice(open, false);
                context.DrawLine(OpenPen.RenderObject, x, y, Container.Region.Right, y);
                var renderText = string.IsNullOrEmpty(OpenText) ? periodStr + "Open" : OpenText;

                if (ShowText)
                    DrawString(context, renderText, y, OpenPen.RenderObject.Color);
            }

            if (!isCurrentPeriod && closeBar >= 0 && closeBar <= LastVisibleBarNumber)
            {
                var x = ChartInfo.PriceChartContainer.GetXByBar(closeBar, false);
                var y = ChartInfo.PriceChartContainer.GetYByPrice(close, false);
                context.DrawLine(ClosePen.RenderObject, x, y, Container.Region.Right, y);
                var renderText = string.IsNullOrEmpty(CloseText) ? periodStr + "Close" : CloseText;

                if (ShowText)
                    DrawString(context, renderText, y, ClosePen.RenderObject.Color);
            }

            if (highBar >= 0 && highBar <= LastVisibleBarNumber)
            {
                var x = ChartInfo.PriceChartContainer.GetXByBar(highBar, false);
                var y = ChartInfo.PriceChartContainer.GetYByPrice(high, false);
                context.DrawLine(HighPen.RenderObject, x, y, Container.Region.Right, y);
                var renderText = string.IsNullOrEmpty(HighText) ? periodStr + "High" : HighText;

                if (ShowText)
                    DrawString(context, renderText, y, HighPen.RenderObject.Color);
            }

            if (lowBar >= 0 && lowBar <= LastVisibleBarNumber)
            {
                var x = ChartInfo.PriceChartContainer.GetXByBar(lowBar, false);
                var y = ChartInfo.PriceChartContainer.GetYByPrice(low, false);
                context.DrawLine(LowPen.RenderObject, x, y, Container.Region.Right, y);
                var renderText = string.IsNullOrEmpty(LowText) ? periodStr + "Low" : LowText;

                if (ShowText)
                    DrawString(context, renderText, y, LowPen.RenderObject.Color);
            }
        }
        else
        {
            var yOpen = ChartInfo.PriceChartContainer.GetYByPrice(open, false);
            context.DrawLine(OpenPen.RenderObject, Container.Region.Left, yOpen, Container.Region.Right, yOpen);
            var renderText = string.IsNullOrEmpty(OpenText) ? periodStr + "Open" : OpenText;

            if (ShowText)
                DrawString(context, renderText, yOpen, OpenPen.RenderObject.Color);

            if (!isCurrentPeriod)
            {
                var yClose = ChartInfo.PriceChartContainer.GetYByPrice(close, false);
                context.DrawLine(ClosePen.RenderObject, Container.Region.Left, yClose, Container.Region.Right, yClose);
                renderText = string.IsNullOrEmpty(CloseText) ? periodStr + "Close" : CloseText;

                if (ShowText)
                    DrawString(context, renderText, yClose, ClosePen.RenderObject.Color);
            }

            var yHigh = ChartInfo.PriceChartContainer.GetYByPrice(high, false);
            context.DrawLine(HighPen.RenderObject, Container.Region.Left, yHigh, Container.Region.Right, yHigh);
            renderText = string.IsNullOrEmpty(HighText) ? periodStr + "High" : HighText;

            if (ShowText)
                DrawString(context, renderText, yHigh, HighPen.RenderObject.Color);

            var yLow = ChartInfo.PriceChartContainer.GetYByPrice(low, false);
            context.DrawLine(LowPen.RenderObject, Container.Region.Left, yLow, Container.Region.Right, yLow);
            renderText = string.IsNullOrEmpty(LowText) ? periodStr + "Low" : LowText;

            if (ShowText)
                DrawString(context, renderText, yLow, LowPen.RenderObject.Color);
        }

        if (!ShowPrice)
            return;

        var bounds = context.ClipBounds;
        context.ResetClip();
        context.SetTextRenderingHint(RenderTextRenderingHint.Aliased);

        if ((_openBar >= 0 && _openBar <= LastVisibleBarNumber) || !DrawFromBar)
            DrawPrice(context, open, OpenPen.RenderObject);

        if ((!isCurrentPeriod && _closeBar >= 0 && _closeBar <= LastVisibleBarNumber) || !DrawFromBar)
            DrawPrice(context, close, ClosePen.RenderObject);

        if ((_highBar >= 0 && _highBar <= LastVisibleBarNumber) || !DrawFromBar)
            DrawPrice(context, high, HighPen.RenderObject);

        if ((_lowBar >= 0 && _lowBar <= LastVisibleBarNumber) || !DrawFromBar)
            DrawPrice(context, low, LowPen.RenderObject);

        context.SetTextRenderingHint(RenderTextRenderingHint.AntiAlias);
        context.SetClip(bounds);
    }

    protected override void OnRecalculate()
    {
        ResetFields();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        try
        {
            if (bar == 0)
            {
                _openBar = _closeBar = _highBar = _lowBar = -1;

                _lastSession = bar;

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

            if (bar < _targetBar)
                return;

            var candle = GetCandle(bar);

            var isNewSession = (((IsNewSession(bar) && !CustomSession) || (IsNewCustomSession(bar) && CustomSession)) &&
                    Period is PeriodType.CurrentDay or PeriodType.PreviousDay)
                || (Period is PeriodType.CurrenWeek or PeriodType.PreviousWeek && IsNewWeek(bar))
                || (Period is PeriodType.CurrentMonth or PeriodType.PreviousMonth && IsNewMonth(bar))
                || bar == _targetBar;

            if (isNewSession && (_lastSession != bar || bar == 0))
            {
                _prevOpenBar = _openBar;
                _prevCloseBar = _closeBar;
                _prevHighBar = _highBar;
                _prevLowBar = _lowBar;

                _prevOpen = _currentOpen;
                _prevClose = _currentClose;
                _prevHigh = _currentHigh;
                _prevLow = _currentLow;

                _openBar = _closeBar = _highBar = _lowBar = bar;
                _currentOpen = candle.Open;
                _currentClose = candle.Close;
                _currentHigh = candle.High;
                _currentLow = candle.Low;

                _lastSession = bar;
            }
            else
            {
                if (CustomSession && !InsideSession(bar) && Period is PeriodType.CurrentDay or PeriodType.PreviousDay)
                    return;

                UpdateLevels(bar);
            }
        }
        catch (Exception e)
        {
            this.LogError("Daily lines error ", e);
        }
    }

    #endregion

    #region Private methods

    private void ResetFields()
    {
        _lastSession = 0;
        _targetBar = 0;

        _prevClose = 0;
        _prevCloseBar = 0;
        _prevHigh = 0;
        _prevHighBar = 0;
        _prevLow = 0;
        _prevLowBar = 0;
        _prevOpen = 0;
        _prevOpenBar = 0;

        _highBar = 0;
        _lowBar = 0;
        _openBar = 0;

        _closeBar = 0;
        _currentClose = 0;
        _currentHigh = 0;
        _currentLow = 0;

        _currentOpen = 0;
    }

    private void UpdateLevels(int bar)
    {
        var candle = GetCandle(bar);

        _currentClose = candle.Close;
        _closeBar = bar;

        if (_currentHigh < candle.High)
        {
            _currentHigh = candle.High;
            _highBar = bar;
        }

        if (_currentLow > candle.Low)
        {
            _currentLow = candle.Low;
            _lowBar = bar;
        }
    }

    private bool InsideSession(int bar)
    {
        var diff = InstrumentInfo.TimeZone;
        var candle = GetCandle(bar);
        var time = candle.Time.AddHours(diff);

        if (_startTime < _endTime)
            return time.TimeOfDay <= EndTime && time.TimeOfDay >= StartTime;

        return (time.TimeOfDay >= EndTime && time.TimeOfDay >= StartTime && time.TimeOfDay <= new TimeSpan(23, 23, 59))
            || (time.TimeOfDay <= _startTime && time.TimeOfDay <= EndTime && time.TimeOfDay >= TimeSpan.Zero);
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
            if (_startTime < _endTime)
            {
                return (candleStart <= _startTime && candleEnd >= _endTime)
                    || (candleStart >= _startTime && candleEnd <= _endTime)
                    || (candleStart < _startTime && candleEnd > _startTime && candleEnd <= _endTime);
            }

            return candleStart >= _startTime || candleStart <= _endTime;
        }

        var diff = InstrumentInfo.TimeZone;

        var prevCandle = GetCandle(bar - 1);
        var prevTime = prevCandle.LastTime.AddHours(diff);

        var time = candle.LastTime.AddHours(diff);

        if (_startTime < _endTime)
        {
            return time.TimeOfDay >= _startTime && time.TimeOfDay <= EndTime &&
                !(prevTime.TimeOfDay >= _startTime && prevTime.TimeOfDay <= EndTime);
        }

        return time.TimeOfDay >= _startTime && time.TimeOfDay >= EndTime && time.TimeOfDay <= new TimeSpan(23, 23, 59)
            && !((prevTime.TimeOfDay >= _startTime && prevTime.TimeOfDay >= EndTime && prevTime.TimeOfDay <= new TimeSpan(23, 23, 59))
                ||
                (time.TimeOfDay <= _startTime && time.TimeOfDay <= EndTime && time.TimeOfDay >= TimeSpan.Zero))
            && !(prevTime.TimeOfDay <= _startTime && prevTime.TimeOfDay <= EndTime && prevTime.TimeOfDay >= TimeSpan.Zero);
    }

    private void DrawString(RenderContext context, string renderText, int yPrice, Color color)
    {
        var textSize = context.MeasureString(renderText, _font);
        context.DrawString(renderText, _font, color, Container.Region.Right - textSize.Width - 5, yPrice - textSize.Height);
    }

    private void DrawPrice(RenderContext context, decimal price, RenderPen pen)
    {
        var y = ChartInfo.GetYByPrice(price, false);

        var renderText = price.ToString(CultureInfo.InvariantCulture);
        var textWidth = context.MeasureString(renderText, _font).Width;

        if (y + 8 > Container.Region.Height)
            return;

        var polygon = new Point[]
        {
            new(Container.Region.Right, y),
            new(Container.Region.Right + 6, y - 7),
            new(Container.Region.Right + textWidth + 8, y - 7),
            new(Container.Region.Right + textWidth + 8, y + 8),
            new(Container.Region.Right + 6, y + 8)
        };

        context.FillPolygon(pen.Color, polygon);
        context.DrawString(renderText, _font, Color.White, Container.Region.Right + 6, y - 6);
    }

    #endregion
}