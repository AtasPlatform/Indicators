namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

[DisplayName("Bar Range")]
public class BarRange : Indicator
{
	#region Fields

	private Highest _highestVol = new();
	private ValueDataSeries _maxVolSeries;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Resources), Name = "Show", GroupName = "MaxValue", Order = 100)]
	public bool ShowMaxVolume
	{
		get => _maxVolSeries.VisualType is not VisualMode.Hide;
		set => _maxVolSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
	}

	[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "MaxValue", Order = 110)]
	[Range(1, 100000)]
	public int HiVolPeriod
	{
		get => _highestVol.Period;
		set => _highestVol.Period = value;
	}

	[Display(ResourceType = typeof(Resources), Name = "HighLineColor", GroupName = "MaxValue", Order = 120)]
	public Color LineColor
	{
		get => _maxVolSeries.Color;
		set => _maxVolSeries.Color = value;
	}

	#endregion

	#region ctor

	public BarRange()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		_maxVolSeries = (ValueDataSeries)_highestVol.DataSeries[0];
		_maxVolSeries.IsHidden = true;
		_maxVolSeries.Color = DefaultColors.Green.Convert();
		_maxVolSeries.IgnoredByAlerts = true;

		((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Histogram;
		DataSeries.Add(_maxVolSeries);
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);

		this[bar] = candle.High - candle.Low;
		_highestVol.Calculate(bar, this[bar]);
	}

	#endregion
}