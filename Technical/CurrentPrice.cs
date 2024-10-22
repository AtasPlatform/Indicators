namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

    [DisplayName("Current price")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CurrentPriceDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602361-current-price")]
	public class CurrentPrice : Indicator
	{
		#region Fields

		private Color _background = Color.Blue;
		private RenderFont _font = new("Roboto", 14);

		private RenderStringFormat _stringFormat = new()
			{ LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Far };

		private Color _textColor = Color.LightBlue;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), Description = nameof(Strings.LabelFillColorDescription))]
		public CrossColor Background
		{
			get => _background.Convert();
			set => _background = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextColor), Description = nameof(Strings.LabelTextColorDescription))]
		public CrossColor TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontSize), Description = nameof(Strings.FontSizeDescription))]
		[Range(6, 30)]
		public float FontSize
		{
			get => _font.Size;
			set
			{
				_font = new RenderFont("Roboto", value);
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowTime), GroupName = nameof(Strings.Time), Description = nameof(Strings.IsNeedShowCurrentTimeDescription))]
		public bool ShowTime { get; set; } = true;

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFormat), GroupName = nameof(Strings.Time), Description = nameof(Strings.TimeFormatDescription))]
		public string TimeFormat { get; set; } = "HH:mm:ss";

		#endregion

		#region ctor

		public CurrentPrice()
			: base(true)
		{
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			EnableCustomDrawing = true;
			DataSeries[0].IsHidden = true;
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods
		
		protected override void OnCalculate(int bar, decimal value)
		{
		}
		
		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			if (LastVisibleBarNumber != CurrentBar - 1 || LastVisibleBarNumber < 0)
				return;

			var candle = GetCandle(LastVisibleBarNumber);
			var priceString = candle.Close.ToString();
			var size = context.MeasureString(priceString, _font);

			var x = (int)(ChartInfo.GetXByBar(LastVisibleBarNumber) + ChartInfo.PriceChartContainer.BarsWidth);
			var y = ChartInfo.GetYByPrice(candle.Close, false);
			var rectangle = new Rectangle(x + 10, y - size.Height / 2, size.Width + 10, size.Height);

			var points = new List<Point>
			{
				new(x, y),
				new(rectangle.X, rectangle.Y),
				new(rectangle.X + rectangle.Width, rectangle.Y),
				new(rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height),
				new(rectangle.X, rectangle.Y + rectangle.Height)
			};

			context.FillPolygon(_background, points.ToArray());

			rectangle.Y++;
			context.DrawString(priceString, _font, _textColor, rectangle, _stringFormat);

			if (!ShowTime)
				return;

			var time = MarketTime.AddHours(InstrumentInfo.TimeZone).ToString(TimeFormat);
			size = context.MeasureString(time, _font);
			context.DrawString(time, _font, _textColor, rectangle.X + rectangle.Width - size.Width, rectangle.Y - size.Height);
		}

		#endregion
	}
}