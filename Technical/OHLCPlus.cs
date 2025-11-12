namespace ATAS.Indicators.Technical;

using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Linq;

public enum LabelPosition
{
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
    None = 0,

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bar))]
    Bar = 1,

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Right))]
    Right = 2,

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Left))]
    Left = 3
}

public enum LineType
{
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.None))]
    None = 0,

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TillBar))]
    Bar = 1,

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FullWidth))]
    Full = 2
}

// How colors are chosen at render time
public enum ColorMode
{
    // Use the color in each LevelSettings (current behavior)
    PerLineSettings = 0,
    // Override by timeframe (Day/PrevDay/Week/...)
    ByPeriod = 1,
    // Override by level type (Open/High/Low/Close/EQ/POC/VWAP/VAH/VAL)
    ByLevel = 2,
    // NEW: applies semantic styles (color, width, style) from _semanticStyles
    SemanticMatrix = 3
}

public abstract class NotifiableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

[Editor(typeof(ATAS.Indicators.Technical.Editors.LevelSettingsEditor), typeof(ATAS.Indicators.Technical.Editors.LevelSettingsEditor))]
public class LevelSettings : NotifiableObject
{
    #region Fields

    private bool _enabled;
    private CrossColor _color;
    private bool _showPrice;
    private LineType _lineType;
    private int _width;
    private LineDashStyle _lineStyle;
    private LabelPosition _labelPosition;

    #endregion
      
    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Enabled))]
    public bool Enabled 
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color))]
    public CrossColor Color 
    {
        get => _color;
        set => SetField(ref _color, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowPrice))]
    public bool ShowPrice 
    {
        get => _showPrice;
        set => SetField(ref _showPrice, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Line))]
    public LineType LineType 
    {
        get => _lineType;
        set => SetField(ref _lineType, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Width))]
    [Range(1, 10)]
    public int Width 
    { 
        get => _width;
        set => SetField(ref _width, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineStyle))]
    public LineDashStyle LineStyle 
    { 
        get => _lineStyle;
        set => SetField(ref _lineStyle, value);
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Label))]
    public LabelPosition LabelPosition 
    {
        get => _labelPosition;
        set => SetField(ref _labelPosition, value);
    }

    private string _overrideLabel = string.Empty;

    // If set, this replaces the ENTIRE visual label (ignores template/prefix/suffix)
    [Display(Name = "Override label (optional)")]
    public string OverrideLabel
    {
        get => _overrideLabel;
        set => SetField(ref _overrideLabel, value);
    }

    private bool _overrideColorInSchemes;

    [Display(GroupName = "Colors", Name = "Override color in scheme modes")]
    [Description("If enabled, this line will use its own Color even when ColorMode is ByPeriod or ByLevel.")]
    public bool OverrideColorInSchemes
    {
        get => _overrideColorInSchemes;
        set => SetField(ref _overrideColorInSchemes, value);
    }

    private bool _overrideWidthInSchemes;
    
    [Display(GroupName = "Colors", Name = "Override width in scheme modes")]
    public bool OverrideWidthInSchemes
    {
        get => _overrideWidthInSchemes;
        set => SetField(ref _overrideWidthInSchemes, value);
    }

    private bool _overrideStyleInSchemes;
    [Display(GroupName = "Colors", Name = "Override line style in scheme modes")]
    public bool OverrideStyleInSchemes
    {
        get => _overrideStyleInSchemes;
        set => SetField(ref _overrideStyleInSchemes, value);
    }

    [Browsable(false)]
    public RenderPen RenderPen => new PenSettings { Color = Color, Width = Width, LineDashStyle = LineStyle }.RenderObject;

    #endregion

    #region ctor

    public LevelSettings
    (
        bool enabled = false,
        CrossColor color = default,
        int width = 1,
        LineDashStyle lineStyle = LineDashStyle.Solid,
        bool showPrice = true,
        LabelPosition labelPosition = LabelPosition.Bar,
        LineType lineType = LineType.Bar
    )
    {
        Enabled = enabled;
        Color = color == default ? System.Drawing.Color.Blue.Convert() : color;
        Width = width;
        LineStyle = lineStyle;
        ShowPrice = showPrice;
        LabelPosition = labelPosition;
        LineType = lineType;
        OverrideColorInSchemes = false;
    }

    #endregion
}

[DisplayName("OHLC Plus")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.OHLCPlusDescription))]
public class OHLCPlus : Indicator
{
    #region Nested types

    private class LevelData
    {
        public decimal Price { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool IsValid { get; set; }
    }

    private sealed class RefEqComparer : IEqualityComparer<object>
    {
        public static readonly RefEqComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    #endregion

    #region Fields

    private readonly HashSet<LevelSettings> _subscribedLevels = new(RefEqComparer.Instance);
    private readonly Dictionary<LevelSettings, FixedProfilePeriods> _periodByLevel = new(RefEqComparer.Instance);
    private readonly Dictionary<FixedProfilePeriods, string[]> _keys = new()
    {
        [FixedProfilePeriods.CurrentDay] = ["dOpen", "dHigh", "dLow", "dClose", "dEQ", "dPOC", "dVWAP", "dVAH", "dVAL"],
        [FixedProfilePeriods.LastDay] = ["pOpen", "pHigh", "pLow", "pClose", "pEQ", "pPOC", "pVWAP", "pVAH", "pVAL"],
        [FixedProfilePeriods.CurrentWeek] = ["wOpen", "wHigh", "wLow", "wClose", "wEQ", "wPOC", "wVWAP", "wVAH", "wVAL"],
        [FixedProfilePeriods.LastWeek] = ["pwOpen", "pwHigh", "pwLow", "pwClose", "pwEQ", "pwPOC", "pwVWAP", "pwVAH", "pwVAL"],
        [FixedProfilePeriods.CurrentMonth] = ["mOpen", "mHigh", "mLow", "mClose", "mEQ", "mPOC", "mVWAP", "mVAH", "mVAL"],
        [FixedProfilePeriods.LastMonth] = ["pmOpen", "pmHigh", "pmLow", "pmClose", "pmEQ", "pmPOC", "pmVWAP", "pmVAH", "pmVAL"],
        [FixedProfilePeriods.Contract] = ["cOpen", "cHigh", "cLow", "cClose", "cEQ", "cPOC", "cVWAP", "cVAH", "cVAL"],
    };


    private readonly Dictionary<FixedProfilePeriods, IndicatorCandle> _profileCandles = [];
    private readonly ConcurrentDictionary<string, LevelData> _levels = [];
    private readonly RenderFont _font = new("Arial", 10);
    private readonly RenderFont _axisFont = new("Arial", 11);
    private readonly RenderStringFormat _stringRightFormat = new()
    {
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };
    
    private readonly RenderStringFormat _stringLeftFormat = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private int _lastBar = -1;
    private bool _candleRequested;

    private bool _needDay;
    private bool _needPrevDay;
    private bool _needWeek;
    private bool _needPrevWeek;
    private bool _needMonth;
    private bool _needPrevMonth;
    private bool _needContract;

    // Scoped display-prefix override for the current group render
    private string? _scopedStoragePrefix = null;
    private string? _scopedDisplayPrefix = null;

    // HVN prices by period
    private readonly Dictionary<FixedProfilePeriods, List<VolumeNodeBand>> _hvnBands = new();

    // LVN prices by period
    private readonly Dictionary<FixedProfilePeriods, List<VolumeNodeBand>> _lvnBands = new();

    private static readonly FixedProfilePeriods[] _nodePriorityOrder = new[]
    {
    FixedProfilePeriods.CurrentDay,
    FixedProfilePeriods.LastDay,
    FixedProfilePeriods.CurrentWeek,
    FixedProfilePeriods.LastWeek,
    FixedProfilePeriods.CurrentMonth,
    FixedProfilePeriods.LastMonth,
    FixedProfilePeriods.Contract
    };

    // 2) Classifications for mapping (storagePrefix, suffix) -> style lookup
    private enum PeriodKind { CurrentDay, PrevDay, CurrentWeek, PrevWeek, CurrentMonth, PrevMonth, Contract, Other }
    private enum LevelKind { Open, High, Low, Close, EQ, POC, VWAP, VAH, VAL, Other }

    // 3) Visual style record (already present) and the style matrix
    private record VisualStyle(CrossColor Color, int Width, LineDashStyle Style);

    // Helper colors tuned for DARK backgrounds (high contrast)
    private static CrossColor CWhite(int a = 245) => System.Drawing.Color.FromArgb(a, 255, 255, 255).Convert();
    private static CrossColor CGray(int a = 230) => System.Drawing.Color.FromArgb(a, 180, 180, 190).Convert();
    private static CrossColor CGrayDim(int a = 210) => System.Drawing.Color.FromArgb(a, 130, 135, 145).Convert();
    private static CrossColor CViolet(int a = 235) => System.Drawing.Color.FromArgb(a, 168, 85, 247).Convert(); // ≈ #A855F7
    private static CrossColor CCyan(int a = 240) => System.Drawing.Color.FromArgb(a, 56, 189, 255).Convert(); // ≈ #38BDFF
    private static CrossColor CTealCyan() => System.Drawing.Color.FromArgb(210, 34, 211, 238).Convert(); // ≈ #22D3EE
    private static CrossColor VWAPColor() => System.Drawing.ColorTranslator.FromHtml("#FF446EA2").Convert(); // requested

