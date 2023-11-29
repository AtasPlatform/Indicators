namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("Arms Ease of Movement")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.EMVDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000602315")]
public class EMV : Indicator
{
	#region Nested types

	public enum MovingType
	{
		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMA))]
		Ema,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.LinearReg))]
		LinReg,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.WMA))]
		Wma,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA))]
		Sma,

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMMA))]
		Smma
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _renderSeries = new("RenderSeries", "ADXR");

	private object _movingIndicator;
	private MovingType _movingType = MovingType.Ema;

	private int _period = 9;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MovingType), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MovingTypeDescription), Order = 100)]
	public MovingType MaType
	{
		get => _movingType;
		set
		{
			_movingType = value;
			RecalculateValues();
		}
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 110)]
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

	#endregion

	#region ctor

	public EMV()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;
		DataSeries[0] = _renderSeries;
	}

	#endregion

	#region Protected methods

	protected override void OnRecalculate()
	{
		_movingIndicator = _movingType switch
		{
			MovingType.Ema => new EMA { Period = _period },
			MovingType.LinReg => new LinearReg { Period = _period },
			MovingType.Wma => new WMA { Period = _period },
			MovingType.Sma => new SMA { Period = _period },
			MovingType.Smma => new SMMA { Period = _period },
			_ => _movingIndicator
		};
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			IndicatorCalculate(bar, _movingType, 0);
			return;
		}

		var candle = GetCandle(bar);
		var prevCandle = GetCandle(bar - 1);
		var midPoint = (candle.High + candle.Low) / 2m - (prevCandle.High + prevCandle.Low) / 2m;
		var ratio = candle.High - candle.Low == 0 ? 0 : candle.Volume / (candle.High - candle.Low);
		var emv = ratio == 0 ? 0 : midPoint / ratio;
		_renderSeries[bar] = IndicatorCalculate(bar, _movingType, emv);
	}

	#endregion

	#region Private methods

	private decimal IndicatorCalculate(int bar, MovingType type, decimal value)
	{
		var movingValue = type switch
		{
			MovingType.Ema => ((EMA)_movingIndicator).Calculate(bar, value),
			MovingType.LinReg => ((LinearReg)_movingIndicator).Calculate(bar, value),
			MovingType.Wma => ((WMA)_movingIndicator).Calculate(bar, value),
			MovingType.Sma => ((SMA)_movingIndicator).Calculate(bar, value),
			MovingType.Smma => ((SMMA)_movingIndicator).Calculate(bar, value),
			_ => 0m
		};

		return movingValue;
	}

	#endregion
}