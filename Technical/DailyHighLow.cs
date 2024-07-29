namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Drawing;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Daily HighLow")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.DailyHighLowDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602609")]
public class DailyHighLow : Indicator
{
	#region Fields

	private readonly ValueDataSeries _highSeries = new("HighSeries", "High")
	{
		Color = System.Drawing.Color.FromArgb(255, 135, 135, 135).Convert(),
		VisualType = VisualMode.Square,
		DescriptionKey= nameof(Strings.CurrentDayHighDescription)
    };

	private readonly ValueDataSeries _lowSeries = new("LowSeries", "Low")
	{
		Color = System.Drawing.Color.FromArgb(255, 135, 135, 135).Convert(),
		VisualType = VisualMode.Square,
        DescriptionKey = nameof(Strings.CurrentDayLowDescription)
    };

	private readonly ValueDataSeries _medianSeries = new("MedianSeries", "Median")
	{
		Color = DefaultColors.Lime.Convert(),
		VisualType = VisualMode.Square,
        DescriptionKey = nameof(Strings.CurrentDayMedianDescription)
    };

	private readonly ValueDataSeries _prevMiddleSeries = new("PrevMiddleSeries", "Yesterday median")
	{
		Color = DefaultColors.Blue.Convert(),
		VisualType = VisualMode.Square,
        DescriptionKey = nameof(Strings.PrevDayMedianDescription)
    };

	private int _days = 20;

	private decimal _high;
	private bool _highSpecified;
	private DateTime _lastSessionTime;
	private decimal _low;
	private bool _lowSpecified;
	private decimal _median;

	private decimal _prevMiddle;
	private int _targetBar;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
    [Range(0, 1000)]
	public int Days
	{
		get => _days;
		set
		{
			_days = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public DailyHighLow()
		: base(true)
	{
		DenyToChangePanel = true;

		DataSeries[0] = _highSeries;
		DataSeries.Add(_lowSeries);
		DataSeries.Add(_medianSeries);
		DataSeries.Add(_prevMiddleSeries);
	}

	#endregion

	#region Public methods

	public override string ToString()
	{
		return "Daily HighLow";
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			if (_days == 0)
				_targetBar = 0;
			else
			{
				var days = 0;

				for (var i = CurrentBar - 1; i >= 0; i--)
				{
					_targetBar = i;

					if (!IsNewSession(i))
						continue;

					days++;

					if (days == _days)
						break;
				}
			}

			_high = _low = _prevMiddle = 0;
			_highSpecified = _lowSpecified = false;
			DataSeries.ForEach(x => x.Clear());
		}

		if (bar < _targetBar)
			return;

		var candle = GetCandle(bar);

		if (IsNewSession(bar))
		{
			if (_lastSessionTime != candle.Time)
			{
				_lastSessionTime = candle.Time;
				_prevMiddle = _median;
				_high = _low = 0;
				_highSpecified = _lowSpecified = false;
			}
		}

		if (candle.High > _high || !_highSpecified)
			_high = candle.High;

		if (candle.Low < _low || !_lowSpecified)
			_low = candle.Low;

		_median = _low + (_high - _low) / 2;

		_highSpecified = _lowSpecified = true;
		_highSeries[bar] = _high;
		_lowSeries[bar] = _low;
		_medianSeries[bar] = _median;
		_prevMiddleSeries[bar] = _prevMiddle;
	}

	#endregion
}