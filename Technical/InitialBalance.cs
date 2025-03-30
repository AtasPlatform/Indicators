namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Pen = System.Drawing.Pen;
using CrossColor = System.Windows.Media.Color;

[DisplayName("Initial Balance")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.InitialBalanceIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602294")]
public class InitialBalance : Indicator
{
	#region Nested types

	public enum PeriodType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Minutes))]
		Minutes,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Bars))]
		Bars
	}

	#endregion

	#region Fields

	// ==========================
    // Main price level series
    // ==========================

    private ValueDataSeries _ibh;
    private ValueDataSeries _ibhx1;
    private ValueDataSeries _ibhx2;
    private ValueDataSeries _ibhx3;
    private ValueDataSeries _ibl;
    private ValueDataSeries _iblx1;
    private ValueDataSeries _iblx2;
    private ValueDataSeries _iblx3;
    private ValueDataSeries _ibm;
    private ValueDataSeries _mid;

    // ==========================
    // Value area series (between price levels)
    // ==========================

    private RangeDataSeries _ibhx32;
    private RangeDataSeries _ibhx21;
    private RangeDataSeries _ibhx1h;
    private RangeDataSeries _ibHm;
    private RangeDataSeries _ibMl;
    private RangeDataSeries _ibl1;
    private RangeDataSeries _iblx12;
    private RangeDataSeries _iblx23;

    // ==========================
    // Style and display parameters
    // ==========================

    private CrossColor _borderColor = DefaultColors.Red.Convert();
    private CrossColor _fillColor = DefaultColors.Yellow.Convert();
    private int _borderWidth = 1;
    private RenderFont _font;
    private float _fontSize = 12.0f;
    private bool _drawText = true;
    private bool _calculate;
    private bool _customSessionStart;
    private bool _highLowIsSet;
    private bool _initialized;
    private bool _isStarted;

    // ==========================
    // Time and session management
    // ==========================

    private int _days = 20;
    private int _period = 60;
    private TimeSpan _startDate = new(9, 0, 0);
    private TimeSpan _endDate;
    private DateTime _endTime = DateTime.MaxValue;
    private PeriodType _periodMode = PeriodType.Minutes;
    private DrawingRectangle _rectangle = new(0, 0, 0, 0, Pens.Gray, new SolidBrush(DefaultColors.Yellow));

    // ==========================
    // Bar and value tracking
    // ==========================

    private int _lastStartBar = -1;
    private int _lastEndBar = -1;
    private int _targetBar;
    private decimal _maxValue = decimal.MinValue;
    private decimal _minValue = decimal.MaxValue;
    private decimal _ibMax = decimal.MinValue;
    private decimal _ibMin = decimal.MaxValue;
    private decimal _ibmValue;
    private decimal mid;
    private decimal ibhx1;
    private decimal ibhx2;
    private decimal ibhx3;
    private decimal iblx1;
    private decimal iblx2;
    private decimal iblx3;

    // ==========================
    // Range multipliers (IBHX / IBLX)
    // ==========================
    private decimal _x1 = 1m;
    private decimal _x2 = 2m;
    private decimal _x3 = 3m;

    // ==========================
    // Advanced visual configuration
    // ==========================

    private bool _showOpenRange = false;
    private bool _showDuringFormation = false;
    private readonly Dictionary<ValueDataSeries, CrossColor> _originalColors = new();

    // ==========================
    // Custom session management
    // ==========================

    private class Session
    {
        public int StartBar;
        public int EndBar;
        public DateTime StartTime;
        public DateTime EndTime;
        public bool IsCalculationComplete;
    }

    private readonly List<Session> _sessions = new();
    private Session _currentSession;

    #endregion

    #region Properties

// Number of days to look back to display previous sessions.
[Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation),
    Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
[Range(0, 1000)]
public int Days
{
    get => _days;
    set
    {
        _days = value;
        RecalculateValues();
    }
}

// ==========================
// Visual configuration of the Open Range rectangle
// ==========================

