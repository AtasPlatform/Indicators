namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Super Trend")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.SuperTrendDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602482")]
public class SuperTrend : Indicator
{
	#region Fields

	private readonly ATR _atr = new() { Period = 14 };

    private ValueDataSeries _trend = new("trend");
    private ValueDataSeries _upTrend = new("UpTrendId", "Up Trend")
    {
        Color = DefaultColors.Blue.Convert(),
        Width = 2,
        VisualType = VisualMode.Square,
        ShowZeroValue = false,
        DescriptionKey = nameof(Strings.UpTrendSettingsDescription)
    };

    private ValueDataSeries _dnTrend = new("DnTrend", "Down Trend")
	{
		VisualType = VisualMode.Square,
		Color = DefaultColors.Maroon.Convert(),
		Width = 2,
		ShowZeroValue = false,
        DescriptionKey = nameof(Strings.DownTrendSettingsDescription)
    };

	private int _lastAlert;
	private decimal _lastPrice;
	private decimal _multiplier = 1.7m;
	private string _tickFormat;

	#endregion

	#region Properties

	[Parameter]
	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 20)]
	[Range(1, 10000)]
	public int Period
	{
		get => _atr.Period;
		set
		{
			_atr.Period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription), Order = 30)]
	public decimal Multiplier
	{
		get => _multiplier;
		set
		{
			_multiplier = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertDescription), Order = 100)]
	public bool UseAlert { get; set; }

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 110)]
	public string AlertFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertPerBar), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertPerBarDescription), Order = 120)]
	public bool AlertPerBar { get; set; } = true;

	#endregion

	#region ctor

	public SuperTrend()
		: base(true)
	{
		DenyToChangePanel = true;
		DataSeries[0] = _upTrend;
		DataSeries.Add(_dnTrend);
		Add(_atr);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_tickFormat = "{0:0.";

			for (var i = 0; i < InstrumentInfo.TickSize.Scale; i++)
				_tickFormat += "#";

			_tickFormat += "}";
			return;
		}

		_upTrend[bar] = _dnTrend[bar] = 0;
		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);
		var median = (candle.Low + candle.High) / 2;
		var atr = _atr[bar];
		var dUpperLevel = median + atr * Multiplier;
		var dLowerLevel = median - atr * Multiplier;

		// Set supertrend levels
		if (candle.Close > _trend[bar - 1] && prevCandle.Close <= _trend[bar - 1])
			_trend[bar] = dLowerLevel;
		else if (candle.Close < _trend[bar - 1] && prevCandle.Close >= _trend[bar - 1])
			_trend[bar] = dUpperLevel;
		else if (_trend[bar - 1] < dLowerLevel)
			_trend[bar] = dLowerLevel;
		else if (_trend[bar - 1] > dUpperLevel)
			_trend[bar] = dUpperLevel;
		else
			_trend[bar] = _trend[bar - 1];

		if (candle.Close > _trend[bar] || (candle.Close == _trend[bar] && prevCandle.Close > _trend[bar - 1]))
			_upTrend[bar] = _trend[bar];
		else if (candle.Close < _trend[bar] || (candle.Close == _trend[bar] && prevCandle.Close < _trend[bar - 1]))
			_dnTrend[bar] = _trend[bar];

		if (_upTrend[bar - 1] is 0)
			_upTrend.SetPointOfEndLine(bar - 1);

		if (_dnTrend[bar - 1] is 0)
			_dnTrend.SetPointOfEndLine(bar - 1);

		if (bar != CurrentBar - 1 || !UseAlert)
			return;

		if (_lastPrice == 0)
		{
			_lastPrice = candle.Close;
			return;
		}

		var upBrake = (_lastPrice < _upTrend[bar - 1] && candle.Close >= _upTrend[bar - 1])
			|| (_lastPrice > _upTrend[bar - 1] && candle.Close <= _upTrend[bar - 1]);

		var downBrake = (_lastPrice < _dnTrend[bar - 1] && candle.Close >= _dnTrend[bar - 1])
			|| (_lastPrice > _dnTrend[bar - 1] && candle.Close <= _dnTrend[bar - 1]);

		if ((upBrake || downBrake) && (_lastAlert != bar || !AlertPerBar))
		{
			var breakLevel = Math.Max(_upTrend[bar - 1], _dnTrend[bar - 1]);

			AddAlert(AlertFile, InstrumentInfo.Instrument, "Supertrend level break: " + string.Format(_tickFormat, breakLevel), Colors.Black, Colors.White);
			_lastAlert = bar;
		}

		_lastPrice = candle.Close;
	}

	#endregion
}