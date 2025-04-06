namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

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
     // IB levels structure per session
     // ==========================
 
     public class IBLevels
 	{
     	public int StartBarLine;
     	public int StartBarRectangle;
     	public int EndBar;
     	public decimal IBH, IBL, IBM, MID;
     	public decimal IBHX1, IBHX2, IBHX3;
     	public decimal IBLX1, IBLX2, IBLX3;
 	}
  
    private Dictionary<Session, IBLevels> _sessionIBValues = new();

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
    private bool _waitingForNextSession = false;

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
        else if (_currentSession != null)
	{
    		if (!_sessionIBValues.ContainsKey(_currentSession))
    		{
        		_sessionIBValues[_currentSession] = new IBLevels
        		{
            			StartBarRectangle = _lastStartBar,
            			StartBarLine = CurrentBar - 1,
            			IBH = _ibMax,
            			IBL = _ibMin,
            			IBM = _ibmValue,
            			MID = mid,
            			IBHX1 = ibhx1,
            			IBHX2 = ibhx2,
            			IBHX3 = ibhx3,
            			IBLX1 = iblx1,
            			IBLX2 = iblx2,
            			IBLX3 = iblx3
        		};
    		}

    		if (_currentSession.EndBar > 0)
        		DrawOpenRangeRectangle(_currentSession.EndBar, _sessionIBValues[_currentSession]);
    		else if (_calculate && ShowDuringFormation)
        		DrawOpenRangeRectangle(CurrentBar - 1, _sessionIBValues[_currentSession]);
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

 	if (!_showDuringFormation && _currentSession != null)
	{
    		foreach (var series in DataSeries.OfType<ValueDataSeries>())
        		series.SetPointOfEndLine(_currentSession.StartBar - 1);

    		// 🔥 Borra valores temporales si no ha terminado la sesión
    		// 🔥 Deletes temporary values if the session hasn't ended
    		if (_currentSession.EndBar <= 0)
        		_sessionIBValues.Remove(_currentSession);
	}

	// ✅ NUEVO: guarda temporalmente niveles actuales en _sessionIBValues
	// ✅ NEW: temporarily stores current levels in _sessionIBValues
	if (_showDuringFormation && _currentSession != null && !_sessionIBValues.ContainsKey(_currentSession))
	{
    		_sessionIBValues[_currentSession] = new IBLevels
    		{
        		StartBarRectangle = _lastStartBar,
        		StartBarLine = CurrentBar - 1,
        		IBH = _ibMax,
        		IBL = _ibMin,
        		IBM = _ibmValue,
        		MID = mid,
        		IBHX1 = ibhx1,
        		IBHX2 = ibhx2,
        		IBHX3 = ibhx3,
        		IBLX1 = iblx1,
        		IBLX2 = iblx2,
        		IBLX3 = iblx3
    		};
	}

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
    if (!CustomSessionStart && _currentSession != null && _currentSession.EndBar == -1)
	{
    	if (candleDateTime >= _currentSession.EndTime)
    		{
        	_currentSession.EndBar = bar;
        	PrepareForNextSession();
    		}
	}

	if (CustomSessionStart)
    		UpdateCustomSession(candleDateTime, bar);
          
    	// Checks if we're waiting for the next session to begin.
    	if (_waitingForNextSession)
    		{
        	// Have we already entered the new session?
        	if (_currentSession != null && bar >= _currentSession.StartBar)
            		_waitingForNextSession = false;
        	else
            		return; // still waiting
    		}

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
        EndCalculationWindow(_currentSession, bar);
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
        _messageLineIndex = 0; // 🔄 Reinicia el índice de línea de mensajes de depuración al inicio del renderizado
                           // 🔄 Reset debug message line index at the start of rendering

    	// 🚫 Si no se ha inicializado correctamente o no hay barra válida, se sale
    	// 🚫 Exit if not properly initialized or no valid bar
    	if (_lastStartBar < 0 || !_initialized)
        	return;

    	// 🔁 Recorre todas las sesiones guardadas
    	// 🔁 Iterate through all saved sessions
    	foreach (var session in _sessions)
    	{
        	// ❓ Si no hay valores de Initial Balance para esta sesión, se muestra un mensaje de error y se salta
        	// ❓ If no Initial Balance data for this session, show error and skip
        	if (!_sessionIBValues.TryGetValue(session, out var ib))
        	{
            	//DrawMessage(context, $"[X] Session sin datos: SB {session.StartBar} / EB {session.EndBar}");
            	continue;
        	}

        	// ✅ Muestra un mensaje de depuración con info de la sesión actual
        	// ✅ Show debug message with current session info
        	//DrawMessage(context, $"[OK] Session SB {session.StartBar} | EB {session.EndBar} | IBRect {ib.StartBarRectangle}-{ib.EndBar} | CB {CurrentBar}");

        	// ⚠️ Si los datos del rectángulo de apertura no son válidos, se informa con un mensaje de advertencia
        	// ⚠️ Show warning if the Initial Balance rectangle data is invalid
        	//if (ib.EndBar < ib.StartBarRectangle || ib.EndBar <= 0 || ib.StartBarRectangle < 0)
            		//DrawMessage(context, $"⚠️ Rectángulo inválido: {ib.StartBarRectangle}-{ib.EndBar}");

        	// 📅 Calcula el rango visual de la sesión en coordenadas X de la pantalla
        	// 📅 Compute the visible X range of the session on screen
        	var startsession = _lastEndBar > 0 ? _lastEndBar : CurrentBar - 1;
        	int endsession = session.EndBar > 0 ? session.EndBar : CurrentBar - 1;
        	int xStart = ChartInfo.GetXByBar(startsession, false);
        	int xEnd = ChartInfo.GetXByBar(endsession, false);
        	bool sessionVisible = xEnd >= context.ClipBounds.Left && xStart <= context.ClipBounds.Right;

        	// 🧩 Dibuja la sesión si no es la sesión actual
        	// 🧩 Draw the session if it's not the current one
        	if (session != _currentSession)
        	{
            		DrawIBVisuals(context, session, drawLabels: DrawText, drawLines: true);
        	}
        
        	// ⏳ Si es la sesión actual, la dibuja solo si no está en cálculo o si está activada la opción de mostrar durante formación
        	// ⏳ If it's the current session, draw only if not in calculation or if ShowDuringFormation is enabled
        	else if ((!_calculate || ShowDuringFormation) && sessionVisible)
        	{
            		DrawIBVisuals(context, session, drawLabels: DrawText, drawLines: true);
        	}

        	// 🟦 Dibuja el rectángulo del rango de apertura si los valores son válidos y la opción está activada
        	// 🟦 Draw the Open Range rectangle if values are valid and option is enabled
        	if (ShowOpenRange && ib.EndBar > ib.StartBarRectangle && ib.EndBar <= CurrentBar)
            		DrawOpenRangeRectangle(ib.EndBar, ib);
    	}

    	// ❌ Si estamos en formación y no se debe mostrar nada, se detiene aquí el renderizado
    	// ❌ If in formation and ShowDuringFormation is disabled, skip rendering
    	bool sessionInFormation = _calculate && _currentSession?.EndBar <= 0;

    	if (sessionInFormation && !ShowDuringFormation)
        	return;

}

