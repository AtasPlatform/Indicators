namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;

using Pen = System.Drawing.Pen;

[DisplayName("Initial Balance")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.InitialBalanceIndDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602294")]
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

	// ValueDataSeries (IB levels and projections)
    	private readonly ValueDataSeries _mid;
    	private readonly ValueDataSeries _ibh, _ibhx1, _ibhx2, _ibhx3;
    	private readonly ValueDataSeries _ibl, _iblx1, _iblx2, _iblx3;
    	private readonly ValueDataSeries _ibm;

    	// RangeDataSeries (Value areas between levels)
    	private RangeDataSeries _ibhx32, _ibhx21, _ibhx1h;
    	private RangeDataSeries _ibHm, _ibMl, _ibl1;
    	private RangeDataSeries _iblx12, _iblx23;

    	// Visual settings	
     	private CrossColor _borderColor = DefaultColors.Red.Convert();
	private CrossColor _fillColor = DefaultColors.Yellow.Convert();
	private int _borderWidth = 1;
	private DrawingRectangle _rectangle = new(0, 0, 0, 0, Pens.Gray, new SolidBrush(DefaultColors.Yellow));
     	private RenderFont _font;
    	private float _fontSize = 12.0f;
	private bool _drawText = true;
	private bool _showOpenRange = true;
 	private bool _showDuringFormation = false;

	// Internal state flags
	private bool _calculate;
	private bool _customSessionStart;
	private bool _highLowIsSet;
	private bool _initialized;
	private bool _isStarted;

	// Session configuration and timing
	private int _days = 20;
	private int _period = 60;
	private TimeSpan _startDate = new(9, 0, 0);
	private TimeSpan _endDate;
	private DateTime _endTime = DateTime.MaxValue;
	private PeriodType _periodMode = PeriodType.Minutes;

	// Tracking bars and ranges
	private int _lastStartBar = -1;
    	private int _lastEndBar = -1;
	private int _targetBar;
	private decimal _maxValue = decimal.MinValue;
	private decimal _minValue = decimal.MaxValue;

	// IB levels and derived values
	private decimal _ibMax = decimal.MinValue;
	private decimal _ibMin = decimal.MaxValue;
	private decimal _ibmValue = decimal.Zero;
	private decimal mid = decimal.Zero;

	// Projection levels (IBHX / IBLX)
	private decimal ibhx1 = decimal.Zero;
	private decimal ibhx2 = decimal.Zero;
	private decimal ibhx3 = decimal.Zero;
	private decimal iblx1 = decimal.Zero;
	private decimal iblx2 = decimal.Zero;
	private decimal iblx3 = decimal.Zero;

 	// IB levels structure per session
	public class IBLevels
	{
    		public int StartBarLine;
    		public int EndBar;
    		public decimal IBH, IBL, IBM, MID;
    		public decimal IBHX1, IBHX2, IBHX3;
    		public decimal IBLX1, IBLX2, IBLX3;
	}
	
 	private readonly Dictionary<Session, IBLevels> _sessionIBValues = new();


	// Range multipliers
	private decimal _x1 = 1m;
	private decimal _x2 = 2m;
	private decimal _x3 = 3m;

 	// Custom session management
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
	
 	// Show or hide the Open Range rectangle
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show),
		GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.ShowOpenRangeDescription), Order = 10)]
	public bool ShowOpenRange
	{
		get => _showOpenRange;
		set
		{
			_showOpenRange = value;
			RecalculateValues();
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
		}
	}

    	// Border color of the Open Range rectangle
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BorderColor),
		GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.BorderColorDescription),Order = 30)]
	public CrossColor BorderColor
	{
		get => _borderColor;
		set
		{
			_borderColor = value;
			RecalculateValues();
		}
	}

    	// Fill color of the Open Range rectangle
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FillColor),
		GroupName = nameof(Strings.OpenRange), Description = nameof(Strings.FillColorDescription),Order = 40)]
	public CrossColor FillColor
	{
		get => _fillColor;
		set
		{
			_fillColor = value;
			RecalculateValues();
		}
	}

    	// Enables or disables the use of a custom session
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CustomSession),
		GroupName = nameof(Strings.SessionTime), Description = nameof(Strings.IsCustomSessionDescription),Order = 10)]
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
   			RedrawChart();
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
      			RedrawChart();
		}
	}

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

     // Initial Balance Expansion Multipliers
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
		GroupName = nameof(Strings.Multiplier), Description = nameof(Strings.MultiplierDescription),Order = 110)]
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
        		_font = new RenderFont("Arial", _fontSize); // Se actualiza cuando cambia // Updated when changed
        		RecalculateValues();
    		}
	}

	// Extend lines to the right edge of the chart
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ExtendLast),
 	GroupName = nameof(Strings.Drawing), Description = nameof(Strings.ExtendLastDescription), Order = 150)]
	public bool ExtendLastLineToRight
	{
    		get => _extendLastLineToRight;
    		set
    		{
        		_extendLastLineToRight = value;
			RedrawChart();
    		}
	}
	private bool _extendLastLineToRight = true;

	// Show levels during Initial Balance formation
 	[Display(ResourceType = typeof(Strings), Name = "Show During Formation",
		GroupName = nameof(Strings.Show), Description = "Show IB lines during first hour", Order = 160)]
	public bool ShowDuringFormation
	{
    		get => _showDuringFormation;
    		set
    		{
        		_showDuringFormation = value;
        		RecalculateValues();
    		}
	}

    	// Background colors of Initial Balance expansion zones
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX32), 
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 200)]
	public CrossColor Ibhx32
	{
		get=>_ibhx32.RangeColor; 
		set=>_ibhx32.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX21),
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription),Order = 210)]
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
	 
        // Initialization of ValueDataSeries
    	_mid = CreateValueSeries("MidId", "Mid", CrossColor.FromArgb(0, 0, 255, 0), LineDashStyle.Solid, nameof(Strings.SessionAveragePriceDescription));
	_ibh = CreateValueSeries("Ibh", "IBH", DefaultColors.Blue.Convert(), LineDashStyle.Dash, nameof(Strings.TopBandDscription));
	_ibl = CreateValueSeries("Ibl", "IBL", DefaultColors.Red.Convert(), LineDashStyle.Dash, nameof(Strings.BottomBandDscription));
	_ibm = CreateValueSeries("Ibm", "IBM", DefaultColors.Green.Convert(), LineDashStyle.Dash, nameof(Strings.MidBandDescription));
	_ibhx1 = CreateValueSeries("Ibhx1", "IBHX1", DefaultColors.Fuchsia.Convert());
	_ibhx2 = CreateValueSeries("Ibhx2", "IBHX2", DefaultColors.Fuchsia.Convert());
	_ibhx3 = CreateValueSeries("Ibhx3", "IBHX3", DefaultColors.Fuchsia.Convert());
	_iblx1 = CreateValueSeries("Iblx1", "IBLX1", DefaultColors.Purple.Convert());
	_iblx2 = CreateValueSeries("Iblx2", "IBLX2", DefaultColors.Purple.Convert());
	_iblx3 = CreateValueSeries("Iblx3", "IBLX3", DefaultColors.Purple.Convert());

        DataSeries[0] = _mid;
        DataSeries.AddRange(new[]
 	{
	_ibh, _ibl, _ibm,
	_ibhx1, _ibhx2, _ibhx3,
	_iblx1, _iblx2, _iblx3
	});
 
	// RangeDataSeries (Value areas)
	_ibhx32 = CreateRangeSeries("Ibhx32", "ibhx32");
	_ibhx21 = CreateRangeSeries("Ibhx21", "ibhx21");
	_ibhx1h = CreateRangeSeries("Ibhx1h", "ibhx1h");
	_ibHm = CreateRangeSeries("IbHm", "ibHm");
	_ibMl = CreateRangeSeries("IbM1", "ibM1");
	_ibl1 = CreateRangeSeries("Ibl1", "ibl1");
	_iblx12 = CreateRangeSeries("Ibl12", "ibl12");
	_iblx23 = CreateRangeSeries("Ibl23", "ibl23");

        // Add RangeDataSeries to the indicator's DataSeries collection
	DataSeries.AddRange(new[]
	{
	_ibhx32, _ibhx21, _ibhx1h,
	_ibHm, _ibMl, _ibl1,
	_iblx12, _iblx23
	});

	// Subscribe to property changes
	foreach (var series in new[]
	{
	_ibh, _ibl, _ibm,
	_ibhx1, _ibhx2, _ibhx3,
	_iblx1, _iblx2, _iblx3
	})
	{
    series.PropertyChanged += DataSeriesPropertyChanged;
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
		var candle = GetCandle(bar);

		var time = candle.Time.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
		var lastTime = candle.LastTime.AddHours(InstrumentInfo.TimeZone).TimeOfDay;
		
        if (CustomSessionStart)
		{
			bool inSession;

            if (StartDate < EndDate)
				inSession = (time >= StartDate || lastTime >= StartDate) && time < EndDate;
			else if (StartDate > EndDate)
			{
				inSession = ((time >= StartDate || lastTime >= StartDate) && time > EndDate)
						 || ((time <= EndDate || lastTime <= EndDate) && time < EndDate);
            }
			else
				inSession = true;

            if (!inSession)
			{
				_isStarted = false;
    
                    		if (_currentSession != null && _currentSession.EndBar <= 0)
                    			_currentSession.EndBar = bar - 1;


                		foreach (var dataSeries in DataSeries)
					if (dataSeries is ValueDataSeries series)
						series.SetPointOfEndLine(bar - 1);
                		return;
			}
		}

        var candleFullDateTime = candle.Time.AddHours(InstrumentInfo.TimeZone);
		var isStart = false;
		var isEnd = false;

        if (!_isStarted)
		{
			isStart = _customSessionStart
				   ? bar != 0 && (time >= StartDate || lastTime >= StartDate) && (GetPrevDateTime(bar).TimeOfDay < StartDate || GetPrevDateTime(bar).Date < candleFullDateTime.Date)
				   : IsNewSession(bar);
        }

        if (_isStarted)
		{
			isEnd = (PeriodMode is PeriodType.Minutes && candleFullDateTime >= _endTime && GetPrevDateTime(bar) < _endTime)
				 || (PeriodMode is PeriodType.Bars && bar - _lastStartBar >= Period);
        }           

		if (isStart)
		{
			BeginCalculationWindow(bar, candleFullDateTime);

            		if (ShowOpenRange)
			{
				var pen = new Pen(ConvertColor(_borderColor))
				{
					Width = _borderWidth
				};
				var brush = new SolidBrush(ConvertColor(_fillColor));

				_rectangle = new DrawingRectangle(bar, decimal.Zero, bar, decimal.Zero, pen, brush);

				if (Rectangles.LastOrDefault()?.FirstBar == bar)
					Rectangles.RemoveAt(Rectangles.Count - 1);

				Rectangles.Add(_rectangle);
			}
		}
		else if (isEnd)
		{
			EndCalculationWindow(_currentSession, bar);
        	}

		if (_calculate)
		{
            		// Updates the high and low for the Initial Balance.
            		UpdateIbHighLow(candle);

			if (ShowOpenRange)
			{
				UpdateRectangleDuringCalculation(bar);
			}
		}

        	// Updates the session high and low (not just the IB window).
        	UpdateSessionHighLow(candle);

		if (!_highLowIsSet)
			return;

        	// Calculates the Initial Balance levels.
        	CalculateIbLevels(bar);

          	// Fills in the Value Areas between levels.
        	FillValueAreas(bar);
        }

 	protected override void OnRender(RenderContext context, DrawingLayouts layout)
	{
    		if (!_initialized || _lastStartBar < 0)
        		return;

    		foreach (var session in _sessions)
    		{
        		bool isCurrent = session == _currentSession;
        		bool inFormation = isCurrent && _calculate && session.EndBar <= 0;

        		if (inFormation && !ShowDuringFormation)
  	          			continue;

        		bool drawLines = isCurrent && ExtendLastLineToRight;

        		DrawIBVisuals(context, session, drawLabels: DrawText, drawLines: drawLines);
    		}
	}
	
    // Returns the datetime of the previous candle adjusted by the instrument's time zone
    private DateTime GetPrevDateTime(int bar)
    {
		return GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
    }

    #endregion

    #region Private methods

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

		RecalculateValues();
	}

    	// Converts a custom CrossColor to a system Color
	private System.Drawing.Color ConvertColor(CrossColor color)
	{
		return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
	}

 	// Creates a ValueDataSeries with default visual properties.
	// Optionally assigns a description key for localization.
	private static ValueDataSeries CreateValueSeries(string name, string shortName, CrossColor color, LineDashStyle dash = LineDashStyle.Dash, string? descriptionKey = null)
	{
    		var series = new ValueDataSeries(name, shortName)
    		{
        		Color = color,
        		LineDashStyle = dash,
        		VisualType = VisualMode.Square,
        		Width = 1
    		};

    		if (!string.IsNullOrEmpty(descriptionKey))
        		series.DescriptionKey = descriptionKey;

    		return series;
	}
 
	// Creates and returns a new RangeDataSeries with default settings.
	private static RangeDataSeries CreateRangeSeries(string name, string? shortName = null)
	{
    		return new RangeDataSeries(name, shortName ?? name)
    		{
        		IsHidden = true,
			DrawAbovePrice = false,
			RangeColor = System.Drawing.Color.Transparent.Convert()
		};
	}
 
	/// Resets the high/low level tracking variables to their default values.
	private void ResetLevels()
	{
    		_maxValue = decimal.MinValue;
    		_minValue = decimal.MaxValue;
    		_ibMax = decimal.MinValue;
    		_ibMin = decimal.MaxValue;
    		_ibmValue = mid = ibhx1 = ibhx2 = ibhx3 = iblx1 = iblx2 = iblx3 = decimal.Zero;
	}

	/// Clears all internal state, data series, and session tracking variables.
	private void ResetState()
	{
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
    		_targetBar = 0;

    		// Reset calculation window end time
    		_endTime = DateTime.MaxValue;
	}
 
	/// Identifies the oldest bar to be considered based on the 'Days' parameter.
	private void InitializeTargetBar()
	{
    		if (_days <= 0)
        		return;

    		var days = 0;

    		for (var i = CurrentBar - 1; i >= 0; i--)
    		{
        		_targetBar = i;

        		// Only count a new day if there’s a session change
        		if (!IsNewSession(i))
            			continue;

        		days++;

        		if (days == _days)
            			break;
    		}
	}

	/// Initializes a new calculation window, resets high/low tracking and ends previous lines.
	private void BeginCalculationWindow(int bar, DateTime candleTime)
	{
         	// End previous session
        	if (_currentSession != null && _currentSession.EndBar <= 0)
            		_currentSession.EndBar = bar - 1;
	      
    		_calculate = true;
    		_highLowIsSet = false;
    		_lastStartBar = bar;
    		_endTime = candleTime.AddMinutes(_period);
    		_isStarted = true;

    		ResetLevels();

    		EndPreviousValueLines(bar);
          	_currentSession = new Session
    		{
        		StartBar = bar,
        		StartTime = GetCandle(bar).Time.Date,
        		EndBar = -1
    		};

    		_sessions.Add(_currentSession);

	}
 
	/// Finalizes the calculation window and forces chart redraw.
	private void EndCalculationWindow(Session session, int bar)
	{
    		_calculate = false;
    		_isStarted = false;
    		_lastEndBar = bar;

    		// Save calculated levels in session dictionary
    		_sessionIBValues[session] = new IBLevels
    		{
        		StartBarLine = bar,
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
	}

	// Ends all previously calculated lines visually
	private void EndPreviousValueLines(int bar)
	{
    		foreach (var dataSeries in DataSeries)
        		if (dataSeries is ValueDataSeries series)
				series.SetPointOfEndLine(bar-1);
	}

        // Updates the high and low for the Initial Balance.
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
 
	// Updates the Initial Balance rectangle coordinates.
	private void UpdateRectangleDuringCalculation(int bar)
	{
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

 		mid = (_minValue + _maxValue) / 2m;
		_ibmValue = (_ibMin + _ibMax) / 2m;

		var diff = _ibMax - _ibMin;
		ibhx1 = _ibMax + diff * _x1;
		ibhx2 = _ibMax + diff * _x2;
		ibhx3 = _ibMax + diff * _x3;
		iblx1 = _ibMin - diff * _x1;
		iblx2 = _ibMin - diff * _x2;
		iblx3 = _ibMin - diff * _x3;

         	if (!ShowDuringFormation && _calculate)
        	{
            		EndPreviousValueLines(bar+1);
            		return;
        	}
    		
      		_mid[bar] = mid;
		_ibh[bar] = _ibMax;
		_ibl[bar] = _ibMin;
		_ibm[bar] = _ibmValue;

		_ibhx1[bar] = ibhx1;
		_ibhx2[bar] = ibhx2;
		_ibhx3[bar] = ibhx3;
		_iblx1[bar] = iblx1;
		_iblx2[bar] = iblx2;
		_iblx3[bar] = iblx3;
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

  	// Returns X2 (final) coordinate for a session based on its status
 	private int GetSessionX2(RenderContext context, Session session)
 	{
     		if (session == _currentSession)
     		{
         		if (_currentSession.EndBar > 0)
         		{
             			// Case 3: session has already ended → draw line up to the session's EndBar
             			return ChartInfo.GetXByBar(_currentSession.EndBar, false);
         		}
         		else if (!_calculate)
         		{
             			// Case 1 & 2: IB calculation has ended but session is still active
             			if (ExtendLastLineToRight)
                 			return context.ClipBounds.Right;
             			else
                 			return ChartInfo.GetXByBar(CurrentBar - 1, false);
         		}
         		else
         		{
             			// Still in formation → draw line up to the current visible candle
             			return ChartInfo.GetXByBar(CurrentBar - 1, false);
         		}
     		}
     		else
     		{
         		// Previous sessions → use their EndBar
         		return ChartInfo.GetXByBar(session.EndBar, false);
     		}
 	}
  
 	// Draws the Initial Balance labels
 	private void DrawIBVisuals(RenderContext context, Session session, bool drawLabels = true, bool drawLines = true)
 	{
     		const int offset = 2;

     		if (!_sessionIBValues.TryGetValue(session, out var ib))
         		return;

     		int sessionX2 = GetSessionX2(context, session);
     		int sessionX1 = session == _currentSession && ExtendLastLineToRight
                     		? ChartInfo.GetXByBar(CurrentBar - 1, false)
                     		: ChartInfo.GetXByBar(session.StartBar, false);

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
