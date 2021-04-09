namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[DisplayName("Daily Change")]
	public class DailyChange : Indicator
	{
		#region Nested types

		public enum Align
		{
			[Display(ResourceType = typeof(Resources), Name = "TopLeft")]
			TopLeft = 0,

			[Display(ResourceType = typeof(Resources), Name = "TopRight")]
			TopRight = 1,

			[Display(ResourceType = typeof(Resources), Name = "BottomLeft")]
			BottomLeft = 2,

			[Display(ResourceType = typeof(Resources), Name = "BottomRight")]
			BottomRight = 3
		}

		public enum CalculationType
		{
			[Display(ResourceType = typeof(Resources), Name = "OpenCurDay")]
			CurrentDayOpen = 0,

			[Display(ResourceType = typeof(Resources), Name = "ClosePrevDay")]
			PreviousDayClose = 1
		}

		public enum ValueType
		{
			[Display(ResourceType = typeof(Resources), Name = "Percent")]
			Percent = 0,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks = 1,

			[Display(ResourceType = typeof(Resources), Name = "PriceChange")]
			Price = 2
		}

		#endregion

		#region Fields

		private readonly Color _gray = Color.LightSlateGray;
		private readonly Color _green = Color.LimeGreen;

		private readonly Color _red = Color.Red;

		private readonly RenderStringFormat _textFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private Color _backgroundBuyColor;
		private Color _backgroundSellColor;
		private Color _buyColor;

		private CalculationType _calcType;

		private decimal _changeValue;
		private int _lastSession = -1;
		private Color _sellColor;
		private decimal _startPrice;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Colors", Order = 1)]
		public System.Windows.Media.Color BuyColor
		{
			get => _buyColor.Convert();
			set => _buyColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BackGroundBuyColor", GroupName = "Colors", Order = 2)]
		public System.Windows.Media.Color BackGroundBuyColor
		{
			get => _backgroundBuyColor.Convert();
			set => _backgroundBuyColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Colors", Order = 3)]
		public System.Windows.Media.Color SellColor
		{
			get => _sellColor.Convert();
			set => _sellColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BackGroundSellColor", GroupName = "Colors", Order = 4)]
		public System.Windows.Media.Color BackGroundSellColor
		{
			get => _backgroundSellColor.Convert();
			set => _backgroundSellColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Common")]
		public CalculationType CalcType
		{
			get => _calcType;
			set
			{
				_calcType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextLocation", GroupName = "Common")]
		public Align Alignment { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "Type", GroupName = "Common")]
		public ValueType ValType { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "FontSize", GroupName = "Common")]
		public int FontSize { get; set; }

		#endregion

		#region ctor

		public DailyChange()
			: base(true)
		{
			DenyToChangePanel = true;
			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.Final);
			DataSeries[0].IsHidden = true;

			_calcType = CalculationType.PreviousDayClose;

			Alignment = Align.BottomRight;
			FontSize = 14;
			_buyColor = Color.LimeGreen;
			_sellColor = Color.Red;
			_backgroundBuyColor = _backgroundSellColor = Color.LightGray;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			if (IsNewSession(bar))
				_lastSession = bar;

			if (_lastSession < 0)
				return;

			var candle = GetCandle(bar);

			switch (CalcType)
			{
				case CalculationType.PreviousDayClose:
					_startPrice = GetCandle(_lastSession - 1).Close;
					break;

				case CalculationType.CurrentDayOpen:
					_startPrice = GetCandle(_lastSession).Open;
					break;
			}

			_changeValue = candle.Close - _startPrice;
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var renderValue = 0.0m;
			var renderText = "";

			if (_lastSession > 0 || CalcType != CalculationType.PreviousDayClose)
			{
				switch (ValType)
				{
					case ValueType.Percent:
						renderValue = _changeValue / _startPrice * 100.0m;
						renderText = $"{renderValue:0.00}" + "%";
						break;

					case ValueType.Ticks:
						renderValue = _changeValue / ChartInfo.PriceChartContainer.Step;
						renderText = $"{renderValue} ticks";
						break;

					case ValueType.Price:
						renderValue = _changeValue;
						renderText = $"{renderValue}";
						break;
				}
			}
			else
				renderText = "Previous day is not loaded";

			var font = new RenderFont("Arial", FontSize);
			var stringSize = context.MeasureString(renderText, font);
			var width = stringSize.Width + 10;
			var height = stringSize.Height + 5;
			int x = 0, y = 0;

			switch (Alignment)
			{
				case Align.BottomRight:
					x = Container.Region.Width - width;
					y = Container.Region.Height - height - 15;
					break;

				case Align.BottomLeft:
					y = Container.Region.Height - height - 15;
					break;

				case Align.TopRight:
					x = Container.Region.Width - width;
					break;
			}

			var textColor = renderValue < 0 ? _sellColor : _buyColor;
			var backgroundColor = renderValue < 0 ? _backgroundSellColor : _backgroundBuyColor;

			if (_changeValue > 0)
				renderText = "+" + renderText;

			var rectangle = new Rectangle(x, y, width, height);

			context.FillRectangle(backgroundColor, backgroundColor, rectangle);

			context.DrawString(renderText, font, textColor, rectangle, _textFormat);
		}

		#endregion
	}
}