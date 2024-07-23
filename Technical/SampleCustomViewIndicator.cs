namespace ATAS.Indicators.Technical;

using System.ComponentModel;

using ATAS.Indicators.Technical.Editors;

[DisplayName("Custom View")]
[Category("Samples")]
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