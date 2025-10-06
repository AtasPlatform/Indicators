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
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

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

    // Last completed IB snapshot (used after the window ends)
    private int _lastIbEndBar = -1;
    private decimal _sIBH, _sIBL, _sIBM, _sMID;
    private decimal _sIBHX1, _sIBHX2, _sIBHX3;
    private decimal _sIBLX1, _sIBLX2, _sIBLX3;

    // Track where the whole trading session ended (last bar inside the session).
    private int _lastSessionEndBar = -1;

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

    [Display(Name = "Label Position",
    GroupName = nameof(Strings.Show),
    Description = "Where to draw labels for IB levels",
    Order = 136)]
    private LabelPosition _labelPosition = LabelPosition.Bar;
    public LabelPosition LabelPosition
    {
        get => _labelPosition;
        set
        {
            if (_labelPosition == value)
                return;

            _labelPosition = value;
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

                // Remember the last in-session bar (set only once)
                if (_lastSessionEndBar < 0)
                    _lastSessionEndBar = Math.Max(0, bar - 1);

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
            _lastIbEndBar = -1;
            _lastSessionEndBar = -1;  // <— reset anchor for labels

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
            // Compute final values
            var finalDiff = _ibMax - _ibMin;
            var finalMid = (_minValue + _maxValue) / 2m;
            var finalIbm = (_ibMin + _ibMax) / 2m;

            // Snapshot last session
            _lastIbEndBar = bar;
            _sIBH = _ibMax; _sIBL = _ibMin; _sIBM = finalIbm; _sMID = finalMid;
            _sIBHX1 = _ibMax + finalDiff * _x1;
            _sIBHX2 = _ibMax + finalDiff * _x2;
            _sIBHX3 = _ibMax + finalDiff * _x3;
            _sIBLX1 = _ibMin - finalDiff * _x1;
            _sIBLX2 = _ibMin - finalDiff * _x2;
            _sIBLX3 = _ibMin - finalDiff * _x3;

            _calculate = _isStarted = false;

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

    /// <summary>
    /// Renders a single set of IB labels for the most recent session.
    /// It does not draw historical session labels and does not modify any ValueDataSeries.
	//// Labels rendering - freeze at the last in-session bar only for CUSTOM sessions.
	/// For auto sessions, labels always follow the chosen LabelPosition.
    /// </summary>
    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (LabelPosition == LabelPosition.None || ChartInfo is null)
            return;

        var chartWidth = ChartInfo.PriceChartContainer.Region.Width;
        var endBar = Math.Max(0, CurrentBar - 1);

        // IB window finished (we have a snapshot of IB levels)
        bool ibWindowFinished = _lastIbEndBar >= 0 && endBar >= _lastIbEndBar && !_isStarted;

        // Session considered finished ONLY for custom sessions.
        // For auto sessions we do not freeze labels; they continue to follow LabelPosition.
        bool afterCustomSession =
            _customSessionStart &&
            _lastSessionEndBar >= 0 &&
            endBar >= _lastSessionEndBar;

        // Resolve level values:
        // - If the IB window finished, prefer the snapshot (stable values during/after session).
        // - Otherwise, use the last formed bar
        decimal vMid, vIbh, vIbl, vIbm, vX1u, vX2u, vX3u, vX1l, vX2l, vX3l;

        if (ibWindowFinished)
        {
            vMid = _sMID; vIbh = _sIBH; vIbl = _sIBL; vIbm = _sIBM;
            vX1u = _sIBHX1; vX2u = _sIBHX2; vX3u = _sIBHX3;
            vX1l = _sIBLX1; vX2l = _sIBLX2; vX3l = _sIBLX3;
        }
        else
        {
            vMid = _mid[endBar]; vIbh = _ibh[endBar]; vIbl = _ibl[endBar]; vIbm = _ibm[endBar];
            vX1u = _ibhx1[endBar]; vX2u = _ibhx2[endBar]; vX3u = _ibhx3[endBar];
            vX1l = _iblx1[endBar]; vX2l = _iblx2[endBar]; vX3l = _iblx3[endBar];
        }

        // Build the draw list (label text + series reference + value)
        var items = new (string Label, ValueDataSeries Series, decimal Value)[]
        {
        ("Mid",   _mid,   vMid),
        ("IBH",   _ibh,   vIbh),
        ("IBL",   _ibl,   vIbl),
        ("IBM",   _ibm,   vIbm),
        ("IBHX1", _ibhx1, vX1u),
        ("IBHX2", _ibhx2, vX2u),
        ("IBHX3", _ibhx3, vX3u),
        ("IBLX1", _iblx1, vX1l),
        ("IBLX2", _iblx2, vX2l),
        ("IBLX3", _iblx3, vX3l),
        };

        // Decide the X anchor for labels:
        // - If AFTER a CUSTOM session ended: freeze at the last in-session bar (Bar-style), ignoring LabelPosition.
        // - Else (during session OR auto sessions): follow LabelPosition normally.
        int x; bool alignRight;

        if (afterCustomSession)
        {
            // Freeze at the last bar of the custom session (behaves like Bar position).
            var xBar = ChartInfo.GetXByBar(_lastSessionEndBar);
            var barWidth = (int)ChartInfo.PriceChartContainer.BarsWidth;
            x = xBar + barWidth + 5;
            alignRight = false;
        }
        else
        {
            // Live (or auto sessions): honor the chosen LabelPosition
            switch (LabelPosition)
            {
                case LabelPosition.Right:
                    x = chartWidth - 5; alignRight = true; break;
                case LabelPosition.Left:
                    x = 5; alignRight = false; break;
                case LabelPosition.Bar:
                default:
                    var xBar = ChartInfo.GetXByBar(endBar);
                    var barWidth = (int)ChartInfo.PriceChartContainer.BarsWidth;
                    x = xBar + barWidth + 5; alignRight = false; break;
            }
        }

        // Draw labels (after any overlay lines so labels stay on top)
        foreach (var (label, series, price) in items)
        {
            if (price == 0m || price == decimal.MinValue || price == decimal.MaxValue)
                continue;

            var y = ChartInfo.GetYByPrice(price, false);
            if (y < 0 || y > ChartInfo.PriceChartContainer.Region.Height)
                continue;

            DrawTextLabel(context, label, x, y, series, alignRight);
        }
    }

    private void DrawTextLabel(RenderContext context, string text, int x, int y, ValueDataSeries series, bool alignRight)
    {
        var size = context.MeasureString(text, _font);
        var textColor = ChartInfo.ColorsStore.MouseTextColor;
        var backgroundColor = ChartInfo.ColorsStore.BaseBackgroundColor;

        var pen = new PenSettings
        {
            Color = series.Color,
            Width = series.Width,
            LineDashStyle = series.LineDashStyle
        }.RenderObject;

        var rectX = alignRight ? x - size.Width : x;
        var rect = new Rectangle(rectX - 2, y - size.Height / 2 - 1, size.Width + 4, size.Height + 2);

        context.FillRectangle(backgroundColor, rect);
        context.DrawRectangle(pen, rect);

        var textRect = new Rectangle(rectX, y - size.Height / 2, size.Width, size.Height);
        var format = alignRight ? _stringRightFormat : _stringLeftFormat;
        context.DrawString(text, _font, textColor, textRect, format);
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

    private RenderStringFormat _stringRightFormat = new()
    {
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    private RenderStringFormat _stringLeftFormat = new()
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };

    #endregion
}