namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Super Trend")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.SuperTrendDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000602482")]
public class SuperTrend : Indicator
{
	#region Fields

	private readonly ATR _atr = new() { Period = 14 };

	[Obsolete]
    private ValueDataSeries _upTrend = new("UpTrendId", "Up Trend")
    {
        Color = DefaultColors.Blue.Convert(),
        Width = 2,
        VisualType = VisualMode.Square,
        ShowZeroValue = false,
        IsHidden = true,
        DescriptionKey = nameof(Strings.UpTrendSettingsDescription)
    };

	[Obsolete]
    private ValueDataSeries _dnTrend = new("DnTrend", "Down Trend")
	{
		VisualType = VisualMode.Square,
		Color = DefaultColors.Maroon.Convert(),
		Width = 2,
		ShowZeroValue = false,
		IsHidden = true,
        DescriptionKey = nameof(Strings.DownTrendSettingsDescription)
    };

	private int _lastAlert;
	private decimal _lastPrice;
	private decimal _multiplier = 1.7m;
	private string _tickFormat;
	private Color _upColor = DefaultColors.Blue;
	private Color _downColor = DefaultColors.Maroon;

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

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BullishColor), GroupName = nameof(Strings.Drawing),
		Description = nameof(Strings.BullishColorDescription), Order = 200)]
	public Color UpColor
	{
		get => _upColor;
		set
		{
			_upColor = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BearishColor), GroupName = nameof(Strings.Drawing),
		Description = nameof(Strings.BearishColorDescription), Order = 210)]
	public Color DownColor
	{
		get => _downColor;
		set
		{
			_downColor = value; 
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public SuperTrend()
		: base(true)
	{
		DenyToChangePanel = true;
		SupportsCalculationTimeFrame = true;
		var series = (ValueDataSeries)DataSeries[0];
		series.VisualType = VisualMode.Square;
		series.Width = 2;
		series.ShowZeroValue = false;
#pragma warning disable CS0612
		DataSeries.Add(_upTrend);
		DataSeries.Add(_dnTrend);
#pragma warning restore CS0612
		Add(_atr);
	}

	#endregion

	#region Protected methods

	protected override void OnInitialize()
	{
#pragma warning disable CS0612
		_upTrend.IsHidden = _dnTrend.IsHidden = true;
		_upTrend.VisualType = _dnTrend.VisualType = VisualMode.Hide;
#pragma warning restore CS0612
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var series = (ValueDataSeries)DataSeries[0];
		
		if (bar == 0)
		{
			_tickFormat = "{0:0.";

			for (var i = 0; i < InstrumentInfo.TickSize.Scale; i++)
				_tickFormat += "#";

			_tickFormat += "}";

			series.SetPointOfEndLine(bar);
			return;
		}

		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);
		var median = (candle.Low + candle.High) / 2;
		var atr = _atr[bar];
		var dUpperLevel = median + atr * Multiplier;
		var dLowerLevel = median - atr * Multiplier;

		// Set supertrend levels
		if (candle.Close > this[bar - 1] && prevCandle.Close <= this[bar - 1])
			this[bar] = dLowerLevel;
		else if (candle.Close < this[bar - 1] && prevCandle.Close >= this[bar - 1])
			this[bar] = dUpperLevel;
		else if (this[bar - 1] < dLowerLevel)
			this[bar] = dLowerLevel;
		else if (this[bar - 1] > dUpperLevel)
			this[bar] = dUpperLevel;
		else
			this[bar] = this[bar - 1];
		
        if (candle.Close > this[bar] || (candle.Close == this[bar] && prevCandle.Close > this[bar - 1]))
        {
	        series.Colors[bar] = UpColor;
        }
		else if (candle.Close < this[bar] || (candle.Close == this[bar] && prevCandle.Close < this[bar - 1]))
		{
			series.Colors[bar] = DownColor;
        }

		if (series.Colors[bar] != series.Colors[bar - 1])
			series.SetPointOfEndLine(bar - 1);
		else
			series.RemovePointOfEndLine(bar - 1);

		if (bar != CurrentBar - 1 || !UseAlert)
			return;

		if (_lastPrice == 0)
		{
			_lastPrice = candle.Close;
			return;
		}

		var brake = (_lastPrice < this[bar - 1] && candle.Close >= this[bar - 1]) || 
			(_lastPrice > this[bar - 1] && candle.Close <= this[bar - 1]);
		
		if (brake && (_lastAlert != bar || !AlertPerBar))
		{
			var breakLevel = this[bar - 1];

			AddAlert(AlertFile, InstrumentInfo.Instrument, "Supertrend level break: " + string.Format(_tickFormat, breakLevel),
				System.Drawing.Color.Black.Convert(), System.Drawing.Color.White.Convert());

			_lastAlert = bar;
		}

		_lastPrice = candle.Close;
	}

	#endregion
}