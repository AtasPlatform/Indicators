namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[DisplayName("Adaptive RSI Moving Average")]
[Category("Technical indicators")]
[FeatureId("NotApproved")]
public class AdaptiveRsiAverage : Indicator
{
	#region Fields

	private EMA _priceSmoothed = new();
	private RSI _rsi = new();
	private EMA _rsiSmoothed = new();
	private decimal _scaleFactor = 0.5m;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "RSI", Order = 100)]
	[Range(1, 10000)]
	public int RsiPeriod
	{
		get => _rsi.Period;
		set
		{
			_rsi.Period = value;
			RecalculateValues();
		}
	}

	[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "RSI", Order = 110)]
	[Range(1, 10000)]
	public FilterInt RsiSmooth { get; set; } = new(true)
	{
		Enabled = true,
		Value = 10
	};

	[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Values", Order = 200)]
	[Range(1, 10000)]
	public FilterInt PriceSmooth { get; set; } = new(true)
	{
		Enabled = true,
		Value = 10
	};

	[Display(ResourceType = typeof(Resources), Name = "Scale", GroupName = "Values", Order = 210)]
	[Range(0.00000001, 2)]
	public decimal ScaleFactor
	{
		get => _scaleFactor;
		set
		{
			_scaleFactor = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public AdaptiveRsiAverage()
	{
		RsiSmooth.PropertyChanged += RsiFilterChanged;
		PriceSmooth.PropertyChanged += ValuesFilterChanged;
	}

	#endregion

	#region Protected methods

	protected override void OnFinishRecalculate()
	{
		RedrawChart();
	}

	protected override void OnRecalculate()
	{
		Clear();
	}

	protected override void OnCalculate(int bar, decimal value)
	{
		var calcValue = PriceSmooth.Enabled
			? _priceSmoothed.Calculate(bar, value)
			: value;

		var rsiValue = _rsi.Calculate(bar, calcValue);

		if (RsiSmooth.Enabled)
			rsiValue = _rsiSmoothed.Calculate(bar, rsiValue);

		var sFactor = 2 * ScaleFactor * Math.Abs(rsiValue / 100m - 0.5m);

		this[bar] = bar <= RsiPeriod
			? calcValue
			: (calcValue - this[bar - 1]) * sFactor + this[bar - 1];
	}

	#endregion

	#region Private methods

	private void ValuesFilterChanged(object sender, PropertyChangedEventArgs e)
	{
		_priceSmoothed.Period = PriceSmooth.Value;
		RecalculateValues();
	}

	private void RsiFilterChanged(object sender, PropertyChangedEventArgs e)
	{
		_rsiSmoothed.Period = RsiSmooth.Value;
		RecalculateValues();
	}

	#endregion
}