// Show or hide the Open Range rectangle
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show),
    GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.ShowOpenRangeDescription), Order = 10)]
public bool ShowOpenRange
{
    get => _showOpenRange;
    set
    {
        _showOpenRange = value;

        if (!_showOpenRange)
        {
            // Clear rectangle from the chart
            Rectangles.Clear();
        }
        else
        {
            // Redraw the rectangle if there are valid values
            if (_lastEndBar > 0)
                DrawOpenRangeRectangle(_lastEndBar);
            else if (_calculate && ShowDuringFormation)
                DrawOpenRangeRectangle(CurrentBar - 1);
        }

        RecalculateValues();
        RedrawChart();
    }
}

// Border thickness of the Open Range rectangle
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderWidth),
    GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.BorderWidthPixelDescription), Order = 20)]
[Range(1, 100)]
public int BorderWidth
{
    get => _borderWidth;
    set
    {
        _borderWidth = value;
        RecalculateValues();
        RedrawChart();
    }
}

// Border color of the Open Range rectangle
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderColor),
    GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.BorderColorDescription), Order = 30)]
public CrossColor BorderColor
{
    get => _borderColor;
    set
    {
        _borderColor = value;
        RecalculateValues();
        RedrawChart();
    }
}

// Fill color of the Open Range rectangle
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FillColor),
    GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.FillColorDescription), Order = 40)]
public CrossColor FillColor
{
    get => _fillColor;
    set
    {
        _fillColor = value;
        RecalculateValues();
        RedrawChart();
    }
}

// ==========================
// Custom Session Configuration
// ==========================

// Enables or disables the use of a custom session
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession),
    GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.IsCustomSessionDescription), Order = 10)]
public bool CustomSessionStart
{
    get => _customSessionStart;
    set
    {
        _customSessionStart = value;
        RecalculateValues();
    }
}

// Start time of the custom session
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.StartTime),
    GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.StartTimeDescription), Order = 20)]
public TimeSpan StartDate
{
    get => _startDate;
    set
    {
        _startDate = value;
        RecalculateValues();
    }
}

// End time of the custom session
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.EndTime),
    GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.EndTimeDescription), Order = 20)]
public TimeSpan EndDate
{
    get => _endDate;
    set
    {
        _endDate = value;
        RecalculateValues();
    }
}

// ==========================
// Initial Balance Calculation Parameters
// ==========================

// Duration of the Initial Balance period
[Parameter]
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period),
    GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.PeriodDescription), Order = 30)]
[Range(1, 10000)]
public int Period
{
    get => _period;
    set
    {
        _period = value;
        RecalculateValues();
    }
}

// Type of Initial Balance period (minutes or bars)
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodType),
    GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.PeriodTypeDescription), Order = 40)]
public PeriodType PeriodMode
{
    get => _periodMode;
    set
    {
        _periodMode = value;
        RecalculateValues();
    }
}

// ==========================
// Initial Balance Expansion Multipliers
// ==========================

[Parameter]
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier1),
    GroupName = nameof(Strings.Multiplier), Description = nameof(Strings.MultiplierDescription), Order = 100)]
public decimal X1
{
    get => _x1;
    set
    {
        _x1 = value;
        RecalculateValues();
    }
}

[Parameter]
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier2),
    GroupName = nameof(Strings.Multiplier), Description = nameof(Strings.MultiplierDescription), Order = 110)]
public decimal X2
{
    get => _x2;
    set
    {
        _x2 = value;
        RecalculateValues();
    }
}

[Parameter]
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier3),
    GroupName = nameof(Strings.Multiplier), Description = nameof(Strings.MultiplierDescription), Order = 120)]
public decimal X3
{
    get => _x3;
    set
    {
        _x3 = value;
        RecalculateValues();
    }
}

// ==========================
// Visual settings for lines and labels
// ==========================

// Show labels at the end of lines
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Text),
    GroupName = nameof(Strings.Show), Description = nameof(Strings.IsNeedShowLabelDescription), Order = 130)]
