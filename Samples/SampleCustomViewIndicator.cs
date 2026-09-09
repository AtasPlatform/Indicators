namespace ATAS.Indicators.Samples;

using System.ComponentModel;

using ATAS.Indicators.Samples.Editors;

[DisplayName("Custom View Sample")]
[Category(IndicatorCategories.Samples)]
[Editor(typeof(CustomView), typeof(CustomView))]
public class SampleCustomViewIndicator : Indicator
{
	#region Custom properties

	public string? StringValue { get; set; }

	public int NumberValue { get; set; }

	#endregion

	#region Overrides of BaseIndicator

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	#endregion
}
