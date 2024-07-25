﻿namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Windows.Media;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;

using Color = System.Windows.Media.Color;
using Pen = System.Drawing.Pen;

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
		Color = Color.FromArgb(0, 0, 255, 0),
		LineDashStyle = LineDashStyle.Solid,
		VisualType = VisualMode.Square,
		Width = 1,
        DescriptionKey = nameof(Strings.SessionAveragePriceDescription)
    };

	private RangeDataSeries _ibhx32 = new("Ibhx32", "ibhx32")
	{
		RangeColor = Colors.Transparent,
		DrawAbovePrice = false,
		IsHidden = true
	};
	private RangeDataSeries _ibhx21 = new("Ibhx21", "ibhx21")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibhx1h = new("Ibhx1h", "ibhx1h")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibHm = new("IbHm", "ibHm")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibMl = new("IbM1", "ibM1")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _ibl1 = new("Ibl1", "ibl1")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _iblx12 = new("Ibl12", "ibl12")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};
	private RangeDataSeries _iblx23 = new("Ibl23", "ibl23")
	{
		RangeColor = Colors.Transparent,
        DrawAbovePrice = false,
        IsHidden = true
	};

    private Color _borderColor = DefaultColors.Red.Convert();
	private int _borderWidth = 1;
	private bool _calculate;
	private bool _customSessionStart;
	private int _days = 20;
    private bool _drawText = true;
	private TimeSpan _endDate;
	private DateTime _endTime = DateTime.MaxValue;
	private Color _fillColor = DefaultColors.Yellow.Convert();
	private bool _highLowIsSet;
	private decimal _ibMax = decimal.MinValue;
	private decimal _ibMin = decimal.MaxValue;
	private decimal _ibmValue = decimal.Zero;

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
	public Color BorderColor
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
	public Color FillColor
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
	public Color Ibhx32
	{
		get=>_ibhx32.RangeColor; 
		set=>_ibhx32.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX21),
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription),Order = 210)]
	public Color Ibhx21 
	{
		get => _ibhx21.RangeColor;
		set => _ibhx21.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHX1H),
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 220)]
	public Color Ibhx1h 
	{
		get => _ibhx1h.RangeColor;
		set => _ibhx1h.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBHM), 
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 230)]
	public Color IbHm
	{
		get => _ibHm.RangeColor;
		set => _ibHm.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBML), 
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 240)]
	public Color IbMl
	{
		get => _ibMl.RangeColor;
		set => _ibMl.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBL1), 
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 250)]
	public Color Ibl1
	{
		get => _ibl1.RangeColor;
		set => _ibl1.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBLX12),
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 260)]
	public Color Iblx12
	{
		get => _iblx12.RangeColor;
		set => _iblx12.RangeColor = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.IBLX23),
		GroupName = nameof(Strings.BackGround), Description = nameof(Strings.AreaColorDescription), Order = 270)]
	public Color Iblx23
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

			return;
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

			if (StartDate <= EndDate)
				inSession = (StartDate <= time || StartDate <= lastTime) && time < EndDate;
			else
			{
				inSession = (StartDate <= lastTime && time >= StartDate && time > EndDate)
					||
					((EndDate >= time || EndDate >= lastTime) && time < EndDate);
			}

			if (!inSession)
			{
				foreach (var dataSeries in DataSeries)
					if (dataSeries is ValueDataSeries series)
						series.SetPointOfEndLine(bar - 1);
                return;
			}
		}

		var prevTime = GetCandle(bar - 1).Time.AddHours(InstrumentInfo.TimeZone);
		var candleFullTime = candle.Time.AddHours(InstrumentInfo.TimeZone);

		var isStart = _customSessionStart ? time >= _startDate && (prevTime.TimeOfDay < _startDate || prevTime.Date < candleFullTime.Date) : IsNewSession(bar);

		var isEnd = (PeriodMode is PeriodType.Minutes && candleFullTime >= _endTime && prevTime < _endTime)
			|| (PeriodMode is PeriodType.Bars && bar - _lastStartBar >= Period);

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
			_endTime = candleFullTime.AddMinutes(_period);

			foreach (var dataSeries in DataSeries)
				if(dataSeries is ValueDataSeries series)
					series.SetPointOfEndLine(bar - 1);

			if (ShowOpenRange)
			{
				var pen = new Pen(ConvertColor(_borderColor))
				{
					Width = _borderWidth
				};
				var brush = new SolidBrush(ConvertColor(_fillColor));
				_rectangle = new DrawingRectangle(bar, decimal.Zero, bar, decimal.Zero, pen, brush);
				Rectangles.Add(_rectangle);
			}
		}
		else if (isEnd)
		{
			_calculate = false;
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

	#endregion

	#region Private methods
	
	private void DataSeriesPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (!_initialized)
			return;

		RecalculateValues();
	}

	private System.Drawing.Color ConvertColor(Color color)
	{
		return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
	}

	#endregion
}