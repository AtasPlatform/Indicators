namespace ATAS.Indicators.Samples;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

using ATAS.Indicators.Samples.Editors;

[DisplayName("Custom Editors Sample")]
[Category(IndicatorCategories.Samples)]
public class SampleCustomEditorsIndicator : Indicator
{
	#region Custom properties

	[Display(Name = "About", GroupName = "Examples")]
	[Editor(typeof(AboutEditor), typeof(AboutEditor))]
	public string About { get; } =
		"This indicator shows custom property editors declared with the [Editor] attribute. " +
		"The classic ATAS (WPF) build uses the *.xaml editors, the ATAS X (Avalonia) build the *.axaml ones. " +
		"This very block is a read-only multiline AboutEditor.";

	[Display(Name = "Range", GroupName = "Examples")]
	[Editor(typeof(RangeEditor), typeof(RangeEditor))]
	public RangeValue Range { get; set; } = new();

	[Display(Name = "Number", GroupName = "Examples")]
	public int Number { get; set; }

	#endregion

	#region Overrides of BaseIndicator

	protected override void OnCalculate(int bar, decimal value)
	{
	}

	#endregion
}

public class RangeValue : INotifyPropertyChanged
{
	#region Fields

	private int _from;
	private int _to = 100;

	#endregion

	#region Properties

	public int From
	{
		get => _from;
		set
		{
			_from = value;
			OnPropertyChanged(nameof(From));
		}
	}

	public int To
	{
		get => _to;
		set
		{
			_to = value;
			OnPropertyChanged(nameof(To));
		}
	}

	#endregion

	#region Events

	public event PropertyChangedEventHandler? PropertyChanged;

	#endregion

	#region Public methods

	public override string ToString() => $"{From} - {To}";

	#endregion

	#region Private methods

	private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

	#endregion
}