#endregion

#region Private methods

// Returns the datetime of the previous candle adjusted by the instrument's time zone
private DateTime GetPrevDateTime(int bar)
{
    return GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
}

// Converts platform's LineDashStyle to system drawing DashStyle
private System.Drawing.Drawing2D.DashStyle ConvertDashStyle(LineDashStyle style)
{
    return style switch
    {
        LineDashStyle.Solid => System.Drawing.Drawing2D.DashStyle.Solid,
	LineDashStyle.Dash => System.Drawing.Drawing2D.DashStyle.Dash,
	LineDashStyle.Dot => System.Drawing.Drawing2D.DashStyle.Dot,
	LineDashStyle.DashDot => System.Drawing.Drawing2D.DashStyle.DashDot,
	LineDashStyle.DashDotDot => System.Drawing.Drawing2D.DashStyle.DashDotDot,
	_ => System.Drawing.Drawing2D.DashStyle.Solid
    };
}

// Handles property changes in visual data series
private void DataSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
{
    if (!_initialized)
        return;

    // If the user changes the color of a line from the UI, save it as the original color.
    if (sender is ValueDataSeries series && e.PropertyName == nameof(ValueDataSeries.Color))
    {
        _originalColors[series] = series.Color;

 	for (int bar = _lastStartBar; bar <= CurrentBar; bar++)
	{
       		// If in formation and ShowDuringFormation is active, apply the color
    		if (_calculate && ShowDuringFormation)
        		series.Colors[bar] = ConvertColor(series.Color);
    		else
        		series.Colors[bar] = System.Drawing.Color.Transparent;
	}
 	RedrawChart();
    }

    RecalculateValues();
}

