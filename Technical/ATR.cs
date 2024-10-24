namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("ATR")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ATRDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602536")]
public class ATR : Indicator
{
	#region Fields

	private int _period = 10;
	private decimal _multiplier = 1;
	private ValueDataSeries _values = new("values");

	#endregion

	#region Properties

	[Parameter]
	[Display(ResourceType = typeof(Strings),
		Name = nameof(Strings.Period),
		GroupName = nameof(Strings.Common),
        Description = nameof(Strings.PeriodDescription),
        Order = 20)]
	[Range(1, 10000)]
	public int Period
	{
		get => _period;
		set
		{
			_period = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings),
		Name = nameof(Strings.Multiplier),
		GroupName = nameof(Strings.Common),
        Description = nameof(Strings.ATRMultiplierDescription),
        Order = 20)]
	[Range(0.0000001, 10000000)]
	public decimal Multiplier
	{
		get => _multiplier;
		set
		{
			_multiplier = value;
			RecalculateValues();
		}
	}

    #endregion

    #region ctor

    public ATR()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
    }

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0 && ChartInfo != null)
			((ValueDataSeries)DataSeries[0]).StringFormat = ChartInfo.StringFormat;

		var candle = GetCandle(bar);
		var high0 = candle.High;
		var low0 = candle.Low;

		if (bar == 0)
			_values[bar] = high0 - low0;
		else
		{
			var close1 = GetCandle(bar - 1).Close;
			var trueRange = Math.Max(Math.Abs(low0 - close1), Math.Max(high0 - low0, Math.Abs(high0 - close1)));
			_values[bar] = ((Math.Min(CurrentBar + 1, Period) - 1) * _values[bar - 1] + trueRange) / Math.Min(CurrentBar + 1, Period);
		}

        this[bar] = Multiplier * _values[bar];
    }

	#endregion
}