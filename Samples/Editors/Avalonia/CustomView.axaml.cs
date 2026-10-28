namespace ATAS.Indicators.Samples.Editors;

using System;

using Avalonia.Controls;

using OFT.Rendering.Settings;

public partial class CustomView : UserControl
{
	#region ctor

	public CustomView()
	{
		InitializeComponent();

		VisualTypeSelector.ItemsSource = Enum.GetValues<VisualMode>();
		LineStyleSelector.ItemsSource = Enum.GetValues<LineDashStyle>();
	}

	#endregion
}
