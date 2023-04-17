namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Super Trend")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/14383-super-trend")]
public class SuperTrend : Indicator
{
	#region Fields

	private readonly ATR _atr = new() { Period = 14 };

	private ValueDataSeries _dnTrend = new("Down Trend")
	{
		VisualType = VisualMode.Square,
		Color = DefaultColors.Maroon.Convert(),
		Width = 2
	};

	private int _lastAlert;
	private decimal _lastPrice;

	private decimal _multiplier = 1.7m;

	private string _tickFormat;

	private ValueDataSeries _trend = new("trend");

	private ValueDataSeries _upTrend = new("Up Trend")
	{
		Color = DefaultColors.Blue.Convert(),
		Width = 2,
		VisualType = VisualMode.Square,
		ShowZeroValue = false
	};

	#endregion

	#region Properties

	[Parameter]
	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common", Order = 20)]
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

	[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Common", Order = 30)]
	public decimal Multiplier
	{
		get => _multiplier;
		set
		{
			_multiplier = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 100)]
	public bool UseAlert { get; set; }

	[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 110)]
	public string AlertFile { get; set; } = "alert1";

	[Display(ResourceType = typeof(Resources), Name = "AlertPerBar", GroupName = "Alerts", Order = 120)]
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