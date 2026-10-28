namespace ATAS.Indicators.Samples.Editors
{
	using System;

	using OFT.Rendering.Settings;

	public partial class CustomView
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
}
