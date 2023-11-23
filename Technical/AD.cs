namespace ATAS.Indicators.Technical;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using OFT.Attributes;
using OFT.Localization;

[DisplayName("AD")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.ADDescription))]
[HelpLink("https://help.atas.net/en/support/solutions/articles/72000606733")]
public class AD : Indicator
{
	#region ctor

	public AD()
		: base(true)
	{
		Panel = IndicatorDataProvider.NewPanel;

		DataSeries[0] = new ValueDataSeries("Ad", "AD")
		{
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};
	}

	#endregion

	#region Protected methods

	protected override void OnCalculate(int bar, decimal value)
	{
		var candle = GetCandle(bar);
		var prev = bar == 0 ? 0m : this[bar - 1];

		var diff = candle.High - candle.Low;

		this[bar] = diff == 0
			? prev
			: (candle.Close - candle.Low - (candle.High - candle.Close)) * candle.Volume / diff + prev;
	}

	#endregion
}