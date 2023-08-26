namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Accelerator Oscillator")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38047-accelerator-oscilator")]
public class AC : Indicator
{
	#region Fields

	private readonly SMA _smaAc = new();
	private readonly SMA _smaLong = new();
	private readonly SMA _smaShort = new();

	private Color _negColor = DefaultColors.Red;
	private Color _neutralColor = DefaultColors.Silver;
	private Color _posColor = DefaultColors.Green;

	private ValueDataSeries _renderSeries = new("RenderSeries", Resources.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

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

	[Display(ResourceType = typeof(Resources), Name = "Neutral", GroupName = "Drawing", Order = 630)]
	public System.Windows.Media.Color NeutralColor
	{
		get => _neutralColor.Convert();
		set
		{
			_neutralColor = value.Convert();
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public AC()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		_smaShort.Period = 5;
		_smaLong.Period = 34;
		_smaAc.Period = 5;

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
	    NeutralColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		var medPrice = (candle.High - candle.Low) / 2.0m;
		var ao = _smaShort.Calculate(bar, medPrice) - _smaLong.Calculate(bar, medPrice);

		var seriesValue = ao - _smaAc.Calculate(bar, ao);
		_renderSeries[bar] = seriesValue;

		if (bar > 0)
		{
			var prevValue = _renderSeries[bar - 1];

			if (seriesValue > prevValue)
				_renderSeries.Colors[bar] = _posColor;
			else if (seriesValue < prevValue)
				_renderSeries.Colors[bar] = _negColor;
			else
				_renderSeries.Colors[bar] = _neutralColor;
		}
	}

	#endregion
}

public class SampleChartManaging : Indicator
{
	private decimal _barsSpacing = 1;
	private decimal _barsWidth = 1;
	private ChartVisualModes _modes;
	private FootprintVisualModes _visualMode;
	private FootprintContentModes _contentMode;

    public decimal BarsSpacing
	{
		get => _barsSpacing;
		set
		{
			_barsSpacing = value;
			ChartInfo?.PriceChartContainer?.SetCustomBarsSpacing(_barsSpacing);
			RedrawChart();
		}
	}

	public decimal BarsWidth
	{
		get => _barsWidth;
		set
		{
			_barsWidth = value;
			ChartInfo?.PriceChartContainer?.SetCustomBarsWidth(_barsWidth, false);
			RedrawChart();
		}
	}

	public ChartVisualModes ChartMode
	{
		get => _modes;
		set
		{
			_modes = value;

			if (ChartInfo != null)
				ChartInfo.ChartVisualMode = value;

			RedrawChart();
		}
	}

	public FootprintContentModes FootprintContentMode
    {
		get => _contentMode;
		set
		{
			_contentMode = value;

			if (ChartInfo != null)
				ChartInfo.FootprintContentMode = value;
        }
	}

	public FootprintVisualModes FootprintVisualMode
	{
		get => _visualMode;
		set
		{
			_visualMode = value;

			if (ChartInfo != null)
				ChartInfo.FootprintVisualMode = value;
        }
	}

	private FootprintColorSchemes _colorScheme;

	public FootprintColorSchemes FootprintColorScheme
    {
		get => _colorScheme;
		set
		{
			_colorScheme = value;

			if (ChartInfo != null)
				ChartInfo.FootprintColorScheme = value;
        }
	}

    protected override void OnCalculate(int bar, decimal value)
	{
	}

	protected override void OnDispose()
	{
		ChartInfo?.PriceChartContainer?.SetCustomBarsSpacing(null);
		ChartInfo?.PriceChartContainer?.SetCustomBarsWidth(ChartInfo.PriceChartContainer.BarsWidth, false);
	}
}