    // NEW: style matrix (LevelKind × PeriodKind) -> VisualStyle
    private readonly Dictionary<(LevelKind L, PeriodKind P), VisualStyle> _styleMatrix =
        new Dictionary<(LevelKind, PeriodKind), VisualStyle>
    {
    // ---- POC: white; current < previous (dot vs solid, width 2 vs 3)
    {(LevelKind.POC, PeriodKind.CurrentDay),   new(CWhite(), 2, LineDashStyle.Dot)},
    {(LevelKind.POC, PeriodKind.PrevDay),     new(CWhite(), 3, LineDashStyle.Solid)},
    {(LevelKind.POC, PeriodKind.CurrentWeek), new(CWhite(), 2, LineDashStyle.Dot)},
    {(LevelKind.POC, PeriodKind.PrevWeek),    new(CWhite(), 3, LineDashStyle.Solid)},
    // Month/Contract lighter reference
    {(LevelKind.POC, PeriodKind.CurrentMonth),new(CWhite(225), 2, LineDashStyle.Dash)},
    {(LevelKind.POC, PeriodKind.PrevMonth),   new(CWhite(230), 2, LineDashStyle.Solid)},
    {(LevelKind.POC, PeriodKind.Contract),    new(CWhite(220), 2, LineDashStyle.Dot)},

    // ---- VWAP: #FF446EA2; current > previous (solid 3 vs dash 2)
    {(LevelKind.VWAP, PeriodKind.CurrentDay),   new(VWAPColor(), 3, LineDashStyle.Solid)},
    {(LevelKind.VWAP, PeriodKind.PrevDay),      new(VWAPColor(), 2, LineDashStyle.Dash)},
    {(LevelKind.VWAP, PeriodKind.CurrentWeek),  new(VWAPColor(), 3, LineDashStyle.Solid)},
    {(LevelKind.VWAP, PeriodKind.PrevWeek),     new(VWAPColor(), 2, LineDashStyle.Dash)},
    {(LevelKind.VWAP, PeriodKind.CurrentMonth), new(VWAPColor(), 2, LineDashStyle.Dot)},
    {(LevelKind.VWAP, PeriodKind.PrevMonth),    new(VWAPColor(), 2, LineDashStyle.Dot)},
    {(LevelKind.VWAP, PeriodKind.Contract),     new(VWAPColor(), 2, LineDashStyle.Dot)},

    // ---- VAH / VAL (bounds): violet; closed (prev*) > open (current*)
    {(LevelKind.VAH, PeriodKind.CurrentDay),   new(CViolet(220), 2, LineDashStyle.Dash)},
    {(LevelKind.VAH, PeriodKind.PrevDay),      new(CViolet(235), 3, LineDashStyle.Solid)},
    {(LevelKind.VAH, PeriodKind.CurrentWeek),  new(CViolet(220), 2, LineDashStyle.Dash)},
    {(LevelKind.VAH, PeriodKind.PrevWeek),     new(CViolet(235), 3, LineDashStyle.Solid)},
    {(LevelKind.VAH, PeriodKind.CurrentMonth), new(CViolet(210), 2, LineDashStyle.Dot)},
    {(LevelKind.VAH, PeriodKind.PrevMonth),    new(CViolet(220), 2, LineDashStyle.Solid)},
    {(LevelKind.VAH, PeriodKind.Contract),     new(CViolet(200), 2, LineDashStyle.Dot)},

    {(LevelKind.VAL, PeriodKind.CurrentDay),   new(CViolet(220), 2, LineDashStyle.Dash)},
    {(LevelKind.VAL, PeriodKind.PrevDay),      new(CViolet(235), 3, LineDashStyle.Solid)},
    {(LevelKind.VAL, PeriodKind.CurrentWeek),  new(CViolet(220), 2, LineDashStyle.Dash)},
    {(LevelKind.VAL, PeriodKind.PrevWeek),     new(CViolet(235), 3, LineDashStyle.Solid)},
    {(LevelKind.VAL, PeriodKind.CurrentMonth), new(CViolet(210), 2, LineDashStyle.Dot)},
    {(LevelKind.VAL, PeriodKind.PrevMonth),    new(CViolet(220), 2, LineDashStyle.Solid)},
    {(LevelKind.VAL, PeriodKind.Contract),     new(CViolet(200), 2, LineDashStyle.Dot)},

    // ---- High / Low: current day dotted gray; previous day stronger
    {(LevelKind.High, PeriodKind.CurrentDay),  new(CGrayDim(), 2, LineDashStyle.Dot)},
    {(LevelKind.Low,  PeriodKind.CurrentDay),  new(CGrayDim(), 2, LineDashStyle.Dot)},
    {(LevelKind.High, PeriodKind.PrevDay),     new(CGray(),    3, LineDashStyle.Solid)},
    {(LevelKind.Low,  PeriodKind.PrevDay),     new(CGray(),    3, LineDashStyle.Solid)},

    // Weeks: mirror the same idea but slightly lighter
    {(LevelKind.High, PeriodKind.CurrentWeek), new(CGrayDim(205), 2, LineDashStyle.Dash)},
    {(LevelKind.Low,  PeriodKind.CurrentWeek), new(CGrayDim(205), 2, LineDashStyle.Dash)},
    {(LevelKind.High, PeriodKind.PrevWeek),    new(CGray(220),    2, LineDashStyle.Solid)},
    {(LevelKind.Low,  PeriodKind.PrevWeek),    new(CGray(220),    2, LineDashStyle.Solid)},

    // EQ / Close / Open: keep neutral/cool, moderate prominence
    {(LevelKind.EQ,   PeriodKind.CurrentDay),   new(CCyan(230), 2, LineDashStyle.Dash)},
    {(LevelKind.EQ,   PeriodKind.PrevDay),      new(CCyan(235), 2, LineDashStyle.Solid)},
    {(LevelKind.Close,PeriodKind.CurrentDay),   new(CGrayDim(210), 2, LineDashStyle.Dot)},
    {(LevelKind.Open, PeriodKind.CurrentDay),   new(CGrayDim(210), 2, LineDashStyle.Dot)},

    // Fallback references for higher TF / contract
    {(LevelKind.High, PeriodKind.CurrentMonth), new(CTealCyan(), 2, LineDashStyle.Dot)},
    {(LevelKind.Low,  PeriodKind.CurrentMonth), new(CTealCyan(), 2, LineDashStyle.Dot)},
    {(LevelKind.High, PeriodKind.Contract),     new(CTealCyan(), 2, LineDashStyle.Dot)},
    {(LevelKind.Low,  PeriodKind.Contract),     new(CTealCyan(), 2, LineDashStyle.Dot)},
    };

    // ===================== LABEL LAYOUT: fields & helpers =====================

    // Occupancy of already placed label rects (per frame)
    private readonly List<Rectangle> _occupiedLabelRects = new();

    // Horizontal probing config (pixels)
    private const int LabelHStep = 10;        // horizontal step per probe
    private const int LabelHMaxShift = 120;  // max horizontal travel allowed

    // Vertical fallback (Y nudges) when X corridor is full
    private const int LabelYNudgeStep = 8;
    private const int LabelYNudgeMax = 24;

    // Request to draw a text label; enqueued during RenderLevel and committed at the end of OnRender
    private sealed class LabelDrawRequest
    {
        public string Text = string.Empty;
        public Rectangle Desired;      // base rect including border padding
        public int MinX;               // left bound for horizontal movement
        public int MaxX;               // right bound for horizontal movement
        public int Dir;                // +1 probe right, -1 probe left
        public bool AlignRight;        // right-aligned text?
        public int Priority;           // higher wins
        public RenderPen Pen = null!;

        // NEW: info to draw/crop the line after placing the label
        public bool DeferLine;            // true => draw line after label
        public LineType LineType;         // which line to draw
        public LabelPosition LabelPos;    // left/right/bar
        public int Y;                     // line Y
        public int CurrentBarRightX;      // for LineType.Bar
        public int ChartWidth;            // for LineType.Full
    }

    // Per-frame queue for label requests
    private readonly List<LabelDrawRequest> _labelQueue = new();

    #endregion

    #region Properties

