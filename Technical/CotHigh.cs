namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

[DisplayName("COT High/Low")]
public class CotHigh : Indicator
{
	#region Nested types

	public enum CotMode
	{
		[Display(ResourceType = typeof(Resources), Name = "High")]
		High,

		[Display(ResourceType = typeof(Resources), Name = "Low")]
		Low
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
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

    [Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 100)]
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