public bool DrawText
{
    get => _drawText;
    set
    {
        _drawText = value;
        RecalculateValues();
        RedrawChart();
    }
}

// Font size for labels
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontSize),
   GroupName = nameof(Strings.Show), Description = nameof(Strings.TextSizeDescription), Order = 140)]
[Range(6, 48)]
public float FontSize
{
    get => _fontSize;
    set
    {
        _fontSize = value;
        _font = new RenderFont("Arial", _fontSize); // Updated when changed
        RecalculateValues();
    }
}

// Extend lines to the right edge of the chart
[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ExtendLast),
 GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ExtendLastDescription), Order = 150)]
public bool ExtendLastLineToRight { get; set; } = true;

// Show levels during Initial Balance formation
[Display(ResourceType = typeof(Strings), Name = "Show During Formation",
GroupName = nameof(Strings.Show), Description = "Show IB lines during first hour", Order = 160)]
public bool ShowDuringFormation
{
    get => _showDuringFormation;
    set
    {
        _showDuringFormation = value;
        // Update line background color
        UpdateSeriesVisibility();
        RecalculateValues();
        RedrawChart();
    }
}

// ==========================
// Background colors of Initial Balance expansion zones
// ==========================

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX32),
GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 200)]
public CrossColor Ibhx32
{
    get => _ibhx32.RangeColor;
    set => _ibhx32.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX21),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 210)]
public CrossColor Ibhx21
{
    get => _ibhx21.RangeColor;
    set => _ibhx21.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX1H),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 220)]
public CrossColor Ibhx1h
{
    get => _ibhx1h.RangeColor;
    set => _ibhx1h.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHM),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 230)]
public CrossColor IbHm
{
    get => _ibHm.RangeColor;
    set => _ibHm.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBML),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 240)]
public CrossColor IbMl
{
    get => _ibMl.RangeColor;
    set => _ibMl.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBL1),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 250)]
public CrossColor Ibl1
{
    get => _ibl1.RangeColor;
    set => _ibl1.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBLX12),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 260)]
public CrossColor Iblx12
{
    get => _iblx12.RangeColor;
    set => _iblx12.RangeColor = value;
}

[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBLX23),
    GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 270)]
public CrossColor Iblx23
{
    get => _iblx23.RangeColor;
    set => _iblx23.RangeColor = value;
}

#endregion

	
   #region ctor

public InitialBalance()
    : base(true)
{
    DenyToChangePanel = true;
    EnableCustomDrawing = true;
    SubscribeToDrawingEvents(DrawingLayouts.Final);
    _font = new RenderFont("Arial", _fontSize);

    // ==========================
    // Initialization of ValueDataSeries
    // ==========================

    _mid = CreateValueSeries("Mid", CrossColor.FromArgb(0, 0, 255, 0), LineDashStyle.Solid);
    _ibh = CreateValueSeries("IBH", DefaultColors.Blue.Convert());
    _ibl = CreateValueSeries("IBL", DefaultColors.Red.Convert());
    _ibm = CreateValueSeries("IBM", DefaultColors.Green.Convert());
    _ibhx1 = CreateValueSeries("IBHX1", DefaultColors.Fuchsia.Convert());
    _ibhx2 = CreateValueSeries("IBHX2", DefaultColors.Fuchsia.Convert());
    _ibhx3 = CreateValueSeries("IBHX3", DefaultColors.Fuchsia.Convert());
    _iblx1 = CreateValueSeries("IBLX1", DefaultColors.Purple.Convert());
    _iblx2 = CreateValueSeries("IBLX2", DefaultColors.Purple.Convert());
    _iblx3 = CreateValueSeries("IBLX3", DefaultColors.Purple.Convert());

    // Add ValueDataSeries to the indicator's DataSeries collection
    DataSeries[0] = _mid;
    DataSeries.AddRange(new[]
    {
        _ibh, _ibl, _ibm,
        _ibhx1, _ibhx2, _ibhx3,
        _iblx1, _iblx2, _iblx3
    });

    // ==========================
    // Initialization of RangeDataSeries
    // ==========================

    _ibhx32 = CreateRangeSeries("IBHX32");
    _ibhx21 = CreateRangeSeries("IBHX21");
    _ibhx1h = CreateRangeSeries("IBHX1H");
    _ibHm = CreateRangeSeries("IBHM");
    _ibMl = CreateRangeSeries("IBML");
    _ibl1 = CreateRangeSeries("IBL1");
    _iblx12 = CreateRangeSeries("IBLX12");
    _iblx23 = CreateRangeSeries("IBLX23");

    // Add RangeDataSeries to the indicator's DataSeries collection
    DataSeries.AddRange(new[]
    {
        _ibhx32, _ibhx21, _ibhx1h,
        _ibHm, _ibMl, _ibl1,
        _iblx12, _iblx23
    });

    // ==========================
    // Subscribe to property changes
    // ==========================

    foreach (var series in new[]
    {
        _ibh, _ibl, _ibm,
        _ibhx1, _ibhx2, _ibhx3,
        _iblx1, _iblx2, _iblx3
    })
    {
        _originalColors[series] = series.Color;
        series.PropertyChanged += DataSeriesPropertyChanged;
    }
}