// Converts a custom CrossColor to a system Color
private System.Drawing.Color ConvertColor(CrossColor color)
{
    return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
}
	
/// <summary>
/// Displays a message on the chart, typically used for debugging or information.
/// </summary>
private int _messageLineIndex = 0;
private void DrawMessage(RenderContext context, string message)
{
    var font = new RenderFont("Arial", 12);
    var textSize = context.MeasureString(message, font);

    // Posición fija en la esquina inferior izquierda
    // Fixed position at bottom left corner
    var x = 10;
    var lineHeight = (int)textSize.Height + 4;
    var y = (int)context.ClipBounds.Bottom - ((_messageLineIndex + 1) * lineHeight) - 10;

    // Fondo negro con borde (por si hay confusión con el gráfico)
    // Black background with margin
    var backgroundRect = new Rectangle(x - 5, y - 2, (int)textSize.Width + 10, lineHeight);
    context.FillRectangle(System.Drawing.Color.Black, backgroundRect);

    // Dibuja el texto en rojo
    // Draw the message text in red
    context.DrawString(message, font, System.Drawing.Color.Red, x, y);

    _messageLineIndex++;
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
	// Initialized with a transparent background
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
    // Clear previous sessions
    _sessions.Clear();
    _currentSession = null;
    // Clear all data series
    DataSeries.ForEach(x => x.Clear());
    // Reset calculated levels
    ResetLevels();
    // Reset calculation flags and state
    _highLowIsSet = false;
    _calculate = false;
    _isStarted = false;
    _initialized = false;
    // Reset bar tracking variables
    _lastStartBar = -1;
    _lastEndBar = -1;
    _targetBar = 0;
    // Reset calculation window end time
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

	// Only count a new day if there’s a session change
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

    // If session crosses midnight, adjust EndDate to next day
    if (EndDate < StartDate)
        sessionEnd = sessionEnd.AddDays(1);

    // If no active session or the previous one has ended, attempt to start a new one
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
    
    // If candle is before the session start (possible in cross-midnight sessions), cancel the session
    else if (dateTime < _currentSession.StartTime)
    {
        _currentSession = null;
    }
    
    // If session end is reached, store EndBar and trigger completion logic
    if (_currentSession != null && dateTime >= _currentSession.EndTime && _currentSession.EndBar == -1)
    {
        _currentSession.EndBar = bar;
	PrepareForNextSession();
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

    // Reset max, min and derived levels
    ResetLevels(); 
               
    // Ends previously drawn lines from last session
    EndPreviousValueLines(bar);                             

	if (!CustomSessionStart)
	{
    		var sessionStart = GetCandle(bar).Time.Date;
		var sessionEnd = sessionStart.AddHours(23).AddMinutes(59).AddSeconds(59);

    		_currentSession = new Session
    		{
        		StartBar = bar,
        		EndBar = -1,
       		 	StartTime = sessionStart,
        		EndTime = sessionEnd
    		};

    	_sessions.Add(_currentSession);
	}
}

/// <summary>
/// Finalizes the calculation window and forces chart redraw.
/// </summary>
private void EndCalculationWindow(Session session, int bar)
{
    _calculate = false;
    _isStarted = false;
    _lastEndBar = bar;
    UpdateSeriesVisibility(); 
    RedrawChart(); 

    // Save calculated levels in session dictionary
    _sessionIBValues[session] = new IBLevels
	{
    		StartBarLine = bar,              // for the lines (from the end of the IB)
    		StartBarRectangle = _lastStartBar, // for the rectangle (from the start of the IB)
    		EndBar = bar,
    		IBH = _ibMax,
    		IBL = _ibMin,
    		IBM = _ibmValue,
    		MID = mid,
    		IBHX1 = ibhx1,
   		IBHX2 = ibhx2,
    		IBHX3 = ibhx3,
    		IBLX1 = iblx1,
    		IBLX2 = iblx2,
    		IBLX3 = iblx3
	};

	EndPreviousValueLines(bar);
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
    if (!_calculate)
    	return;
     
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

    // Save temporary IB levels during formation if ShowDuringFormation is active
    if (_calculate && ShowDuringFormation && _currentSession != null)
    {
    	_sessionIBValues[_currentSession] = new IBLevels
    	{
        	StartBarRectangle = _lastStartBar,
        	StartBarLine = bar,
        	EndBar = bar, // No finalizado aún // Not finished yet
        	IBH = _ibMax,
        	IBL = _ibMin,
        	IBM = _ibmValue,
        	MID = mid,
        	IBHX1 = ibhx1,
        	IBHX2 = ibhx2,
        	IBHX3 = ibhx3,
        	IBLX1 = iblx1,
        	IBLX2 = iblx2,
        	IBLX3 = iblx3
    	};
     }
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

// Draws the opening range rectangle
private void DrawOpenRangeRectangle(int bar, IBLevels ib)
{
    var pen = new Pen(ConvertColor(_borderColor)) { Width = _borderWidth };
    var brush = new SolidBrush(ConvertColor(_fillColor));

    var rect = new DrawingRectangle(
            ib.StartBarRectangle, ib.IBH,
            bar, ib.IBL,
            pen, brush
    );

    // Remove any previous rectangle
    Rectangles.Clear();

    // Add the new rectangle
    Rectangles.Add(rect);
}

// Updates series visibility depending on current state
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
    // Prepares the indicator state for the next session
    private void PrepareForNextSession()
    {
        _calculate = false;
        _isStarted = false;
        _highLowIsSet = false;
        _waitingForNextSession = true;


        //Ends all active lines visually
        foreach (var series in DataSeries.OfType<ValueDataSeries>())
        {            
            series.SetPointOfEndLine(CurrentBar);
        }
        
        UpdateSeriesVisibility();

        RedrawChart();
    }

    // Returns X2 (final) coordinate for a session based on its status
    private int GetSessionX2(RenderContext context, Session session)
    {
        if (session == _currentSession)
        {
            if (_currentSession.EndBar > 0)
            {
                //Case 3: Session already ended → line to the end of the session
                return ChartInfo.GetXByBar(_currentSession.EndBar, false);
            }
            else if (!_calculate)
            {
                //Case 1 and 2: calculation already ended but session is still active
                if (ExtendLastLineToRight)
                    return context.ClipBounds.Right;
                else
                    return ChartInfo.GetXByBar(CurrentBar - 1, false);
            }
            else
            {
                //Still in formation → to the current visible candle
                return ChartInfo.GetXByBar(CurrentBar - 1, false);
            }
        }
        else
        {
            // Previous sessions → use their EndBar
            return ChartInfo.GetXByBar(session.EndBar, false);
        }
    }

    // Draws the Initial Balance lines and their labels
    private void DrawIBVisuals(RenderContext context, Session session, bool drawLabels = true, bool drawLines = true)
    {
        const int offset = 2;

        if (!_sessionIBValues.TryGetValue(session, out var ib))
            return;

        int sessionX2 = GetSessionX2(context, session);
        int sessionX1 = ChartInfo.GetXByBar(ib.StartBarLine, false);

        var items = new[]
        {
        ("IBH", _ibh, ib.IBH),
        ("IBL", _ibl, ib.IBL),
        ("IBM", _ibm, ib.IBM),
        ("MID", _mid, ib.MID),
        ("IBHX1", _ibhx1, ib.IBHX1),
        ("IBHX2", _ibhx2, ib.IBHX2),
        ("IBHX3", _ibhx3, ib.IBHX3),
        ("IBLX1", _iblx1, ib.IBLX1),
        ("IBLX2", _iblx2, ib.IBLX2),
        ("IBLX3", _iblx3, ib.IBLX3)
    };

        foreach (var (label, series, price) in items)
        {
            var y = ChartInfo.GetYByPrice(price, false);
            if (y < 0)
                continue;

            if (drawLines)
            {
                var pen = new RenderPen(ConvertColor(series.Color), series.Width, ConvertDashStyle(series.LineDashStyle));
                context.DrawLine(pen, sessionX1, y, sessionX2, y);
            }

            if (drawLabels)
            {
                var size = context.MeasureString(label, _font);
                context.DrawString(label, _font, ConvertColor(series.Color),
                    sessionX2 - size.Width - 2,
                    y - size.Height - offset);
            }
        }
    }
#endregion
}
