namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using Rectangle = System.Drawing.Rectangle;

#if CROSS_PLATFORM
    using Color = System.Drawing.Color;
#else
using Color = System.Windows.Media.Color;
#endif

[DisplayName("Volume-based Support & Resistance Zones")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeSupResZonesDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000619397")]
public class VolumeSupResZones : Indicator
 {
    #region Nested Types

    public enum LabelLocations
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Left))]
        Left,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Right))]
        Right
    }

    public enum DisplayMode
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Zone))]
        Zone,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line))]
        Line,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Disabled))]
        Disabled
    }

    public enum TimeFrameScale
    {
        M1 = 1,
        M5 = 5,
        M10 = 10,
        M15 = 15,
        M30 = 30,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Hourly))]
        Hourly = 60,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.H2))]
        H2 = 120,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.H4))]
        H4 = 240,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.H6))]
        H6 = 360,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Daily))]
        Daily = 1440,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Weekly))]
        Weekly = 10080,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Monthly))]
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
        private readonly string _name;

        private bool _isNewPeriod;

        internal TFPeriod this[int index]
        {
            get => _periods[Count - 1 - index];
            set => _periods[Count - 1 - index] = value;
        }

        internal string Name => _name;
        internal int Count => _periods.Count;
        internal bool IsNewPeriod => _isNewPeriod;
        internal int SecondsPerTframe => _secondsPerTframe;

        internal TimeFrameObj(TimeFrameScale timeFrame, 
                            int smaPeriod,
                            string name,
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
            _name = name;
        }

        internal decimal GetSmaVolume(int index) 
        {
            var sum = 0m;
            var start = Math.Max(0, Count - _smaPeriod - index);
            var realSmaPeriod = Math.Min(Count, _smaPeriod);

            for (int i = start; i < (start + realSmaPeriod); i++)
            {
                sum += _periods[i].Volume / realSmaPeriod;
            }

            return sum;
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
    private readonly FontSetting _labelFont = new() { FontFamily = "Arial", Size = 10 };
    private readonly PenSettings _pen = new();

    private TimeFrameObj _tfObj1;
    private TimeFrameObj _tfObj2;
    private TimeFrameObj _tfObj3;
    private TimeFrameObj _tfObj4;

    private System.Drawing.Color _resColorTransp1;
    private System.Drawing.Color _supColorTransp1;
    private System.Drawing.Color _resColorTransp2;
    private System.Drawing.Color _supColorTransp2;
    private System.Drawing.Color _resColorTransp3;
    private System.Drawing.Color _supColorTransp3;
    private System.Drawing.Color _resColorTransp4;
    private System.Drawing.Color _supColorTransp4;

    private bool _isFixedTimeFrame;
    private int _secondsPerCandle;
   
    private TimeFrameScale _timeFrameType1;
    private int _smaPeriod1 = 6;
    private TimeFrameScale _timeFrameType2;
    private int _smaPeriod2 = 6;
    private TimeFrameScale _timeFrameType3;
    private int _smaPeriod3 = 6;
    private TimeFrameScale _timeFrameType4;
    private int _smaPeriod4 = 6;
    private Color _resColor1 = System.Drawing.Color.Red.Convert();
    private Color _supColor1 = System.Drawing.Color.Green.Convert();
    private int _zoneTransparency1 = 5;
    private Color _resColor2 = System.Drawing.Color.Red.Convert();
    private Color _supColor2 = System.Drawing.Color.Green.Convert();
    private int _zoneTransparency2 = 5;
    private Color _resColor3 = System.Drawing.Color.Red.Convert();
    private Color _supColor3 = System.Drawing.Color.Green.Convert();
    private int _zoneTransparency3 = 5;
    private Color _resColor4 = System.Drawing.Color.Red.Convert();
    private Color _supColor4 = System.Drawing.Color.Green.Convert();
    private int _zoneTransparency4 = 5;
    private Color _alertForeColor = Color.FromArgb(255, 247, 249, 249);
    private Color _alertBackgroundColor = Color.FromArgb(255, 75, 72, 72);

    #endregion

    #region Properties

    #region General

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ExtendPrevious), GroupName = nameof(Strings.General), Description = nameof(Strings.ExtendPreviousDescription))]
    public bool ExtendPrevious { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ExtendLast), GroupName = nameof(Strings.General), Description = nameof(Strings.ExtendLastDescription))]
    public bool ExtendLast { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTimeFrameLabel), GroupName = nameof(Strings.General), Description = nameof(Strings.IsNeedShowLabelDescription))]
    public bool ShowTimeFrameLabel { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LabelLocation), GroupName = nameof(Strings.General), Description = nameof(Strings.LabelLocationDescription))]
    public LabelLocations LabelLocation { get; set; }

    [Range(1, 50)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextSize), GroupName = nameof(Strings.General), Description = nameof(Strings.TextSizeDescription))]
    public int LabelTextSize 
    {
        get => _labelFont.Size;
        set => _labelFont.Size = value;
    }

    #endregion

    #region HighLow

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLines), GroupName = nameof(Strings.HighLow), Description = nameof(Strings.IsNeedShowLinesDescription))]
    public bool ShowHLLines { get; set; } = true;

    [Range(1, 20)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.HighLow), Description = nameof(Strings.LineWidthDescription))]
    public int HLLineWidth { get; set; } = 2;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineStyle), GroupName = nameof(Strings.HighLow), Description = nameof(Strings.LineDashStyleDescription))]
    public LineDashStyle HLLineStyle { get; set; } = LineDashStyle.Solid;

    #endregion

    #region OpenClose

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLines), GroupName = nameof(Strings.OpenClose), Description = nameof(Strings.IsNeedShowLinesDescription))]
    public bool ShowOCLines { get; set; } = true;

    [Range(1, 20)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.OpenClose), Description = nameof(Strings.LineWidthDescription))]
    public int OCLineWidth { get; set; } = 2;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineStyle), GroupName = nameof(Strings.OpenClose), Description = nameof(Strings.LineDashStyleDescription))]
    public LineDashStyle OCLineStyle { get; set; } = LineDashStyle.Solid;

    #endregion

    #region TimeFrame1

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.SelectTimeframeDescription))]
    public TimeFrameScale TimeFrameType1
    { 
        get => _timeFrameType1;
        set
        {
            _timeFrameType1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DisplayMode), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.ElementDisplayModeDescription))]
    public DisplayMode DisplayMode1 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.PeriodDescription))]
    public int SmaPeriod1
    {
        get => _smaPeriod1; 
        set
        {
            _smaPeriod1 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResistanceColor), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.ResistanceColorDescription))]
    public Color ResColor1 
    { 
        get => _resColor1;
        set
        {
            _resColor1 = value;
            _resColorTransp1 = GetColorTransparency(_resColor1, _zoneTransparency1).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SupportColor), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.SupportColorDescription))]
    public Color SupColor1 
    {
        get => _supColor1;
        set
        {
            _supColor1 = value;
            _supColorTransp1 = GetColorTransparency(_supColor1, _zoneTransparency1).Convert();
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.TimeFrame1), Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int ZoneTransparency1 
    { 
        get => _zoneTransparency1; 
        set
        {
            _zoneTransparency1 = value;
            _resColorTransp1 = GetColorTransparency(_resColor1, _zoneTransparency1).Convert();
            _supColorTransp1 = GetColorTransparency(_supColor1, _zoneTransparency1).Convert();
        }
    }

    #endregion

    #region TimeFrame2

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.SelectTimeframeDescription))]
    public TimeFrameScale TimeFrameType2 
    { 
        get => _timeFrameType2;
        set
        {
            _timeFrameType2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DisplayMode), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.ElementDisplayModeDescription))]
    public DisplayMode DisplayMode2 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.PeriodDescription))]
    public int SmaPeriod2 
    { 
        get => _smaPeriod2; 
        set
        {
            _smaPeriod2 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResistanceColor), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.ResistanceColorDescription))]
    public Color ResColor2 
    {
        get => _resColor2; 
        set
        {
            _resColor2 = value;
            _resColorTransp2 = GetColorTransparency(_resColor2, _zoneTransparency2).Convert();
        } 
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SupportColor), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.SupportColorDescription))]
    public Color SupColor2 
    { 
        get => _supColor2; 
        set
        {
            _supColor2 = value;
            _supColorTransp2 = GetColorTransparency(_supColor2, _zoneTransparency2).Convert();
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.TimeFrame2), Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int ZoneTransparency2 
    {
        get => _zoneTransparency2;
        set
        {
            _zoneTransparency2 = value;
            _resColorTransp2 = GetColorTransparency(_resColor2, _zoneTransparency2).Convert();
            _supColorTransp2 = GetColorTransparency(_supColor2, _zoneTransparency2).Convert();
        }
    }

    #endregion

    #region TimeFrame3

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.SelectTimeframeDescription))]
    public TimeFrameScale TimeFrameType3 
    {
        get => _timeFrameType3;
        set
        {
            _timeFrameType3 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DisplayMode), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.ElementDisplayModeDescription))]
    public DisplayMode DisplayMode3 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.PeriodDescription))]
    public int SmaPeriod3 
    {
        get => _smaPeriod3; 
        set
        {
            _smaPeriod3 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResistanceColor), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.ResistanceColorDescription))]
    public Color ResColor3 
    { 
        get => _resColor3;
        set
        {
            _resColor3 = value;
            _resColorTransp3 = GetColorTransparency(_resColor3, _zoneTransparency3).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SupportColor), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.SupportColorDescription))]
    public Color SupColor3 
    { 
        get => _supColor3;
        set
        {
            _supColor3 = value;
            _supColorTransp3 = GetColorTransparency(_supColor3, _zoneTransparency3).Convert();
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.TimeFrame3), Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int ZoneTransparency3 
    {
        get => _zoneTransparency3; 
        set
        {
            _zoneTransparency3 = value;
            _resColorTransp3 = GetColorTransparency(_resColor3, _zoneTransparency3).Convert();
            _supColorTransp3 = GetColorTransparency(_supColor3, _zoneTransparency3).Convert();
        }
    }

    #endregion

    #region TimeFrame4

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.SelectTimeframeDescription))]
    public TimeFrameScale TimeFrameType4 
    { 
        get => _timeFrameType4;
        set
        {
            _timeFrameType4 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DisplayMode), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.ElementDisplayModeDescription))]
    public DisplayMode DisplayMode4 { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.PeriodDescription))]
    public int SmaPeriod4 
    { 
        get => _smaPeriod4; 
        set
        {
            _smaPeriod4 = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ResistanceColor), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.ResistanceColorDescription))]
    public Color ResColor4 
    { 
        get => _resColor4; 
        set
        {
            _resColor4 = value;
            _resColorTransp4 = GetColorTransparency(_resColor4, _zoneTransparency4).Convert();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SupportColor), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.SupportColorDescription))]
    public Color SupColor4 
    {
        get => _supColor4;
        set
        {
            _supColor4 = value;
            _supColorTransp4 = GetColorTransparency(_supColor4, _zoneTransparency4).Convert();
        }
    }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.TimeFrame4), Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int ZoneTransparency4 
    { 
        get => _zoneTransparency4;
        set
        {
            _zoneTransparency4 = value;
            _resColorTransp4 = GetColorTransparency(_resColor4, _zoneTransparency4).Convert();
            _supColorTransp4 = GetColorTransparency(_supColor4, _zoneTransparency4).Convert();
        }
    }

    #endregion

    #region Alerts

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertDescription))]
    public bool UseAlert { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Foreground), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription))]
    [DataMember]
    public Color AlertForeColor
    {
        get => _alertForeColor;
        set => _alertForeColor = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription))]
    [DataMember]
    public Color AlertBGColor
    {
        get => _alertBackgroundColor;
        set => _alertBackgroundColor = value;
    }

    #endregion

    #endregion

    #region ctor

    public VolumeSupResZones() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        _resColorTransp1 = GetColorTransparency(_resColor1, _zoneTransparency1).Convert();
        _supColorTransp1 = GetColorTransparency(_supColor1, _zoneTransparency1).Convert();
        _resColorTransp2 = GetColorTransparency(_resColor2, _zoneTransparency2).Convert();
        _supColorTransp2 = GetColorTransparency(_supColor2, _zoneTransparency2).Convert();
        _resColorTransp3 = GetColorTransparency(_resColor3, _zoneTransparency3).Convert();
        _supColorTransp3 = GetColorTransparency(_supColor3, _zoneTransparency3).Convert();
        _resColorTransp4 = GetColorTransparency(_resColor4, _zoneTransparency4).Convert();
        _supColorTransp4 = GetColorTransparency(_supColor4, _zoneTransparency4).Convert();
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        GetCandleSeconds();
        _tfObj1 = new(TimeFrameType1, _smaPeriod1, "Time frame 1", IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj2 = new(TimeFrameType2, _smaPeriod2, "Time frame 2", IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj3 = new(TimeFrameType3, _smaPeriod3, "Time frame 3", IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _tfObj4 = new(TimeFrameType4, _smaPeriod4, "Time frame 4", IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
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

        DrawSupportResistance(context, _tfObj1, DisplayMode1, _supColor1, _resColor1, _supColorTransp1, _resColorTransp1, _timeFrameType1);
        DrawSupportResistance(context, _tfObj2, DisplayMode2, _supColor2, _resColor2, _supColorTransp2, _resColorTransp2, _timeFrameType2);
        DrawSupportResistance(context, _tfObj3, DisplayMode3, _supColor3, _resColor3, _supColorTransp3, _resColorTransp3, _timeFrameType3);
        DrawSupportResistance(context, _tfObj4, DisplayMode4, _supColor4, _resColor4, _supColorTransp4, _resColorTransp4, _timeFrameType4);
    }

    #endregion

    #region Private Methods

    private void DrawSupportResistance(RenderContext context, TimeFrameObj tfObj, DisplayMode displayMode,
                                       Color supColor, Color resColor,
                                       System.Drawing.Color supColorTransp, System.Drawing.Color resColorTransp, 
                                       TimeFrameScale tfType)
    {
        if(displayMode == DisplayMode.Disabled) return;

        var upper = tfObj._upperSignals;
        DrawSignals(context, upper, displayMode, resColor, resColorTransp, tfType, true);

        var lower = tfObj._lowerSignals;
        DrawSignals(context, lower, displayMode, supColor, supColorTransp, tfType, false);
    }

    private void DrawSignals(RenderContext context, List<Signal> signals, DisplayMode displayMode,
                             Color color, System.Drawing.Color colorTransp, TimeFrameScale tfType, bool isUpper)
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
            _pen.Color = color;

            if (ShowHLLines && ShowOCLines && displayMode == DisplayMode.Zone)
            {
                var rec = new Rectangle(x1, highY, x2 - x1, lowY - highY);
                context.FillRectangle(colorTransp, rec);
            }

            if (ShowHLLines)
            {
                _pen.Width = HLLineWidth;
                _pen.LineDashStyle = HLLineStyle;
                context.DrawLine(_pen.RenderObject, x1, highY, x2, highY);
            }

            if (ShowOCLines)
            {
                _pen.Width = OCLineWidth;
                _pen.LineDashStyle = OCLineStyle;
                context.DrawLine(_pen.RenderObject, x1, lowY, x2, lowY);
            }

            if (signal == signals.Last() && ShowTimeFrameLabel)
            {
                var zoneType = isUpper ? "R" : "S";
                var lText = $"{tfType} ({zoneType})";
                var labelSize = context.MeasureString(lText, _labelFont.RenderObject);
                var lX = LabelLocation == LabelLocations.Left ? x1 - labelSize.Width : ChartInfo.GetXByBar(endBar);
                var lY = isUpper ? highY - _shift - labelSize.Height : lowY + _shift;
                var rec = new Rectangle(lX, lY, labelSize.Width, labelSize.Height);
                context.DrawString(lText, _labelFont.RenderObject, color.Convert(), rec);
            } 
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5)
    {
        var alfa = Math.Max(color.A - tr * 25, 0);
        return Color.FromArgb((byte)(alfa), color.R, color.G, color.B);
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
                TrySetAlert(bar, tfObj, true);
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
                TrySetAlert(bar, tfObj, false);
            }
        }
    }

    private void TrySetAlert(int bar, TimeFrameObj tfObj, bool isUpper)
    {
        if (bar != CurrentBar - 1 || !UseAlert)
            return;

        var dir = isUpper ? "upper" : "lower";
        var endLine = Environment.NewLine;
        var message = $"New Zone appeared!{endLine}" +
                      $"{tfObj.Name} {dir}{endLine}";

        AddAlert(AlertFile, InstrumentInfo.Instrument, message, _alertBackgroundColor, _alertForeColor);
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
            "H1" => 60 * (int)TimeFrameScale.Hourly,
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
