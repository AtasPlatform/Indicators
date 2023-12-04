namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[DisplayName("Daily Change")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DailyChangeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602542")]
	public class DailyChange : Indicator
	{
		#region Nested types

		public enum Align
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopLeft))]
			TopLeft = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TopRight))]
			TopRight = 1,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomLeft))]
			BottomLeft = 2,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomRight))]
			BottomRight = 3
		}

		public enum CalculationType
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OpenCurDay))]
			CurrentDayOpen = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.ClosePrevDay))]
			PreviousDayClose = 1
		}

		public enum ValueType
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent))]
			Percent = 0,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Ticks = 1,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.PriceChange))]
			Price = 2
		}

		#endregion

		#region Fields
		
		private readonly RenderStringFormat _textFormat = new()
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

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.PositiveValueColorDescription), Order = 1)]
		public System.Windows.Media.Color BuyColor
		{
			get => _buyColor.Convert();
			set => _buyColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGroundBuyColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.LabelFillColorDescription), Order = 2)]
		public System.Windows.Media.Color BackGroundBuyColor
		{
			get => _backgroundBuyColor.Convert();
			set => _backgroundBuyColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.NegativeValueColorDescription), Order = 3)]
		public System.Windows.Media.Color SellColor
		{
			get => _sellColor.Convert();
			set => _sellColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGroundSellColor), GroupName = nameof(Strings.Colors), Description = nameof(Strings.LabelFillColorDescription), Order = 4)]
		public System.Windows.Media.Color BackGroundSellColor
		{
			get => _backgroundSellColor.Convert();
			set => _backgroundSellColor = value.Convert();
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Common), Description = nameof(Strings.CalculationModeDescription))]
		public CalculationType CalcType
		{
			get => _calcType;
			set
			{
				_calcType = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.TextLocation), GroupName = nameof(Strings.Common), Description = nameof(Strings.LabelLocationDescription))]
		public Align Alignment { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Type), GroupName = nameof(Strings.Common), Description = nameof(Strings.SourceDescription))]
		public ValueType ValType { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontSize), GroupName = nameof(Strings.Common), Description = nameof(Strings.FontSizeDescription))]
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

			if (_lastSession < 0 && CalcType == CalculationType.PreviousDayClose)
				return;

			var candle = GetCandle(bar);

			switch (CalcType)
			{
				case CalculationType.PreviousDayClose:
					_startPrice = GetCandle(_lastSession - 1).Close;
					break;

				case CalculationType.CurrentDayOpen:
					_startPrice = GetCandle(Math.Max(0, _lastSession)).Open;
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

			if (_changeValue > 0)
				renderText = "+" + renderText;

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

				case Align.TopLeft:
					if (!MouseLocationInfo.IsMouseLeave)
						y = 15;
					break;
			}

			var textColor = renderValue < 0 ? _sellColor : _buyColor;
			var backgroundColor = renderValue < 0 ? _backgroundSellColor : _backgroundBuyColor;
			
			var rectangle = new Rectangle(x, y, width, height);

			context.FillRectangle(backgroundColor, backgroundColor, rectangle);

			context.DrawString(renderText, font, textColor, rectangle, _textFormat);
		}

		#endregion
	}
}