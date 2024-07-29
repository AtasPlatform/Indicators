namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

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
		Color = Color.Red.Convert(),
		VisualType = VisualMode.DownArrow,
		DescriptionKey = nameof(Strings.NegativeDeltaSettingsDescription)
	};

	private readonly ValueDataSeries _posSeries = new("PosSeries", Strings.Up)
	{
		Color = Color.Green.Convert(),
		VisualType = VisualMode.UpArrow,
		DescriptionKey = nameof(Strings.PositiveDeltaSettingsDescription)
	};

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlert), GroupName = nameof(Strings.UpAlert),
		Description = nameof(Strings.UpAlertFileFilterDescription), Order = 300)]
	public bool UseAlerts { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts),
		Description = nameof(Strings.AlertFileDescription), Order = 320)]
	public string AlertFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription), Order = 330)]
	public CrossColor AlertForeColor { get; set; } = CrossColor.FromArgb(255, 247, 249, 249);

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription), Order = 340)]
	public CrossColor AlertBGColor { get; set; } = CrossColor.FromArgb(255, 75, 72, 72);

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

		var checkAlerts = bar == CurrentBar - 1 && UseAlerts;

		if (prevCandle.Close - prevCandle.Open > 0
		    && prev2Candle.Close - prev2Candle.Open > 0
		    && candle.Close - candle.Open < 0
		    && candle.High >= prevCandle.High
		    && candle.Delta < 0)
		{
			var lastValue = _negSeries[bar];
			_negSeries[bar] = candle.High + InstrumentInfo.TickSize * 2;

			if(lastValue == 0 && checkAlerts)
				AddAlert(AlertFile, InstrumentInfo.Instrument, "Delta turnaround down signal.", AlertBGColor, AlertForeColor);
		}
		else
			_negSeries[bar] = 0;

		if (prevCandle.Close - prevCandle.Open < 0
		    && prev2Candle.Close - prev2Candle.Open < 0
		    && candle.Close - candle.Open > 0
		    && candle.Low <= prevCandle.Low
		    && candle.Delta > 0)
		{
			var lastValue = _posSeries[bar];
            _posSeries[bar] = candle.Low - InstrumentInfo.TickSize * 2;

			if (lastValue == 0 && checkAlerts)
				AddAlert(AlertFile, InstrumentInfo.Instrument, "Delta turnaround down signal.", AlertBGColor, AlertForeColor);
        }
		else
			_posSeries[bar] = 0;
	}

	#endregion
}