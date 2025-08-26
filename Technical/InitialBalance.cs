using ATAS.Indicators;

namespace ATAS.Indicators.Technical;

using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Settings;
using OFT.Rendering.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using Pen = System.Drawing.Pen;

[DisplayName("Initial Balance")]
[Category(IndicatorCategories.VolumeOrderFlow)]
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

	private readonly ValueDataSeries _ibh = new("Ibh", "IBH")
	{
		Color = DefaultColors.Blue.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1,
		DescriptionKey = nameof(Strings.TopBandDscription)
	};

	private readonly ValueDataSeries _ibhx1 = new("Ibhx1", "IBHX1")
	{
		Color = DefaultColors.Fuchsia.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _ibhx2 = new("Ibhx2", "IBHX2")
	{
		Color = DefaultColors.Fuchsia.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _ibhx3 = new("Ibhx3", "IBHX3")
	{
		Color = DefaultColors.Fuchsia.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _ibl = new("Ibl", "IBL")
	{
		Color = DefaultColors.Red.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1,
        DescriptionKey = nameof(Strings.BottomBandDscription)
    };

	private readonly ValueDataSeries _iblx1 = new("Iblx1", "IBLX1")
	{
		Color = DefaultColors.Purple.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _iblx2 = new("Iblx2", "IBLX2")
	{
		Color = DefaultColors.Purple.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _iblx3 = new("Iblx3", "IBLX3")
	{
		Color = DefaultColors.Purple.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1
	};

	private readonly ValueDataSeries _ibm = new("Ibm", "IBM")
	{
		Color = DefaultColors.Green.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		VisualType = VisualMode.Square,
		Width = 1,
        DescriptionKey = nameof(Strings.MidBandDescription)
    };

	private readonly ValueDataSeries _mid = new("MidId", "Mid")
	{
		Color = CrossColor.FromArgb(0, 0, 255, 0),
		LineDashStyle = LineDashStyle.Solid,
		VisualType = VisualMode.Square,
		Width = 1,
        DescriptionKey = nameof(Strings.SessionAveragePriceDescription)
    };

	private RangeDataSeries _ibhx32 = new("Ibhx32", "ibhx32")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
		DrawAbovePrice = false,
		IsHidden = true
	};
	private RangeDataSeries _ibhx21 = new("Ibhx21", "ibhx21")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibhx1h = new("Ibhx1h", "ibhx1h")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibHm = new("IbHm", "ibHm")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibMl = new("IbM1", "ibM1")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibl1 = new("Ibl1", "ibl1")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _iblx12 = new("Ibl12", "ibl12")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _iblx23 = new("Ibl23", "ibl23")
	{
		RangeColor = System.Drawing.Color.Transparent.Convert(),
        DrawAbovePrice = false,
        IsHidden = true
	};

    private CrossColor _borderColor = DefaultColors.Red.Convert();
	private int _borderWidth = 1;
	private bool _calculate;
	private bool _customSessionStart;
	// NEW: extension/anchor control: lines may extend to the right edge while the session is active;
    // labels always anchor at the line end (either the right edge if extended, or the last bar).
    private bool _extendLastLineToRight = true;
    private int _lastEndBar = -1; // last bar of the custom session; -1 means still active
    private int _days = 20;
    private bool _drawText = true;
	private TimeSpan _endDate;
	private DateTime _endTime = DateTime.MaxValue;
	private CrossColor _fillColor = DefaultColors.Yellow.Convert();
	private bool _highLowIsSet;
	private decimal _ibMax = decimal.MinValue;
	private decimal _ibMin = decimal.MaxValue;
	private decimal _ibmValue = decimal.Zero;
    private float _fontSize = 12.0f;
    private RenderFont _font;

    private bool _initialized;
	private int _lastStartBar = -1;
	private decimal _maxValue = decimal.MinValue;
	private decimal _minValue = decimal.MaxValue;
	private int _period = 60;
	private PeriodType _periodMode = PeriodType.Minutes;
	private DrawingRectangle _rectangle = new(0, 0, 0, 0, Pens.Gray, new SolidBrush(DefaultColors.Yellow));
	private bool _showOpenRange = true;
	private TimeSpan _startDate = new(9, 0, 0);
	private int _targetBar;
	private decimal _x1 = 1m;
	private decimal _x2 = 2m;
	private decimal _x3 = 3m;
	private decimal ibhx1 = decimal.Zero;
	private decimal ibhx2 = decimal.Zero;
	private decimal ibhx3 = decimal.Zero;
	private decimal iblx1 = decimal.Zero;
	private decimal iblx2 = decimal.Zero;
	private decimal iblx3 = decimal.Zero;
	private decimal mid = decimal.Zero;

	private bool _isStarted;

	// --- ShowDuringFormation state & per-session buffer ---
	// When false: do not write ValueDataSeries during formation; keep values buffered only.
	// When true : mirror buffered values to ValueDataSeries (live display).
	private bool _showDuringFormation = false;

	// Snapshot of IB levels per bar (current session)
	private sealed class IbSnapshot
	{
		public int Bar;
		public decimal Mid, IBH, IBL, IBM;
		public decimal IBHX1, IBHX2, IBHX3;
		public decimal IBLX1, IBLX2, IBLX3;
	}

	// In-memory buffer while the IB window is forming
	private readonly List<IbSnapshot> _sessionBuffer = new();

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

	// Label font size (for text drawing)
    [Display(Name = "Font Size",
	GroupName = nameof(Strings.Show), Description = "Label font size", Order = 135)]
    [Range(6, 48)]
    public float FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            _font = new RenderFont("Arial", _fontSize);
            RecalculateValues();
        }
    }

	// Extend current session lines to the chart's right edge while the session is active.
    // Labels ALWAYS appear where lines end:
    // - If extended: at the right edge
    // - If not extended (or session ended): at the last bar (or the recorded session end bar)
    [Display(Name = "Extend Last Line to Right",
	GroupName = nameof(Strings.Show), Description = "Extend lines to the right edge while session is active; labels anchor at line end", Order = 137)]
    public bool ExtendLastLineToRight
    {
        get => _extendLastLineToRight;
        set
        {
            _extendLastLineToRight = value;
            RedrawChart();
        }
    }

	// During IB formation: when false, nothing is drawn (lines/labels) and values are buffered in-memory.
	// When toggled to true mid-formation, the buffer is backfilled to ValueDataSeries immediately.
	[Display(Name = "Show During Formation",
	GroupName = nameof(Strings.Show), Description = "Show IB lines/labels while the initial balance window is forming",	Order = 160)]
	public bool ShowDuringFormation
	{
		get => _showDuringFormation;
		set
		{
			if (_showDuringFormation == value)
				return;

			_showDuringFormation = value;

			// Live toggle without full historical recalc
			if (_initialized && _calculate && _lastStartBar >= 0)
			{
				if (_showDuringFormation)
					BackfillBufferToSeries();
				else
					TruncateVisibleCurrentSession();
			}

			RedrawChart();
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
        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        _font = new RenderFont("Arial", _fontSize);

        DataSeries[0] = _mid;
        DataSeries.Add(_ibh);
		DataSeries.Add(_ibl);
		DataSeries.Add(_ibm);
		DataSeries.Add(_ibhx1);
		DataSeries.Add(_ibhx2);
		DataSeries.Add(_ibhx3);
		DataSeries.Add(_iblx1);
		DataSeries.Add(_iblx2);
		DataSeries.Add(_iblx3);

		DataSeries.Add(_ibhx32);
		DataSeries.Add(_ibhx21);
		DataSeries.Add(_ibhx1h);
		DataSeries.Add(_ibHm);
		DataSeries.Add(_ibMl);
		DataSeries.Add(_ibl1);
		DataSeries.Add(_iblx12);
		DataSeries.Add(_iblx23);

		_ibh.PropertyChanged += DataSeriesPropertyChanged;
		_ibl.PropertyChanged += DataSeriesPropertyChanged;
		_ibm.PropertyChanged += DataSeriesPropertyChanged;
		_ibhx1.PropertyChanged += DataSeriesPropertyChanged;
		_ibhx2.PropertyChanged += DataSeriesPropertyChanged;
		_ibhx3.PropertyChanged += DataSeriesPropertyChanged;
		_iblx1.PropertyChanged += DataSeriesPropertyChanged;
		_iblx2.PropertyChanged += DataSeriesPropertyChanged;
		_iblx3.PropertyChanged += DataSeriesPropertyChanged;
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			DataSeries.ForEach(x => x.Clear());
			ibhx1 = decimal.Zero;
			ibhx2 = decimal.Zero;
			ibhx3 = decimal.Zero;
			iblx1 = decimal.Zero;
			iblx2 = decimal.Zero;
			iblx3 = decimal.Zero;
			mid = decimal.Zero;
			_maxValue = decimal.MinValue;
			_minValue = decimal.MaxValue;
			_ibMax = decimal.MinValue;
			_ibMin = decimal.MaxValue;
			_ibmValue = decimal.Zero;
			_highLowIsSet = false;
			_lastStartBar = -1;
			_endTime = DateTime.MaxValue;
			_calculate = false;
			_initialized = false;
			_targetBar = 0;
			_isStarted = false;

            if (_days <= 0)
				return;

			var days = 0;

			for (var i = CurrentBar - 1; i >= 0; i--)
			{
				_targetBar = i;

				if (!IsNewSession(i))
					continue;

				days++;

				if (days == _days)
					break;
			}
		}

		if (bar < _targetBar)
			return;

		_initialized = true;
		var candle = GetCandle(bar);

        // Local candle boundaries
        var candleStartLocal = candle.Time.AddHours(InstrumentInfo.TimeZone);
        var candleEndLocal = candle.LastTime.AddHours(InstrumentInfo.TimeZone);

        // Preserve upstream variables used later for isStart logic
		var time = candleStartLocal.TimeOfDay;
        var lastTime = candleEndLocal.TimeOfDay;

        // Robust custom-session check using absolute datetimes (overnight-safe)
        if (CustomSessionStart)
		{
            var (sessionStart, sessionEnd) = GetCustomSessionWindow(candleStartLocal);
            var inSession = Intersects(candleStartLocal, candleEndLocal, sessionStart, sessionEnd);

            if (!inSession)
			{
				_isStarted = false;
                // Record where the custom session ended so labels can anchor there after exit.
                _lastEndBar = Math.Max(0, bar - 1);

                foreach (var dataSeries in DataSeries)
					if (dataSeries is ValueDataSeries series)
						series.SetPointOfEndLine(Math.Max(0, bar - 1));
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
			//Clear all values
			_maxValue = decimal.MinValue;
			_minValue = decimal.MaxValue;
			_ibMax = decimal.MinValue;
			_ibMin = decimal.MaxValue;
			_ibmValue = decimal.Zero;
			ibhx1 = decimal.Zero;
			ibhx2 = decimal.Zero;
			ibhx3 = decimal.Zero;
			iblx1 = decimal.Zero;
			iblx2 = decimal.Zero;
			iblx3 = decimal.Zero;
			_calculate = true;
			_highLowIsSet = false;
			_lastStartBar = bar;
			_endTime = candleFullDateTime.AddMinutes(_period);
            _isStarted = true;
            _lastEndBar = -1; // reset anchor for the new session

            foreach (var dataSeries in DataSeries)
                if (dataSeries is ValueDataSeries series)
                    series.SetPointOfEndLine(bar - 1);

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
            // IB window finished: if hidden during formation, backfill the session now
            if (!_showDuringFormation)
                BackfillBufferToSeries();
            _calculate = _isStarted = false;
            _sessionBuffer.Clear();
            _lastEndBar = Math.Max(_lastEndBar, bar);
        }

		if (_calculate)
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

			if (ShowOpenRange)
			{
				_rectangle.SecondBar = bar;
				_rectangle.FirstPrice = _ibMax;
				_rectangle.SecondPrice = _ibMin;
			}
		}

		if (candle.High > _maxValue)
			_maxValue = candle.High;

		if (candle.Low < _minValue)
			_minValue = candle.Low;

		if (!_highLowIsSet)
			return;

        // Compute current levels (locals first)
        mid = (_minValue + _maxValue) / 2m;
        _ibmValue = (_ibMin + _ibMax) / 2m;
        var diff = _ibMax - _ibMin;

        ibhx1 = _ibMax + diff * _x1;
        ibhx2 = _ibMax + diff * _x2;
        ibhx3 = _ibMax + diff * _x3;
        iblx1 = _ibMin - diff * _x1;
        iblx2 = _ibMin - diff * _x2;
        iblx3 = _ibMin - diff * _x3;

        // Buffer snapshot for this bar
        _sessionBuffer.Add(new IbSnapshot
        {
			Bar = bar,
			Mid = mid,
			IBH = _ibMax,
			IBL = _ibMin,
			IBM = _ibmValue,
			IBHX1 = ibhx1,
			IBHX2 = ibhx2,
			IBHX3 = ibhx3,
			IBLX1 = iblx1,
			IBLX2 = iblx2,
			IBLX3 = iblx3
       });

        // Mirror to series only if we show during formation; otherwise keep the chart clean
        if (_showDuringFormation)
        {
            WriteSnapshotToSeries(bar, _sessionBuffer[^1]);
        }
        else
        {
			// Hide current session lines while forming
            TruncateVisibleCurrentSession();
        }

        // Labels are now drawn in OnRender; do not spawn text objects in OnCalculate.
    }

    private DateTime GetPrevDateTime(int bar)
    {
		return GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
    }

    #endregion

    #region Private methods

    private void DataSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (!_initialized)
			return;

		RecalculateValues();
	}

	private System.Drawing.Color ConvertColor(CrossColor color)
	{
		return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

	// Convert platform LineDashStyle to System.Drawing dash style for overlay rendering.
    private static System.Drawing.Drawing2D.DashStyle ConvertDashStyle(LineDashStyle style) =>
		style switch
        {
            LineDashStyle.Solid => System.Drawing.Drawing2D.DashStyle.Solid,
            LineDashStyle.Dash => System.Drawing.Drawing2D.DashStyle.Dash,
            LineDashStyle.Dot => System.Drawing.Drawing2D.DashStyle.Dot,
            LineDashStyle.DashDot => System.Drawing.Drawing2D.DashStyle.DashDot,
            LineDashStyle.DashDotDot => System.Drawing.Drawing2D.DashStyle.DashDotDot,
            _ => System.Drawing.Drawing2D.DashStyle.Solid
        };

    // Render labels exactly at the line end (right edge if extension is active; else at last/session-end bar)
    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (!_initialized)
			return;
        
        // Use the last completed/visible bar as the anchor for values and positions.
        var bar = Math.Max(0, CurrentBar - 1);
        if (bar <= 0)
			return;
        
        // Determine if custom session is currently active. If no custom session, treat as active (for extension semantics opt-in only).
        bool sessionActive = true;
        if (CustomSessionStart)
        {
            var lastStart = GetCandle(bar).Time.AddHours(InstrumentInfo.TimeZone);
            var lastEnd = GetCandle(bar).LastTime.AddHours(InstrumentInfo.TimeZone);
            var(ss, se) = GetCustomSessionWindow(lastStart);
            sessionActive = Intersects(lastStart, lastEnd, ss, se);
        }
        
        // Compute X coordinates:
        // - xLast: where the plotted series already ends (last bar)
        // - x2: where the line should end (right edge if extending & active; otherwise anchor bar)
        var xLast = ChartInfo.GetXByBar(bar, false);
        int x2;
        if (_extendLastLineToRight && sessionActive)
        {
			// Active and extension enabled ? anchor at right edge
            x2 = context.ClipBounds.Right;
        }
        else
        {
            // No extension ? anchor at last known session end bar (if recorded), else at last bar
            var anchorBar = _lastEndBar >= 0 ? _lastEndBar : bar;
            x2 = ChartInfo.GetXByBar(Math.Max(0, anchorBar), false);
        }
        
        // Only draw the extension segment to avoid overdrawing series; skip if x2 is not beyond last plotted X.
        var drawExtension = _extendLastLineToRight && sessionActive && x2 > xLast;
        
        // Prepare label/series/value triplets using the latest values at 'bar'.
        var items = new (string Label, ValueDataSeries Series, decimal Value)[]
        {
            ("Mid", _mid, _mid[bar]),
			("IBH", _ibh, _ibh[bar]),
			("IBL", _ibl, _ibl[bar]),
			("IBM", _ibm, _ibm[bar]),
			("IBHX1", _ibhx1, _ibhx1[bar]),
			("IBHX2", _ibhx2, _ibhx2[bar]),
			("IBHX3", _ibhx3, _ibhx3[bar]),
			("IBLX1", _iblx1, _iblx1[bar]),
			("IBLX2", _iblx2, _iblx2[bar]),
			("IBLX3", _iblx3, _iblx3[bar]),
        };
        
        const int pad = 2;
        
        foreach (var (label, series, price) in items)
        {
			// Skip invalid or uninitialized values
            if (price == 0m || price == decimal.MinValue || price == decimal.MaxValue)
				continue;
            
            var y = ChartInfo.GetYByPrice(price, false);
            if (y < 0)
				continue;
            
            // 1) Draw only the extension segment (if applicable)
            if (drawExtension)
            {
                var pen = new RenderPen(ConvertColor(series.Color), series.Width, ConvertDashStyle(series.LineDashStyle));
                context.DrawLine(pen, xLast, y, x2, y);
            }
            
            // 2) Draw label at line end (x2) if labels are enabled
            if (_drawText)
            {
                var size = context.MeasureString(label, _font);
                var xText = (int)(x2 - size.Width - pad);
                var yText = (int)(y - size.Height - pad);
                context.DrawString(label, _font, ConvertColor(series.Color), xText, yText);
            }
        }
    }

	// --- Helpers for robust custom-session handling ---
    // Normalizes custom session window to absolute datetimes for the candle's local date.
    // Handles overnight sessions: if StartDate > EndDate, the end is on the next day.
    private (DateTime start, DateTime end) GetCustomSessionWindow(DateTime candleLocalStart)
    {
        var baseDate = candleLocalStart.Date;
        var start = baseDate + StartDate;
        var end = baseDate + EndDate;

        if (StartDate > EndDate)
            end = end.AddDays(1);

        return (start, end);
    }

    // Returns true if [aStart, aEnd] intersects [bStart, bEnd)
    private static bool Intersects(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        return aStart < bEnd && aEnd > bStart;
    }

	// --- Buffer helpers ---

	// Write one buffered snapshot into ValueDataSeries and range bands at a specific bar
	private void WriteSnapshotToSeries(int bar, IbSnapshot s)
	{
		_mid[bar]   = s.Mid;
		_ibh[bar]   = s.IBH;
		_ibl[bar]   = s.IBL;
		_ibm[bar]   = s.IBM;
		_ibhx1[bar] = s.IBHX1;
		_ibhx2[bar] = s.IBHX2;
		_ibhx3[bar] = s.IBHX3;
		_iblx1[bar] = s.IBLX1;
		_iblx2[bar] = s.IBLX2;
		_iblx3[bar] = s.IBLX3;

		WriteRanges(bar, s.IBHX1, s.IBHX2, s.IBHX3, s.IBH, s.IBM, s.IBL, s.IBLX1, s.IBLX2, s.IBLX3);
	}

	// Write/refresh the background ranges for a bar from level values
	private void WriteRanges(
		int bar,
		decimal ibhx1, decimal ibhx2, decimal ibhx3,
		decimal ibh, decimal ibm, decimal ibl,
		decimal iblx1, decimal iblx2, decimal iblx3)
	{
		_ibhx32[bar].Upper = ibhx3;
		_ibhx32[bar].Lower = _ibhx21[bar].Upper = ibhx2;
		_ibhx21[bar].Lower = _ibhx1h[bar].Upper = ibhx1;
		_ibhx1h[bar].Lower = _ibHm[bar].Upper  = ibh;
		_ibHm[bar].Lower   = _ibMl[bar].Upper  = ibm;
		_ibMl[bar].Lower   = _ibl1[bar].Upper  = ibl;
		_ibl1[bar].Lower   = _iblx12[bar].Upper = iblx1;
		_iblx12[bar].Lower = _iblx23[bar].Upper = iblx2;
		_iblx23[bar].Lower = iblx3;
	}

	// Backfill the whole buffered session (from _lastStartBar to current bar) into visible series
   private void BackfillBufferToSeries()
   {
		if (_lastStartBar < 0 || _sessionBuffer.Count == 0)
			return;

		foreach (var s in _sessionBuffer)
			WriteSnapshotToSeries(s.Bar, s);
   }

	// Visually truncate current session lines while keeping the buffer intact
   private void TruncateVisibleCurrentSession()
   {
		if (_lastStartBar < 0)
			return;
    
        foreach (var ds in DataSeries)
			if (ds is ValueDataSeries vs)
				vs.SetPointOfEndLine(Math.Max(0, _lastStartBar - 1));
   }

    #endregion
}