#endregion


	#region Protected methods

protected override void OnCalculate(int bar, decimal value)
{
    if (bar == 0)
    {
        // Initializes all variables to start fresh from the first bar.
        ResetState();

        // Sets the first bar to start calculations based on the 'Days' parameter.
        InitializeTargetBar();
    }

    // Ignores bars prior to the target point and retrieves the current local candle time.
    if (bar < _targetBar)
        return;

    _initialized = true;

    // Retrieves the current candle and its local time.
    var candle = GetLocalCandle(bar, out var time, out var lastTime);

    // Manages custom session logic, identifying when sessions start and end.
    var candleDateTime = candle.Time.AddHours(InstrumentInfo.TimeZone);
    UpdateCustomSession(candleDateTime, bar);

    // Checks if a new calculation should start.
    var isStart = ShouldStartNewCalculation(bar, time, lastTime, candleDateTime);

    // Checks if the current calculation should end.
    var isEnd = ShouldEndCurrentCalculation(bar, candleDateTime);

    // Handles the appropriate logic depending on the session state.
    if (isStart)
    {
        BeginCalculationWindow(bar, candleDateTime);
    }
    else if (isEnd)
    {
        EndCalculationWindow(bar);
    }

    if (_calculate)
    {
        // Updates the high and low for the Initial Balance.
        UpdateIbHighLow(candle);

        // Updates the Initial Balance rectangle coordinates.
        UpdateRectangleDuringCalculation(bar);

        // Updates visibility of the value lines.
        UpdateSeriesVisibility();

        // Redraws the Initial Balance area in real time if ShowDuringFormation is active.
        if (ShowOpenRange && ShowDuringFormation)
            RedrawChart();
    }

    // Updates the session high and low (not just the IB window).
    UpdateSessionHighLow(candle);

    if (!_highLowIsSet)
        return;

    // Calculates and stores the Initial Balance levels.
    CalculateIbLevels(bar);

    // Fills in the Value Areas between levels.
    FillValueAreas(bar);
}

