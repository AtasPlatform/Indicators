namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Technical.Properties;

[DisplayName("COT High/Low")]
public class CotHigh : Indicator
{
	#region Nested types

	public enum CotMode
	{
		[Display(ResourceType = typeof(Resources), Name = "High")]
		High,

		[Display(ResourceType = typeof(Resources), Name = "Low")]
		Low
	}

	#endregion

	#region Fields

	private readonly ValueDataSeries _negSeries = new(Resources.Negative)
	{
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private readonly ValueDataSeries _posSeries = new(Resources.Positive)
	{
		Color = Colors.Green,
		VisualType = VisualMode.Histogram,
		ShowZeroValue = false,
		UseMinimizedModeIfEnabled = true
	};

	private decimal _extValue;
	private CotMode _mode = CotMode.High;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 100)]
	public CotMode Mode
	{
		get => _mode;
		set
		{
			_mode = value;
			RecalculateValues();
		}
	}

	#endregion

	#region ctor

	public CotHigh()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = _posSeries;
		DataSeries.Add(_negSeries);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		if (bar == 0)
		{
			_extValue = 0;
			DataSeries.ForEach(x => x.Clear());
		}

		var candle = GetCandle(bar);

		if ((candle.High >= _extValue && Mode is CotMode.High)
		    ||
		    (candle.Low >= _extValue && Mode is CotMode.High))
		{
			_extValue = Mode is CotMode.High
				? candle.High
				: candle.Low;
			DrawValue(bar, candle.Delta);
		}
		else
		{
			var renderValue = LastValue(bar) + candle.Delta;
			DrawValue(bar, renderValue);
		}
	}

	#endregion

	#region Private methods

	private void DrawValue(int bar, decimal value)
	{
		if (value > 0)
			_posSeries[bar] = value;
		else
			_negSeries[bar] = value;
	}

	private decimal LastValue(int bar)
	{
		if (bar == 0)
		{
			return
				_posSeries[bar] != 0
					? _posSeries[bar]
					: _negSeries[bar];
		}

		return
			_posSeries[bar - 1] != 0
				? _posSeries[bar - 1]
				: _negSeries[bar - 1];
	}

	#endregion
}