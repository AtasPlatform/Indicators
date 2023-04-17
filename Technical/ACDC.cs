namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("AC DC Histogram")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43350-ac-dc-histogram")]
public class ACDC : Indicator
{
	#region Fields

	private readonly ValueDataSeries _ao = new("AO");

	private readonly ValueDataSeries _averPrice = new("Price");

	private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
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

	[Display(ResourceType = typeof(Resources), Name = "Positive", GroupName = "Drawing", Order = 610)]
	public System.Windows.Media.Color PosColor
	{
		get => _posColor.Convert();
		set
		{
			_posColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Negative", GroupName = "Drawing", Order = 620)]
	public System.Windows.Media.Color NegColor
	{
		get => _negColor.Convert();
		set
		{
			_negColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "Settings", Order = 100)]
	public int SmaPeriod1
	{
		get => _sma1.Period;
		set
		{
			if (value <= 0)
				return;

			_sma1.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "Settings", Order = 110)]
	public int SmaPeriod2
	{
		get => _sma2.Period;
		set
		{
			if (value <= 0)
				return;

			_sma2.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "SMAPeriod3", GroupName = "Settings", Order = 120)]
	public int SmaPeriod3
	{
		get => _sma3.Period;
		set
		{
			if (value <= 0)
				return;

			_sma3.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "SMAPeriod4", GroupName = "Settings", Order = 130)]
	public int SmaPeriod4
	{
		get => _sma4.Period;
		set
		{
			if (value <= 0)
				return;

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