protected override void OnRender(RenderContext context, DrawingLayouts layout)
{
    if (_lastStartBar < 0 || !_initialized)
        return;

    // Do not render anything during formation if ShowDuringFormation is disabled.
    if (_calculate && !ShowDuringFormation)
        return;

    // Define the initial point: either the last completed calculation or the most recent bar.
    var startBar = _lastEndBar > 0 ? _lastEndBar : CurrentBar - 1;

    // Define the endpoint: either the right edge of the chart or the latest candle.
    var x1 = ChartInfo.GetXByBar(startBar, false);
    var x2 = ExtendLastLineToRight && !_calculate
        ? context.ClipBounds.Right
        : ChartInfo.GetXByBar(CurrentBar - 1, false);

    // If for any reason x2 is invalid (e.g., overlaps or precedes x1), abort rendering.
    if (!_calculate && (x1 < 0 || x2 <= x1))
        return;

    // Draws Initial Balance horizontal level lines.
    DrawIBLines(context, ChartInfo.GetXByBar(CurrentBar - 1, false), x2);

    // Optionally draw level labels at the end of the lines.
    if (DrawText)
    {
        DrawIBLabels(context, x2);
    }

    // Optionally draw the Initial Balance rectangle if it's enabled.
    if (ShowOpenRange)
    {
        var barToDraw = _lastEndBar > 0 ? _lastEndBar : (_calculate && ShowDuringFormation ? CurrentBar : -1);

        if (barToDraw >= 0)
            DrawOpenRangeRectangle(barToDraw);
    }
}

#endregion

#region Private methods

private DateTime GetPrevDateTime(int bar)
{
    return GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
}

private System.Drawing.Drawing2D.DashStyle ConvertDashStyle(LineDashStyle style)
{
    return style switch
    {
        LineDashStyle.Solid => DashStyle.Solid,
        LineDashStyle.Dash => DashStyle.Dash,
        LineDashStyle.Dot => DashStyle.Dot,
        LineDashStyle.DashDot => DashStyle.DashDot,
        LineDashStyle.DashDotDot => DashStyle.DashDotDot,
        _ => DashStyle.Solid
    };
}

private void DataSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (!_initialized)
        return;

    // If the user changes the color of a line from the UI, save it as the original color.
    if (sender is ValueDataSeries series && e.PropertyName == nameof(ValueDataSeries.Color))
    {
        _originalColors[series] = series.Color;
    }

    RecalculateValues();
}

private System.Drawing.Color ConvertColor(CrossColor color)
{
    return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}
	
/// <summary>
/// Displays a message on the chart, typically used for debugging or information.
/// </summary>
private void DrawMessage(RenderContext context, string message)
{
    var font = new RenderFont("Arial", 12);
    var textSize = context.MeasureString(message, font);

    // Fixed position at bottom left corner
    var x = 10;
    var y = (int)context.ClipBounds.Bottom - (int)textSize.Height - 10;

    // Black background with margin
    var backgroundRect = new Rectangle(x - 5, y - 2, (int)textSize.Width + 10, (int)textSize.Height + 4);
    context.FillRectangle(System.Drawing.Color.Black, backgroundRect);

    // Red text
    context.DrawString(message, font, System.Drawing.Color.Red, x, y);
}

/// <summary>
/// Creates and returns a new ValueDataSeries with default visual settings.
/// </summary>
private static ValueDataSeries CreateValueSeries(string name, CrossColor color, LineDashStyle dash = LineDashStyle.Dash)
{
    return new ValueDataSeries(name)
    {
        Color = color,
        LineDashStyle = dash,
        VisualType = VisualMode.Square,
        Width = 1
    };
}

/// <summary>
/// Creates and returns a new RangeDataSeries with default settings.
/// </summary>
private static RangeDataSeries CreateRangeSeries(string name)
{
    return new RangeDataSeries(name)
    {
        IsHidden = true,
        DrawAbovePrice = false,
        RangeColor = System.Drawing.Color.Transparent.Convert()
    };
}

/// <summary>
/// Resets the high/low level tracking variables to their default values.
/// </summary>
private void ResetLevels()
{
    _maxValue = decimal.MinValue;
    _minValue = decimal.MaxValue;
    _ibMax = decimal.MinValue;
    _ibMin = decimal.MaxValue;
    _ibmValue = mid = ibhx1 = ibhx2 = ibhx3 = iblx1 = iblx2 = iblx3 = decimal.Zero;
}

