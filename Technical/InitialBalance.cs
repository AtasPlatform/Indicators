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
	private bool _drawText = true;
	private bool _showOpenRange = true;

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

	// Range multipliers
	private decimal _x1 = 1m;
	private decimal _x2 = 2m;
	private decimal _x3 = 3m;

    	#endregion

    #region Properties

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
			_calculate = _isStarted = false;
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

        if (DrawText)
		{
			AddText(_lastStartBar + "Mid", "Mid", true, bar, mid, 0, 0, ConvertColor(_mid.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBH", "IBH", true, bar, _ibMax, 0, 0, ConvertColor(_ibh.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBL", "IBL", true, bar, _ibMin, 0, 0, ConvertColor(_ibl.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBM", "IBM", true, bar, _ibmValue, 0, 0, ConvertColor(_ibm.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBHX1", "IBHX1", true, bar, ibhx1, 0, 0, ConvertColor(_ibhx1.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBHX2", "IBHX2", true, bar, ibhx2, 0, 0, ConvertColor(_ibhx2.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBHX3", "IBHX3", true, bar, ibhx3, 0, 0, ConvertColor(_ibhx3.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBLX1", "IBLX1", true, bar, iblx1, 0, 0, ConvertColor(_iblx1.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBLX2", "IBLX2", true, bar, iblx2, 0, 0, ConvertColor(_iblx2.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);

			AddText(_lastStartBar + "IBLX3", "IBLX3", true, bar, iblx3, 0, 0, ConvertColor(_iblx3.Color), System.Drawing.Color.Transparent,
				System.Drawing.Color.Transparent, 12.0f, DrawingText.TextAlign.Right);
		}
	}
	
    // Returns the datetime of the previous candle adjusted by the instrument's time zone
    private DateTime GetPrevDateTime(int bar)
    {
		return GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
    }

    #endregion

    #region Private methods

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
    		_calculate = true;
    		_highLowIsSet = false;
    		_lastStartBar = bar;
    		_endTime = candleTime.AddMinutes(_period);
    		_isStarted = true;

    		ResetLevels();

    		EndPreviousValueLines(bar);
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

	#endregion
}
