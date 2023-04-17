namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Daily HighLow")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/387-daily-highlow")]
public class DailyHighLow : Indicator
{
	#region Fields

	private readonly ValueDataSeries _highSeries = new("High")
	{
		Color = Color.FromArgb(255, 135, 135, 135),
		VisualType = VisualMode.Square
	};

	private readonly ValueDataSeries _lowSeries = new("Low")
	{
		Color = Color.FromArgb(255, 135, 135, 135),
		VisualType = VisualMode.Square
	};

	private readonly ValueDataSeries _medianSeries = new("Median")
	{
		Color = DefaultColors.Lime.Convert(),
		VisualType = VisualMode.Square
	};

	private readonly ValueDataSeries _prevMiddleSeries = new("Yesterday median")
	{
		Color = DefaultColors.Blue.Convert(),
		VisualType = VisualMode.Square
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

    [Display(ResourceType = typeof(Resources), GroupName = "Calculation", Name = "DaysLookBack", Order = int.MaxValue, Description = "DaysLookBackDescription")]
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