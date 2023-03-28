namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

using ATAS.Indicators.Drawing;
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

	private readonly ValueDataSeries _vr = new("VR");
	private readonly ValueDataSeries _vrMa = new(Resources.Visualization);
	private Mode _calcMode;
	private decimal _lastBar;
	private Color _lowColor = DefaultColors.Maroon;
	private Color _lowerColor = DefaultColors.Red;

	private object _movingIndicator;
	private MovingType _movingType;
	private int _period;
	private decimal _prevValue;

	private ValueDataSeries _renderSeries = new(Resources.Visualization)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true,
		ResetAlertsOnNewBar = true
	};

	private Color _upColor = DefaultColors.Green;
	private Color _upperColor = DefaultColors.Lime;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Upper", GroupName = "Drawing", Order = 610)]
	public System.Windows.Media.Color UpperColor
	{
		get => _upperColor.Convert();
		set
		{
			_upperColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Up", GroupName = "Drawing", Order = 620)]
	public System.Windows.Media.Color UpColor
	{
		get => _upColor.Convert();
		set
		{
			_upColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Drawing", Order = 630)]
	public System.Windows.Media.Color LowColor
	{
		get => _lowColor.Convert();
		set
		{
			_lowColor = value.Convert();
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Lower", GroupName = "Drawing", Order = 640)]
	public System.Windows.Media.Color LowerColor
	{
		get => _lowerColor.Convert();
		set
		{
			_lowerColor = value.Convert();
			RecalculateValues();
		}
	}

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
		DataSeries[0] = _renderSeries;
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

		_renderSeries[bar] = maValue;

		SetColor(bar, maValue);

		if (bar != _lastBar)
			_prevValue = maValue;

		_lastBar = bar;
	}

	#endregion

	#region Private methods

	private void SetColor(int bar, decimal maValue)
	{
		if (maValue > 0)
			_renderSeries.Colors[bar] = maValue >= _prevValue ? _upperColor : _upColor;
		else
			_renderSeries.Colors[bar] = maValue <= _prevValue ? _lowerColor : _lowColor;
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