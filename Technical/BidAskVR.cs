namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Bid Ask Volume Ratio")]
[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43420-bid-ask-volume-ratio")]
public class BidAskVR : Indicator
{
	#region Nested types

	public enum Mode
	{
		[Display(ResourceType = typeof(Resources), Name = "AskBid")]
		AskBid,

		[Display(ResourceType = typeof(Resources), Name = "BidAsk")]
		BidAsk
	}

	public enum MovingType
	{
		[Display(ResourceType = typeof(Resources), Name = "EMA")]
		Ema,

		[Display(ResourceType = typeof(Resources), Name = "LinearReg")]
		LinReg,

		[Display(ResourceType = typeof(Resources), Name = "WMA")]
		Wma,

		[Display(ResourceType = typeof(Resources), Name = "SMA")]
		Sma,

		[Display(ResourceType = typeof(Resources), Name = "SMMA")]
		Smma
	}

    #endregion

    #region Fields

	#region Histogramms

    private readonly ValueDataSeries _low = new(Resources.Low)
    {
	    Color = Colors.Maroon,
	    VisualType = VisualMode.Histogram,
	    Digits = 6,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true
    };

    private readonly ValueDataSeries _lower = new(Resources.Lower)
    {
	    Color = Colors.Red,
	    VisualType = VisualMode.Histogram,
	    Digits = 6,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true
    };

    private readonly ValueDataSeries _up = new(Resources.Up)
    {
	    Color = Colors.Green,
	    VisualType = VisualMode.Histogram,
	    Digits = 6,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true
    };

    private readonly ValueDataSeries _upper = new(Resources.Upper)
    {
	    Color = Colors.Lime,
	    VisualType = VisualMode.Histogram,
	    Digits = 6,
	    ShowZeroValue = false,
	    UseMinimizedModeIfEnabled = true
    };

    #endregion

    private readonly ValueDataSeries _vr = new("VR");
	private readonly ValueDataSeries _vrMa = new(Resources.Visualization);
	private Mode _calcMode;
	private decimal _lastBar;

	private object _movingIndicator;
	private MovingType _movingType;
	private int _period;
	private decimal _prevValue;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "MovingType", GroupName = "Settings", Order = 100)]
	public MovingType MaType
	{
		get => _movingType;
		set
		{
			_movingType = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
	public int Period
	{
		get => _period;
		set
		{
			if (value <= 0)
				return;

			_period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Mode", GroupName = "Settings", Order = 120)]
	public Mode CalcMode
	{
		get => _calcMode;
		set
		{
			_calcMode = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public BidAskVR()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		_period = 10;
		_vrMa.VisualType = VisualMode.Histogram;
		DataSeries[0] = _low;
		DataSeries.Add(_lower);
		DataSeries.Add(_up);
		DataSeries.Add(_upper);
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		switch (_movingType)
		{
			case MovingType.Ema:
				_movingIndicator = new EMA
					{ Period = _period };
				break;
			case MovingType.LinReg:
				_movingIndicator = new LinearReg
					{ Period = _period };
				break;
			case MovingType.Wma:
				_movingIndicator = new WMA
					{ Period = _period };
				break;
			case MovingType.Sma:
				_movingIndicator = new SMA
					{ Period = _period };
				break;
			case MovingType.Smma:
				_movingIndicator = new SMMA
					{ Period = _period };
				break;
		}

		_prevValue = 0;
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		var diff = _calcMode == Mode.AskBid
			? candle.Ask - candle.Bid
			: candle.Bid - candle.Ask;

		if (candle.Ask + candle.Bid != 0)
			_vr[bar] = 100 * diff / (candle.Ask + candle.Bid);

		var maValue = 0m;

		if (bar < _period && bar > 0)
		{
			if (_prevValue == 0)
				maValue = 2m / (bar + 2) * _vr[bar] + (1 - 2m / (bar + 2)) * _vr[bar - 1];
			else
				maValue = 2m / (bar + 2) * _vr[bar] + (1 - 2m / (bar + 2)) * _prevValue;
		}

		if (bar >= _period)
			maValue = IndicatorCalculate(bar, _movingType, _vr[bar]);

		SetValue(bar, maValue);

		if (bar != _lastBar)
			_prevValue = maValue;
		_lastBar = bar;
	}

	#endregion

	#region Private methods

	private void SetValue(int bar, decimal maValue)
	{
		if (maValue > 0)
		{
			if (maValue >= _prevValue)
			{
				_upper[bar] = maValue;
				_up[bar] = _low[bar] = _lower[bar] = 0;
			}
			else
			{
				_up[bar] = maValue;
				_upper[bar] = _low[bar] = _lower[bar] = 0;
			}
		}
		else
		{
			if (maValue <= _prevValue)
			{
				_lower[bar] = maValue;
				_up[bar] = _low[bar] = _upper[bar] = 0;
			}
			else
			{
				_low[bar] = maValue;
				_upper[bar] = _up[bar] = _lower[bar] = 0;
			}
		}
	}

	private decimal IndicatorCalculate(int bar, MovingType type, decimal value)
	{
		var movingValue = 0m;

		switch (type)
		{
			case MovingType.Ema:
				movingValue = ((EMA)_movingIndicator).Calculate(bar, value);
				break;
			case MovingType.LinReg:
				movingValue = ((LinearReg)_movingIndicator).Calculate(bar, value);
				break;
			case MovingType.Wma:
				movingValue = ((WMA)_movingIndicator).Calculate(bar, value);
				break;
			case MovingType.Sma:
				movingValue = ((SMA)_movingIndicator).Calculate(bar, value);
				break;
			case MovingType.Smma:
				movingValue = ((SMMA)_movingIndicator).Calculate(bar, value);
				break;
		}

		return movingValue;
	}

	#endregion

}