/// <summary>
/// Clears all internal state, data series, and session tracking variables.
/// </summary>
private void ResetState()
{
    _sessions.Clear();
    _currentSession = null;
    DataSeries.ForEach(x => x.Clear());
    ResetLevels();
    _highLowIsSet = false;
    _calculate = false;
    _isStarted = false;
    _initialized = false;
    _lastStartBar = -1;
    _lastEndBar = -1;
    _targetBar = 0;
    _endTime = DateTime.MaxValue;
}

/// <summary>
/// Identifies the oldest bar to be considered based on the 'Days' parameter.
/// </summary>
private void InitializeTargetBar()
{
    if (_days <= 0)
        return;

    int daysCounted = 0;

    for (int i = CurrentBar - 1; i >= 0; i--)
    {
        _targetBar = i;

        if (!IsNewSession(i))
            continue;

        daysCounted++;
        if (daysCounted == _days)
            break;
    }
}

/// <summary>
/// Tracks custom sessions by checking start/end time of each candle.
/// </summary>
private void UpdateCustomSession(DateTime dateTime, int bar)
{
    if (!CustomSessionStart)
        return;

    var sessionStart = dateTime.Date + StartDate;
    var sessionEnd = dateTime.Date + EndDate;

    if (EndDate < StartDate)
        sessionEnd = sessionEnd.AddDays(1);

    if (_currentSession == null || dateTime >= _currentSession.EndTime)
    {
        if (dateTime >= sessionStart && dateTime < sessionEnd)
        {
            _currentSession = new Session
            {
                StartBar = bar,
                StartTime = sessionStart,
                EndTime = sessionEnd,
                EndBar = -1,
                IsCalculationComplete = false
            };

            _sessions.Add(_currentSession);
        }
    }
    else if (dateTime < _currentSession.StartTime)
    {
        _currentSession = null;
    }

    if (_currentSession != null && dateTime >= _currentSession.EndTime && _currentSession.EndBar == -1)
    {
        _currentSession.EndBar = bar;
    }
}

/// <summary>
/// Returns the candle at given bar with local timestamp information.
/// </summary>
private IndicatorCandle GetLocalCandle(int bar, out TimeSpan time, out TimeSpan lastTime)
{
    var candle = GetCandle(bar);
    var localTime = candle.Time.AddHours(InstrumentInfo.TimeZone);
    var localLastTime = candle.LastTime.AddHours(InstrumentInfo.TimeZone);
    time = localTime.TimeOfDay;
    lastTime = localLastTime.TimeOfDay;
    return candle;
}

/// <summary>
/// Determines if a new calculation window should be started.
/// </summary>
private bool ShouldStartNewCalculation(int bar, TimeSpan time, TimeSpan lastTime, DateTime currentDateTime)
{
    if (_isStarted)
        return false;

    if (CustomSessionStart)
    {
        var prevDateTime = GetPrevDateTime(bar);
        return bar != 0
            && (time >= StartDate || lastTime >= StartDate)
            && (prevDateTime.TimeOfDay < StartDate || prevDateTime.Date < currentDateTime.Date);
    }

    return IsNewSession(bar);
}

/// <summary>
/// Determines if the calculation window should be closed.
/// </summary>
private bool ShouldEndCurrentCalculation(int bar, DateTime currentDateTime)
{
    if (!_isStarted)
        return false;

    return (PeriodMode == PeriodType.Minutes && currentDateTime >= _endTime && GetPrevDateTime(bar) < _endTime)
        || (PeriodMode == PeriodType.Bars && bar - _lastStartBar >= Period);
}

/// <summary>
/// Initializes a new calculation window, resets high/low tracking and ends previous lines.
/// </summary>
private void BeginCalculationWindow(int bar, DateTime candleTime)
{
    _calculate = true;
    _highLowIsSet = false;
    _lastStartBar = bar;
    _endTime = candleTime.AddMinutes(_period);
    _isStarted = true;

    ResetLevels();
    EndPreviousValueLines(bar);

    if (ShowOpenRange && ShowDuringFormation)
        DrawOpenRangeRectangle(bar);
}

