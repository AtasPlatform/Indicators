namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

[DisplayName("Volume-based Support & Resistance Zones")]
public class VolumeSupResZones : Indicator
 {
    #region Nested Types

    public enum LabelLocations
    {
        [Display(ResourceType = typeof(Resources), Name = "Left")]
        Left,

        [Display(ResourceType = typeof(Resources), Name = "Right")]
        Right
    }

    public enum DisplayMode
    {
        [Display(ResourceType = typeof(Resources), Name = "Zone")]
        Zone,

        [Display(ResourceType = typeof(Resources), Name = "Line")]
        Line,

        [Display(ResourceType = typeof(Resources), Name = "Disabled")]
        Disabled
    }

    public enum TimeFrameScale
    {
        M1 = 1,
        M5 = 5,
        M10 = 10,
        M15 = 15,
        M30 = 30,

        [Display(ResourceType = typeof(Resources), Name = "Hourly")]
        Hourly = 60,

        [Display(ResourceType = typeof(Resources), Name = "H2")]
        H2 = 120,

        [Display(ResourceType = typeof(Resources), Name = "H4")]
        H4 = 240,

        [Display(ResourceType = typeof(Resources), Name = "H6")]
        H6 = 360,

        [Display(ResourceType = typeof(Resources), Name = "Daily")]
        Daily = 1440,

        [Display(ResourceType = typeof(Resources), Name = "Weekly")]
        Weekly = 10080,

        [Display(ResourceType = typeof(Resources), Name = "Monthly")]
        Monthly = 0
    }

    internal class TFPeriod
    {
        private int _startBar;
        private int _endBar;
        private decimal _open;
        private decimal _high = decimal.MinValue;
        private decimal _low = decimal.MaxValue;
        private decimal _close;
        private decimal _volume;
        private decimal _curBarVolume; 
        private int _highBar;
        private int _lowBar;
        private int _lastBar = -1;
        private bool _isNewPeriod;

        internal int StartBar => _startBar;
        internal int EndBar => _endBar;
        internal decimal Open => _open;
        internal decimal High => _high;
        internal decimal Low => _low;
        internal decimal Close => _close;
        internal decimal Volume => _volume + _curBarVolume;
        internal int HighBar => _highBar;
        internal int LowBar => _lowBar;
        internal bool IsNewPeriod => _isNewPeriod;

        internal TFPeriod(int bar, IndicatorCandle candle)
        {
            _startBar = bar;
            _open = candle.Open;
            AddCandle(bar, candle);
        }

        internal void AddCandle(int bar, IndicatorCandle candle)
        {
            if (candle.High > _high)
            {
                _high = candle.High;
                _highBar = bar;
            }

            if (candle.Low < _low)
            {
                _low = candle.Low;
                _lowBar = bar;
            }

            _close = candle.Close;
            _endBar = bar;
            _isNewPeriod = false;

            if (bar != _lastBar)
            {
                _volume += _curBarVolume;
                _isNewPeriod = true;
            }

            _curBarVolume = candle.Volume;

            _lastBar = bar;
        }
    }

    internal class Signal
    {
        internal int StartBar { get; set; }
        internal int StartBarNext { get; set; }
        internal int EndBar { get; set; }
        internal decimal HighPrice { get; set; }
        internal decimal LowPrice { get; set; }
    }

    internal class TimeFrameObj
    {
        private readonly List<TFPeriod> _periods = new();
        private readonly TimeFrameScale _timeFrame;
        private readonly int _secondsPerTframe;
        private readonly Func<int, bool> IsNewSession;
        private readonly Func<int, bool> IsNewWeek;
        private readonly Func<int, bool> IsNewMonth;
        private readonly Func<int, IndicatorCandle> GetCandle;
        private readonly int _smaPeriod;

        internal readonly List<Signal> _upperSignals = new();
        internal readonly List<Signal> _lowerSignals = new();
        private bool _isNewPeriod;

        internal TFPeriod this[int index]
        {
            get => _periods[Count - 1 - index];
            set => _periods[Count - 1 - index] = value;
        }

        internal int Count => _periods.Count;
        internal bool IsNewPeriod => _isNewPeriod;
        internal int SecondsPerTframe => _secondsPerTframe;

        internal TimeFrameObj(TimeFrameScale timeFrame, 
                            int smaPeriod,
                            Func<int, bool> isNewSession,
                            Func<int, bool> isNewWeek,
                            Func<int, bool> isNewMonth,
                            Func<int, IndicatorCandle> getCandle)
        {
            _timeFrame = timeFrame;
            _secondsPerTframe = 60 * (int)timeFrame;
            IsNewSession = isNewSession;
            IsNewWeek = isNewWeek;
            IsNewMonth = isNewMonth;
            GetCandle = getCandle;
            _smaPeriod = smaPeriod;
        }

        internal decimal GetSmaVolume(int index) 
        {
            var sum = 0m;
            var start = Math.Max(0, Count - _smaPeriod - index);
            var realSmaPeriod = Math.Min(Count, _smaPeriod);

            for (int i = start; i < (start + realSmaPeriod); i++) 
            {
                sum += _periods[i].Volume;
            }

            return sum / _smaPeriod;
        }

        internal void AddBar(int bar)
        {
            _isNewPeriod = false;
            var candle = GetCandle(bar);

            if (bar == 0)
                CreateNewPeriod(bar, candle);

            var beginTime = GetBeginTime(candle.Time, _timeFrame);
            var isNewBar = false;
            var isCustomPeriod = false;
            var endBar = _periods.Last().EndBar;

            if (_timeFrame == TimeFrameScale.Weekly)
            {
                isCustomPeriod = true;
                isNewBar = IsNewWeek(bar);
            }
            else if (_timeFrame == TimeFrameScale.Monthly)
            {
                isCustomPeriod = true;
                isNewBar = IsNewMonth(bar);
            }
            else if (_timeFrame == TimeFrameScale.Daily)
            {
                isCustomPeriod = true;
                isNewBar = IsNewSession(bar);
            }

            if (isNewBar || !isCustomPeriod && (beginTime >= GetCandle(endBar).LastTime))
            {
                if (!_periods.Exists(p => p.StartBar == bar))
                    CreateNewPeriod(bar, candle);
            }
            else
                _periods.Last().AddCandle(bar, candle);
        }

        private void CreateNewPeriod(int bar, IndicatorCandle candle)
        {
            _periods.Add(new TFPeriod(bar, candle));
            _isNewPeriod = true;
        }

        private DateTime GetBeginTime(DateTime time, TimeFrameScale period)
        {
            if (period == TimeFrameScale.Monthly)
                return new DateTime(time.Year, time.Month, 1);

            var tim = time;
            tim = tim.AddMilliseconds(-tim.Millisecond);
            tim = tim.AddSeconds(-tim.Second);

            var begin = (tim - new DateTime()).TotalMinutes % (int)period;
            var res = tim.AddMinutes(-begin);
            return res;
        }
    }

    #endregion

    #region Fields

    private readonly int _shift = 5;

    private TimeFrameObj _tfObj1;
    private TimeFrameObj _tfObj2;
    private TimeFrameObj _tfObj3;
    private TimeFrameObj _tfObj4;

    private RenderFont _labelFont;
    private bool _isFixedTimeFrame;
    private int _secondsPerCandle;
   
    private float _labelTextSize = 10f;
    private TimeFrameScale _timeFrameType1;
    private int _smaPeriod1 = 6;
    private TimeFrameScale _timeFrameType2;
    private int _smaPeriod2 = 6;
    private TimeFrameScale _timeFrameType3;
    private int _smaPeriod3 = 6;
    private TimeFrameScale _timeFrameType4;
    private int _smaPeriod4 = 6;
    
    #endregion

    #region Properties

    [Display(ResourceType = typeof(Resources), Name = "ExtendPrevious", GroupName = "General")]
    public bool ExtendPrevious { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "ExtendLast", GroupName = "General")]
    public bool ExtendLast { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "ShowTimeFrameLabel", GroupName = "General")]
    public bool ShowTimeFrameLabel { get; set; }

    [Display(ResourceType = typeof(Resources), Name = "LabelLocation", GroupName = "General")]
    public LabelLocations LabelLocation { get; set; }

    [Range(1, 50)]
    [Display(ResourceType = typeof(Resources), Name = "TextSize", GroupName = "General")]
    public float LabelTextSize 
    { 
        get => _labelTextSize; 
        set
        {
            _labelTextSize = value;
            _labelFont = new RenderFont("Arial", _labelTextSize);
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ShowLines", GroupName = "HighLow")]
    public bool ShowHLLines { get; set; } = true;

    [Range(1, 20)]
    [Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "HighLow")]
    public float HLLineWidth { get; set; } = 2f;

    [Display(ResourceType = typeof(Resources), Name = "LineStyle", GroupName = "HighLow")]
    public DashStyle HLLineStyle { get; set; } = DashStyle.Solid;

    [Display(ResourceType = typeof(Resources), Name = "ShowLines", GroupName = "OpenClose")]
    public bool ShowOCLines { get; set; } = true;

    [Range(1, 20)]
    [Display(ResourceType = typeof(Resources), Name = "LineWidth", GroupName = "OpenClose")]
    public float OCLineWidth { get; set; } = 2f;

    [Display(ResourceType = typeof(Resources), Name = "LineStyle", GroupName = "OpenClose")]
    public DashStyle OCLineStyle { get; set; } = DashStyle.Solid;


    [Display(ResourceType = typeof(Resources), Name = "TimeFrame", GroupName = "TimeFrame1")]
    public TimeFrameScale TimeFrameType1
    { 
        get => _timeFrameType1;
        set
        {
            _timeFrameType1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "DisplayMode", GroupName = "TimeFrame1")]
    public DisplayMode DisplayMode1 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "TimeFrame1")]
    public int SmaPeriod1
    {
        get => _smaPeriod1; 
        set
        {
            _smaPeriod1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ResistanceColor", GroupName = "TimeFrame1")]
    public Color ResColor1 { get; set; } = DefaultColors.Red;

    [Display(ResourceType = typeof(Resources), Name = "SupportColor", GroupName = "TimeFrame1")]
    public Color SupColor1 { get; set; } = DefaultColors.Green;

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "TimeFrame1")]
    public int ZoneTransparency1 { get; set; } = 5;

    [Display(ResourceType = typeof(Resources), Name = "TimeFrame", GroupName = "TimeFrame2")]
    public TimeFrameScale TimeFrameType2 
    { 
        get => _timeFrameType2;
        set
        {
            _timeFrameType2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "DisplayMode", GroupName = "TimeFrame2")]
    public DisplayMode DisplayMode2 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "TimeFrame2")]
    public int SmaPeriod2 
    { 
        get => _smaPeriod2; 
        set
        {
            _smaPeriod2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ResistanceColor", GroupName = "TimeFrame2")]
    public Color ResColor2 { get; set; } = DefaultColors.Red;

    [Display(ResourceType = typeof(Resources), Name = "SupportColor", GroupName = "TimeFrame2")]
    public Color SupColor2 { get; set; } = DefaultColors.Green;

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "TimeFrame2")]
    public int ZoneTransparency2 { get; set; } = 5;

    [Display(ResourceType = typeof(Resources), Name = "TimeFrame", GroupName = "TimeFrame3")]
    public TimeFrameScale TimeFrameType3 
    {
        get => _timeFrameType3;
        set
        {
            _timeFrameType3 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "DisplayMode", GroupName = "TimeFrame3")]
    public DisplayMode DisplayMode3 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "TimeFrame3")]
    public int SmaPeriod3 
    {
        get => _smaPeriod3; 
        set
        {
            _smaPeriod3 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ResistanceColor", GroupName = "TimeFrame3")]
    public Color ResColor3 { get; set; } = DefaultColors.Red;

    [Display(ResourceType = typeof(Resources), Name = "SupportColor", GroupName = "TimeFrame3")]
    public Color SupColor3 { get; set; } = DefaultColors.Green;

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "TimeFrame3")]
    public int ZoneTransparency3 { get; set; } = 5;

    [Display(ResourceType = typeof(Resources), Name = "TimeFrame", GroupName = "TimeFrame4")]
    public TimeFrameScale TimeFrameType4 
    { 
        get => _timeFrameType4;
        set
        {
            _timeFrameType4 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "DisplayMode", GroupName = "TimeFrame4")]
    public DisplayMode DisplayMode4 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "TimeFrame4")]
    public int SmaPeriod4 
    { 
        get => _smaPeriod4; 
        set
        {
            _smaPeriod4 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Resources), Name = "ResistanceColor", GroupName = "TimeFrame4")]
    public Color ResColor4 { get; set; } = DefaultColors.Red;

    [Display(ResourceType = typeof(Resources), Name = "SupportColor", GroupName = "TimeFrame4")]
    public Color SupColor4 { get; set; } = DefaultColors.Green;

    [Range(0, 10)]
    [Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "TimeFrame4")]
    public int ZoneTransparency4 { get; set; } = 5;

    #endregion

    #region ctor

    public VolumeSupResZones() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        _labelFont = new RenderFont("Arial", _labelTextSize);
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        GetCandleSeconds();
        _tfObj1 = new(TimeFrameType1, _smaPeriod1, IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj2 = new(TimeFrameType2, _smaPeriod2, IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj3 = new(TimeFrameType3, _smaPeriod3, IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj4 = new(TimeFrameType4, _smaPeriod4, IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (!_isFixedTimeFrame) return;

        TimeFrameObjCalculate(bar, _tfObj1);
        TimeFrameObjCalculate(bar, _tfObj2);
        TimeFrameObjCalculate(bar, _tfObj3);
        TimeFrameObjCalculate(bar, _tfObj4);
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo == null) return;

        DrawSupportResistance(context, _tfObj1, DisplayMode1, SupColor1, ResColor1, ZoneTransparency1, _timeFrameType1);
        DrawSupportResistance(context, _tfObj2, DisplayMode2, SupColor2, ResColor2, ZoneTransparency2, _timeFrameType2);
        DrawSupportResistance(context, _tfObj3, DisplayMode3, SupColor3, ResColor3, ZoneTransparency3, _timeFrameType3);
        DrawSupportResistance(context, _tfObj4, DisplayMode4, SupColor4, ResColor4, ZoneTransparency4, _timeFrameType4);
    }

    #endregion

    #region Private Methods

    private void DrawSupportResistance(RenderContext context, TimeFrameObj tfObj,
                             DisplayMode displayMode, Color supColor, Color resColor,
                             int zoneTransparency, TimeFrameScale tfType)
    {
        if(displayMode == DisplayMode.Disabled) return;

        var upper = tfObj._upperSignals;
        DrawSignals(context, upper, displayMode, resColor, zoneTransparency, tfType, true);

        var lower = tfObj._lowerSignals;
        DrawSignals(context, lower, displayMode, supColor, zoneTransparency, tfType, false);
    }

    private void DrawSignals(RenderContext context, List<Signal> signals, DisplayMode displayMode,
                             Color color, int zoneTransparency, TimeFrameScale tfType, bool isUpper)
    {
        foreach (var signal in signals)
        {
            var endBar = ExtendPrevious
                ? signal == signals.Last()
                  ? signal.EndBar
                  : signal.StartBarNext
                : signal.EndBar;

            if (signal == signals.Last() && ExtendLast)
                endBar = CurrentBar - 1;

            if (signal.StartBar > LastVisibleBarNumber || endBar < FirstVisibleBarNumber)
                continue;

            var x1 = ChartInfo.GetXByBar(signal.StartBar);
            var x2 = ChartInfo.GetXByBar(endBar);

            if (signal == signals.Last() && ExtendLast)
                x2 = ChartInfo.Region.Width;

            var highY = ChartInfo.GetYByPrice(signal.HighPrice, false);
            var lowY = ChartInfo.GetYByPrice(signal.LowPrice, false);

            if (ShowHLLines && ShowOCLines && displayMode == DisplayMode.Zone)
            {
                var rec = new Rectangle(x1, highY, x2 - x1, lowY - highY);
                context.FillRectangle(GetColorTransparency(color, zoneTransparency), rec);
            }

            if (ShowHLLines)
            {
                var pen = new RenderPen(color, HLLineWidth) { DashStyle = HLLineStyle };
                context.DrawLine(pen, x1, highY, x2, highY);
            }

            if (ShowOCLines)
            {
                var pen = new RenderPen(color, OCLineWidth) { DashStyle = OCLineStyle };
                context.DrawLine(pen, x1, lowY, x2, lowY);
            }

            if (signal == signals.Last() && ShowTimeFrameLabel)
            {
                var zoneType = isUpper ? "R" : "S";
                var lText = $"{tfType} ({zoneType})";
                var labelSize = context.MeasureString(lText, _labelFont);
                var lX = LabelLocation == LabelLocations.Left ? x1 - labelSize.Width : ChartInfo.GetXByBar(endBar);
                var lY = isUpper ? highY - _shift - labelSize.Height : lowY + _shift;
                var rec = new Rectangle(lX, lY, labelSize.Width, labelSize.Height);
                context.DrawString(lText, _labelFont, color, rec);
            } 
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        return Color.FromArgb((byte)(tr * 25), color.R, color.G, color.B);
    }

    private void TimeFrameObjCalculate(int bar, TimeFrameObj tfObj)
    {
        if (_secondsPerCandle > tfObj.SecondsPerTframe) return;

        tfObj.AddBar(bar);

        if (tfObj.IsNewPeriod && tfObj.Count > 5)
        {
            if (tfObj[3].High > tfObj[4].High && tfObj[4].High > tfObj[5].High
                && tfObj[3].High > tfObj[2].High && tfObj[2].High > tfObj[1].High
                && tfObj[3].Volume > tfObj.GetSmaVolume(3))
            {
                if (tfObj._upperSignals.Count > 0)
                    tfObj._upperSignals[^1].StartBarNext = tfObj[3].HighBar;

                var signal = new Signal()
                {
                    StartBar = tfObj[3].HighBar,
                    EndBar = tfObj[0].StartBar,
                    HighPrice = tfObj[3].High,
                    LowPrice = Math.Max(tfObj[3].Open, tfObj[3].Close)
                };

                tfObj._upperSignals.Add(signal);
            }
            else if (tfObj[3].Low < tfObj[4].Low && tfObj[4].Low < tfObj[5].Low
                && tfObj[3].Low < tfObj[2].Low && tfObj[2].Low < tfObj[1].Low
                && tfObj[3].Volume > tfObj.GetSmaVolume(3))
            {
                if (tfObj._lowerSignals.Count > 0)
                    tfObj._lowerSignals[^1].StartBarNext = tfObj[3].LowBar;

                var signal = new Signal()
                {
                    StartBar = tfObj[3].LowBar,
                    EndBar = tfObj[0].StartBar,
                    LowPrice = tfObj[3].Low,
                    HighPrice = Math.Min(tfObj[3].Open, tfObj[3].Close)
                };

                tfObj._lowerSignals.Add(signal);
            }
        }
    }

    private void GetCandleSeconds()
    {
        if (ChartInfo is null) return;

        var timeFrame = ChartInfo.TimeFrame;

        if (ChartInfo.ChartType == "Seconds")
        {
            _isFixedTimeFrame = true;

            _secondsPerCandle = ChartInfo.TimeFrame switch
            {
                "5" => 5,
                "10" => 10,
                "15" => 15,
                "30" => 30,
                _ => 0
            };

            if (_secondsPerCandle == 0)
            {
                if (int.TryParse(Regex.Match(timeFrame, @"\d{1,}$").Value, out var periodSec))
                {
                    _secondsPerCandle = periodSec;
                    return;
                }
            }
        }

        if (ChartInfo.ChartType != "TimeFrame")
            return;

        _isFixedTimeFrame = true;

        _secondsPerCandle = ChartInfo.TimeFrame switch
        {
            "M1" => 60 * (int)TimeFrameScale.M1,
            "M5" => 60 * (int)TimeFrameScale.M5,
            "M10" => 60 * (int)TimeFrameScale.M10,
            "M15" => 60 * (int)TimeFrameScale.M15,
            "M30" => 60 * (int)TimeFrameScale.M30,
            "Hourly" => 60 * (int)TimeFrameScale.Hourly,
            "H2" => 60 * (int)TimeFrameScale.H2,
            "H4" => 60 * (int)TimeFrameScale.H4,
            "H6" => 60 * (int)TimeFrameScale.H6,
            "Daily" => 60 * (int)TimeFrameScale.Daily,
            "Weekly" => 60 * (int)TimeFrameScale.Weekly,
            _ => 0
        };

        if (_secondsPerCandle != 0)
            return;

        if (!int.TryParse(Regex.Match(timeFrame, @"\d{1,}$").Value, out var period))
            return;

        if (timeFrame.Contains('M'))
        {
            _secondsPerCandle = 60 * (int)TimeFrameScale.M1 * period;
            return;
        }

        if (timeFrame.Contains('H'))
        {
            _secondsPerCandle = 60 * (int)TimeFrameScale.Daily * period;
            return;
        }

        if (timeFrame.Contains('D'))
            _secondsPerCandle = 60 * (int)TimeFrameScale.Daily * period;
    }


    #endregion
}
