namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Drawing.Imaging;
	using System.IO;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;
	using OFT.Rendering.Context;

	[DisplayName("Background Picture")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/44029-logo")]
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
		private Image _image;
		private DateTime _lastRender = DateTime.Now;
		private object _locker = new();
		private Image _source;
		private int _transparency;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "LogoLocation", GroupName = "Common", Order = 20)]
		public Location LogoLocation { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Scale", GroupName = "Common", Order = 22)]
		[NumericEditor(NumericEditorTypes.TrackBar, 0, 100)]
		public int Scale { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Transparency", GroupName = "Common", Order = 24)]
		[NumericEditor(NumericEditorTypes.TrackBar, 0, 100)]
		public int Transparency
		{
			get => _transparency;
			set
			{
				if (_source != null && (DateTime.Now - _lastRender).TotalMilliseconds >= 200)
				{
					lock (_locker)
					{
						_image = SetOpacity(_source, (float)(value * 0.01));
						_lastRender = DateTime.Now;
						RedrawChart();
					}
				}

				_transparency = value;
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

		[Display(ResourceType = typeof(Resources), Name = "ImageLocation", GroupName = "Location", Description = "LogoFilePathDescription", Order = 70)]
		[SelectFileEditor(Environment.SpecialFolder.MyPictures, Filter = "Image files (*.bmp, *.gif, *.jpeg, *.jpg, *.png, *.tiff)|*.bmp;*.gif;*.jpeg;*.jpg;*.png;*.tiff", IsTextEditable = false)]
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
			_transparency = 100;
			Scale = 100;
			DataSeries[0].IsHidden = true;
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Historical);
			DrawAbovePrice = false;
		}

		#endregion

		#region Private methods

		private Image SetOpacity(Image image, float opacity)
		{
			var bmp = new Bitmap(image.Width, image.Height);

			using var g = Graphics.FromImage(bmp);

			var matrix = new ColorMatrix
			{
				Matrix33 = opacity
			};
			var attributes = new ImageAttributes();

			attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default,
				ColorAdjustType.Bitmap);

			g.DrawImage(image, new Rectangle(0, 0, bmp.Width, bmp.Height),
				0, 0, image.Width, image.Height,
				GraphicsUnit.Pixel, attributes);

			return bmp;
		}

		#endregion

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			lock (_locker)
			{
				if (File.Exists(_filePath))
				{
					if (new FileInfo(_filePath).Length <= Math.Pow(2, 20))
						_source = Image.FromFile(_filePath);
					else
						AddAlert("alert1", "File is too big to load!");
				}
				else
					_source = null;

				if (_source != null)
					_image = SetOpacity(_source, (float)(Transparency * 0.01));
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			lock (_locker)
			{
				if (_source == null)
					return;
			}

			var x = 0;
			var y = 0;

			lock (_locker)
			{
				var imageWidth = (int)Math.Round(Scale * 0.01m * _image.Width);
				var imageHeight = (int)Math.Round(Scale * 0.01m * _image.Height);

				switch (LogoLocation)
				{
					case Location.Center:
					{
						x = ChartInfo.PriceChartContainer.Region.Width / 2 - imageWidth / 2 + HorizontalOffset;

						y = ChartInfo.PriceChartContainer.Region.Height / 2 - imageHeight / 2 + VerticalOffset;

						break;
					}
					case Location.TopLeft:
					{
						x = HorizontalOffset;
						y = VerticalOffset;
						break;
					}
					case Location.TopRight:
					{
						x = ChartInfo.PriceChartContainer.Region.Width - imageWidth + HorizontalOffset;
						y = VerticalOffset;
						break;
					}
					case Location.BottomLeft:
					{
						x = HorizontalOffset;
						y = ChartInfo.PriceChartContainer.Region.Height - imageHeight + VerticalOffset;

						break;
					}
					case Location.BottomRight:
					{
						x = ChartInfo.PriceChartContainer.Region.Width - imageWidth + HorizontalOffset;
						y = ChartInfo.PriceChartContainer.Region.Height - imageHeight + VerticalOffset;

						break;
					}
					default:
						throw new ArgumentOutOfRangeException();
				}

				var rect = new Rectangle(x, y, imageWidth, imageHeight);
				context.DrawStaticImage(_image, rect);
			}
		}

		#endregion
	}
}