/// <summary>
/// Finalizes the calculation window and forces chart redraw.
/// </summary>
private void EndCalculationWindow(int bar)
{
    _calculate = false;
    _isStarted = false;
    _lastEndBar = bar;
    UpdateSeriesVisibility();
    RedrawChart();
}

/// <summary>
/// Ends all lines from the previous session visually at a specific bar.
/// </summary>
private void EndPreviousValueLines(int bar)
{
    foreach (var series in DataSeries.OfType<ValueDataSeries>())
        series.SetPointOfEndLine(bar - 1);
}

/// <summary>
/// Updates the highest and lowest values during the Initial Balance window.
/// </summary>
private void UpdateIbHighLow(IndicatorCandle candle)
{
    if (candle.High > _maxValue)
    {
        _highLowIsSet = true;
        _ibMax = _maxValue = candle.High;
    }

    if (candle.Low < _minValue)
    {
        _highLowIsSet = true;
        _ibMin = _minValue = candle.Low;
    }
}

/// <summary>
/// Dynamically adjusts the rectangle's coordinates based on the current candle.
/// </summary>
private void UpdateRectangleDuringCalculation(int bar)
{
    if (!_calculate)
        return;

    _rectangle.SecondBar = bar;
    _rectangle.FirstPrice = _ibMax;
    _rectangle.SecondPrice = _ibMin;
}

/// <summary>
/// Updates the session's high and low values.
/// </summary>
private void UpdateSessionHighLow(IndicatorCandle candle)
{
    if (candle.High > _maxValue)
        _maxValue = candle.High;

    if (candle.Low < _minValue)
        _minValue = candle.Low;
}

/// <summary>
/// Dynamically updates the coordinates of the opening range rectangle during the calculation period.
/// It runs whenever the calculation is active, regardless of whether the user has enabled its visualization.
/// </summary>
/// <param name="bar">Current calculation bar.</param>
private void UpdateRectangleDuringCalculation(int bar)
{
    if (!_calculate)
        return;

    _rectangle.SecondBar = bar;
    _rectangle.FirstPrice = _ibMax;
    _rectangle.SecondPrice = _ibMin;
}

// Updates the session high and low
private void UpdateSessionHighLow(IndicatorCandle candle)
{
    if (candle.High > _maxValue)
        _maxValue = candle.High;

    if (candle.Low < _minValue)
        _minValue = candle.Low;
}

// Calculates the initial balance levels
private void CalculateIbLevels(int bar)
{
    _mid[bar] = mid = (_minValue + _maxValue) / 2m;
    _ibh[bar] = _ibMax;
    _ibl[bar] = _ibMin;
    _ibmValue = _ibm[bar] = (_ibMin + _ibMax) / 2m;

    var diff = _ibMax - _ibMin;
    ibhx1 = _ibhx1[bar] = _ibMax + diff * _x1;
    ibhx2 = _ibhx2[bar] = _ibMax + diff * _x2;
    ibhx3 = _ibhx3[bar] = _ibMax + diff * _x3;
    iblx1 = _iblx1[bar] = _ibMin - diff * _x1;
    iblx2 = _iblx2[bar] = _ibMin - diff * _x2;
    iblx3 = _iblx3[bar] = _ibMin - diff * _x3;
}

// Fills the value areas
private void FillValueAreas(int bar)
{
    _ibhx32[bar].Upper = ibhx3;
    _ibhx32[bar].Lower = _ibhx21[bar].Upper = ibhx2;
    _ibhx21[bar].Lower = _ibhx1h[bar].Upper = ibhx1;
    _ibhx1h[bar].Lower = _ibHm[bar].Upper = _ibh[bar];
    _ibHm[bar].Lower = _ibMl[bar].Upper = _ibm[bar];
    _ibMl[bar].Lower = _ibl1[bar].Upper = _ibl[bar];
    _ibl1[bar].Lower = _iblx12[bar].Upper = iblx1;
    _iblx12[bar].Lower = _iblx23[bar].Upper = iblx2;
    _iblx23[bar].Lower = iblx3;
}

