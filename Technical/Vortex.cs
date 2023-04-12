namespace ATAS.Indicators.Technical;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using OFT.Attributes;

[FeatureId("NotApproved")]
[DisplayName("Vortex")]
public class Vortex : Indicator
{
	#region Fields

	private int _period = 10;

	private ValueDataSeries _trueRange = new("TrueRange");
	private ValueDataSeries _vortexMoveDown = new("MoveDown");
	private ValueDataSeries _vortexMoveUp = new("MoveUp");
	private ValueDataSeries _vortexNeg = new("Vortex-");

	private ValueDataSeries _vortexPos = new("Vortex+")
	{
		Color = DefaultColors.Green.Convert()
	};

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

	public Vortex()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _vortexPos;
		DataSeries.Add(_vortexNeg);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		if (bar == 0)
		{
			_trueRange[bar] = candle.High - candle.Low;
			_vortexMoveUp[bar] = Math.Abs(candle.High - candle.Low);
			_vortexMoveDown[bar] = Math.Abs(candle.Low - candle.High);

			DataSeries.ForEach(x => ((ValueDataSeries)x).SetPointOfEndLine(0));
			return;
		}

		var prevCandle = GetCandle(bar - 1);

		var highLow = candle.High - candle.Low;
		var highCloseDiff = Math.Abs(candle.High - prevCandle.Close);
		var lowCloseDiff = Math.Abs(candle.Low - prevCandle.Close);

		var trueRange = Math.Max(highLow, highCloseDiff);
		trueRange = Math.Max(trueRange, lowCloseDiff);

		_trueRange[bar] = trueRange;

		_vortexMoveUp[bar] = Math.Abs(candle.High - prevCandle.Low);
		_vortexMoveDown[bar] = Math.Abs(candle.Low - prevCandle.High);

		var trueRangeSum = _trueRange.CalcSum(Period, bar);

		var moveUpSum = _vortexMoveUp.CalcSum(Period, bar);

		_vortexPos[bar] = trueRangeSum == 0
			? _vortexMoveUp[bar - 1]
			: moveUpSum / trueRangeSum;

		var moveDownSum = _vortexMoveDown.CalcSum(Period, bar);

		_vortexNeg[bar] = trueRangeSum == 0
			? _vortexMoveDown[bar - 1]
			: moveDownSum / trueRangeSum;
	}

	#endregion
}