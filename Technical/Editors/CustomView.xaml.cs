using System.Windows.Controls;

namespace ATAS.Indicators.Technical.Editors
{
	using System;
	using System.Globalization;
	using System.Windows.Data;

	using OFT.Rendering.Settings;

	using WpfColor = System.Windows.Media.Color;
	using SysColor = System.Drawing.Color;

	public partial class CustomView
	{
		public CustomView()
		{
			InitializeComponent();

			VisualTypeSelector.ItemsSource = Enum.GetValues(typeof(VisualMode));
			LineStyleSelector.ItemsSource = Enum.GetValues(typeof(LineDashStyle));
		}
	}

	class WpfColorToColorConverter : IValueConverter
	{
		private static object Convert(object value) => value switch
		{
			WpfColor w => SysColor.FromArgb(w.A, w.R, w.G, w.B),
			SysColor s => WpfColor.FromArgb(s.A, s.R, s.G, s.B),

			_ => value
		};

		#region Implementation of IValueConverter

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Convert(value);
		}
		
		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Convert(value);
		}

		#endregion
	}
}
