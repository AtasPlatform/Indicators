namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Delta Turnaround")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DeltaTurnaroundDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602364")]
public class DeltaTurnaround : Indicator
{
	#region Fields

	private readonly ValueDataSeries _negSeries = new("NegSeries", Strings.Down)
	{
		Color = System.Drawing.Color.Red.Convert(),
		VisualType = VisualMode.DownArrow,
        DescriptionKey = nameof(Strings.NegativeDeltaSettingsDescription),
    };

	private readonly ValueDataSeries _posSeries = new("PosSeries", Strings.Up)
	{
		Color = System.Drawing.Color.Green.Convert(),
		VisualType = VisualMode.UpArrow,
        DescriptionKey = nameof(Strings.PositiveDeltaSettingsDescription),
    };

	#endregion

	#region ctor

	public DeltaTurnaround()
		: base(true)
	{
		DenyToChangePanel = true;

		DataSeries[0] = _posSeries;
		DataSeries.Add(_negSeries);
	}

    #endregion

    #region Protected methods

    protected override void OnApplyDefaultColors()
    {
	    if (ChartInfo is null)
		    return;

	    _posSeries.Color = ChartInfo.ColorsStore.UpCandleColor.Convert();
	    _negSeries.Color = ChartInfo.ColorsStore.DownCandleColor.Convert();
    }

    protected override void OnCalculate(int bar, decimal value)
	{
		if (bar < 2)
			return;

		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);
		var prev2Candle = GetCandle(bar - 2);

		if (prevCandle.Close - prevCandle.Open > 0
		    && prev2Candle.Close - prev2Candle.Open > 0
		    && candle.Close - candle.Open < 0
		    && candle.High >= prevCandle.High
		    && candle.Delta < 0)
			_negSeries[bar] = candle.High + InstrumentInfo.TickSize * 2;
		else
			_negSeries[bar] = 0;

		if (prevCandle.Close - prevCandle.Open < 0
		    && prev2Candle.Close - prev2Candle.Open < 0
		    && candle.Close - candle.Open > 0
		    && candle.Low <= prevCandle.Low
		    && candle.Delta > 0)
			_posSeries[bar] = candle.Low - InstrumentInfo.TickSize * 2;
		else
			_posSeries[bar] = 0;
	}

	#endregion
}