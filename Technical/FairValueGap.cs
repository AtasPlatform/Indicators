namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using Color = System.Drawing.Color;

[DisplayName("Fair Value Gap")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.FairValueGapDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000618795")]
public class FairValueGap : Indicator
{
    #region Nested Types

    public enum TimeFrameScale
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M1))]
        M1 = 1,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M5))]
        M5 = 5,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M10))]
        M10 = 10,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M15))]
        M15 = 15,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.M30))]
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

        internal int StartBar => _startBar;
        internal int EndBar => _endBar;
        internal decimal Open => _open;
        internal decimal High => _high;
        internal decimal Low => _low;
        internal decimal Close => _close;
        internal decimal Volume => _volume + _curBarVolume;
        internal int HighBar => _highBar;
        internal int LowBar => _lowBar;

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

            if (bar != _lastBar)
                _volume += _curBarVolume;

            _curBarVolume = candle.Volume;

            _lastBar = bar;
        }
    }

    internal class Signal
    {
        private decimal _high;
        private decimal _low;

        internal int StartBar { get; set; }
        internal int EndBar { get; set; }
        internal decimal HighPrice { get; set; }
        internal decimal LowPrice { get; set; }
        internal decimal FirstHighPrice => _high;
        internal decimal FirstLowPrice => _low;
        internal decimal MidPrice => (_high + _low) / 2;

        internal Signal(decimal high, decimal low)
        {
            _high = high;
            _low = low;
        }
    }

    internal class SignalContainer
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, Signal> _activeSignals = new();
        private readonly List<Signal> _closedSignals = new();

        internal void ProcessActiveSignals(Action<Signal> processor, List<Signal> signalsToClose)
        {
            lock (_lock)
            {
                foreach (var signal in _activeSignals.Values)
                {
                    processor(signal);
                }

                foreach (var signal in signalsToClose)
                {
                    _activeSignals.Remove(signal.StartBar);
                    _closedSignals.Add(signal);
                }
            }
        }

        internal Signal[] GetAllSignalsSnapshot()
        {
            lock (_lock)
            {
                var result = new Signal[_activeSignals.Count + _closedSignals.Count];
                var index = 0;
                foreach (var signal in _activeSignals.Values)
                    result[index++] = signal;
                foreach (var signal in _closedSignals)
                    result[index++] = signal;
                return result;
            }
        }

        internal void AddOrUpdate(int bar, decimal high, decimal low)
        {
            lock (_lock)
            {
                if (_activeSignals.TryGetValue(bar, out var signal))
                {
                    signal.HighPrice = high;
                    signal.LowPrice = low;
                }
                else
                {
                    signal = new Signal(high, low)
                    {
                        StartBar = bar,
                        HighPrice = high,
                        LowPrice = low
                    };
                    _activeSignals.Add(bar, signal);
                }
            }
        }

        internal void Remove(int bar)
        {
            lock (_lock)
            {
                _activeSignals.Remove(bar);
            }
        }

        internal void Clear()
        {
            lock (_lock)
            {
                _activeSignals.Clear();
                _closedSignals.Clear();
            }
        }
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

        internal readonly SignalContainer _upperSignals = new();
        internal readonly SignalContainer _lowerSignals = new();
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

    internal readonly SignalContainer _upperSignals = new();
    internal readonly SignalContainer _lowerSignals = new();

    private bool _isFixedTimeFrame;
    private int _secondsPerCandle;
    private TimeFrameObj _higherTfObj;
    private FontSetting _labelFont = new() { FontFamily = "Arial", Size = 10 };
    private PenSettings _bullishCurrentTfPen = new() { Color = DefaultColors.Green.Convert() };
    private PenSettings _bearishCurrentTfPen = new() { Color = DefaultColors.Red.Convert() };
    private PenSettings _bullishHigherTfPen = new() { Color = DefaultColors.Olive.Convert() };
    private PenSettings _bearishHigherTfPen = new() { Color = DefaultColors.Purple.Convert() };
    private PenSettings _midpointPen = new() { Color = DefaultColors.Gray.Convert() };
    private Color _bullishColorCurrentTFTransp;
    private Color _bearishColorCurrentTFTransp;
    private Color _bullishColorHigherTFTransp;
    private Color _bearishColorHigherTFTransp;

    private TimeFrameScale _timeframe;
    private bool _midpointTouch;
    private int _transparency = 5;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFrame), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SelectTimeframeDescription))]
    public TimeFrameScale HigherTimeframe 
    { 
        get => _timeframe;
        set
        {
            _timeframe = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MidpointTouch), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShowMidlineDescription))]
    public bool MidpointTouch 
    { 
        get => _midpointTouch; 
        set
        {
            _midpointTouch = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HideOlds), GroupName = nameof(Strings.Settings), Description = nameof(Strings.HideOldsElementsDescription))]
    public bool HideOlds { get; set; }

    [Range(0, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Transparency), GroupName = nameof(Strings.Settings), Description = nameof(Strings.VisualObjectsTransparencyDescription))]
    public int Transparency
    { 
        get => _transparency;
        set
        {
            _transparency = value;
            _bullishColorCurrentTFTransp = GetColorTransparency(_bullishCurrentTfPen.Color.Convert(), _transparency);
            _bearishColorCurrentTFTransp = GetColorTransparency(_bearishCurrentTfPen.Color.Convert(), _transparency);
            _bullishColorHigherTFTransp = GetColorTransparency(_bullishHigherTfPen.Color.Convert(), _transparency);
            _bearishColorHigherTFTransp = GetColorTransparency(_bearishHigherTfPen.Color.Convert(), _transparency);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.CurrentTimeFrame), Description = nameof(Strings.ShowGapsDescription))]
    public bool ShowCurrentTF{ get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.CurrentTimeFrame), Description = nameof(Strings.BullishColorDescription))]
    public Color BullishColorCurrentTF 
    {
        get => _bullishCurrentTfPen.Color.Convert();
        set
        {
            _bullishCurrentTfPen.Color = value.Convert();
            _bullishColorCurrentTFTransp = GetColorTransparency(_bullishCurrentTfPen.Color.Convert(), _transparency);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.CurrentTimeFrame), Description = nameof(Strings.BearishColorDescription))]
    public Color BearishColorCurrentTF 
    {
        get => _bearishCurrentTfPen.Color.Convert();
        set
        {
            _bearishCurrentTfPen.Color = value.Convert();
            _bearishColorCurrentTFTransp = GetColorTransparency(_bearishCurrentTfPen.Color.Convert(), _transparency);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.HigherTimeFrame), Description = nameof(Strings.ShowGapsDescription))]
    public bool ShowHigherTF { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.HigherTimeFrame), Description = nameof(Strings.BullishColorDescription))]
    public Color BullishColorHigherTF 
    { 
        get => _bullishHigherTfPen.Color.Convert();
        set
        {
            _bullishHigherTfPen.Color = value.Convert();
            _bullishColorHigherTFTransp = GetColorTransparency(_bullishHigherTfPen.Color.Convert(), _transparency);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearlishColor), GroupName = nameof(Strings.HigherTimeFrame), Description = nameof(Strings.BearishColorDescription))]
    public Color BearishColorHigherTF 
    { 
        get => _bearishHigherTfPen.Color.Convert(); 
        set
        {
            _bearishHigherTfPen.Color = value.Convert();
            _bearishColorHigherTFTransp = GetColorTransparency(_bearishHigherTfPen.Color.Convert(), _transparency);
        }
    }

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width), GroupName = nameof(Strings.Midpoint), Description = nameof(Strings.LineWidthDescription))]
    public int MidPointWidth 
    { 
        get => _midpointPen.Width; 
        set => _midpointPen.Width = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Midpoint), Description = nameof(Strings.LineColorDescription))]
    public Color MidPointColor
    { 
        get => _midpointPen.Color.Convert();
        set => _midpointPen.Color = value.Convert();
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Label), Description = nameof(Strings.IsNeedShowLabelDescription))]
    public bool ShowLabel { get; set; } = true;

    [Range(1, 50)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Size), GroupName = nameof(Strings.Label), Description = nameof(Strings.TextSizeDescription))]
    public int LabelSize 
    {
        get => _labelFont.Size;
        set => _labelFont.Size = value;
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Label), Description = nameof(Strings.LabelTextColorDescription))]
    public Color LabelColor { get; set; } = DefaultColors.Gray;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetX), GroupName = nameof(Strings.Label), Description = nameof(Strings.LabelOffsetXDescription))]
    public int LabelOffsetX { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.OffsetY), GroupName = nameof(Strings.Label), Description = nameof(Strings.LabelOffsetYDescription))]
    public int LabelOffsetY { get; set; } = 10;

    #endregion

    #region ctor

    public FairValueGap() : base(true)
    {

        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);

        _bullishColorCurrentTFTransp = GetColorTransparency(_bullishCurrentTfPen.Color.Convert(), _transparency);
        _bearishColorCurrentTFTransp = GetColorTransparency(_bearishCurrentTfPen.Color.Convert(), _transparency);
        _bullishColorHigherTFTransp = GetColorTransparency(_bullishHigherTfPen.Color.Convert(), _transparency);
        _bearishColorHigherTFTransp = GetColorTransparency(_bearishHigherTfPen.Color.Convert(), _transparency);
    }

    #endregion

    #region Protected Methods

    protected override void OnRecalculate()
    {
        GetCandleSeconds();
        _higherTfObj = new(HigherTimeframe, IsNewSession, IsNewWeek, IsNewMonth, GetCandle);
        _upperSignals.Clear();
        _lowerSignals.Clear();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        var currentCandle = GetCandle(bar);

        if (_isFixedTimeFrame)
        {
            HigherTfCalculate(bar);
            TryCloseGaps(bar, currentCandle, _higherTfObj._upperSignals, _higherTfObj._lowerSignals);
        }

        CurrentTfCalculate(bar, currentCandle);
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null) return;

        if (ShowCurrentTF)
        {
            DrawGaps(context, _upperSignals, ChartInfo.TimeFrame, _bullishColorCurrentTFTransp, _bullishCurrentTfPen);
            DrawGaps(context, _lowerSignals, ChartInfo.TimeFrame, _bearishColorCurrentTFTransp, _bearishCurrentTfPen);
        }

        if (ShowHigherTF)
        {
            DrawGaps(context, _higherTfObj._upperSignals, HigherTimeframe.ToString(), _bullishColorHigherTFTransp, _bullishHigherTfPen);
            DrawGaps(context, _higherTfObj._lowerSignals, HigherTimeframe.ToString(), _bearishColorHigherTFTransp, _bearishHigherTfPen);
        }
    }

    #endregion

    #region Private Methods

    private void DrawGaps(RenderContext context, SignalContainer gaps, string timeFrame, Color color, PenSettings penSet)
    {
        var signals = gaps.GetAllSignalsSnapshot();

        foreach (var signal in signals)
        {
            // Check visibility
            if (signal.StartBar > LastVisibleBarNumber)
                continue;
            if (signal.EndBar > 0 && signal.EndBar < FirstVisibleBarNumber)
                continue;

            if (HideOlds && signal.EndBar > 0)
                continue;

            var isClusterMode = ChartInfo.ChartVisualMode == ChartVisualModes.Clusters;
            var x = ChartInfo.GetXByBar(signal.StartBar);
            var x2 = signal.EndBar > 0 ? ChartInfo.GetXByBar(signal.EndBar) : ChartInfo.Region.Width;
            var priceRowHeight = isClusterMode ? (int)ChartInfo.PriceChartContainer.PriceRowHeight : 0;
            var y = ChartInfo.GetYByPrice(signal.HighPrice, isClusterMode) + priceRowHeight;
            var w = x2 - x;
            var h = ChartInfo.GetYByPrice(signal.LowPrice, isClusterMode) - y;
            var rec = new Rectangle(x, y, w, h);
            context.DrawFillRectangle(penSet.RenderObject, color, rec);

            if (_midpointTouch)
            {
                var isBodyEven = (signal.FirstHighPrice - signal.FirstLowPrice) / InstrumentInfo.TickSize % 2 == 0;
                var midPriceRound = RoundToFraction(signal.MidPrice, InstrumentInfo.TickSize);
                var midPrice = signal.MidPrice < midPriceRound
                   ? midPriceRound - InstrumentInfo.TickSize
                   : midPriceRound;

                var y2 = ChartInfo.GetYByPrice(midPrice, !isBodyEven);
                context.DrawLine(_midpointPen.RenderObject, x, y2, x2, y2);
            }

            if (ShowLabel)
            {
                var text = $"{timeFrame} FVG";
                var labelSize = context.MeasureString(text, _labelFont.RenderObject);
                var lX = x + ChartInfo.PriceChartContainer.BarsWidth * LabelOffsetX;
                var lY = y + ChartInfo.PriceChartContainer.PriceRowHeight * LabelOffsetY;
                var lRec = new Rectangle((int)lX, (int)lY, labelSize.Width, labelSize.Height);
                context.DrawString(text, _labelFont.RenderObject, LabelColor, lRec);
            }
        }
    }

    private Color GetColorTransparency(Color color, int tr = 5) => Color.FromArgb((byte)(tr * 25), color.R, color.G, color.B);

    private void CurrentTfCalculate(int bar, IndicatorCandle currentCandle)
    {
        if (bar < 2) return;

        var candle1 = GetCandle(bar - 2);

        if (candle1.High < currentCandle.Low)
            _upperSignals.AddOrUpdate(bar, currentCandle.Low, candle1.High);
        else
            _upperSignals.Remove(bar);

        if (candle1.Low > currentCandle.High)
            _lowerSignals.AddOrUpdate(bar, candle1.Low, currentCandle.High);
        else
            _lowerSignals.Remove(bar);

        TryCloseGaps(bar, currentCandle, _upperSignals, _lowerSignals);
    }

    private void TryCloseGaps(int bar, IndicatorCandle candle, SignalContainer upperSignals, SignalContainer lowerSignals)
    {
        var signalsToClose = new List<Signal>();

        upperSignals.ProcessActiveSignals(signal =>
        {
            if (candle.Low >= signal.HighPrice)
                return;

            var triggerPrice = _midpointTouch ? signal.MidPrice : signal.LowPrice;

            if (candle.Low <= triggerPrice)
            {
                if (candle.Low > signal.LowPrice)
                    signal.HighPrice = candle.Low;

                signal.EndBar = bar;
                signalsToClose.Add(signal);
            }
            else
                signal.HighPrice = candle.Low;
        }, signalsToClose);

        signalsToClose.Clear();

        lowerSignals.ProcessActiveSignals(signal =>
        {
            if (candle.High <= signal.LowPrice)
                return;

            var triggerPrice = _midpointTouch ? signal.MidPrice : signal.HighPrice;

            if (candle.High >= triggerPrice)
            {
                if (candle.High < signal.HighPrice)
                    signal.LowPrice = candle.High;

                signal.EndBar = bar;
                signalsToClose.Add(signal);
            }
            else
                signal.LowPrice = candle.High;
        }, signalsToClose);
    }

    private void HigherTfCalculate(int bar)
    {
        if (_secondsPerCandle > _higherTfObj.SecondsPerTframe) return;

        _higherTfObj.AddBar(bar);

        if (!_higherTfObj.IsNewPeriod || _higherTfObj.Count < 3) return;

        if (_higherTfObj[2].High < _higherTfObj[0].Low)
            _higherTfObj._upperSignals.AddOrUpdate(bar, _higherTfObj[0].Low, _higherTfObj[2].High);
        else if (_higherTfObj[2].Low > _higherTfObj[0].High)
            _higherTfObj._lowerSignals.AddOrUpdate(bar, _higherTfObj[2].Low, _higherTfObj[0].High);
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
    private decimal RoundToFraction(decimal value, decimal fraction) => Math.Round(value / fraction) * fraction;

    #endregion
}
