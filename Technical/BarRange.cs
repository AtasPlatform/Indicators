namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;

#if CROSS_PLATFORM
    using CrossColor = System.Drawing.Color;
#else
using CrossColor = System.Windows.Media.Color;
#endif

[DisplayName("Bar Range")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.BarRangeIndDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000618458")]
public class BarRange : Indicator
{
	#region Fields

	private Highest _highestVol = new();
	private ValueDataSeries _maxVolSeries;

	#endregion

	#region Properties

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.MaxValue), Description = nameof(Strings.ShowMaxValueDescription), Order = 100)]
	public bool ShowMaxVolume
	{
		get => _maxVolSeries.VisualType is not VisualMode.Hide;
		set => _maxVolSeries.VisualType = value ? VisualMode.Line : VisualMode.Hide;
	}

    [Parameter]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.MaxValue), Description = nameof(Strings.PeriodDescription), Order = 110)]
	[Range(1, 100000)]
	public int HiVolPeriod
	{
		get => _highestVol.Period;
		set => _highestVol.Period = value;
	}

	[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLineColor), GroupName = nameof(Strings.MaxValue), Description = nameof(Strings.LineColorDescription), Order = 120)]
	public CrossColor LineColor
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
		_maxVolSeries.Id = "HighestVolDataSeries";
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