namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Settings;

using Color = System.Drawing.Color;

[DisplayName("Woodies CCI")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.WoodiesCCIDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602565")]
public class WoodiesCCI : Indicator
{
	#region Fields

	private readonly ValueDataSeries _cciSeries = new("CciSeries", "CCI")
	{
		VisualType = VisualMode.Histogram, 
		ShowCurrentValue = false, 
		Width = 2
	};
	
	private readonly CCI _entryCci = new() { Name = "Entry CCI" };

	private readonly ValueDataSeries _lsmaSeries = new("LsmaSeries", "LSMA")
	{
		VisualType = VisualMode.Block, 
		ShowCurrentValue = false, 
		ScaleIt = false, 
		Width = 2, 
		IgnoredByAlerts = true,
		ShowTooltip = false
	};
	
	private readonly CCI _trendCci = new() { Name = "Trend CCI" };

	private LineSeries _line100 = new("Line100", "100")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = 100,
		Width = 1,
		IsHidden = true,
		DescriptionKey = nameof(Strings.OverboughtLimitDescription)
	};

	private LineSeries _line200 = new("Line200", "200")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = 200,
		Width = 1,
		IsHidden = true,
        DescriptionKey = nameof(Strings.OverboughtLimitDescription)
    };

	private LineSeries _line300 = new("Line300", "300")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = 300,
		Width = 1,
		IsHidden = true,
		UseScale = true,
        DescriptionKey = nameof(Strings.OverboughtLimitDescription)
    };

	private LineSeries _lineM100 = new("LineM100", "-100")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = -100,
		Width = 1,
		IsHidden = true,
        DescriptionKey = nameof(Strings.OversoldLimitDescription)
    };

	private LineSeries _lineM200 = new("LineM200", "-200")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = -200,
		Width = 1,
		IsHidden = true,
        DescriptionKey = nameof(Strings.OversoldLimitDescription)
    };

	private LineSeries _lineM300 = new("LineM300", "-300")
	{
		Color = Color.Gray.Convert(),
		LineDashStyle = LineDashStyle.Dash,
		Value = -300,
		Width = 1,
		UseScale = true,
		IsHidden = true,
        DescriptionKey = nameof(Strings.OversoldLimitDescription)
    };

	private bool _drawLines = true;
	private int _lsmaPeriod = 25;
	private int _trendPeriod = 5;

	private int _trendUp, _trendDown;
	private Color _trendUpColor = DefaultColors.Blue;
	private Color _trendDownColor = DefaultColors.Maroon;
	private	Color _noTrendColor = DefaultColors.Gray;
	private Color _timeBarColor = DefaultColors.Yellow;
	private Color _positiveLsmaColor = DefaultColors.Green;
	private Color _negativeLsmaColor = DefaultColors.Red;

	#endregion

	#region Properties

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = "LSMA Period", GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriodDescription))]
	[Range(1, 10000)]
	public int LSMAPeriod
	{
		get => _lsmaPeriod;
		set
		{
			_lsmaPeriod = value;
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = "Trend Period", GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
	[Range(1, 10000)]
	public int TrendPeriod
	{
		get => _trendPeriod;
		set
		{
			_trendPeriod = value;
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = "Trend CCI Period", GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
	[Range(1, 10000)]
	public int TrendCCIPeriod
	{
		get => _trendCci.Period;
		set
		{
			_trendCci.Period = value;
			RecalculateValues();
		}
	}

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = "Entry CCI Period", GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
	[Range(1, 10000)]
	public int EntryCCIPeriod
	{
		get => _entryCci.Period;
		set
		{
			_entryCci.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Line), Description = nameof(Strings.DrawLinesDescription))]
	public bool DrawLines
	{
		get => _drawLines;
		set
		{
			_drawLines = value;

			if (value)
			{
				if (LineSeries.Any())
					return;

				LineSeries.Add(_line100);
				LineSeries.Add(_line200);
				LineSeries.Add(_line300);
				LineSeries.Add(_lineM100);
				LineSeries.Add(_lineM200);
				LineSeries.Add(_lineM300);
			}
			else
			{
				LineSeries.Clear();
			}

			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.p300), GroupName = nameof(Strings.Line), Description = nameof(Strings.OverboughtLimitDescription))]
	public LineSeries Line300
	{
		get => _line300;
		set => _line300 = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.p200), GroupName = nameof(Strings.Line), Description = nameof(Strings.OverboughtLimitDescription))]
	public LineSeries Line200
	{
		get => _line200;
		set => _line200 = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.p100), GroupName = nameof(Strings.Line), Description = nameof(Strings.OverboughtLimitDescription))]
	public LineSeries Line100
	{
		get => _line100;
		set => _line100 = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.m100), GroupName = nameof(Strings.Line), Description = nameof(Strings.OversoldLimitDescription))]
	public LineSeries LineM100
	{
		get => _lineM100;
		set => _lineM100 = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.m200), GroupName = nameof(Strings.Line), Description = nameof(Strings.OversoldLimitDescription))]
	public LineSeries LineM200
	{
		get => _lineM200;
		set => _lineM200 = value;
	}
	
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.m300), GroupName = nameof(Strings.Line), Description = nameof(Strings.OversoldLimitDescription))]
	public LineSeries LineM300
	{
		get => _lineM300;
		set => _lineM300 = value;
	}

    [Display(ResourceType = typeof(Strings), Name = "CCI Trend Up", GroupName = nameof(Strings.Colors), Description = nameof(Strings.BullishColorDescription))]
    public Color TrendUpColor
    {
        get => _trendUpColor;
        set
        {
            _trendUpColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "CCI Trend Down", GroupName = nameof(Strings.Colors), Description = nameof(Strings.BearishColorDescription))]
    public Color TrendDownColor
    {
        get => _trendDownColor;
        set
        {
            _trendDownColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "No Trend", GroupName = nameof(Strings.Colors), Description = nameof(Strings.NeutralColorDescription))]
    public Color NoTrendColor
    {
        get => _noTrendColor;
        set
        {
            _noTrendColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Time Bar", GroupName = nameof(Strings.Colors), Description = nameof(Strings.NewTrendColorDescription))]
    public Color TimeBarColor
    {
        get => _timeBarColor;
        set
        {
            _timeBarColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Negative LSMA", GroupName = nameof(Strings.Colors), Description = nameof(Strings.NegativeValueColorDescription))]
    public Color NegativeLsmaColor
    {
        get => _negativeLsmaColor;
        set
        {
            _negativeLsmaColor = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "Positive LSMA", GroupName = nameof(Strings.Colors), Description = nameof(Strings.PositiveValueColorDescription))]
    public Color PositiveLsmaColor
    {
        get => _positiveLsmaColor;
        set
        {
            _positiveLsmaColor = value;
            RecalculateValues();
        }
    }

    #endregion

    #region ctor

    public WoodiesCCI() : base(true)
	{
        Panel = IndicatorDataProvider.NewPanel;
        DenyToChangePanel = true;

        TrendCCIPeriod = 14;
		EntryCCIPeriod = 6;

		var trendCciDataSeries = (ValueDataSeries)_trendCci.DataSeries[0];
		trendCciDataSeries.Id = "TrendCciDataSeries";
        trendCciDataSeries.Name = "Trend CCI";
		trendCciDataSeries.Width = 2;
		trendCciDataSeries.Color = DefaultColors.Purple.Convert();
		trendCciDataSeries.IgnoredByAlerts = true;

		var entryCciDataSeries = (ValueDataSeries)_entryCci.DataSeries[0];
		entryCciDataSeries.Id = "EntryCciDataSeries";
        entryCciDataSeries.Name = "Entry CCI";
		entryCciDataSeries.Color = DefaultColors.Orange.Convert();
		entryCciDataSeries.IgnoredByAlerts = true;

		var zeroLineDataSeries = (ValueDataSeries)DataSeries[0];
		zeroLineDataSeries.ShowCurrentValue = false;
		zeroLineDataSeries.Name = "Zero Line";
		zeroLineDataSeries.Color = Color.Gray.Convert();
		zeroLineDataSeries.VisualType = VisualMode.Hide;
		zeroLineDataSeries.IgnoredByAlerts = true;
		zeroLineDataSeries.DescriptionKey = Strings.ZeroLineDescription;


        DataSeries.Add(_cciSeries);
		DataSeries.Add(trendCciDataSeries);
		DataSeries.Add(entryCciDataSeries);
		DataSeries.Add(_lsmaSeries);

        LineSeries.Add(_line100);
		LineSeries.Add(_line200);
		LineSeries.Add(_line300);
		LineSeries.Add(_lineM100);
		LineSeries.Add(_lineM200);
		LineSeries.Add(_lineM300);

		Add(_trendCci);
		Add(_entryCci);
	}

	#endregion

	#region Protected methods
	
	protected override void OnCalculate(int bar, decimal value)
	{
		try
		{
			this[bar] = 0;

			if (_trendCci[bar] > 0 && _trendCci[bar - 1] < 0)
			{
				if (_trendDown > TrendPeriod)
					_trendUp = 0;
			}

			_cciSeries[bar] = _trendCci[bar];

			if (_trendCci[bar] > 0)
			{
				if (_trendUp < TrendPeriod)
				{
					_cciSeries.Colors[bar] = _noTrendColor;
					_trendUp++;
				}

				if (_trendUp == TrendPeriod)
				{
					_cciSeries.Colors[bar] = _timeBarColor;
					_trendUp++;
				}

				if (_trendUp > TrendPeriod)
					_cciSeries.Colors[bar] = _trendUpColor;
			}

			if (_trendCci[bar] < 0 && _trendCci[bar - 1] > 0)
			{
				if (_trendUp > TrendPeriod)
					_trendDown = 0;
			}

			if (_trendCci[bar] < 0)
			{
				if (_trendDown < TrendPeriod)
				{
					_cciSeries.Colors[bar] = _noTrendColor;
					_trendDown++;
				}

				if (_trendDown == TrendPeriod)
				{
					_cciSeries.Colors[bar] = _timeBarColor;
					_trendDown++;
				}

				if (_trendDown > TrendPeriod)
					_cciSeries.Colors[bar] = _trendDownColor;
			}

			decimal summ = 0;

			if (bar < LSMAPeriod + 2)
				return;

			var lengthvar = (decimal)((LSMAPeriod + 1) / 3.0);

			for (var i = LSMAPeriod; i >= 1; i--)
				summ += (i - lengthvar) * GetCandle(bar - LSMAPeriod + i).Close;

			var wt = summ * 6 / (LSMAPeriod * (LSMAPeriod + 1));
			_lsmaSeries[bar] = 0.00001m;

			_lsmaSeries.Colors[bar] = wt > GetCandle(bar).Close
									? _negativeLsmaColor
									: _positiveLsmaColor;
		}
		catch { }
	}
	
	#endregion
}