    #region Day Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings DayOpenLevel { get; set; } = new(
        enabled: true,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings DayHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings DayLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings DayCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings DayEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings DayPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.CurrentDay), Order = 65)]
    public LevelSettings DayVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings DayVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings DayVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _dayHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = "Enable HVN", Order = 90)]
    public bool DayHVNEnabled
    {
        get => _dayHVNEnabled;
        set
        {
            if (_dayHVNEnabled == value) return;
            _dayHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _dayLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = "Enable LVN", Order = 92)]
    public bool DayLVNEnabled
    {
        get => _dayLVNEnabled;
        set
        {
            if (_dayLVNEnabled == value) return;
            _dayLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = "VN Color", Order = 95)]
    public CrossColor DayHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 30, 144, 255).Convert(); // semi-transparent blue/violet



    #endregion

    #region Prev.Day Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings PrevDayOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings PrevDayHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings PrevDayLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings PrevDayCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings PrevDayEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings PrevDayPOCLevel { get; set; } = new(
        enabled: true,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.PreviousDay), Order = 65)]
    public LevelSettings PrevDayVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings PrevDayVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings PrevDayVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _prevdayHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = "Enable HVN", Order = 90)]
    public bool PrevDayHVNEnabled
    {
        get => _prevdayHVNEnabled;
        set
        {
            if (_prevdayHVNEnabled == value) return;
            _prevdayHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _prevdayLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = "Enable LVN", Order = 92)]
    public bool PrevDayLVNEnabled
    {
        get => _prevdayLVNEnabled;
        set
        {
            if (_prevdayLVNEnabled == value) return;
            _prevdayLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = "VN Color", Order = 95)]
    public CrossColor PrevDayHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 255, 140, 0).Convert(); // semi-transparent amber.

    #endregion

    #region Week Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings WeekOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings WeekHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings WeekLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings WeekCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings WeekEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings WeekPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.CurrentWeek), Order = 65)]
    public LevelSettings WeekVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings WeekVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings WeekVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _WeekHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = "Enable HVN", Order = 90)]
    public bool WeekHVNEnabled
    {
        get => _WeekHVNEnabled;
        set
        {
            if (_WeekHVNEnabled == value) return;
            _WeekHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _WeekLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = "Enable LVN", Order = 92)]
    public bool WeekLVNEnabled
    {
        get => _WeekLVNEnabled;
        set
        {
            if (_WeekLVNEnabled == value) return;
            _WeekLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = "VN Color", Order = 95)]
    public CrossColor WeekHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 70, 130, 180).Convert(); // steel-ish

    #endregion

    #region Prev.Week Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings PrevWeekOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings PrevWeekHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings PrevWeekLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings PrevWeekCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings PrevWeekEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings PrevWeekPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.PreviousWeek), Order = 65)]
    public LevelSettings PrevWeekVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings PrevWeekVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings PrevWeekVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _prevWeekHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = "Enable HVN", Order = 90)]
    public bool PrevWeekHVNEnabled
    {
        get => _prevWeekHVNEnabled;
        set
        {
            if (_prevWeekHVNEnabled == value) return;
            _prevWeekHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _prevWeekLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = "Enable LVN", Order = 92)]
    public bool PrevWeekLVNEnabled
    {
        get => _prevWeekLVNEnabled;
        set
        {
            if (_prevWeekLVNEnabled == value) return;
            _prevWeekLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = "VN Color", Order = 95)]
    public CrossColor PrevWeekHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 186, 85, 211).Convert(); // mediumPurple


    #endregion

    #region Month Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings MonthOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings MonthHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings MonthLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings MonthCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings MonthEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings MonthPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.CurrentMonth), Order = 65)]
    public LevelSettings MonthVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings MonthVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings MonthVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _monthHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = "Enable HVN", Order = 90)]
    public bool MonthHVNEnabled
    {
        get => _monthHVNEnabled;
        set
        {
            if (_monthHVNEnabled == value) return;
            _monthHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _monthLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = "Enable LVN", Order = 92)]
    public bool MonthLVNEnabled
    {
        get => _monthLVNEnabled;
        set
        {
            if (_monthLVNEnabled == value) return;
            _monthLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = "VN Color", Order = 95)]
    public CrossColor MonthHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 0, 128, 128).Convert(); // teal

    #endregion

    #region Prev.Month Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings PrevMonthOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings PrevMonthHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings PrevMonthLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings PrevMonthCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings PrevMonthEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings PrevMonthPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.PreviousMonth), Order = 65)]
    public LevelSettings PrevMonthVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings PrevMonthVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings PrevMonthVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _prevMonthHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = "Enable HVN", Order = 90)]
    public bool PrevMonthHVNEnabled
    {
        get => _prevMonthHVNEnabled;
        set
        {
            if (_prevMonthHVNEnabled == value) return;
            _prevMonthHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _prevMonthLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = "Enable LVN", Order = 92)]
    public bool PrevMonthLVNEnabled
    {
        get => _prevMonthLVNEnabled;
        set
        {
            if (_prevMonthLVNEnabled == value) return;
            _prevMonthLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = "VN Color", Order = 95)]
    public CrossColor PrevMonthHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 47, 79, 79).Convert(); // darkslategray

    #endregion

    #region Contract Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarOpen), Order = 10)]
    public LevelSettings ContractOpenLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarHigh), Order = 20)]
    public LevelSettings ContractHighLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Green.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarLow), Order = 30)]
    public LevelSettings ContractLowLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Red.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.BarClose), Order = 40)]
    public LevelSettings ContractCloseLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Gray.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.Equilibrium), Order = 50)]
    public LevelSettings ContractEquilibriumLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Yellow.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dash,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.POC), Order = 60)]
    public LevelSettings ContractPOCLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Orange.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VWAP), GroupName = nameof(Strings.Contract), Order = 65)]
    public LevelSettings ContractVWAPLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.SteelBlue.Convert(),
        width: 2,
        lineStyle: LineDashStyle.Solid,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.VAH), Order = 70)]
    public LevelSettings ContractVAHLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = nameof(Strings.VAL), Order = 80)]
    public LevelSettings ContractVALLevel { get; set; } = new(
        enabled: false,
        color: System.Drawing.Color.Purple.Convert(),
        width: 1,
        lineStyle: LineDashStyle.Dot,
        showPrice: true,
        labelPosition: LabelPosition.Bar,
        lineType: LineType.Bar
    );

    private bool _contractHVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = "Enable HVN", Order = 90)]
    public bool ContractHVNEnabled
    {
        get => _contractHVNEnabled;
        set
        {
            if (_contractHVNEnabled == value) return;
            _contractHVNEnabled = value;
            RefreshData();
        }
    }

    private bool _contractLVNEnabled;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = "Enable LVN", Order = 92)]
    public bool ContractLVNEnabled
    {
        get => _contractLVNEnabled;
        set
        {
            if (_contractLVNEnabled == value) return;
            _contractLVNEnabled = value;
            RefreshData();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = "VN Color", Order = 95)]
    public CrossColor ContractHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 30, 144, 255).Convert(); // dodgerblue

    #endregion

    #region HVN Settings
    private int _hvnThresholdPct = 80;
    [Display(GroupName = "HVN Settings", Name = "Threshold (% of POC volume)", Order = 10)]
    [Range(1, 99)]
    public int HVNThresholdPct
    {
        get => _hvnThresholdPct;
        set
        {
            if (_hvnThresholdPct == value) return;
            _hvnThresholdPct = value;
            RefreshData();
        }
    }

    private int _hvnGapToleranceTicks = 1;
    [Display(GroupName = "HVN Settings", Name = "Gap tolerance (ticks)", Order = 20)]
    [Range(0, 10)]
    public int HVNGapToleranceTicks
    {
        get => _hvnGapToleranceTicks;
        set
        {
            if (_hvnGapToleranceTicks == value) return;
            _hvnGapToleranceTicks = value;
            RefreshData();
        }
    }

    private int _hvnOcclusionTicks = 1;
    // Overlap tolerance in ticks: if a price falls within N ticks of another already drawn, it is hidden.
    [Display(GroupName = "HVN Settings", Name = "Occlusion tolerance (ticks)", Order = 30)]
    [Range(0, 10)]
    public int HVNOcclusionTicks
    {
        get => _hvnOcclusionTicks;
        set
        {
            if (_hvnOcclusionTicks == value) return;
            _hvnOcclusionTicks = value;
            RefreshData();
        }
    }

    #endregion

    #region LVN Settings

    private int _lvnThresholdPct = 20;
    [Display(GroupName = "LVN Settings", Name = "Threshold (% of POC volume)", Order = 10)]
    [Range(1, 50)]
    [Description("Bands of volume BELOW this percentage of POC volume will be marked as LVNs (voids).")]
    public int LVNThresholdPct
    {
        get => _lvnThresholdPct;
        set
        {
            if (_lvnThresholdPct == value) return;
            _lvnThresholdPct = value;
            RefreshData();
        }
    }

    private int _lvnGapToleranceTicks = 2;
    [Display(GroupName = "LVN Settings", Name = "Gap tolerance (ticks)", Order = 20)]
    [Range(0, 10)]
    [Description("How many non-LVN ticks are allowed inside a void before splitting it.")]
    public int LVNGapToleranceTicks
    {
        get => _lvnGapToleranceTicks;
        set
        {
            if (_lvnGapToleranceTicks == value) return;
            _lvnGapToleranceTicks = value;
            RefreshData();
        }
    }

    private LineDashStyle _lvnBorderStyle = LineDashStyle.Dot;
    [Display(GroupName = "LVN Settings", Name = "LVN Border Style", Order = 30)]
    [Description("Line style for the LVN void border.")]
    public LineDashStyle LVNBorderStyle
    {
        get => _lvnBorderStyle;
        set
        {
            if (_lvnBorderStyle == value) return;
            _lvnBorderStyle = value;
            RefreshData();
        }
    }

    private int _lvnBorderWidth = 1;
    [Display(GroupName = "LVN Settings", Name = "LVN Border Width", Order = 40)]
    [Range(1, 5)]
    [Description("Line width for the LVN void border.")]
    public int LVNBorderWidth
    {
        get => _lvnBorderWidth;
        set
        {
            if (_lvnBorderWidth == value) return;
            _lvnBorderWidth = value;
            RefreshData();
        }
    }

    private int _minPocVolForLVN = 500;
    [Display(GroupName = "LVN Settings", Name = "Min POC Vol for LVN", Order = 50)]
    [Range(1, 10000)]
    [Description("Do not calculate LVNs for a period until its POC Volume is above this threshold. Prevents noise at the start of a new period.")]
    public int MinPocVolForLVN
    {
        get => _minPocVolForLVN;
        set
        {
            if (_minPocVolForLVN == value) return;
            _minPocVolForLVN = value;
            RefreshData();
        }
    }

    private int _lvnTailFilter_MinTicks = 3;
    [Display(GroupName = "LVN Settings", Name = "Tail Filter - Min Ticks", Order = 60)]
    [Range(0, 20)]
    [Description("MINIMUM number of ticks to filter from H/L. Always active.")]
    public int LVNTailFilter_MinTicks
    {
        get => _lvnTailFilter_MinTicks;
        set
        {
            if (_lvnTailFilter_MinTicks == value) return;
            _lvnTailFilter_MinTicks = value;
            RefreshData();
        }
    }

    private int _lvnTailFilter_Pct = 10;
    [Display(GroupName = "LVN Settings", Name = "Tail Filter - Pct of Range", Order = 61)]
    [Range(0, 50)]
    [Description("Adaptive filter. Uses this % of the H/L range if it's larger than 'Min Ticks'. (0 = disabled).")]
    public int LVNTailFilter_Pct
    {
        get => _lvnTailFilter_Pct;
        set
        {
            if (_lvnTailFilter_Pct == value) return;
            _lvnTailFilter_Pct = value;
            RefreshData();
        }
    }

    #endregion

    #region Prefix Settings
    // Display-only prefixes (do not affect storage keys)
    [Display(GroupName = "Prefixes", Name = "Current Day", Order = 10)]
    public string PrefixCurrentDay { get; set; } = "d";

    [Display(GroupName = "Prefixes", Name = "Previous Day", Order = 20)]
    public string PrefixPrevDay { get; set; } = "p";

    [Display(GroupName = "Prefixes", Name = "Current Week", Order = 30)]
    public string PrefixCurrentWeek { get; set; } = "w";

    [Display(GroupName = "Prefixes", Name = "Previous Week", Order = 40)]
    public string PrefixPrevWeek { get; set; } = "pw";

    [Display(GroupName = "Prefixes", Name = "Current Month", Order = 50)]
    public string PrefixCurrentMonth { get; set; } = "m";

    [Display(GroupName = "Prefixes", Name = "Previous Month", Order = 60)]
    public string PrefixPrevMonth { get; set; } = "pm";

    [Display(GroupName = "Prefixes", Name = "Contract", Order = 70)]
    public string PrefixContract { get; set; } = "c";
    #endregion

    #region Labels Settings

    // Template supports {prefix} and {level}
    [Display(GroupName = "Labels", Name = "Label template", Order = 10)]
    public string LabelTemplate { get; set; } = "{prefix}{level}";

    [Display(GroupName = "Labels", Name = "Open", Order = 20)]
    public string OpenLabel { get; set; } = "Open";
    [Display(GroupName = "Labels", Name = "High", Order = 30)]
    public string HighLabel { get; set; } = "High";
    [Display(GroupName = "Labels", Name = "Low", Order = 40)]
    public string LowLabel { get; set; } = "Low";
    [Display(GroupName = "Labels", Name = "Close", Order = 50)]
    public string CloseLabel { get; set; } = "Close";
    [Display(GroupName = "Labels", Name = "Equilibrium", Order = 60)]
    public string EqLabel { get; set; } = "EQ";
    [Display(GroupName = "Labels", Name = "POC", Order = 70)]
    public string POCLabel { get; set; } = "POC";
    [Display(GroupName = "Labels", Name = "VWAP", Order = 80)]
    public string VWAPLabel { get; set; } = "VWAP";
    [Display(GroupName = "Labels", Name = "VAH", Order = 90)]
    public string VAHLabel { get; set; } = "VAH";
    [Display(GroupName = "Labels", Name = "VAL", Order = 100)]
    public string VALLabel { get; set; } = "VAL";

    #endregion

    #region Color scheme
    // Strategy selector
    [Display(GroupName = "Colors", Name = "Mode", Order = 5)]
    public ColorMode ColorMode { get; set; } = ColorMode.PerLineSettings;

    // --- Palette by PERIOD (used when ColorMode == ByPeriod)
    [Display(GroupName = "Colors - By Period", Name = "Current Day", Order = 10)]
    public CrossColor PeriodColorCurrentDay { get; set; } = System.Drawing.Color.OrangeRed.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Previous Day", Order = 11)]
    public CrossColor PeriodColorPrevDay { get; set; } = System.Drawing.Color.SaddleBrown.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Current Week", Order = 12)]
    public CrossColor PeriodColorCurrentWeek { get; set; } = System.Drawing.Color.DeepSkyBlue.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Previous Week", Order = 13)]
    public CrossColor PeriodColorPrevWeek { get; set; } = System.Drawing.Color.DarkSlateBlue.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Current Month", Order = 14)]
    public CrossColor PeriodColorCurrentMonth { get; set; } = System.Drawing.Color.MediumSeaGreen.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Previous Month", Order = 15)]
    public CrossColor PeriodColorPrevMonth { get; set; } = System.Drawing.Color.DarkOliveGreen.Convert();

    [Display(GroupName = "Colors - By Period", Name = "Contract", Order = 16)]
    public CrossColor PeriodColorContract { get; set; } = System.Drawing.Color.Indigo.Convert();

    // --- Palette by LEVEL TYPE (used when ColorMode == ByLevel)
    [Display(GroupName = "Colors - By Level", Name = "Open", Order = 110)]
    public CrossColor LevelColorOpen { get; set; } = System.Drawing.Color.DarkGoldenrod.Convert();

    [Display(GroupName = "Colors - By Level", Name = "High", Order = 120)]
    public CrossColor LevelColorHigh { get; set; } = System.Drawing.Color.ForestGreen.Convert();

    [Display(GroupName = "Colors - By Level", Name = "Low", Order = 130)]
    public CrossColor LevelColorLow { get; set; } = System.Drawing.Color.Firebrick.Convert();

    [Display(GroupName = "Colors - By Level", Name = "Close", Order = 140)]
    public CrossColor LevelColorClose { get; set; } = System.Drawing.Color.DimGray.Convert();

    [Display(GroupName = "Colors - By Level", Name = "Equilibrium (EQ)", Order = 150)]
    public CrossColor LevelColorEQ { get; set; } = System.Drawing.Color.DarkKhaki.Convert();

    [Display(GroupName = "Colors - By Level", Name = "POC", Order = 160)]
    public CrossColor LevelColorPOC { get; set; } = System.Drawing.Color.OrangeRed.Convert();

    [Display(GroupName = "Colors - By Level", Name = "VWAP", Order = 170)]
    public CrossColor LevelColorVWAP { get; set; } = System.Drawing.Color.DodgerBlue.Convert();

    [Display(GroupName = "Colors - By Level", Name = "VAH", Order = 180)]
    public CrossColor LevelColorVAH { get; set; } = System.Drawing.Color.CornflowerBlue.Convert();

    [Display(GroupName = "Colors - By Level", Name = "VAL", Order = 190)]
    public CrossColor LevelColorVAL { get; set; } = System.Drawing.Color.Purple.Convert();
    #endregion

    #endregion

    #region Constructor

    public OHLCPlus()
        : base(true)
    {
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
        DenyToChangePanel = true;
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        DrawAbovePrice = true;
    }

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null)
            return;

        base.OnApplyDefaultColors();
    }

    protected override void OnInitialize()
    {
        RecalcAllNeeds();
        SubscribeAllLevels();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == 0)
        {
            _profileCandles.Clear();
            _levels.Clear();
        }

        if (bar == 0 || IsNewSession(bar) && _lastBar != bar)
            _candleRequested = false;

        if (bar != CurrentBar - 1)
            return;

        if (!_candleRequested)
        {
            _candleRequested = true;
            RequestProfiles();
            _lastBar = bar;
        }

        UpdateAllNeededLevelsFromCache();
    }

    protected override void OnFixedProfilesResponse(IndicatorCandle fixedProfileScaled, IndicatorCandle fixedProfileOriginScale, FixedProfilePeriods period)
    {
        _profileCandles[period] = fixedProfileOriginScale;
        UpdateLevels(period, fixedProfileOriginScale);
        UpdateVolumeNodes(period, fixedProfileOriginScale);
        RedrawChart();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null || InstrumentInfo is null)
            return;

        // Start a fresh label batch for this frame
        _occupiedLabelRects.Clear();
        _labelQueue.Clear();

        // 1) Render lines & price labels; RenderLevel will ENQUEUE text labels instead of drawing them
        RenderLevelGroup(context, storagePrefix: "d", displayPrefix: PrefixCurrentDay, DayOpenLevel, DayHighLevel, DayLowLevel, DayCloseLevel, DayEquilibriumLevel, DayPOCLevel, DayVWAPLevel, DayVAHLevel, DayVALLevel);
        RenderLevelGroup(context, storagePrefix: "p", displayPrefix: PrefixPrevDay, PrevDayOpenLevel, PrevDayHighLevel, PrevDayLowLevel, PrevDayCloseLevel, PrevDayEquilibriumLevel, PrevDayPOCLevel, PrevDayVWAPLevel, PrevDayVAHLevel, PrevDayVALLevel);
        RenderLevelGroup(context, storagePrefix: "w", displayPrefix: PrefixCurrentWeek, WeekOpenLevel, WeekHighLevel, WeekLowLevel, WeekCloseLevel, WeekEquilibriumLevel, WeekPOCLevel, WeekVWAPLevel, WeekVAHLevel, WeekVALLevel);
        RenderLevelGroup(context, storagePrefix: "pw", displayPrefix: PrefixPrevWeek, PrevWeekOpenLevel, PrevWeekHighLevel, PrevWeekLowLevel, PrevWeekCloseLevel, PrevWeekEquilibriumLevel, PrevWeekPOCLevel, PrevWeekVWAPLevel, PrevWeekVAHLevel, PrevWeekVALLevel);
        RenderLevelGroup(context, storagePrefix: "m", displayPrefix: PrefixCurrentMonth, MonthOpenLevel, MonthHighLevel, MonthLowLevel, MonthCloseLevel, MonthEquilibriumLevel, MonthPOCLevel, MonthVWAPLevel, MonthVAHLevel, MonthVALLevel);
        RenderLevelGroup(context, storagePrefix: "pm", displayPrefix: PrefixPrevMonth, PrevMonthOpenLevel, PrevMonthHighLevel, PrevMonthLowLevel, PrevMonthCloseLevel, PrevMonthEquilibriumLevel, PrevMonthPOCLevel, PrevMonthVWAPLevel, PrevMonthVAHLevel, PrevMonthVALLevel);
        RenderLevelGroup(context, storagePrefix: "c", displayPrefix: PrefixContract, ContractOpenLevel, ContractHighLevel, ContractLowLevel, ContractCloseLevel, ContractEquilibriumLevel, ContractPOCLevel, ContractVWAPLevel, ContractVAHLevel, ContractVALLevel);

        // 2) Commit text labels: place by priority; try X, then Y fallbacks; draw line cropped
        if (_labelQueue.Count > 0)
        {
            foreach (var req in _labelQueue.OrderByDescending(r => r.Priority))
            {
                Rectangle placed;

                // Try base X corridor first
                bool ok = TryPlaceLabelHorizontal(req.Desired, req.MinX, req.MaxX, req.Dir, out placed);

                // If failed, try small Y nudges (up/down) and retry X per nudge
                if (!ok)
                {
                    for (int dy = LabelYNudgeStep; dy <= LabelYNudgeMax && !ok; dy += LabelYNudgeStep)
                    {
                        // Up
                        var up = new Rectangle(req.Desired.X, req.Desired.Y - dy, req.Desired.Width, req.Desired.Height);
                        ok = TryPlaceLabelHorizontal(up, req.MinX, req.MaxX, req.Dir, out placed);
                        if (ok) break;

                        // Down
                        var dn = new Rectangle(req.Desired.X, req.Desired.Y + dy, req.Desired.Width, req.Desired.Height);
                        ok = TryPlaceLabelHorizontal(dn, req.MinX, req.MaxX, req.Dir, out placed);
                    }
                }

                if (ok)
                {
                    // Draw the text (reconstruimos el anchor x/y desde el rect)
                    DrawTextLabelCore(context, req.Text, placed, req.Pen, req.AlignRight);

                    // Draw/crop the line now that we know the label position
                    if (req.DeferLine && req.LineType != LineType.None)
                    {
                        int left = 0;
                        int right = req.ChartWidth;

                        if (req.LineType == LineType.Bar)
                        {
                            left = req.CurrentBarRightX;
                            if (req.LabelPos == LabelPosition.Bar)
                                left = Math.Max(left, LineStartAfter(placed)); // start after label
                        }
                        else if (req.LineType == LineType.Full)
                        {
                            if (req.LabelPos == LabelPosition.Left)
                                left = Math.Max(left, LineStartAfter(placed));
                            else if (req.LabelPos == LabelPosition.Right)
                                right = Math.Min(right, LineEndBefore(placed));
                        }

                        if (right > left)
                            context.DrawLine(req.Pen, left, req.Y, right, req.Y);
                    }
                }
                else
                {
                    // No slot -> optionally draw the original (uncropped) line so no level disappears
                    if (req.DeferLine && req.LineType != LineType.None)
                    {
                        switch (req.LineType)
                        {
                            case LineType.Bar:
                                context.DrawLine(req.Pen, req.CurrentBarRightX, req.Y, req.ChartWidth, req.Y);
                                break;
                            case LineType.Full:
                                context.DrawLine(req.Pen, 0, req.Y, req.ChartWidth, req.Y);
                                break;
                        }
                    }
                    // Text label is dropped (lowest priority loses)
                }
            }
        }


        // 3) HVN rects (kept after levels as in your current codebase)
        RenderAllVolumeNodes(context);
    }


    #endregion

    #region Private methods

    #region OnCalculate

    private void UpdateAllNeededLevelsFromCache()
    {
        void UpdateIf(FixedProfilePeriods p)
        {
            if (IsNeeded(p) && _profileCandles.TryGetValue(p, out var candle) && candle is not null)
            {
                UpdateLevels(p, candle);
                UpdateVolumeNodes(p, candle);
            }
        }

        UpdateIf(FixedProfilePeriods.CurrentDay);
        UpdateIf(FixedProfilePeriods.LastDay);
        UpdateIf(FixedProfilePeriods.CurrentWeek);
        UpdateIf(FixedProfilePeriods.LastWeek);
        UpdateIf(FixedProfilePeriods.CurrentMonth);
        UpdateIf(FixedProfilePeriods.LastMonth);
        UpdateIf(FixedProfilePeriods.Contract);

        RedrawChart();
    }

    private void RequestProfiles()
    {
        if (_needDay) RequestProfileForPeriod(FixedProfilePeriods.CurrentDay);
        if (_needPrevDay) RequestProfileForPeriod(FixedProfilePeriods.LastDay);
        if (_needWeek) RequestProfileForPeriod(FixedProfilePeriods.CurrentWeek);
        if (_needPrevWeek) RequestProfileForPeriod(FixedProfilePeriods.LastWeek);
        if (_needMonth) RequestProfileForPeriod(FixedProfilePeriods.CurrentMonth);
        if (_needPrevMonth) RequestProfileForPeriod(FixedProfilePeriods.LastMonth);
        if (_needContract) RequestProfileForPeriod(FixedProfilePeriods.Contract);
    }

    private void RequestProfileForPeriod(FixedProfilePeriods period)
    {
        if (_profileCandles.TryGetValue(period, out var candle) && candle is not null)
        {
            return;
        }

        GetFixedProfile(new FixedProfileRequest(period));
    }

    private void RecalcAllNeeds()
    {
        _needDay = NeedsDayData();
        _needPrevDay = NeedsPrevDayData();
        _needWeek = NeedsWeekData();
        _needPrevWeek = NeedsPrevWeekData();
        _needMonth = NeedsMonthData();
        _needPrevMonth = NeedsPrevMonthData();
        _needContract = NeedsContractData();
    }

    private void RecalcNeedFor(FixedProfilePeriods period)
    {
        switch (period)
        {
            case FixedProfilePeriods.CurrentDay:
                _needDay = NeedsDayData();
                break;
            case FixedProfilePeriods.LastDay:
                _needPrevDay = NeedsPrevDayData();
                break;
            case FixedProfilePeriods.CurrentWeek:
                _needWeek = NeedsWeekData();
                break;
            case FixedProfilePeriods.LastWeek:
                _needPrevWeek = NeedsPrevWeekData();
                break;
            case FixedProfilePeriods.CurrentMonth:
                _needMonth = NeedsMonthData();
                break;
            case FixedProfilePeriods.LastMonth:
                _needPrevMonth = NeedsPrevMonthData();
                break;
            case FixedProfilePeriods.Contract:
                _needContract = NeedsContractData();
                break;
        }
    }

    private bool NeedsDayData()
    {
        return DayOpenLevel.Enabled || DayHighLevel.Enabled || DayLowLevel.Enabled || DayCloseLevel.Enabled ||
               DayEquilibriumLevel.Enabled || DayPOCLevel.Enabled || DayVWAPLevel.Enabled || DayVAHLevel.Enabled || DayVALLevel.Enabled || DayHVNEnabled || DayLVNEnabled;
    }

    private bool NeedsPrevDayData()
    {
        return PrevDayOpenLevel.Enabled || PrevDayHighLevel.Enabled || PrevDayLowLevel.Enabled || PrevDayCloseLevel.Enabled ||
               PrevDayEquilibriumLevel.Enabled || PrevDayPOCLevel.Enabled || PrevDayVWAPLevel.Enabled || PrevDayVAHLevel.Enabled || PrevDayVALLevel.Enabled || PrevDayHVNEnabled || PrevDayLVNEnabled;
    }

    private bool NeedsWeekData()
    {
        return WeekOpenLevel.Enabled || WeekHighLevel.Enabled || WeekLowLevel.Enabled || WeekCloseLevel.Enabled ||
               WeekEquilibriumLevel.Enabled || WeekPOCLevel.Enabled || WeekVWAPLevel.Enabled || WeekVAHLevel.Enabled || WeekVALLevel.Enabled || WeekHVNEnabled || WeekLVNEnabled;
    }

    private bool NeedsPrevWeekData()
    {
        return PrevWeekOpenLevel.Enabled || PrevWeekHighLevel.Enabled || PrevWeekLowLevel.Enabled || PrevWeekCloseLevel.Enabled ||
               PrevWeekEquilibriumLevel.Enabled || PrevWeekPOCLevel.Enabled || PrevWeekVWAPLevel.Enabled || PrevWeekVAHLevel.Enabled || PrevWeekVALLevel.Enabled || PrevWeekHVNEnabled || PrevWeekLVNEnabled;
    }

    private bool NeedsMonthData()
    {
        return MonthOpenLevel.Enabled || MonthHighLevel.Enabled || MonthLowLevel.Enabled || MonthCloseLevel.Enabled ||
               MonthEquilibriumLevel.Enabled || MonthPOCLevel.Enabled || MonthVWAPLevel.Enabled || MonthVAHLevel.Enabled || MonthVALLevel.Enabled || MonthHVNEnabled || MonthLVNEnabled;
    }

    private bool NeedsPrevMonthData()
    {
        return PrevMonthOpenLevel.Enabled || PrevMonthHighLevel.Enabled || PrevMonthLowLevel.Enabled || PrevMonthCloseLevel.Enabled ||
               PrevMonthEquilibriumLevel.Enabled || PrevMonthPOCLevel.Enabled || PrevMonthVWAPLevel.Enabled || PrevMonthVAHLevel.Enabled || PrevMonthVALLevel.Enabled || PrevMonthHVNEnabled || PrevMonthLVNEnabled;
    }

    private bool NeedsContractData()
    {
        return ContractOpenLevel.Enabled || ContractHighLevel.Enabled || ContractLowLevel.Enabled || ContractCloseLevel.Enabled ||
               ContractEquilibriumLevel.Enabled || ContractPOCLevel.Enabled || ContractVWAPLevel.Enabled || ContractVAHLevel.Enabled || ContractVALLevel.Enabled || ContractHVNEnabled || ContractLVNEnabled;
    }

    private void UpdateLevels(FixedProfilePeriods period, IndicatorCandle candle)
    {
        if (candle == null) 
            return;

        var keys = _keys[period];

        // OHLC + EQ
        UpdateLevel(keys[0], candle.Open);                          // Open
        UpdateLevel(keys[1], candle.High);                          // High
        UpdateLevel(keys[2], candle.Low);                           // Low
        UpdateLevel(keys[3], candle.Close);                         // Close
        UpdateLevel(keys[4], (candle.High + candle.Low) / 2);       // EQ

        // POC
        if (candle.MaxVolumePriceInfo != null && candle.MaxVolumePriceInfo.Price > 0)
            UpdateLevel(keys[5], candle.MaxVolumePriceInfo.Price);

        // VWAP
        if (candle.VWAP > 0)
            UpdateLevel(keys[6], candle.VWAP);

        // VAH/VAL
        if (candle.ValueArea != null &&
            candle.ValueArea.ValueAreaHigh > 0 &&
            candle.ValueArea.ValueAreaLow > 0 &&
            candle.ValueArea.ValueAreaHigh >= candle.ValueArea.ValueAreaLow)
        {
            UpdateLevel(keys[7], candle.ValueArea.ValueAreaHigh);
            UpdateLevel(keys[8], candle.ValueArea.ValueAreaLow);
        }
    }

    private void UpdateLevel(string key, decimal price)
    {
        if (!_levels.TryGetValue(key, out var ld))
        {
            ld = new LevelData { Label = key };
            _levels[key] = ld;
        }

        ld.Price = price;
        ld.IsValid = true;
    }

    #endregion

    #region OnRender

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FixedProfilePeriods PeriodFromPrefix(string prefix) => prefix switch
    {
        "d" => FixedProfilePeriods.CurrentDay,
        "p" => FixedProfilePeriods.LastDay,
        "w" => FixedProfilePeriods.CurrentWeek,
        "pw" => FixedProfilePeriods.LastWeek,
        "m" => FixedProfilePeriods.CurrentMonth,
        "pm" => FixedProfilePeriods.LastMonth,
        "c" => FixedProfilePeriods.Contract,
        _ => FixedProfilePeriods.CurrentDay
    };

    // Maps the canonical storage prefix ("d", "p", "w", "pw", "m", "pm", "c")
    // to the user-configurable display prefix for visual labels.
    // NEW: respects a scoped override set by RenderLevelGroup.
    private string DisplayPrefixFor(string storagePrefix)
    {
        // Scoped override: only applies while a group with this storagePrefix is rendering
        if (!string.IsNullOrEmpty(_scopedStoragePrefix) &&
            string.Equals(storagePrefix, _scopedStoragePrefix, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(_scopedDisplayPrefix))
            return _scopedDisplayPrefix!;

        return storagePrefix switch
        {
            "d" => PrefixCurrentDay,
            "p" => PrefixPrevDay,
            "w" => PrefixCurrentWeek,
            "pw" => PrefixPrevWeek,
            "m" => PrefixCurrentMonth,
            "pm" => PrefixPrevMonth,
            "c" => PrefixContract,
            _ => storagePrefix ?? string.Empty
        };
    }


    // Resolve the color respecting the precedence (per-line override > scheme > per-line default)
    private CrossColor ResolveColor(string storagePrefix, string suffix, LevelSettings ls)
    {
        // 1) Per-line hard override
        if (ls?.OverrideColorInSchemes == true)
            return ls.Color;

        // 2) Scheme color (when active)
        switch (ColorMode)
        {
            case ColorMode.ByPeriod:
                return ResolvePeriodPalette(storagePrefix);
            case ColorMode.ByLevel:
                return ResolveLevelPalette(suffix);
            case ColorMode.PerLineSettings:
            default:
                return ls?.Color ?? CrossColors.White; // line's own color (default behavior)
        }
    }

    // Build a pen using the resolved color (do not rely on ls.RenderPen anymore when schemes are active)
    private RenderPen BuildPen(LevelSettings ls, CrossColor color)
        => new PenSettings { Color = color, Width = ls.Width, LineDashStyle = ls.LineStyle }.RenderObject;

    // Palette resolvers
    private CrossColor ResolvePeriodPalette(string storagePrefix) => storagePrefix switch
    {
        "d" => PeriodColorCurrentDay,
        "p" => PeriodColorPrevDay,
        "w" => PeriodColorCurrentWeek,
        "pw" => PeriodColorPrevWeek,
        "m" => PeriodColorCurrentMonth,
        "pm" => PeriodColorPrevMonth,
        "c" => PeriodColorContract,
        _ => CrossColors.White
    };

    private CrossColor ResolveLevelPalette(string suffix) => suffix switch
    {
        "Open" => LevelColorOpen,
        "High" => LevelColorHigh,
        "Low" => LevelColorLow,
        "Close" => LevelColorClose,
        "EQ" => LevelColorEQ,
        "POC" => LevelColorPOC,
        "VWAP" => LevelColorVWAP,
        "VAH" => LevelColorVAH,
        "VAL" => LevelColorVAL,
        _ => CrossColors.White
    };

    private void RenderLevel(RenderContext context, string levelKey, LevelSettings levelSettings)
    {
        if (!levelSettings.Enabled || !_levels.TryGetValue(levelKey, out var level) || !level.IsValid)
            return;

        // --- Calculate actual label ---

        var (prefix, suffix) = SplitKey(levelKey);
        // If an override label is provided for this LevelSettings, use it as-is
        string displayLabel;
        if (!string.IsNullOrWhiteSpace(levelSettings.OverrideLabel))
        {
            displayLabel = levelSettings.OverrideLabel;
        }
        else
        {
            var levelText = ResolveLevelText(suffix);
            displayLabel = BuildDisplayLabel(DisplayPrefixFor(prefix), levelText);
        }
        // ----------------------------------------------------

        // === NEW: style selection ===
        CrossColor color;
        RenderPen renderPen;

        if (ColorMode == ColorMode.SemanticMatrix && TryGetSemanticStyle(prefix, suffix, out var vs))
        {
            // Color: per-line override > semantic matrix
            var effColor = levelSettings.OverrideColorInSchemes
                ? levelSettings.Color
                : vs.Color;

            // Width: per-line override > semantic matrix
            var effWidth = levelSettings.OverrideWidthInSchemes
                ? levelSettings.Width
                : vs.Width;

            // Style: per-line override > semantic matrix
            var effStyle = levelSettings.OverrideStyleInSchemes
                ? levelSettings.LineStyle
                : vs.Style;

            color = effColor;
            renderPen = new PenSettings { Color = effColor, Width = effWidth, LineDashStyle = effStyle }.RenderObject;
        }
        else
        {
            // Existing modes: PerLineSettings / ByPeriod / ByLevel
            color = ResolveColor(prefix, suffix, levelSettings);
            renderPen = BuildPen(levelSettings, color);
        }

        // Validate price is reasonable
        if (level.Price <= 0)
            return;

        var y = ChartInfo.GetYByPrice(level.Price, false);

        // Check if price is visible on chart
        if (y < 0 || y > ChartInfo.PriceChartContainer.Region.Height)
            return;

        var chartWidth = ChartInfo.PriceChartContainer.Region.Width;
        var currentBarX = ChartInfo.GetXByBar(CurrentBar - 1);
        var barWidth = (int)ChartInfo.PriceChartContainer.BarsWidth;
        var currentBarRightX = currentBarX + barWidth;

        // --- Decide if we defer the line (only when there will be a label)
        bool willHaveTextLabel = levelSettings.LabelPosition != LabelPosition.None;
        bool deferLine = willHaveTextLabel && levelSettings.LineType != LineType.None;

        // Draw line now only if not deferring (no cropping case)
        if (!deferLine)
        {
            // Draw line first (if LineType != None)
            switch (levelSettings.LineType)
            {   // If label is at bar position, start line after the label to avoid overlap
                case LineType.Bar:
                    context.DrawLine(renderPen, currentBarRightX, y, chartWidth, y);
                    break;
                case LineType.Full:
                    context.DrawLine(renderPen, 0, y, chartWidth, y);
                    break;
                case LineType.None:
                    break;
            }
        }

        // Price label (unchanged)
        if (levelSettings.ShowPrice)
            DrawPriceLabel(context, level.Price, y, renderPen, color);

        // --- Text label: enqueue (and pass line info if deferLine == true)
        if (willHaveTextLabel)
        {
            int anchorX;
            bool alignRight;

            switch (levelSettings.LabelPosition)
            {
                case LabelPosition.Right:
                    anchorX = chartWidth - 5;
                    alignRight = true;
                    break;
                case LabelPosition.Left:
                    anchorX = 5;
                    alignRight = false;
                    break;
                case LabelPosition.Bar:
                default:
                    // slightly bigger gap from bar: 8 px (was 5)
                    anchorX = currentBarRightX + 8;
                    alignRight = false;
                    break;
            }

            var size = context.MeasureString(displayLabel, _font);
            int rectXBase = alignRight ? anchorX - size.Width : anchorX;
            var desired = new Rectangle(rectXBase - 2, y - size.Height / 2 - 1, size.Width + 4, size.Height + 2);

            // Horizontal corridor & direction
            int minX, maxX, dir;
            if (levelSettings.LabelPosition == LabelPosition.Left)
            {
                minX = 5 - 2;
                maxX = minX + LabelHMaxShift;
                dir = +1; // push right
            }
            else if (levelSettings.LabelPosition == LabelPosition.Right)
            {
                int rightEdge = chartWidth - 5;
                minX = rightEdge - size.Width - LabelHMaxShift - 2;
                maxX = rightEdge - size.Width - 2;
                if (minX > maxX) minX = maxX;
                dir = -1; // push left
            }
            else // Bar -> can move up to the same bound as "Right"
            {
                int rightEdge = chartWidth - 5;
                minX = anchorX - 2;
                maxX = rightEdge - size.Width - 2;
                if (minX > maxX) minX = maxX;
                dir = +1; // push right
            }

            var (sprefix, ssuffix) = SplitKey(levelKey);
            int priority = GetLabelPriority(sprefix, ssuffix);

            _labelQueue.Add(new LabelDrawRequest
            {
                Text = displayLabel,
                Desired = desired,
                MinX = minX,
                MaxX = maxX,
                Dir = dir,
                AlignRight = (levelSettings.LabelPosition == LabelPosition.Right),
                Priority = priority,
                Pen = renderPen,

                // NEW: defer line so we can crop to the final label rect
                DeferLine = deferLine,
                LineType = levelSettings.LineType,
                LabelPos = levelSettings.LabelPosition,
                Y = y,
                CurrentBarRightX = currentBarRightX,
                ChartWidth = chartWidth
            });
        }
    }

    private void DrawPriceLabel(RenderContext context, decimal price, int y, RenderPen pen, CrossColor backgroundColor)
    {
        var priceText = string.Format(ChartInfo.StringFormat, price);

        // Calculate contrasting text color based on background color
        var textColor = GetContrastingColor(backgroundColor);

        this.DrawLabelOnPriceAxis(context, priceText, y, _axisFont, backgroundColor.Convert(), textColor.Convert());
    }

    private CrossColor GetContrastingColor(CrossColor backgroundColor)
    {
        // Calculate luminance using relative luminance formula
        // See: https://www.w3.org/TR/WCAG20/#relativeluminancedef
        double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;

        // If background is dark, use white text; if light, use black text
        if (luminance > 0.5)
        {
            // Dark text for light backgrounds
            return CrossColors.Black;
        }
        else
        {
            // Light text for dark backgrounds
            return CrossColors.White;
        }
    }

    private void DrawTextLabel(RenderContext context, string text, int x, int y, RenderPen pen, bool alignRight)
    {
        var size = context.MeasureString(text, _font);
        var textColor = ChartInfo.ColorsStore.MouseTextColor;

        // Calculate rectangle position based on alignment
        var rectX = alignRight ? x - size.Width : x;
        var rect = new Rectangle(rectX - 2, y - size.Height / 2 - 1, size.Width + 4, size.Height + 2);

        // Draw background with border
        var backgroundColor = ChartInfo.ColorsStore.BaseBackgroundColor;
        context.FillRectangle(backgroundColor, rect);
        context.DrawRectangle(pen, rect);

        // Draw text
        var textRect = new Rectangle(rectX, y - size.Height / 2, size.Width, size.Height);
        var format = alignRight ? _stringRightFormat : _stringLeftFormat;
        context.DrawString(text, _font, textColor, textRect, format);
    }

    private void RenderLevelGroup(RenderContext context, string storagePrefix, string displayPrefix, // NEW: user-configurable display prefix (UI)
    LevelSettings openLevel, LevelSettings highLevel, LevelSettings lowLevel, LevelSettings closeLevel,
    LevelSettings eqLevel, LevelSettings pocLevel, LevelSettings vwapLevel, LevelSettings vahLevel, LevelSettings valLevel)
    {
        // Activate scoped override for this group render
        _scopedStoragePrefix = storagePrefix;
        _scopedDisplayPrefix = displayPrefix;

        try
        {
            var levels = new[]
            {
            ("Open", openLevel),
            ("High", highLevel),
            ("Low", lowLevel),
            ("Close", closeLevel),
            ("EQ", eqLevel),
            ("POC", pocLevel),
            ("VWAP", vwapLevel),
            ("VAH", vahLevel),
            ("VAL", valLevel)
        };

            foreach (var (suffix, levelSettings) in levels)
                RenderLevel(context, $"{storagePrefix}{suffix}", levelSettings);
        }
        finally
        {
            // Always clear the override after finishing this group
            _scopedStoragePrefix = null;
            _scopedDisplayPrefix = null;
        }
    }

    #endregion

    #region SubscribeAllLevels

    private static bool TryParsePeriodFromPropertyName(string propertyName, out FixedProfilePeriods period)
    {
        if (propertyName.StartsWith("Day")) { period = FixedProfilePeriods.CurrentDay; return true; }
        if (propertyName.StartsWith("PrevDay")) { period = FixedProfilePeriods.LastDay; return true; }
        if (propertyName.StartsWith("Week")) { period = FixedProfilePeriods.CurrentWeek; return true; }
        if (propertyName.StartsWith("PrevWeek")) { period = FixedProfilePeriods.LastWeek; return true; }
        if (propertyName.StartsWith("Month")) { period = FixedProfilePeriods.CurrentMonth; return true; }
        if (propertyName.StartsWith("PrevMonth")) { period = FixedProfilePeriods.LastMonth; return true; }
        if (propertyName.StartsWith("Contract")) { period = FixedProfilePeriods.Contract; return true; }

        period = default;
        return false;
    }

    private void SubscribeAllLevels()
    {
        foreach (var (ls, period) in EnumerateAllLevelSettingsWithPeriods())
            TrySubscribe(ls, period);
    }

    private IEnumerable<(LevelSettings ls, FixedProfilePeriods period)> EnumerateAllLevelSettingsWithPeriods()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        foreach (var pi in GetType().GetProperties(flags))
        {
            if (pi.PropertyType != typeof(LevelSettings) || !pi.CanRead)
                continue;

            if (pi.GetValue(this) is LevelSettings ls && TryParsePeriodFromPropertyName(pi.Name, out var period))
                yield return (ls, period);
        }
    }

    private void TrySubscribe(LevelSettings? ls, FixedProfilePeriods period)
    {
        if (ls is null) return;

        if (_subscribedLevels.Add(ls))
        {
            _periodByLevel[ls] = period;
            ls.PropertyChanged += OnLevelSettingsChanged;
        }
    }

    private void OnLevelSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LevelSettings ls)
            return;

        if (e.PropertyName == nameof(LevelSettings.Enabled))
        {
            if (_periodByLevel.TryGetValue(ls, out var period))
            {
                RecalcNeedFor(period);

                if (ls.Enabled && IsNeeded(period))
                    RequestProfileForPeriod(period);
                else
                    RedrawChart();
            }
        }

        // Visual-only changes -> redraw
        if (e.PropertyName == nameof(LevelSettings.Color)
            || e.PropertyName == nameof(LevelSettings.OverrideColorInSchemes)
            || e.PropertyName == nameof(LevelSettings.LineStyle)
            || e.PropertyName == nameof(LevelSettings.Width)
            || e.PropertyName == nameof(LevelSettings.LabelPosition)
            || e.PropertyName == nameof(LevelSettings.ShowPrice)
            || e.PropertyName == nameof(LevelSettings.OverrideLabel)
            || e.PropertyName == nameof(LevelSettings.OverrideWidthInSchemes)
            || e.PropertyName == nameof(LevelSettings.OverrideStyleInSchemes))
        {
            RedrawChart();
        }
    }

    private bool IsNeeded(FixedProfilePeriods period)
    {
        return period switch
        {
            FixedProfilePeriods.CurrentDay => _needDay,
            FixedProfilePeriods.LastDay => _needPrevDay,
            FixedProfilePeriods.CurrentWeek => _needWeek,
            FixedProfilePeriods.LastWeek => _needPrevWeek,
            FixedProfilePeriods.CurrentMonth => _needMonth,
            FixedProfilePeriods.LastMonth => _needPrevMonth,
            FixedProfilePeriods.Contract => _needContract,
            _ => false
        };
    }

    private static (string Prefix, string Suffix) SplitKey(string key)
    {
        // Known suffixes ordered by length to avoid collisions
        foreach (var s in new[] { "Close", "Open", "High", "Low", "VWAP", "POC", "VAH", "VAL", "EQ" })
            if (key.EndsWith(s, StringComparison.Ordinal))
                return (key.Substring(0, key.Length - s.Length), s);
        return ("", key);
    }

    private string ResolveLevelText(string suffix)
    {
        return suffix switch
        {
            "Open" => OpenLabel,
            "High" => HighLabel,
            "Low" => LowLabel,
            "Close" => CloseLabel,
            "EQ" => EqLabel,
            "POC" => POCLabel,
            "VWAP" => VWAPLabel,
            "VAH" => VAHLabel,
            "VAL" => VALLabel,
            _ => suffix
        };
    }

    private string BuildDisplayLabel(string prefix, string levelText)
    {
        var template = string.IsNullOrEmpty(LabelTemplate) ? "{prefix}{level}" : LabelTemplate;
        return template.Replace("{prefix}", prefix ?? string.Empty)
                       .Replace("{level}", levelText ?? string.Empty);
    }

    /// <summary>
    /// Calculates BOTH bands (HVN and LVN) in a single pass over the profile for maximum efficiency.
    /// </summary>
    private void UpdateVolumeNodes(FixedProfilePeriods period, IndicatorCandle candle)
    {
        if (candle == null)
            return;

        // --- Setup HVN ---
        if (!_hvnBands.TryGetValue(period, out var hvnBands))
        {
            hvnBands = new List<VolumeNodeBand>();
            _hvnBands[period] = hvnBands;
        }
        else
        {
            hvnBands.Clear();
        }

        // --- Setup LVN ---
        if (!_lvnBands.TryGetValue(period, out var lvnBands))
        {
            lvnBands = new List<VolumeNodeBand>();
            _lvnBands[period] = lvnBands;
        }
        else
        {
            lvnBands.Clear();
        }

        // 1. Get ALL price levels FIRST.
        var levelsEnum = candle.GetAllPriceLevels();
        if (levelsEnum == null)
            return;

        // 2. Materialize the list and order it by PRICE (we will need it this way for the loop)
        var levels = levelsEnum.OrderBy(l => l.Price).ToList();
        if (levels.Count == 0)
            return;

        var poc = candle.MaxVolumePriceInfo;

        // 4. If it's null (because the API didn't calculate it), WE CALCULATE IT MANUALLY
        if (poc == null)
        {
            // Use 'var' to avoid 'using' issues
            var maxVolumeLevel = (dynamic)null; // Initialize as dynamic/null
            foreach (var level in levels) // Use the 'levels' list we already have
            {
                if (maxVolumeLevel == null || level.Volume > maxVolumeLevel.Volume)
                {
                    maxVolumeLevel = level;
                }
            }
            poc = maxVolumeLevel; // Assign the manually calculated POC
        }
        if (poc == null || poc.Volume <= 0)
            return;

        // --- Cutoffs ---
        var hvnCutoff = poc.Volume * (HVNThresholdPct / 100m);
        var lvnCutoff = 0m; // Initialized to 0

        var tick = InstrumentInfo.TickSize;
        if (tick <= 0m)
            return;

        // --- State for HVN ---
        decimal? hvnRunStart = null;
        decimal hvnLastPriceInRun = 0;
        int hvnGapLeft = 0;

        // --- State for LVN ---
        decimal? lvnRunStart = null;
        decimal lvnLastPriceInRun = 0;
        int lvnGapLeft = 0;

        // --- APPLY LVN NOISE FILTER ---
        // Check if the profile is mature enough to calculate LVNs
        bool lvnEnabledForPeriod = poc.Volume >= MinPocVolForLVN;
        if (lvnEnabledForPeriod)
        {
            lvnCutoff = poc.Volume * (LVNThresholdPct / 100m);
        }
        // If not enabled, lvnCutoff remains 0, so 'isLow' (v <= 0) will always be false.

        bool IsNextTick(decimal prev, decimal next)
          => Math.Abs(next - prev) <= tick * 1.0000001m;

        // --- SINGLE LOOP ---
        for (int i = 0; i < levels.Count; i++)
        {
            var p = levels[i].Price;
            var v = levels[i].Volume;

            bool isHigh = v >= hvnCutoff;
            // 'isLow' is only true if:
            // 1. The profile is mature (lvnEnabledForPeriod == true)
            // 2. The volume is low (v <= lvnCutoff)
            // 3. We are NOT at the exact H/L of the period

            // --- START: Hybrid Tail Filter Logic ---

            // 1. Calculate the percentage filter in ticks (rounded down)
            var rangeInTicks = (candle.High - candle.Low) / tick;
            var pctFilterInTicks = (int)(rangeInTicks * (LVNTailFilter_Pct / 100m));

            // 2. Use the MAXIMUM of the % filter and the Minimum Ticks filter
            var effectiveFilterTicks = Math.Max(LVNTailFilter_MinTicks, pctFilterInTicks);

            // 3. Calculate the final filter in price
            var tailFilterAmount = effectiveFilterTicks * tick;

            // --- END: Filter Logic ---

            bool isLow = lvnEnabledForPeriod &&
             (v <= lvnCutoff) &&
             (p < (candle.High - tailFilterAmount) && p > (candle.Low + tailFilterAmount));

            // ----- STATE MACHINE LOGIC FOR HVN -----
            bool hvnContiguous = (hvnRunStart != null) && IsNextTick(hvnLastPriceInRun, p);

            if (hvnRunStart == null)
            {
                if (isHigh)
                {
                    hvnRunStart = p;
                    hvnLastPriceInRun = p;
                    hvnGapLeft = HVNGapToleranceTicks;
                }
            }
            else if (!hvnContiguous)
            {
                hvnBands.Add(new VolumeNodeBand { Low = hvnRunStart.Value, High = hvnLastPriceInRun });
                hvnRunStart = isHigh ? p : null;
                if (isHigh)
                {
                    hvnLastPriceInRun = p;
                    hvnGapLeft = HVNGapToleranceTicks;
                }
            }
            else if (isHigh)
            {
                hvnLastPriceInRun = p;
                hvnGapLeft = HVNGapToleranceTicks;
            }
            else
            {
                if (hvnGapLeft > 0)
                {
                    hvnGapLeft--;
                    hvnLastPriceInRun = p;
                }
                else
                {
                    var end = hvnLastPriceInRun;
                    if (i > 0 && levels[i - 1].Volume < hvnCutoff)
                        end -= tick;
                    if (end >= hvnRunStart.Value)
                        hvnBands.Add(new VolumeNodeBand { Low = hvnRunStart.Value, High = end });
                    hvnRunStart = null;
                }
            }

            // ----- STATE MACHINE LOGIC FOR LVN -----
            bool lvnContiguous = (lvnRunStart != null) && IsNextTick(lvnLastPriceInRun, p);

            if (lvnRunStart == null)
            {
                if (isLow)
                {
                    lvnRunStart = p;
                    lvnLastPriceInRun = p;
                    lvnGapLeft = LVNGapToleranceTicks;
                }
            }
            else if (!lvnContiguous)
            {
                lvnBands.Add(new VolumeNodeBand { Low = lvnRunStart.Value, High = lvnLastPriceInRun });
                lvnRunStart = isLow ? p : null;
                if (isLow)
                {
                    lvnLastPriceInRun = p;
                    lvnGapLeft = LVNGapToleranceTicks;
                }
            }
            else if (isLow)
            {
                lvnLastPriceInRun = p;
                lvnGapLeft = LVNGapToleranceTicks;
            }
            else
            {
                if (lvnGapLeft > 0)
                {
                    lvnGapLeft--;
                    lvnLastPriceInRun = p;
                }
                else
                {
                    var end = lvnLastPriceInRun;
                    if (i > 0 && levels[i - 1].Volume > lvnCutoff)
                        end -= tick;
                    if (end >= lvnRunStart.Value)
                        lvnBands.Add(new VolumeNodeBand { Low = lvnRunStart.Value, High = end });
                    lvnRunStart = null;
                }
            }
        }

        // --- Close final bands ---
        if (hvnRunStart != null && hvnLastPriceInRun >= hvnRunStart.Value)
            hvnBands.Add(new VolumeNodeBand { Low = hvnRunStart.Value, High = hvnLastPriceInRun });

        if (lvnRunStart != null && lvnLastPriceInRun >= lvnRunStart.Value)
            lvnBands.Add(new VolumeNodeBand { Low = lvnRunStart.Value, High = lvnLastPriceInRun });
    }

    private sealed class VolumeNodeBand
    {
        public decimal Low { get; init; }
        public decimal High { get; init; }
    }

    private void RenderAllVolumeNodes(RenderContext ctx)
    {
        var tick = InstrumentInfo?.TickSize ?? 0m;
        if (tick <= 0) return;

        // The occlusion list is shared across all layers
        var claimed = new List<(decimal Lo, decimal Hi)>();

        // --- LAYER 1: RENDER ALL HVNs (SOLID) ---
        // (Using the priority order we defined: CurrentDay first)
        foreach (var period in _nodePriorityOrder)
        {
            var (hvnEnabled, hvnColor) = period switch
            {
                FixedProfilePeriods.CurrentDay => (DayHVNEnabled, DayHVNColor),
                FixedProfilePeriods.LastDay => (PrevDayHVNEnabled, PrevDayHVNColor),
                FixedProfilePeriods.CurrentWeek => (WeekHVNEnabled, WeekHVNColor),
                FixedProfilePeriods.LastWeek => (PrevWeekHVNEnabled, PrevWeekHVNColor),
                FixedProfilePeriods.CurrentMonth => (MonthHVNEnabled, MonthHVNColor),
                FixedProfilePeriods.LastMonth => (PrevMonthHVNEnabled, PrevMonthHVNColor),
                FixedProfilePeriods.Contract => (ContractHVNEnabled, ContractHVNColor),
                _ => (false, CrossColors.Transparent)
            };

            if (hvnEnabled && _hvnBands.TryGetValue(period, out var hvnBands) && hvnBands.Count > 0)
            {
                foreach (var band in hvnBands)
                {
                    var leftovers = SubtractClaimed(band, claimed, tick);
                    foreach (var piece in leftovers)
                    {
                        RenderHVNBand(ctx, piece, hvnColor);
                        claimed.Add((
                          piece.Low - HVNOcclusionTicks * tick,
                          piece.High + HVNOcclusionTicks * tick
                        ));
                    }
                }
            }
        }

        // --- LAYER 2: RENDER ALL LVNs (VOIDS) ---
        // (The 'claimed' list already contains all HVNs)
        foreach (var period in _nodePriorityOrder)
        {
            // Re-get the period color (needed for LVN)
            var (_, hvnColor) = period switch // We only need the color
            {
                FixedProfilePeriods.CurrentDay => (DayHVNEnabled, DayHVNColor),
                FixedProfilePeriods.LastDay => (PrevDayHVNEnabled, PrevDayHVNColor),
                FixedProfilePeriods.CurrentWeek => (WeekHVNEnabled, WeekHVNColor),
                FixedProfilePeriods.LastWeek => (PrevWeekHVNEnabled, PrevWeekHVNColor),
                FixedProfilePeriods.CurrentMonth => (MonthHVNEnabled, MonthHVNColor),
                FixedProfilePeriods.LastMonth => (PrevMonthHVNEnabled, PrevMonthHVNColor),
                FixedProfilePeriods.Contract => (ContractHVNEnabled, ContractHVNColor),
                _ => (false, CrossColors.Transparent)
            };

            var lvnEnabled = period switch
            {
                FixedProfilePeriods.CurrentDay => DayLVNEnabled,
                FixedProfilePeriods.LastDay => PrevDayLVNEnabled,
                FixedProfilePeriods.CurrentWeek => WeekLVNEnabled,
                FixedProfilePeriods.LastWeek => PrevWeekLVNEnabled,
                FixedProfilePeriods.CurrentMonth => MonthLVNEnabled,
                FixedProfilePeriods.LastMonth => PrevMonthLVNEnabled,
                FixedProfilePeriods.Contract => ContractLVNEnabled,
                _ => false
            };

            if (lvnEnabled && _lvnBands.TryGetValue(period, out var lvnBands) && lvnBands.Count > 0)
            {
                foreach (var band in lvnBands)
                {
                    // Here's the magic: the LVN is "cut" by the already drawn HVNs
                    var leftovers = SubtractClaimed(band, claimed, tick);
                    foreach (var piece in leftovers)
                    {
                        RenderLVNBand(ctx, piece, hvnColor, LVNBorderWidth, LVNBorderStyle);

                        // LVNs also claim space to occlude lower-priority LVNs
                        claimed.Add((
                        piece.Low - HVNOcclusionTicks * tick,
                        piece.High + HVNOcclusionTicks * tick
                        ));
                    }
                }
            }
        }
    }

    private void RenderHVNBand(RenderContext ctx, VolumeNodeBand band, CrossColor crossColor)
    {
        if (band.Low <= 0 || band.High <= 0) return;

        var yHigh = ChartInfo.GetYByPrice(band.High, false);
        var yLow = ChartInfo.GetYByPrice(band.Low, false);

        var top = Math.Min(yHigh, yLow);
        var bottom = Math.Max(yHigh, yLow);

        // Only if visible
        if (bottom < 0 || top > ChartInfo.PriceChartContainer.Region.Height)
            return;

        var w = ChartInfo.PriceChartContainer.Region.Width;
        var drawTop = Math.Max(0, top);
        var drawBot = Math.Min(ChartInfo.PriceChartContainer.Region.Height, bottom);
        var drawH = Math.Max(1, drawBot - drawTop + 1);

        var rect = new System.Drawing.Rectangle(0, drawTop, w, drawH);
        ctx.FillRectangle(crossColor.Convert(), rect);
    }

    private void RenderLVNBand(RenderContext ctx, VolumeNodeBand band, CrossColor color, int borderWidth, LineDashStyle borderStyle)
    {
        if (band.Low <= 0 || band.High <= 0) return;

        var yHigh = ChartInfo.GetYByPrice(band.High, false);
        var yLow = ChartInfo.GetYByPrice(band.Low, false);
        var top = Math.Min(yHigh, yLow);
        var bottom = Math.Max(yHigh, yLow);

        if (bottom < 0 || top > ChartInfo.PriceChartContainer.Region.Height)
            return;

        var w = ChartInfo.PriceChartContainer.Region.Width;
        var drawTop = Math.Max(0, top);
        var drawBot = Math.Min(ChartInfo.PriceChartContainer.Region.Height, bottom);
        var drawH = Math.Max(1, drawBot - drawTop + 1);
        var rect = new System.Drawing.Rectangle(0, drawTop, w, drawH);

        // --- 1. "Ghost" Fill ---
        // We use the period's color but with a very low alpha
        var fillColor = GhostColor(color, 20); // 20 out of 255 is ~8% opacity
        ctx.FillRectangle(fillColor.Convert(), rect);

        // --- 2. Border ---
        // We draw the border on top of the fill.
        // We use the original color (with its medium opacity) for the border.
        var pen = new PenSettings
        {
            Color = color,
            Width = borderWidth,
            LineDashStyle = borderStyle
        }.RenderObject;

        ctx.DrawRectangle(pen, rect);
    }

    // Split a band by subtracting previously-claimed ranges (already expanded by occlusion tolerance).
    // Returns the list of remaining sub-bands aligned to the tick grid.
    private List<VolumeNodeBand> SubtractClaimed(VolumeNodeBand band, List<(decimal Lo, decimal Hi)> claimed, decimal tick)
    {
        // work list as simple (Lo, Hi) intervals
        var segments = new List<(decimal Lo, decimal Hi)> { (band.Low, band.High) };

        foreach (var r in claimed)
        {
            var next = new List<(decimal Lo, decimal Hi)>();

            foreach (var s in segments)
            {
                // no overlap
                if (r.Hi < s.Lo || r.Lo > s.Hi)
                {
                    next.Add(s);
                    continue;
                }

                // overlap -> split into left/right remainders (if any), keeping tick alignment
                if (r.Lo > s.Lo)
                    next.Add((s.Lo, Math.Min(s.Hi, r.Lo - tick)));

                if (r.Hi < s.Hi)
                    next.Add((Math.Max(s.Lo, r.Hi + tick), s.Hi));
            }

            segments = next;
            if (segments.Count == 0)
                break;
        }

        return segments
            .Where(seg => seg.Hi >= seg.Lo)
            .Select(seg => new VolumeNodeBand { Low = seg.Lo, High = seg.Hi })
            .ToList();
    }

    private static PeriodKind MapPeriod(string storagePrefix) => storagePrefix switch
    {
        "d" => PeriodKind.CurrentDay,
        "p" => PeriodKind.PrevDay,
        "w" => PeriodKind.CurrentWeek,
        "pw" => PeriodKind.PrevWeek,
        "m" => PeriodKind.CurrentMonth,
        "pm" => PeriodKind.PrevMonth,
        "c" => PeriodKind.Contract,
        _ => PeriodKind.Other
    };

    private static LevelKind MapLevel(string suffix) => suffix switch
    {
        "Open" => LevelKind.Open,
        "High" => LevelKind.High,
        "Low" => LevelKind.Low,
        "Close" => LevelKind.Close,
        "EQ" => LevelKind.EQ,
        "POC" => LevelKind.POC,
        "VWAP" => LevelKind.VWAP,
        "VAH" => LevelKind.VAH,
        "VAL" => LevelKind.VAL,
        _ => LevelKind.Other
    };

    // Returns a VisualStyle for (prefix, suffix) or null if no rule exists
    private bool TryGetSemanticStyle(string storagePrefix, string suffix, out VisualStyle style)
    {
        var key = (MapLevel(suffix), MapPeriod(storagePrefix));
        if (_styleMatrix.TryGetValue(key, out style))
            return true;

        key = (LevelKind.Other, MapPeriod(storagePrefix));
        if (_styleMatrix.TryGetValue(key, out style))
            return true;

        style = null!;
        return false;
    }

    private CrossColor GhostColor(CrossColor color, int alpha = 40)
    {
        // Creates a new System.Drawing.Color with the modified Alpha
        return System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B).Convert();
    }

    private void RefreshData()
    {
        RecalcAllNeeds();

        // Force the request so ATAS recalculates the profile
        // (necessary if we NOW need the POC and didn't before)
        RequestProfiles();

        // Update from cache (for already loaded profiles)
        UpdateAllNeededLevelsFromCache();

        RedrawChart();
    }

    #endregion

    #region label layout helpers
    /// <summary>
    /// Assigns label priority (higher = more important) using your rules:
    /// - POC: previous day/week > current day/week > others
    /// - VWAP: current day/week > previous day/week > others
    /// - VAH/VAL: closed (previous) > open (current) > others
    /// - High/Low: previous day > current day > others
    /// - Open/Close/EQ: moderate
    /// </summary>
    private int GetLabelPriority(string storagePrefix, string suffix)
    {
        static bool IsCurrent(string sp) => sp is "d" or "w" or "m";
        static bool IsPrevious(string sp) => sp is "p" or "pw" or "pm";
        static bool IsContract(string sp) => sp == "c";

        switch (suffix)
        {
            // POC: previous > current > rest
            case "POC":
                if (IsPrevious(storagePrefix)) return 95;
                if (storagePrefix == "d" || storagePrefix == "w") return 80;
                if (IsCurrent(storagePrefix)) return 70;
                if (IsContract(storagePrefix)) return 65;
                return 60;

            // VWAP: current day/week > previous > rest
            case "VWAP":
                if (storagePrefix == "d" || storagePrefix == "w") return 90;
                if (IsPrevious(storagePrefix)) return 75;
                if (IsCurrent(storagePrefix)) return 65;
                if (IsContract(storagePrefix)) return 60;
                return 55;

            // VAH / VAL: closed (previous) > open (current) > rest
            case "VAH":
            case "VAL":
                if (IsPrevious(storagePrefix)) return 85;
                if (IsCurrent(storagePrefix)) return 70;
                if (IsContract(storagePrefix)) return 55;
                return 50;

            // High / Low: previous day > current day > others
            case "High":
            case "Low":
                if (storagePrefix == "p") return 75;
                if (storagePrefix == "d") return 60;
                if (storagePrefix == "pw" || storagePrefix == "w") return 55;
                return 45;

            // Open / Close / EQ: moderate
            case "Open":
            case "Close":
            case "EQ":
                if (IsPrevious(storagePrefix)) return 60;
                if (IsCurrent(storagePrefix)) return 55;
                if (IsContract(storagePrefix)) return 50;
                return 45;

            default:
                return 40;
        }
    }

    /// <summary>
    /// Try to place a label rectangle by moving it horizontally in [minX, maxX]
    /// stepping by LabelHStep in the given direction (dir: +1 right, -1 left).
    /// Respects already-placed rectangles in _occupiedLabelRects.
    /// </summary>
    private bool TryPlaceLabelHorizontal(Rectangle desired, int minX, int maxX, int dir, out Rectangle placed)
    {
        // Clamp desired to corridor
        int baseX = Math.Min(Math.Max(desired.X, minX), maxX);
        var candidate = new Rectangle(baseX, desired.Y, desired.Width, desired.Height);

        // Helper: test overlap with anything already placed
        bool Overlaps(Rectangle r) => _occupiedLabelRects.Any(o => o.IntersectsWith(r));

        // 1) Try the base position
        if (!Overlaps(candidate))
        {
            placed = candidate;
            _occupiedLabelRects.Add(placed);
            return true;
        }

        // 2) Probe horizontally within the allowed corridor
        int maxShift = Math.Min(LabelHMaxShift, Math.Max(0, (dir > 0 ? maxX - baseX : baseX - minX)));
        for (int shift = LabelHStep; shift <= maxShift; shift += LabelHStep)
        {
            int x = baseX + dir * shift;
            if (x < minX) x = minX;
            if (x > maxX) x = maxX;

            candidate = new Rectangle(x, desired.Y, desired.Width, desired.Height);
            if (!Overlaps(candidate))
            {
                placed = candidate;
                _occupiedLabelRects.Add(placed);
                return true;
            }
        }

        placed = Rectangle.Empty;
        return false; // no slot found -> drop low-priority label
    }

    /// Renders a text label using the existing DrawTextLabel helper, given a final rectangle.
    /// This function reconstructs the (x, y) anchor that DrawTextLabel expects from the
    /// already-placed rectangle, preserving the same padding and alignment.

    private void DrawTextLabelCore(RenderContext context, string text, Rectangle rect, RenderPen pen, bool alignRight)
    {
        // Vertical anchor at the rectangle center (DrawTextLabel will center text around `y`)
        int y = rect.Top + rect.Height / 2;

        // Horizontal anchor reconstructed from the final rectangle, matching DrawTextLabel's padding:
        // - Left-aligned: x is the left inside edge (rect.Left + 2)
        // - Right-aligned: x is the right inside edge (rect.Right - 2)
        int x = alignRight ? rect.Right - 2 : rect.Left + 2;

        // Delegate actual rendering to the existing helper to keep styling consistent
        DrawTextLabel(context, text, x, y, pen, alignRight);
    }

    // End X when label is on the right side (leave a tiny padding)
    private static int LineEndBefore(Rectangle labelRect, int padding = 4)
        => Math.Max(0, labelRect.Left - padding);

    // Start X when label is on the left/bar side
    private static int LineStartAfter(Rectangle labelRect, int padding = 4)
        => labelRect.Right + padding;

    #endregion


    #endregion
}