namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Awesome Oscillator")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/16995-awesome-oscillator")]
public class AwesomeOscillator : Indicator
{
	#region Fields

	private Color _negColor = Color.Red;
	private Color _neutralColor = Color.Gray;

	private int _p1 = 34;
	private int _p2 = 5;
	private Color _posColor = Color.Green;

	private ValueDataSeries _renderSeries = new(Resources.Visualization)
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

	[Range(1, 10000)]
	public int P1
	{
		get => _p1;
		set
		{
			if (value <= _p2)
				return;

			_p1 = value;
			RecalculateValues();
		}
	}

	[Range(1, 10000)]
	public int P2
	{
		get => _p2;
		set
		{
			if (value >= _p1)
				return;

			_p2 = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public AwesomeOscillator()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Public methods

	public override string ToString()
	{
		return "Awesome Oscillator";
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
		if (bar == 0)
			DataSeries.ForEach(x => x.Clear());

		if (bar <= _p1)
			return;

		decimal sma1 = 0;
		decimal sma2 = 0;

		for (var ct = 1; ct <= _p1; ct += 1)
		{
			var candleCt = GetCandle(bar - ct + 1);
			var midPrice = (candleCt.High + candleCt.Low) / 2;
			sma1 += midPrice;

			if (ct <= _p2)
				sma2 += midPrice;
		}

		var aw = sma2 / _p2 - sma1 / _p1;
		_renderSeries[bar] = aw;
		var lastAw = bar > 0 ? _renderSeries[bar - 1] : 0;

		if (aw > lastAw)
			_renderSeries.Colors[bar] = _posColor;
		else if (aw < lastAw)
			_renderSeries.Colors[bar] = _negColor;
		else
			_renderSeries.Colors[bar] = _neutralColor;
	}

	#endregion
}