// Draws the Initial Balance lines
private void DrawIBLines(RenderContext context, int x1, int x2)
{
    var levels = new[]
    {
        ("IBH", _ibh.Color, _ibh.LineDashStyle, _ibh.Width, _ibMax),
        ("IBL", _ibl.Color, _ibl.LineDashStyle, _ibl.Width, _ibMin),
        ("IBM", _ibm.Color, _ibm.LineDashStyle, _ibm.Width, _ibmValue),
        ("MID", _mid.Color, _mid.LineDashStyle, _mid.Width, mid),
        ("IBHX1", _ibhx1.Color, _ibhx1.LineDashStyle, _ibhx1.Width, ibhx1),
        ("IBHX2", _ibhx2.Color, _ibhx2.LineDashStyle, _ibhx2.Width, ibhx2),
        ("IBHX3", _ibhx3.Color, _ibhx3.LineDashStyle, _ibhx3.Width, ibhx3),
        ("IBLX1", _iblx1.Color, _iblx1.LineDashStyle, _iblx1.Width, iblx1),
        ("IBLX2", _iblx2.Color, _iblx2.LineDashStyle, _iblx2.Width, iblx2),
        ("IBLX3", _iblx3.Color, _iblx3.LineDashStyle, _iblx3.Width, iblx3)
    };

    const int offset = 2;

    foreach (var (label, color, dash, width, price) in levels)
    {
        var y = (int)ChartInfo.GetYByPrice(price, false);
        if (y < 0)
            continue;

        var pen = new RenderPen(ConvertColor(color), width, ConvertDashStyle(dash));
        context.DrawLine(pen, x1, y, x2, y);
    }
}

// Draws the labels for the Initial Balance lines
private void DrawIBLabels(RenderContext context, int x2)
{
    const int offset = 2;

    var labels = new[]
    {
        ("IBH", _ibh.Color, _ibMax),
        ("IBL", _ibl.Color, _ibMin),
        ("IBM", _ibm.Color, _ibmValue),
        ("MID", _mid.Color, mid),
        ("IBHX1", _ibhx1.Color, ibhx1),
        ("IBHX2", _ibhx2.Color, ibhx2),
        ("IBHX3", _ibhx3.Color, ibhx3),
        ("IBLX1", _iblx1.Color, iblx1),
        ("IBLX2", _iblx2.Color, iblx2),
        ("IBLX3", _iblx3.Color, iblx3)
    };

    foreach (var (label, color, price) in labels)
    {
        var y = ChartInfo.GetYByPrice(price, false);
        if (y < 0)
            continue;

        var size = context.MeasureString(label, _font);
        context.DrawString(label, _font, ConvertColor(color),
            x2 - size.Width - 2,
            y - size.Height - offset);
    }
}

// Draws the opening range rectangle
private void DrawOpenRangeRectangle(int bar)
{
    if (!ShowOpenRange || _lastStartBar < 0)
        return;

    var pen = new Pen(ConvertColor(_borderColor)) { Width = _borderWidth };
    var brush = new SolidBrush(ConvertColor(_fillColor));

    var rect = new DrawingRectangle(
        _lastStartBar, _ibMax,
        bar, _ibMin,
        pen, brush
    );

    // Remove any previous rectangle
    Rectangles.Clear();

    // Add the new rectangle
    Rectangles.Add(rect);
}

private void UpdateSeriesVisibility()
{
    if (_lastStartBar < 0)
        return;

    for (var bar = _lastStartBar; bar <= CurrentBar; bar++)
    {
        foreach (var s in _originalColors.Keys)
        {
            s.Colors[bar] = ShowDuringFormation || !_calculate
                ? ConvertColor(_originalColors[s])
                : System.Drawing.Color.Transparent;
        }
    }
}
 
 #endregion
}
