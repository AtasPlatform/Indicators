namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("AC DC Histogram")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ACDCDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602293")]
public class ACDC : Indicator
{
	#region Fields

	private readonly ValueDataSeries _ao = new("AO");

	private readonly ValueDataSeries _averPrice = new("Price");

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
    };

	private readonly SMA _sma1 = new();
	private readonly SMA _sma2 = new();
	private readonly SMA _sma3 = new();
	private readonly SMA _sma4 = new();

	private Color _negColor = Color.Red;
	private Color _posColor = Color.Green;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueDescription), Order = 610)]
	public CrossColor PosColor
	{
		get => _posColor.Convert();
		set
		{
			_posColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueDescription), Order = 620)]
	public CrossColor NegColor
	{
		get => _negColor.Convert();
		set
		{
			_negColor = value.Convert();
			RecalculateValues();
		}
	}

    [Parameter]
	[Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod1), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriod1Description), Order = 100)]
	public int SmaPeriod1
	{
		get => _sma1.Period;
		set
		{
			_sma1.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod2), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriod2Description), Order = 110)]
	public int SmaPeriod2
	{
		get => _sma2.Period;
		set
		{
			_sma2.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod3), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriod3Description), Order = 120)]
	public int SmaPeriod3
	{
		get => _sma3.Period;
		set
		{
			_sma3.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Range(1, 10000)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMAPeriod4), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriod4Description), Order = 130)]
	public int SmaPeriod4
	{
		get => _sma4.Period;
		set
		{
			_sma4.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public ACDC()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		_sma1.Period = 34;
		_sma2.Period = 5;
		_sma3.Period = 10;
		_sma4.Period = 5;

		DataSeries[0] = _renderSeries;
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    PosColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
	    NegColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
			DataSeries.ForEach(x => x.Clear());

		var candle = GetCandle(bar);
		_averPrice[bar] = (candle.High + candle.Low) / 2;
		_sma1.Calculate(bar, _averPrice[bar]);
		_sma2.Calculate(bar, _averPrice[bar]);

		_ao[bar] = _sma2[bar] - _sma1[bar];

		_sma4.Calculate(bar, _ao[bar]);
		_renderSeries[bar] = _sma3.Calculate(bar, _ao[bar] - _sma4[bar]);

		if (bar > 0)
		{
			var lastValue = _renderSeries[bar - 1];

			if (_sma3[bar] - lastValue > 0)
				_renderSeries.Colors[bar] = _posColor;
			else if (_sma3[bar] - lastValue < 0)
				_renderSeries.Colors[bar] = _negColor;
		}
		else
		{
			if (_sma3[bar] > 0)
				_renderSeries.Colors[bar] = _posColor;
			else
				_renderSeries.Colors[bar] = _negColor;
		}
	}

	#endregion
}