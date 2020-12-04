namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Color = System.Drawing.Color;

	[Category("Other")]
	[DisplayName("Depth Of Market")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/352-depth-of-market")]
	public class DOM : Indicator
	{
		#region Static and constants

		private const int _fontSize = 10;
		private const int _unitedVolumeHeight = 15;

		#endregion

		#region Fields

		private readonly ValueDataSeries _downScale = new ValueDataSeries("Down");

		private readonly RenderStringFormat _stringLeftFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Near,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private readonly RenderStringFormat _stringRightFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Far,
			LineAlignment = StringAlignment.Center,
			Trimming = StringTrimming.EllipsisCharacter,
			FormatFlags = StringFormatFlags.NoWrap
		};

		private readonly ValueDataSeries _upScale = new ValueDataSeries("Up");

		private Color _askBackGround;

		private Color _askColor;
		private Color _bestAskBackGround;
		private Color _bestBidBackGround;
		private Color _bidBackGround;
		private Color _bidColor;

		private RenderFont _font = new RenderFont("Arial", _fontSize);

		private int _priceLevelsHeight;
		private int _proportionVolume;
		private int _scale;
		private Color _textColor;
		private Color _volumeAskColor;
		private Color _volumeBidColor;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseAutoSize", GroupName = "HistogramSize", Order = 100)]
		public bool UseAutoSize { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "ProportionVolume", GroupName = "HistogramSize", Order = 110)]
		public int ProportionVolume
		{
			get => _proportionVolume;
			set
			{
				if (value < 0)
					return;

				_proportionVolume = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "HistogramSize", Order = 120)]
		public int Width
		{
			get => _width;
			set
			{
				if (value < 0)
					return;

				_width = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RightToLeft", GroupName = "HistogramSize", Order = 130)]
		public bool RightToLeft { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "BidRows", GroupName = "Colors", Order = 200)]
		public System.Windows.Media.Color BidRows
		{
			get => _bidColor.Convert();
			set
			{
				_bidColor = value.Convert();
				_volumeBidColor = Color.FromArgb(50, value.R, value.G, value.B);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Colors", Order = 210)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "AskRows", GroupName = "Colors", Order = 220)]
		public System.Windows.Media.Color AskRows
		{
			get => _askColor.Convert();
			set
			{
				_askColor = value.Convert();
				_volumeAskColor = Color.FromArgb(50, value.R, value.G, value.B);
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BidsBackGround", GroupName = "Colors", Order = 230)]
		public System.Windows.Media.Color BidsBackGround
		{
			get => _bidBackGround.Convert();
			set => _bidBackGround = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "AsksBackGround", GroupName = "Colors", Order = 240)]
		public System.Windows.Media.Color AsksBackGround
		{
			get => _askBackGround.Convert();
			set => _askBackGround = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BestBidBackGround", GroupName = "Colors", Order = 250)]
		public System.Windows.Media.Color BestBidBackGround
		{
			get => _bestBidBackGround.Convert();
			set => _bestBidBackGround = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "BestAskBackGround", GroupName = "Colors", Order = 260)]
		public System.Windows.Media.Color BestAskBackGround
		{
			get => _bestAskBackGround.Convert();
			set => _bestAskBackGround = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "ShowCumulativeValues", GroupName = "Other", Order = 300)]
		public bool ShowCumulativeValues { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "CustomPriceLevelsHeight", GroupName = "Other", Order = 310)]
		public int PriceLevelsHeight
		{
			get => _priceLevelsHeight;
			set
			{
				if (value < 0)
					return;

				_priceLevelsHeight = value;
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "UseScale", GroupName = "Scale", Order = 400)]
		public bool UseScale { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "CustomScale", GroupName = "Scale", Order = 410)]
		public int Scale
		{
			get => _scale;
			set
			{
				if (value < 0)
					return;

				_scale = value;
			}
		}

		#endregion

		#region ctor

		public DOM()
			: base(true)
		{
			DrawAbovePrice = true;
			DenyToChangePanel = true;
			_upScale.IsHidden = _downScale.IsHidden = true;
			_upScale.VisualType = _downScale.VisualType = VisualMode.Hide;
			_upScale.ScaleIt = _downScale.ScaleIt = true;

			DataSeries[0] = _upScale;
			DataSeries.Add(_downScale);

			EnableCustomDrawing = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar | DrawingLayouts.Final);

			UseAutoSize = true;
			ProportionVolume = 100;
			Width = 100;
			RightToLeft = true;

			BidRows = Colors.Green;
			TextColor = Colors.White;
			AskRows = Colors.Red;

			ShowCumulativeValues = true;
			Scale = 20;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			if (bar != ChartInfo.PriceChartContainer.TotalBars)
				return;

			if (!UseScale)
				return;

			_upScale[bar - 1] = 0;
			_downScale[bar - 1] = 0;
			var dom = MarketDepthInfo.GetMarketDepthSnapshot().ToList();

			_upScale[bar] = dom
				.Where(x => x.Direction == TradeDirection.Buy)
				.Max(x => x.Price) + InstrumentInfo.TickSize * (_scale + 3);

			_downScale[bar] = dom
				.Where(x => x.Direction == TradeDirection.Sell)
				.Min(x => x.Price) - InstrumentInfo.TickSize * (_scale + 3);

			RedrawChart();
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var depth = MarketDepthInfo
				.GetMarketDepthSnapshot()
				.ToList();

			if (!depth.Any())
				return;

			var asks = depth
				.Where(x => x.Direction == TradeDirection.Buy)
				.OrderBy(x => x.Price)
				.ToList();

			var bids = depth.Where(x => x.Direction == TradeDirection.Sell)
				.OrderByDescending(x => x.Price)
				.ToList();

			var maxVolume = depth.Max(x => x.Volume);

			var minAsk = 0m;

			if (asks.Count > 0)
				minAsk = asks.First().Price;

			var maxBid = 0m;

			if (bids.Count > 0)
				maxBid = bids.First().Price;

			if (asks.Count != 0 && bids.Count != 0)
			{
				maxVolume = asks.Sum(t => t.Volume) / asks.Count +
					bids.Sum(t => t.Volume) / bids.Count;
			}

			if (!UseAutoSize)
				maxVolume = ProportionVolume;

			var height = (int)Math.Floor(ChartInfo.PriceChartContainer.PriceRowHeight) - 1;

			height = height < 1 ? 1 : height;

			if (PriceLevelsHeight != 0)
				height = PriceLevelsHeight - 2;

			var textAutoSize = GetTextSize(context, height);

			var y2 = ChartInfo.GetYByPrice(minAsk - InstrumentInfo.TickSize);
			var y3 = ChartInfo.GetYByPrice(maxBid);
			var y4 = Container.Region.Height;

			var fullRect = new Rectangle(new Point(Container.Region.Width - Width, 0), new Size(Width, y2));

			context.FillRectangle(_askBackGround, fullRect);

			fullRect = new Rectangle(new Point(Container.Region.Width - Width, y3),
				new Size(Width, y4 - y3));

			context.FillEllipse(_bidBackGround, fullRect);

			var currentPrice = GetCandle(ChartInfo.PriceChartContainer.TotalBars).Close;
			var currentPriceY = ChartInfo.GetYByPrice(currentPrice);

			if (asks.Any())
			{
				int y;
				var firstPrice = asks.First().Price;

				foreach (var priceDepth in asks)
				{
					if (PriceLevelsHeight == 0)
					{
						y = ChartInfo.GetYByPrice(priceDepth.Price);
						height = Math.Abs(y - ChartInfo.GetYByPrice(priceDepth.Price - InstrumentInfo.TickSize)) - 1;

						if (height < 1)
							height = 1;
					}
					else
					{
						height = PriceLevelsHeight - 1;

						if (height < 1)
							height = 1;
						var diff = (priceDepth.Price - firstPrice) / InstrumentInfo.TickSize;
						y = currentPriceY - height * ((int)diff + 1) - (int)diff - 15;
					}

					if (y < Container.Region.Top)
						break;

					var width = (int)Math.Floor(priceDepth.Volume * Width /
						(maxVolume == 0 ? 1 : maxVolume));

					if (priceDepth.Price == minAsk)
					{
						var bestRect = new Rectangle(new Point(Container.Region.Width - Width, y),
							new Size(Width, height));
						context.FillRectangle(_bestAskBackGround, bestRect);
					}

					var rect = new Rectangle(Container.Region.Width - width, y, width, height);

					var form = _stringRightFormat;

					if (!RightToLeft)
					{
						width = Math.Min(width, Width);

						rect = new Rectangle(new Point(Container.Region.Width - Width, y),
							new Size(width, height));
						form = _stringLeftFormat;
					}

					context.FillRectangle(_askColor, rect);

					_font = new RenderFont("Arial", textAutoSize);

					context.DrawString(priceDepth.Volume.ToString(CultureInfo.InvariantCulture),
						_font,
						_textColor,
						rect,
						form);
				}
			}

			if (bids.Any())
			{
				var spread = 0;

				if (asks.Any())
					spread = (int)((asks.First().Price - bids.First().Price) / InstrumentInfo.TickSize);

				int y;
				var firstPrice = bids.First().Price;

				foreach (var priceDepth in bids)
				{
					if (PriceLevelsHeight == 0)
					{
						y = ChartInfo.GetYByPrice(priceDepth.Price);
						height = Math.Abs(y - ChartInfo.GetYByPrice(priceDepth.Price - InstrumentInfo.TickSize)) - 1;

						if (height < 1)
							height = 1;
					}
					else
					{
						height = PriceLevelsHeight - 1;

						if (height < 1)
							height = 1;
						var diff = (firstPrice - priceDepth.Price) / InstrumentInfo.TickSize;
						y = currentPriceY + height * ((int)diff + spread - 1) + (int)diff - 15;
					}

					if (y > Container.Region.Bottom)
						break;

					var width = (int)Math.Floor(priceDepth.Volume * Width /
						(maxVolume == 0 ? 1 : maxVolume));

					if (priceDepth.Price == maxBid)
					{
						var bestRect = new Rectangle(new Point(Container.Region.Width - Width, y),
							new Size(Width, height));
						context.FillRectangle(_bestBidBackGround, bestRect);
					}

					var rect = new Rectangle(new Point(Container.Region.Width - width, y),
						new Size(width, height));

					var form = _stringRightFormat;

					if (!RightToLeft)
					{
						width = Math.Min(width, Width);

						rect = new Rectangle(new Point(Container.Region.Width - Width, y),
							new Size(width, height));
						form = _stringLeftFormat;
					}

					context.FillRectangle(_bidColor, rect);

					_font = new RenderFont("Arial", textAutoSize);

					context.DrawString(priceDepth.Volume.ToString(CultureInfo.InvariantCulture),
						_font,
						_textColor,
						rect,
						form);
				}
			}

			if (!ShowCumulativeValues)
				return;

			var maxWidth = (int)Math.Round(Container.Region.Width * 0.2m);
			var totalVolume = MarketDepthInfo.CumulativeDomAsks + MarketDepthInfo.CumulativeDomBids;

			if (totalVolume == 0)
				return;

			var font = new RenderFont("Arial", 9);

			var askRowWidth = (int)Math.Round(MarketDepthInfo.CumulativeDomAsks * (maxWidth - 1) / totalVolume);
			var bidRowWidth = maxWidth - askRowWidth;
			var yRect = Container.Region.Bottom - _unitedVolumeHeight;
			var bidStr = $"{MarketDepthInfo.CumulativeDomBids:0.##}";
			var askStr = $"{MarketDepthInfo.CumulativeDomAsks:0.##}";

			var askWidth = context.MeasureString(askStr, font).Width;
			var bidWidth = context.MeasureString(bidStr, font).Width;

			if (askWidth > askRowWidth && MarketDepthInfo.CumulativeDomAsks != 0)
			{
				askRowWidth = askWidth;
				maxWidth = (int)Math.Round(Math.Min(Container.Region.Width * 0.3m, totalVolume * askRowWidth / MarketDepthInfo.CumulativeDomAsks + 1));
				bidRowWidth = maxWidth - askRowWidth;
			}

			if (bidWidth > bidRowWidth && MarketDepthInfo.CumulativeDomBids != 0)
			{
				bidRowWidth = bidWidth;
				maxWidth = (int)Math.Round(Math.Min(Container.Region.Width * 0.3m, totalVolume * bidRowWidth / MarketDepthInfo.CumulativeDomBids + 1));
				askRowWidth = maxWidth - bidRowWidth;
			}

			if (askRowWidth > 0)
			{
				var askRect = new Rectangle(new Point(Container.Region.Width - askRowWidth, yRect),
					new Size(askRowWidth, _unitedVolumeHeight));
				context.FillRectangle(_volumeAskColor, askRect);
				context.DrawString(askStr, font, _bidColor, askRect, _stringLeftFormat);
			}

			if (bidRowWidth > 0)
			{
				var bidRect = new Rectangle(new Point(Container.Region.Width - maxWidth, yRect),
					new Size(bidRowWidth, _unitedVolumeHeight));
				context.FillRectangle(_volumeBidColor, bidRect);
				context.DrawString(bidStr, font, _askColor, bidRect, _stringRightFormat);
			}
		}

		protected override void MarketDepthChanged(MarketDataArg depth)
		{
			RedrawChart();
		}

		#endregion

		#region Private methods

		private int GetTextSize(RenderContext context, int height)
		{
			for (var i = _fontSize; i > 0; i--)
			{
				if (context.MeasureString("12", new RenderFont("Arial", i)).Height < height + 5)
					return i;
			}

			return 0;
		}

		#endregion
	}
}