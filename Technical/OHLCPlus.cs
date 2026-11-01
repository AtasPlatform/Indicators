#nullable enable annotations
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
using System.Linq;
using System.Runtime.CompilerServices;

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
    private CrossColor? _textColor;
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

    /// <summary>
    /// Color of the level text: the label on the chart, the price on the price scale and the
    /// level badge in the heatmap. <see langword="null"/> means "Auto": the color is picked for
    /// contrast with the background behind the text. Templates and workspaces saved before the
    /// setting existed carry no value, so they read as Auto.
    /// </summary>
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), Description = nameof(Strings.OhlcPlusTextColorDescription))]
    public CrossColor? TextColor
    {
        get => _textColor;
        set => SetField(ref _textColor, value);
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
        LineType lineType = LineType.Bar,
        CrossColor? textColor = null
    )
    {
        Enabled = enabled;
        Color = color == default ? System.Drawing.Color.Blue.Convert() : color;
        TextColor = textColor;
        Width = width;
        LineStyle = lineStyle;
        ShowPrice = showPrice;
        LabelPosition = labelPosition;
        LineType = lineType;
    }

    #endregion
}

[DisplayName("OHLC Plus")]
[Category(IndicatorCategories.VolumeOrderFlow)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.OHLCPlusDescription))]
public class OHLCPlus : Indicator
{
    #region Nested types

    private sealed class LabelData
    {
        #region Properties

        public Rectangle Bounds { get; set; }
        public int DesiredY { get; init; }
        public decimal Price { get; init; }
        public LabelPosition Position { get; init; }
        public int AnchorX { get; init; }
        public int LineStartX { get; init; }
        public Action<RenderContext, int>? DrawLevelLine { get; init; }
        public Action<RenderContext, int, int> DrawConnector { get; init; }
        public Action<RenderContext, int> Draw { get; init; }

        #endregion
    }

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


    // Written from profile-response callbacks (thread pool) and read/cleared from the
    // calculation thread — same concurrency story as _levels below.
    private readonly ConcurrentDictionary<FixedProfilePeriods, IndicatorCandle> _profileCandles = [];
    private readonly ConcurrentDictionary<FixedProfilePeriods, IndicatorCandle> _originProfileCandles = [];
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
    private bool _useAbsolutePrices;
    private bool _autoUpdateLevels = true;

    private bool _needDay;
    private bool _needPrevDay;
    private bool _needWeek;
    private bool _needPrevWeek;
    private bool _needMonth;
    private bool _needPrevMonth;
    private bool _needContract;

    private bool _allLevelsVisible = true;

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

    #endregion

    #region Calculation Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.AbsolutePrice), Description = nameof(Strings.OhlcPlusAbsolutePriceDescription), Order = 900)]
    public bool UseAbsolutePrices
    {
        get => _useAbsolutePrices;
        set
        {
            _useAbsolutePrices = value;
            UpdateAllNeededLevelsFromCache();
        }
    }

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.AutoUpdate), Description = nameof(Strings.OhlcPlusAutoUpdateDescription), Order = 910)]
    public bool AutoUpdateLevels
    {
        get => _autoUpdateLevels;
        set => _autoUpdateLevels = value;
    }

    #endregion

    #region Visibility Settings

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Visibility), Name = nameof(Strings.ToggleLevelsVisibilityHotKey), Order = 1000)]
    public CrossKey[] ToggleVisibilityHotKey { get; set; } = { CrossKey.Q };

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

    public override bool ProcessKeyDown(CrossKeyEventArgs e)
    {
        if (ToggleVisibilityHotKey != null && ToggleVisibilityHotKey.Contains(e.Key))
        {
            ToggleAllLevelsVisibility();
            return true;
        }

        return base.ProcessKeyDown(e);
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == 0)
        {
            _profileCandles.Clear();
            _originProfileCandles.Clear();
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

        // Both profile candles read through to the live tick-updated profile,
        // so re-reading them here is enough to keep the levels current.
        if (_autoUpdateLevels)
            UpdateAllNeededLevelsFromCache();
    }

