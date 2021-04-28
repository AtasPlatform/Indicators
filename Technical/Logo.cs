namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.IO;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;

	[DisplayName("Logo")]
	public class Logo : Indicator
	{
		#region Nested types

		public enum Location
		{
			[Display(ResourceType = typeof(Resources), Name = "Center")]
			Center,

			[Display(ResourceType = typeof(Resources), Name = "TopLeft")]
			TopLeft,

			[Display(ResourceType = typeof(Resources), Name = "TopRight")]
			TopRight,

			[Display(ResourceType = typeof(Resources), Name = "BottomLeft")]
			BottomLeft,

			[Display(ResourceType = typeof(Resources), Name = "BottomRight")]
			BottomRight
		}

		#endregion

		#region Fields

		private string _filePath;
		private int _height;
		private Image _image;
		private int _transparency;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "LogoLocation", GroupName = "Common", Order = 20)]
		public Location LogoLocation { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Common", Order = 22)]
		public int Width
		{
			get => _width;
			set
			{
				if (value <= 0)
					return;

				_width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Height", GroupName = "Common", Order = 24)]
		public int Height
		{
			get => _height;
			set
			{
				if (value <= 0)
					return;

				_height = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "HorizontalOffset", GroupName = "Common", Order = 30)]
		public int HorizontalOffset { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "VerticalOffset", GroupName = "Common", Order = 40)]
		public int VerticalOffset { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ShowAboveChart", GroupName = "Common", Order = 50)]
		public bool AbovePrice
		{
			get => DrawAbovePrice;
			set => DrawAbovePrice = value;
		}

		[Display(ResourceType = typeof(Resources), Name = "ImageLocation", GroupName = "FirstLine", Order = 70)]
		public string FilePath
		{
			get => _filePath;
			set
			{
				_filePath = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Logo()
			: base(true)
		{
			Height = 200;
			Width = 200;
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Historical);
			DrawAbovePrice = false;
		}

		#endregion

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			_image = File.Exists(_filePath)
				? Image.FromFile(_filePath)
				: null;
		}

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (_image == null)
				return;

			var x = 0;
			var y = 0;

			switch (LogoLocation)
			{
				case Location.Center:
				{
					x = ChartInfo.PriceChartContainer.Region.Width / 2 - _width / 2 + HorizontalOffset;

					y = ChartInfo.PriceChartContainer.Region.Height / 2 - _height / 2 + VerticalOffset;

					break;
				}
				case Location.TopLeft:
				{
					x = HorizontalOffset;
					break;
				}
				case Location.TopRight:
				{
					x = ChartInfo.PriceChartContainer.Region.Width - _width + HorizontalOffset;
					break;
				}
				case Location.BottomLeft:
				{
					x = HorizontalOffset;
					y = ChartInfo.PriceChartContainer.Region.Height - _height + VerticalOffset;

					break;
				}
				case Location.BottomRight:
				{
					x = ChartInfo.PriceChartContainer.Region.Width - _width + HorizontalOffset;
					y = ChartInfo.PriceChartContainer.Region.Height - _height + VerticalOffset;

					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}

			var rect = new Rectangle(x, y, _width, _height);
			context.DrawStaticImage(_image, rect);
		}

		#endregion
	}
}