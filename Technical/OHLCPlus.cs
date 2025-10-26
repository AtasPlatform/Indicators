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
    private readonly Dictionary<FixedProfilePeriods, List<HVNBand>> _hvnBands = new();

    private static readonly FixedProfilePeriods[] _hvnPriorityOrder = new[]
    {
    FixedProfilePeriods.Contract,
    FixedProfilePeriods.LastMonth,
    FixedProfilePeriods.CurrentMonth,
    FixedProfilePeriods.LastWeek,
    FixedProfilePeriods.CurrentWeek,
    FixedProfilePeriods.LastDay,
    FixedProfilePeriods.CurrentDay
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = "Enable HVN", Order = 90)]
    public bool DayHVNEnabled { get; set; } = false;

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentDay), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = "Enable HVN", Order = 90)]
    public bool PrevDayHVNEnabled { get; set; } = false;

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousDay), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = "Enable HVN", Order = 90)]
    public bool WeekHVNEnabled { get; set; } = false;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentWeek), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = "Enable HVN", Order = 90)]
    public bool PrevWeekHVNEnabled { get; set; } = false;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousWeek), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = "Enable HVN", Order = 90)]
    public bool MonthHVNEnabled { get; set; } = false;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.CurrentMonth), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = "Enable HVN", Order = 90)]
    public bool PrevMonthHVNEnabled { get; set; } = false;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.PreviousMonth), Name = "HVN Color", Order = 95)]
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

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = "Enable HVN", Order = 90)]
    public bool ContractHVNEnabled { get; set; } = false;
    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Contract), Name = "HVN Color", Order = 95)]
    public CrossColor ContractHVNColor { get; set; } = System.Drawing.Color.FromArgb(140, 30, 144, 255).Convert(); // dodgerblue

    #endregion

    #region HVN Settings
    [Display(GroupName = "HVN Settings", Name = "Threshold (% of POC volume)", Order = 10)]
    [Range(1, 99)]
    public int HVNThresholdPct { get; set; } = 80;

    [Display(GroupName = "HVN Settings", Name = "Gap tolerance (ticks)", Order = 20)]
    [Range(0, 10)]
    public int HVNGapToleranceTicks { get; set; } = 1;

    // Overlap tolerance in ticks: if a price falls within �N ticks of another already drawn, it is hidden.
    [Display(GroupName = "HVN Settings", Name = "Occlusion tolerance (ticks)", Order = 30)]
    [Range(0, 10)]
    public int HVNOcclusionTicks { get; set; } = 1;

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
        UpdateHVNs(period, fixedProfileOriginScale);
        RedrawChart();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null || InstrumentInfo is null)
            return;

        // Render all levels in groups for better organization
        RenderLevelGroup(context, storagePrefix: "d", displayPrefix: PrefixCurrentDay, DayOpenLevel, DayHighLevel, DayLowLevel, DayCloseLevel, DayEquilibriumLevel, DayPOCLevel, DayVWAPLevel, DayVAHLevel, DayVALLevel);
        RenderLevelGroup(context, storagePrefix: "p", displayPrefix: PrefixPrevDay, PrevDayOpenLevel, PrevDayHighLevel, PrevDayLowLevel, PrevDayCloseLevel, PrevDayEquilibriumLevel, PrevDayPOCLevel, PrevDayVWAPLevel, PrevDayVAHLevel, PrevDayVALLevel);
        RenderLevelGroup(context, storagePrefix: "w", displayPrefix: PrefixCurrentWeek, WeekOpenLevel, WeekHighLevel, WeekLowLevel, WeekCloseLevel, WeekEquilibriumLevel, WeekPOCLevel, WeekVWAPLevel, WeekVAHLevel, WeekVALLevel);
        RenderLevelGroup(context, storagePrefix: "pw", displayPrefix: PrefixPrevWeek, PrevWeekOpenLevel, PrevWeekHighLevel, PrevWeekLowLevel, PrevWeekCloseLevel, PrevWeekEquilibriumLevel, PrevWeekPOCLevel, PrevWeekVWAPLevel, PrevWeekVAHLevel, PrevWeekVALLevel);
        RenderLevelGroup(context, storagePrefix: "m", displayPrefix: PrefixCurrentMonth, MonthOpenLevel, MonthHighLevel, MonthLowLevel, MonthCloseLevel, MonthEquilibriumLevel, MonthPOCLevel, MonthVWAPLevel, MonthVAHLevel, MonthVALLevel);
        RenderLevelGroup(context, storagePrefix: "pm", displayPrefix: PrefixPrevMonth, PrevMonthOpenLevel, PrevMonthHighLevel, PrevMonthLowLevel, PrevMonthCloseLevel, PrevMonthEquilibriumLevel, PrevMonthPOCLevel, PrevMonthVWAPLevel, PrevMonthVAHLevel, PrevMonthVALLevel);
        RenderLevelGroup(context, storagePrefix: "c", displayPrefix: PrefixContract, ContractOpenLevel, ContractHighLevel, ContractLowLevel, ContractCloseLevel, ContractEquilibriumLevel, ContractPOCLevel, ContractVWAPLevel, ContractVAHLevel, ContractVALLevel);

        // HVN rects
        RenderAllHVNsWithPriority(context);
    }

    #endregion

    #region Private methods

    #region OnCalculate

    private void UpdateAllNeededLevelsFromCache()
    {
        void UpdateIf(FixedProfilePeriods p)
        {
            if (IsNeeded(p) && _profileCandles.TryGetValue(p, out var candle) && candle is not null)
                UpdateLevels(p, candle);
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

    private void RequestProfileForPeriod(FixedProfilePeriods period, bool force = true)
    {
        if (!force && _profileCandles.TryGetValue(period, out var candle) && candle is not null)
        {
            RedrawChart();
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
               DayEquilibriumLevel.Enabled || DayPOCLevel.Enabled || DayVWAPLevel.Enabled || DayVAHLevel.Enabled || DayVALLevel.Enabled || DayHVNEnabled;
    }

    private bool NeedsPrevDayData()
    {
        return PrevDayOpenLevel.Enabled || PrevDayHighLevel.Enabled || PrevDayLowLevel.Enabled || PrevDayCloseLevel.Enabled ||
               PrevDayEquilibriumLevel.Enabled || PrevDayPOCLevel.Enabled || PrevDayVWAPLevel.Enabled || PrevDayVAHLevel.Enabled || PrevDayVALLevel.Enabled || PrevDayHVNEnabled;
    }

    private bool NeedsWeekData()
    {
        return WeekOpenLevel.Enabled || WeekHighLevel.Enabled || WeekLowLevel.Enabled || WeekCloseLevel.Enabled ||
               WeekEquilibriumLevel.Enabled || WeekPOCLevel.Enabled || WeekVWAPLevel.Enabled || WeekVAHLevel.Enabled || WeekVALLevel.Enabled || WeekHVNEnabled;
    }

    private bool NeedsPrevWeekData()
    {
        return PrevWeekOpenLevel.Enabled || PrevWeekHighLevel.Enabled || PrevWeekLowLevel.Enabled || PrevWeekCloseLevel.Enabled ||
               PrevWeekEquilibriumLevel.Enabled || PrevWeekPOCLevel.Enabled || PrevWeekVWAPLevel.Enabled || PrevWeekVAHLevel.Enabled || PrevWeekVALLevel.Enabled || PrevWeekHVNEnabled;
    }

    private bool NeedsMonthData()
    {
        return MonthOpenLevel.Enabled || MonthHighLevel.Enabled || MonthLowLevel.Enabled || MonthCloseLevel.Enabled ||
               MonthEquilibriumLevel.Enabled || MonthPOCLevel.Enabled || MonthVWAPLevel.Enabled || MonthVAHLevel.Enabled || MonthVALLevel.Enabled || MonthHVNEnabled;
    }

    private bool NeedsPrevMonthData()
    {
        return PrevMonthOpenLevel.Enabled || PrevMonthHighLevel.Enabled || PrevMonthLowLevel.Enabled || PrevMonthCloseLevel.Enabled ||
               PrevMonthEquilibriumLevel.Enabled || PrevMonthPOCLevel.Enabled || PrevMonthVWAPLevel.Enabled || PrevMonthVAHLevel.Enabled || PrevMonthVALLevel.Enabled || PrevMonthHVNEnabled;
    }

    private bool NeedsContractData()
    {
        return ContractOpenLevel.Enabled || ContractHighLevel.Enabled || ContractLowLevel.Enabled || ContractCloseLevel.Enabled ||
               ContractEquilibriumLevel.Enabled || ContractPOCLevel.Enabled || ContractVWAPLevel.Enabled || ContractVAHLevel.Enabled || ContractVALLevel.Enabled || ContractHVNEnabled;
    }

    private void UpdateLevels(FixedProfilePeriods period, IndicatorCandle candle)
    {
        if (candle == null) return;

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


        // Draw line first (if LineType != None)
        switch (levelSettings.LineType)
        {
            case LineType.Bar:
                // If label is at bar position, start line after the label to avoid overlap
                if (levelSettings.LabelPosition == LabelPosition.Bar)
                {
                    // Calculate actual label width for better positioning
                    var labelSize = context.MeasureString(displayLabel, _font);
                    var labelStartX = currentBarRightX + 5;
                    var lineStartX = labelStartX + labelSize.Width + 4; // 4px padding
                    context.DrawLine(renderPen, lineStartX, y, chartWidth, y);
                }
                else
                {
                    // Normal bar line from right edge of bar to price axis
                    context.DrawLine(renderPen, currentBarRightX, y, chartWidth, y);
                }
                break;
            case LineType.Full:
                context.DrawLine(renderPen, 0, y, chartWidth, y);
                break;
            case LineType.None:
                // No line to draw
                break;
        }

        // Draw price label (if ShowPrice == true)
        if (levelSettings.ShowPrice)
        {
            DrawPriceLabel(context, level.Price, y, renderPen, color);
        }

        // Draw text label (if LabelPosition != None)
        switch (levelSettings.LabelPosition)
        {
            case LabelPosition.Bar:
                var barLabelX = currentBarRightX + 5;
                DrawTextLabel(context, displayLabel, barLabelX, y, renderPen, false);
                break;
            case LabelPosition.Right:
                var rightLabelX = chartWidth - 5;
                DrawTextLabel(context, displayLabel, rightLabelX, y, renderPen, true);
                break;
            case LabelPosition.Left:
                var leftLabelX = 5;
                DrawTextLabel(context, displayLabel, leftLabelX, y, renderPen, false);
                break;
            case LabelPosition.None:
                // No text label to draw
                break;
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
                    RequestProfileForPeriod(period, force: false);
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

    private void UpdateHVNs(FixedProfilePeriods period, IndicatorCandle candle)
    {
        if (candle == null)
            return;

        if (!_hvnBands.TryGetValue(period, out var bands))
        {
            bands = new List<HVNBand>();
            _hvnBands[period] = bands;
        }
        else
        {
            bands.Clear();
        }

        var poc = candle.MaxVolumePriceInfo;
        if (poc == null || poc.Volume <= 0)
            return;

        var cutoff = poc.Volume * (HVNThresholdPct / 100m);

        // Materialize and sort by ascending price
        var levelsEnum = candle.GetAllPriceLevels();
        if (levelsEnum == null)
            return;

        var levels = levelsEnum.OrderBy(l => l.Price).ToList();
        if (levels.Count == 0)
            return;

        var tick = InstrumentInfo.TickSize;
        if (tick <= 0m)
            return;

        // Current run state (a "band" of contiguous HVN ticks allowing small gaps)
        decimal? runStart = null;   // current band start (price)
        decimal lastPriceInRun = 0; // last visited price
        int gapLeft = 0;            // remaining tolerated gaps inside a band

        bool IsNextTick(decimal prev, decimal next)
            => Math.Abs(next - prev) <= tick * 1.0000001m; // tiny tolerance

        for (int i = 0; i < levels.Count; i++)
        {
            var p = levels[i].Price;
            var v = levels[i].Volume; // Si Volume no es decimal, castea a decimal

            bool isHigh = v >= cutoff;

            if (runStart == null)
            {
                // No open band: only start when level is above cutoff
                if (isHigh)
                {
                    runStart = p;
                    lastPriceInRun = p;
                    gapLeft = HVNGapToleranceTicks;
                }
                continue;
            }

            // There is an open band: check contiguity by tick
            bool contiguous = IsNextTick(lastPriceInRun, p);

            if (!contiguous)
            {
                // We jumped several ticks -> close previous band
                bands.Add(new HVNBand { Low = runStart.Value, High = lastPriceInRun });
                runStart = null;


                if (isHigh)
                {
                    runStart = p;
                    lastPriceInRun = p;
                    gapLeft = HVNGapToleranceTicks;
                }
                continue;
            }

            // Contiguous by tick
            if (isHigh)
            {
                lastPriceInRun = p;
                gapLeft = HVNGapToleranceTicks; // reset tolerance when back to "high" zone
            }
            else
            {
                if (gapLeft > 0)
                {
                    gapLeft--;
                    lastPriceInRun = p; // keep band continuity
                }
                else
                {
                    // Tolerance exhausted: close band at last "high" tick
                    var end = lastPriceInRun;

                    // If previous tick was low, step back one tick
                    if (i > 0 && levels[i - 1].Volume < cutoff)
                        end -= tick;

                    if (end >= runStart.Value)
                        bands.Add(new HVNBand { Low = runStart.Value, High = end });

                    runStart = null;

                    // Start a new band with this point if it's high
                    if (isHigh)
                    {
                        runStart = p;
                        lastPriceInRun = p;
                        gapLeft = HVNGapToleranceTicks;
                    }
                }
            }
        }

        // Close trailing band if any
        if (runStart != null && lastPriceInRun >= runStart.Value)
            bands.Add(new HVNBand { Low = runStart.Value, High = lastPriceInRun });

        // Optional: filter very small bands
        // bands.RemoveAll(b => (b.High - b.Low) < tick);
    }

    private sealed class HVNBand
    {
        public decimal Low { get; init; }
        public decimal High { get; init; }
    }

    private void RenderAllHVNsWithPriority(RenderContext ctx)
    {
        var tick = InstrumentInfo?.TickSize ?? 0m;
        if (tick <= 0) return;

        // ranges already claimed by higher-priority periods (stored already expanded)
        var claimed = new List<(decimal Lo, decimal Hi)>();

        foreach (var period in _hvnPriorityOrder)
        {
            var (enabled, color) = period switch
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

            if (!enabled) continue;
            if (!_hvnBands.TryGetValue(period, out var bands) || bands.Count == 0) continue;

            foreach (var band in bands)
            {
                // compute the remaining (non-occluded) parts of this band
                var leftovers = SubtractClaimed(band, claimed, tick);
                foreach (var piece in leftovers)
                {
                    RenderBand(ctx, piece, color);
                    // claim with occlusion tolerance
                    claimed.Add((
                        piece.Low - HVNOcclusionTicks * tick,
                        piece.High + HVNOcclusionTicks * tick
                    ));
                }
            }
        }
    }

    private void RenderBand(RenderContext ctx, HVNBand band, CrossColor crossColor)
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

    // Split a band by subtracting previously-claimed ranges (already expanded by occlusion tolerance).
    // Returns the list of remaining sub-bands aligned to the tick grid.
    private List<HVNBand> SubtractClaimed(HVNBand band, List<(decimal Lo, decimal Hi)> claimed, decimal tick)
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
            .Select(seg => new HVNBand { Low = seg.Lo, High = seg.Hi })
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

    #endregion


    #endregion
}