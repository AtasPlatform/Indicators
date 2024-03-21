namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;

[DisplayName("Ichimoku Kinko Hyo")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.IchimokuDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602553")]
public class Ichimoku : Indicator
{
	#region Fields

	private readonly ValueDataSeries _baseLine = new("BaseLine", "Base")
	{
		Color = Color.FromRgb(153, 21, 21),
        DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
    };

    private readonly ValueDataSeries _conversionLine = new("ConversionLine", "Conversion") 
	{ 
		Color = Color.FromRgb(4, 150, 255),
        DescriptionKey = nameof(Strings.ConversionLineSettingsDescription)
    };

    private readonly ValueDataSeries _laggingSpan = new("LaggingSpan", "Lagging Span")
	{ 
		Color = Color.FromRgb(69, 153, 21),
        DescriptionKey = nameof(Strings.LaggingLineSettingsDescription)
    };

    private readonly ValueDataSeries _leadLine1 = new("LeadLine1", "Lead1") 
	{
		Color = DefaultColors.Green.Convert(),
        DescriptionKey = nameof(Strings.TopChannelSettingsDescription)
    };

    private readonly ValueDataSeries _leadLine2 = new("LeadLine2", "Lead2") 
	{ 
		Color = DefaultColors.Red.Convert(),
        DescriptionKey = nameof(Strings.BottomChannelSettingsDescription)
    };

    private readonly RangeDataSeries _upSeries = new("UpSeries", "Up")
    {
        RangeColor = Color.FromArgb(50, 0, 255, 0),
        DrawAbovePrice = false,
        DescriptionKey = nameof(Strings.UpAreaSettingsDescription)
    };

	private readonly RangeDataSeries _downSeries = new("DownSeries", "Down")
	{
		RangeColor = Color.FromArgb(50, 255, 0, 0),
		DrawAbovePrice = false,
		DescriptionKey = nameof(Strings.DownAreaSettingsDescription)
	};

    private readonly Highest _baseHigh = new() { Period = 26 };
    private readonly Lowest _baseLow = new() { Period = 26 };
	private readonly Highest _conversionHigh = new() { Period = 9 };
	private readonly Lowest _conversionLow = new() { Period = 9 };
	private readonly Highest _spanHigh = new() { Period = 52 };
	private readonly Lowest _spanLow = new() { Period = 52 };
	
    private int _days;
	private int _displacement = 26;
	private int _targetBar;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
    [Range(0, 10000)]
	public int Days
	{
		get => _days;
		set
		{
			_days = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = "TenkanSen", GroupName = nameof(Strings.Settings), Description = nameof(Strings.ConversionLinePeriodDescription), Order = 100)]
	[Range(1, 10000)]
	public int Tenkan
	{
		get => _conversionHigh.Period;
		set
		{
			_conversionHigh.Period = _conversionLow.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
	[Display(ResourceType = typeof(Strings), Name = "KijunSen", GroupName = nameof(Strings.Settings), Description = nameof(Strings.BaseLinePeriodDescription), Order = 110)]
	[Range(1, 10000)]
	public int Kijun
	{
		get => _baseHigh.Period;
		set
		{
			_baseHigh.Period = _baseLow.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = "SenkouSpanB", GroupName = nameof(Strings.Settings), Description = nameof(Strings.LaggingLinePeriodDescription), Order = 120)]
    [Range(1, 10000)]
	public int Senkou
	{
		get => _spanHigh.Period;
		set
		{
			_spanHigh.Period = _spanLow.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = "Displacement", GroupName = nameof(Strings.Settings), Description = nameof(Strings.BarShiftDescription), Order = 130)]
    [Range(1, 10000)]
	public int Displacement
	{
		get => _displacement;
		set
		{
			_displacement = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public Ichimoku()
		: base(true)
	{
		DenyToChangePanel = true;
		EnableCustomDrawing = true;
		SubscribeToDrawingEvents(DrawingLayouts.Historical | DrawingLayouts.LatestBar);

		DataSeries[0] = _conversionLine;
		DataSeries.Add(_baseLine);
        DataSeries.Add(_laggingSpan);
        DataSeries.Add(_leadLine1);
		DataSeries.Add(_leadLine2);
		DataSeries.Add(_upSeries);
		DataSeries.Add(_downSeries);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		if (bar == 0)
		{
			DataSeries.ForEach(x => x.Clear());
			_targetBar = 0;

			if (_days > 0)
			{
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

				if (_targetBar > 0)
				{
					_conversionLine.SetPointOfEndLine(_targetBar - 1);
					_laggingSpan.SetPointOfEndLine(_targetBar - _displacement);
					_baseLine.SetPointOfEndLine(_targetBar - 1);
					_leadLine1.SetPointOfEndLine(_targetBar + _displacement - 2);
					_leadLine2.SetPointOfEndLine(_targetBar + _displacement - 2);
				}
			}
		}

		_conversionHigh.Calculate(bar, candle.High);
		_conversionLow.Calculate(bar, candle.Low);

		_baseHigh.Calculate(bar, candle.High);
		_baseLow.Calculate(bar, candle.Low);

		_spanHigh.Calculate(bar, candle.High);
		_spanLow.Calculate(bar, candle.Low);

		if (bar < _targetBar)
			return;

		_baseLine[bar] = (_baseHigh[bar] + _baseLow[bar]) / 2;
		_conversionLine[bar] = (_conversionHigh[bar] + _conversionLow[bar]) / 2;

		var lineBar = bar + Displacement;
		_leadLine1[lineBar] = (_conversionLine[bar] + _baseLine[bar]) / 2;
		_leadLine2[lineBar] = (_spanHigh[bar] + _spanLow[bar]) / 2;

		if (bar - _displacement + 1 >= 0)
		{
			var targetBar = bar - _displacement;
			_laggingSpan[targetBar] = candle.Close;

			if (bar == CurrentBar - 1)
			{
				for (var i = targetBar + 1; i < CurrentBar; i++)
					_laggingSpan[i] = candle.Close;
			}
		}

		if (_leadLine1[bar] == 0 || _leadLine2[bar] == 0)
			return;

		if (_leadLine1[bar] > _leadLine2[bar])
		{
			_upSeries[bar].Upper = _leadLine1[bar];
			_upSeries[bar].Lower = _leadLine2[bar];

			if (_leadLine1[bar - 1] < _leadLine2[bar - 1])
				_downSeries[bar] = _upSeries[bar];
		}
		else
		{
			_downSeries[bar].Upper = _leadLine2[bar];
			_downSeries[bar].Lower = _leadLine1[bar];

			if (_leadLine1[bar - 1] > _leadLine2[bar - 1])
				_upSeries[bar] = _downSeries[bar];
		}
	}

	#endregion
}