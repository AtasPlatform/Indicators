﻿namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Fisher Transform")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.FisherTransformDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602385")]
public class FisherTransform : Indicator
{
	#region Fields

	private readonly ValueDataSeries _fisher = new("FisherId", "Fisher")
	{
		DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
	};

    private readonly ValueDataSeries _triggers = new("TriggersId", "Triggers")
	{
		Color = System.Drawing.Color.DodgerBlue.Convert(),
        DescriptionKey = nameof(Strings.SignalLineSettingsDescription)
    };

	private readonly ValueDataSeries _values = new("Values");

    private readonly Highest _highest = new() { Period = 10 };
	private readonly decimal _lastBar = -1;
	private readonly Lowest _lowest = new() { Period = 10 };

	private decimal _lastFisher;
	private decimal _lastValue;

    #endregion

    #region Properties

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Common), Description = nameof(Strings.PeriodDescription))]
	[Range(1, 10000)]
	public int Period
	{
		get => _highest.Period;
		set
		{
			_highest.Period = value;
			_lowest.Period = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public FisherTransform()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _triggers;
		DataSeries.Add(_fisher);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar <= Period)
			return;

		if (bar != _lastBar && bar > 0)
		{
			_lastValue = _values[bar - 1];
			_lastFisher = _fisher[bar - 1];
		}

		var candle = GetCandle(bar);

		var sMax = _highest.Calculate(bar, candle.High);
		var sMin = _lowest.Calculate(bar, candle.Close);

		if (sMax == sMin)
			sMax += ChartInfo.PriceChartContainer.Step;

		var wpr = (candle.Close - sMin) / (sMax - sMin);

		var valueSeries = 0.66m * (wpr - 0.5m) + 0.67m * _lastValue;

		if (valueSeries is >= 1 or <= -1)
			valueSeries = Math.Sign(valueSeries) * 0.999m;

		var fisherSeries = 0.5 * Math.Log((1.0 + Convert.ToDouble(valueSeries)) / (1.0 - Convert.ToDouble(valueSeries))) +
			0.5 * Convert.ToDouble(_lastFisher);

		_values[bar] = valueSeries;
		_fisher[bar] = Convert.ToDecimal(fisherSeries);
		_triggers[bar] = _lastFisher;
	}

	#endregion
}