#pragma warning disable CS0672
    protected override void OnFixedProfilesResponse(IndicatorCandle fixedProfileScaled, IndicatorCandle fixedProfileOriginScale, FixedProfilePeriods period)
#pragma warning restore CS0672
    {
        // The scaled profile is binned to the current chart scale, so all levels (OHLC, POC,
        // VWAP and the value area) line up with what the Profile indicator draws. The origin
        // scale profile keeps exact tick prices and reads through to the live profile.
        _profileCandles[period] = fixedProfileScaled;
        _originProfileCandles[period] = fixedProfileOriginScale;
        UpdateLevels(period, _useAbsolutePrices ? fixedProfileOriginScale : fixedProfileScaled);
        RedrawChart();
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null || InstrumentInfo is null)
            return;

        // Draw names after all lines, and arrange names across periods together.
        var labels = new List<LabelData>();
        RenderLevelGroup(context, labels, "d", DayOpenLevel, DayHighLevel, DayLowLevel, DayCloseLevel, DayEquilibriumLevel, DayPOCLevel, DayVWAPLevel, DayVAHLevel, DayVALLevel);
        RenderLevelGroup(context, labels, "p", PrevDayOpenLevel, PrevDayHighLevel, PrevDayLowLevel, PrevDayCloseLevel, PrevDayEquilibriumLevel, PrevDayPOCLevel, PrevDayVWAPLevel, PrevDayVAHLevel, PrevDayVALLevel);
        RenderLevelGroup(context, labels, "w", WeekOpenLevel, WeekHighLevel, WeekLowLevel, WeekCloseLevel, WeekEquilibriumLevel, WeekPOCLevel, WeekVWAPLevel, WeekVAHLevel, WeekVALLevel);
        RenderLevelGroup(context, labels, "pw", PrevWeekOpenLevel, PrevWeekHighLevel, PrevWeekLowLevel, PrevWeekCloseLevel, PrevWeekEquilibriumLevel, PrevWeekPOCLevel, PrevWeekVWAPLevel, PrevWeekVAHLevel, PrevWeekVALLevel);
        RenderLevelGroup(context, labels, "m", MonthOpenLevel, MonthHighLevel, MonthLowLevel, MonthCloseLevel, MonthEquilibriumLevel, MonthPOCLevel, MonthVWAPLevel, MonthVAHLevel, MonthVALLevel);
        RenderLevelGroup(context, labels, "pm", PrevMonthOpenLevel, PrevMonthHighLevel, PrevMonthLowLevel, PrevMonthCloseLevel, PrevMonthEquilibriumLevel, PrevMonthPOCLevel, PrevMonthVWAPLevel, PrevMonthVAHLevel, PrevMonthVALLevel);
        RenderLevelGroup(context, labels, "c", ContractOpenLevel, ContractHighLevel, ContractLowLevel, ContractCloseLevel, ContractEquilibriumLevel, ContractPOCLevel, ContractVWAPLevel, ContractVAHLevel, ContractVALLevel);
        DrawLabels(context, labels, ChartInfo.PriceChartContainer.Region.Height);
    }

    #endregion

    #region Private methods

    private void ToggleAllLevelsVisibility()
    {
        _allLevelsVisible = !_allLevelsVisible;
        RedrawChart();
    }

    #region OnCalculate

    private void UpdateAllNeededLevelsFromCache()
    {
        var candles = _useAbsolutePrices ? _originProfileCandles : _profileCandles;

        void UpdateIf(FixedProfilePeriods p)
        {
            if (IsNeeded(p) && candles.TryGetValue(p, out var candle) && candle is not null)
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

#pragma warning disable CS0618
        GetFixedProfile(new FixedProfileRequest(period));
#pragma warning restore CS0618
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
               DayEquilibriumLevel.Enabled || DayPOCLevel.Enabled || DayVWAPLevel.Enabled || DayVAHLevel.Enabled || DayVALLevel.Enabled;
    }

    private bool NeedsPrevDayData()
    {
        return PrevDayOpenLevel.Enabled || PrevDayHighLevel.Enabled || PrevDayLowLevel.Enabled || PrevDayCloseLevel.Enabled ||
               PrevDayEquilibriumLevel.Enabled || PrevDayPOCLevel.Enabled || PrevDayVWAPLevel.Enabled || PrevDayVAHLevel.Enabled || PrevDayVALLevel.Enabled;
    }

    private bool NeedsWeekData()
    {
        return WeekOpenLevel.Enabled || WeekHighLevel.Enabled || WeekLowLevel.Enabled || WeekCloseLevel.Enabled ||
               WeekEquilibriumLevel.Enabled || WeekPOCLevel.Enabled || WeekVWAPLevel.Enabled || WeekVAHLevel.Enabled || WeekVALLevel.Enabled;
    }

    private bool NeedsPrevWeekData()
    {
        return PrevWeekOpenLevel.Enabled || PrevWeekHighLevel.Enabled || PrevWeekLowLevel.Enabled || PrevWeekCloseLevel.Enabled ||
               PrevWeekEquilibriumLevel.Enabled || PrevWeekPOCLevel.Enabled || PrevWeekVWAPLevel.Enabled || PrevWeekVAHLevel.Enabled || PrevWeekVALLevel.Enabled;
    }

    private bool NeedsMonthData()
    {
        return MonthOpenLevel.Enabled || MonthHighLevel.Enabled || MonthLowLevel.Enabled || MonthCloseLevel.Enabled ||
               MonthEquilibriumLevel.Enabled || MonthPOCLevel.Enabled || MonthVWAPLevel.Enabled || MonthVAHLevel.Enabled || MonthVALLevel.Enabled;
    }

    private bool NeedsPrevMonthData()
    {
        return PrevMonthOpenLevel.Enabled || PrevMonthHighLevel.Enabled || PrevMonthLowLevel.Enabled || PrevMonthCloseLevel.Enabled ||
               PrevMonthEquilibriumLevel.Enabled || PrevMonthPOCLevel.Enabled || PrevMonthVWAPLevel.Enabled || PrevMonthVAHLevel.Enabled || PrevMonthVALLevel.Enabled;
    }

    private bool NeedsContractData()
    {
        return ContractOpenLevel.Enabled || ContractHighLevel.Enabled || ContractLowLevel.Enabled || ContractCloseLevel.Enabled ||
               ContractEquilibriumLevel.Enabled || ContractPOCLevel.Enabled || ContractVWAPLevel.Enabled || ContractVAHLevel.Enabled || ContractVALLevel.Enabled;
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

    private void RenderLevel(RenderContext context, List<LabelData> labels, string levelKey, LevelSettings levelSettings)
    {
        if (!_allLevelsVisible || !levelSettings.Enabled || !_levels.TryGetValue(levelKey, out var level) || !level.IsValid)
            return;
            
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

        // Get pen from LevelSettings
        var renderPen = levelSettings.RenderPen;

        // Draw line first (if LineType != None)
        switch (levelSettings.LineType)
        {
            case LineType.Bar:
                // Bar labels and their lines share the final connector anchor, computed
                // after measuring all names. Other positions keep their original line start.
                if (levelSettings.LabelPosition != LabelPosition.Bar)
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
            var priceText = string.Format(ChartInfo.StringFormat, level.Price);
            var priceHeight = context.MeasureString(priceText, _axisFont).Height + 2;
            var background = levelSettings.Color.Convert();
            var foreground = (levelSettings.TextColor ?? GetContrastingColor(levelSettings.Color)).Convert();
            void DrawPrice(RenderContext target, int placedY) => DrawPriceLabel(target, priceText, y, placedY, background, foreground);

            if (ChartInfo is not IPriceAxisLabelRenderer renderer || !renderer.TryEnqueuePriceAxisLabel(y, priceHeight, DrawPrice))
                DrawPrice(context, y);
        }

        // Draw text label (if LabelPosition != None)
        switch (levelSettings.LabelPosition)
        {
            case LabelPosition.Bar:
                var barLabelX = currentBarRightX + 5;
                AddTextLabel(context, labels, level.Label, level.Price, barLabelX, y, renderPen, levelSettings.TextColor,
                    LabelPosition.Bar, currentBarRightX, levelSettings.LineType == LineType.Bar);
                break;
            case LabelPosition.Right:
                var rightLabelX = chartWidth - 5;
                AddTextLabel(context, labels, level.Label, level.Price, rightLabelX, y, renderPen, levelSettings.TextColor, LabelPosition.Right);
                break;
            case LabelPosition.Left:
                var leftLabelX = 5;
                AddTextLabel(context, labels, level.Label, level.Price, leftLabelX, y, renderPen, levelSettings.TextColor, LabelPosition.Left);
                break;
            case LabelPosition.None:
                // No text label to draw
                break;
        }
    }

    private void DrawPriceLabel(RenderContext context, string text, int originalY, int y, Color background, Color foreground)
    {
        if (originalY != y)
        {
            var clip = context.ClipBounds;
            context.ResetClip();
            try
            {
                var x = ChartInfo.PriceChartContainer.Region.Width + 1;
                var connector = new RenderPen(background);
                context.DrawLine(connector, x, originalY, x, y);
            }
            finally
            {
                context.SetClip(clip);
            }
        }

        this.DrawLabelOnPriceAxis(context, text, y, _axisFont, background, foreground);
    }

    private void AddTextLabel(RenderContext context, List<LabelData> labels, string text, decimal price, int x, int y,
        RenderPen pen, CrossColor? textColor, LabelPosition position, int lineStartX = 0, bool startsLine = false)
    {
        var size = context.MeasureString(text, _font);
        var inset = (int)Math.Ceiling(pen.Width / 2);
        var width = size.Width + 4 + 2 * inset;
        var chartWidth = ChartInfo.PriceChartContainer.Region.Width;
        if (width > chartWidth)
        {
            if (startsLine)
                context.DrawLine(pen, Math.Clamp(lineStartX, 0, chartWidth), y, chartWidth, y);
            return;
        }

        var alignRight = position == LabelPosition.Right;
        var rectX = alignRight ? x - size.Width : x;
        rectX = Math.Clamp(rectX, 2 + inset, chartWidth - size.Width - 2 - inset);
        var bounds = new Rectangle(rectX - 2 - inset, y - size.Height / 2 - 1 - inset,
            width, size.Height + 2 + 2 * inset);
        var edge = alignRight ? rectX - 2 - inset : rectX + size.Width + 2 + inset;
        labels.Add(new LabelData
        {
            Bounds = bounds,
            DesiredY = y,
            Price = price,
            Position = position,
            AnchorX = Math.Clamp(edge + (alignRight ? -4 : 4), 0, chartWidth),
            LineStartX = Math.Clamp(lineStartX, 0, chartWidth),
            DrawLevelLine = startsLine ? (target, anchor) => target.DrawLine(pen, anchor, y, chartWidth, y) : null,
            DrawConnector = (target, placedY, anchor) =>
            {
                if (placedY != y || startsLine)
                    target.DrawLine(new RenderPen(pen.Color), anchor, y, edge, placedY);
            },
            Draw = (target, placedY) => DrawTextLabel(target, text, alignRight ? rectX + size.Width : rectX, placedY, pen, textColor, alignRight)
        });
    }

    private void DrawLabels(RenderContext context, List<LabelData> labels, int height)
    {
        // Equal-price labels keep their collection order. Only horizontally intersecting
        // rectangles constrain each other, so independent left/right labels stay at price.
        var selected = new List<LabelData>();
        foreach (var label in labels.OrderBy(label => label.DesiredY).ThenByDescending(label => label.Price))
        {
            var top = 0;
            foreach (var previous in selected)
            {
                if (previous.Bounds.Left < label.Bounds.Right && label.Bounds.Left < previous.Bounds.Right)
                    top = Math.Max(top, previous.Bounds.Bottom + 1);
            }

            // Reserve space using compact placement first. If even that does not fit,
            // retain the higher-price labels and keep every underlying level line.
            if (top + label.Bounds.Height > height)
                continue;

            label.Bounds = new Rectangle(label.Bounds.X, top, label.Bounds.Width, label.Bounds.Height);
            selected.Add(label);
        }

        for (var i = 0; i < selected.Count; i++)
        {
            var label = selected[i];
            var top = Math.Max(0, label.DesiredY - label.Bounds.Height / 2);
            for (var j = 0; j < i; j++)
            {
                var previous = selected[j];
                if (previous.Bounds.Left < label.Bounds.Right && label.Bounds.Left < previous.Bounds.Right)
                    top = Math.Max(top, previous.Bounds.Bottom + 1);
            }

            label.Bounds = new Rectangle(label.Bounds.X, top, label.Bounds.Width, label.Bounds.Height);
        }

        for (var i = selected.Count - 1; i >= 0; i--)
        {
            var label = selected[i];
            var top = Math.Min(label.Bounds.Top, height - label.Bounds.Height);
            for (var j = i + 1; j < selected.Count; j++)
            {
                var next = selected[j];
                if (next.Bounds.Left < label.Bounds.Right && label.Bounds.Left < next.Bounds.Right)
                    top = Math.Min(top, next.Bounds.Top - 1 - label.Bounds.Height);
            }

            label.Bounds = new Rectangle(label.Bounds.X, top, label.Bounds.Width, label.Bounds.Height);
        }

        var barAnchor = Math.Min(ChartInfo.PriceChartContainer.Region.Width,
            selected.Where(label => label.Position == LabelPosition.Bar).Select(label => label.Bounds.Right).DefaultIfEmpty(0).Max() + 4);
        foreach (var label in labels)
            label.DrawLevelLine?.Invoke(context, selected.Contains(label) ? barAnchor : label.LineStartX);

        foreach (var label in selected)
            label.DrawConnector(context, label.Bounds.Top + label.Bounds.Height / 2,
                label.Position == LabelPosition.Bar ? barAnchor : label.AnchorX);

        foreach (var label in selected)
            label.Draw(context, label.Bounds.Top + label.Bounds.Height / 2);
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

    private void DrawTextLabel(RenderContext context, string text, int x, int y, RenderPen pen, CrossColor? explicitTextColor, bool alignRight)
    {
        var size = context.MeasureString(text, _font);
        var backgroundColor = ChartInfo.ColorsStore.BaseBackgroundColor;
        // An explicit text color is used as is; Auto contrasts with the chart background the label sits on
        var textColor = explicitTextColor ?? GetContrastingColor(backgroundColor.Convert());

        // Calculate rectangle position based on alignment
        var rectX = alignRight ? x - size.Width : x;
        var rect = new Rectangle(rectX - 2, y - size.Height / 2 - 1, size.Width + 4, size.Height + 2);

        // Draw background with border
        context.FillRectangle(backgroundColor, rect);
        context.DrawRectangle(pen, rect);

        // Draw text
        var textRect = new Rectangle(rectX, y - size.Height / 2, size.Width, size.Height);
        var format = alignRight ? _stringRightFormat : _stringLeftFormat;
        context.DrawString(text, _font, textColor.Convert(), textRect, format);
    }

    private void RenderLevelGroup(RenderContext context, List<LabelData> labels, string prefix,
      LevelSettings openLevel, LevelSettings highLevel, LevelSettings lowLevel, LevelSettings closeLevel,
      LevelSettings eqLevel, LevelSettings pocLevel, LevelSettings vwapLevel, LevelSettings vahLevel, LevelSettings valLevel)
    {
        var keys = _keys[PeriodFromPrefix(prefix)];
        // 0 Open, 1 High, 2 Low, 3 Close, 4 EQ, 5 POC, 6 VWAP, 7 VAH, 8 VAL

        RenderLevel(context, labels, keys[0], openLevel);
        RenderLevel(context, labels, keys[1], highLevel);
        RenderLevel(context, labels, keys[2], lowLevel);
        RenderLevel(context, labels, keys[3], closeLevel);
        RenderLevel(context, labels, keys[4], eqLevel);
        RenderLevel(context, labels, keys[5], pocLevel);
        RenderLevel(context, labels, keys[6], vwapLevel);
        RenderLevel(context, labels, keys[7], vahLevel);
        RenderLevel(context, labels, keys[8], valLevel);
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

    #endregion

    #endregion
}
