namespace ATAS.Indicators.Technical;

using OFT.Attributes;
using OFT.Localization;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
#else
using CrossColor = System.Windows.Media.Color;
#endif

[DisplayName("COT High/Low")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.CotHighDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602603")]
public class CotHigh : Indicator
{
	#region Nested types

	public enum CotMode
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.High))]
		High,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Low))]
		Low
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};
	
	private decimal _extValue;
	private CotMode _mode = CotMode.High;

	private System.Drawing.Color _negColor = System.Drawing.Color.Red;
	private System.Drawing.Color _posColor = System.Drawing.Color.Green;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Positive), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.PositiveValueColorDescription), Order = 610)]
    public CrossColor PosColor
    {
	    get => _posColor.Convert();
	    set
	    {
		    _posColor = value.Convert();
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Negative), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.NegativeValueColorDescription), Order = 620)]
    public CrossColor NegColor
    {
	    get => _negColor.Convert();
	    set
	    {
		    _negColor = value.Convert();
		    RecalculateValues();
	    }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 100)]
	public CotMode Mode
	{
		get => _mode;
		set
		{
			_mode = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public CotHigh()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

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
		{
			_extValue = 0;
			DataSeries.ForEach(x => x.Clear());
		}

		var candle = GetCandle(bar);

		if ((candle.High >= _extValue && Mode is CotMode.High)
		    ||
		    (candle.Low >= _extValue && Mode is CotMode.High))
		{
			_extValue = Mode is CotMode.High
				? candle.High
				: candle.Low;

			_renderSeries[bar] = candle.Delta;
		}
		else
		{
			_renderSeries[bar] = bar == 0 ? candle.Delta : _renderSeries[bar - 1] + candle.Delta;
		}

		_renderSeries.Colors[bar] = _renderSeries[bar] >= 0 ? _posColor : _negColor;
	}
	
	#endregion
}