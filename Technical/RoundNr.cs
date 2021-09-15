namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Windows.Media;

	using OFT.Localization;
	using OFT.Rendering.Context;
	using OFT.Rendering.Settings;
	using OFT.Rendering.Tools;

	[DisplayName("Round Numbers")]
	public class RoundNr : Indicator
	{
		#region Fields

		private readonly RenderFont _renderFont = new("Arial", 10);
		private int _step;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "Step", GroupName = "Settings", Order = 100)]
		public int Step
		{
			get => _step;
			set
			{
				if (value <= 0)
					return;

				_step = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "Line", GroupName = "Settings", Order = 110)]
		public PenSettings Pen { get; set; } = new()
			{ Color = Colors.Red, Width = 1 };

		#endregion

		#region ctor

		public RoundNr()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final | DrawingLayouts.Historical);
			_step = 100;
			DataSeries[0].IsHidden = true;
		}

		#endregion

		#region Protected methods

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var low = GetFirstValue(ChartInfo.PriceChartContainer.Low);
			var high = ChartInfo.PriceChartContainer.High;
			var levelHeight = ChartInfo.GetYByPrice(0) - ChartInfo.GetYByPrice(InstrumentInfo.TickSize * Step);
			var renderText = "TextCheck";
			var textHeight = context.MeasureString(renderText, _renderFont).Height;
			var isFreeSpace = levelHeight > textHeight;

			for (var i = low; i <= high; i += InstrumentInfo.TickSize * _step)
			{
				var y = ChartInfo.GetYByPrice(i, false);

				if (y > ChartInfo.Region.Height)
					continue;

				if (y < 0)
					break;

				context.DrawLine(Pen.RenderObject, 0, y, ChartInfo.Region.Width, y);

				if (isFreeSpace)
				{
					var textWidth = context.MeasureString(i.ToString(CultureInfo.InvariantCulture), _renderFont).Width;
					var rect = new Rectangle(ChartInfo.Region.Width - textWidth, y - textHeight, textWidth, textHeight);
					context.DrawString(i.ToString(CultureInfo.InvariantCulture), _renderFont, Pen.RenderObject.Color, rect);
				}
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		#endregion

		#region Private methods

		private decimal GetFirstValue(decimal low)
		{
			var lowLines = low / (_step * InstrumentInfo.TickSize);

			if (lowLines % 1 == 0)
				return low;

			return Math.Truncate(lowLines) * _step * InstrumentInfo.TickSize;
		}

		#endregion
	}
}