namespace ATAS.Indicators.Technical
{
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	[DisplayName("Spread Volumes Indicator")]
	[Category("Order Flow")]
	public class SpreadVolume : Indicator
	{
		#region Nested types

		private class SpreadIndicatorItem
		{
			#region Properties

			public decimal BidPrice { get; }

			public decimal AskPrice { get; }

			public decimal BidVol { get; set; }

			public decimal AskVol { get; set; }

			#endregion

			#region ctor

			public SpreadIndicatorItem(decimal bidPrice, decimal askPrice)
			{
				BidPrice = bidPrice;
				AskPrice = askPrice;
			}

			#endregion
		}

		#endregion

		#region Fields

		private readonly RenderFont _font = new RenderFont("Arial", 10);
		private readonly List<SpreadIndicatorItem> _prints = new List<SpreadIndicatorItem>();

		private readonly RenderStringFormat _textFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private readonly List<CumulativeTrade> _trades = new List<CumulativeTrade>();

		private Color _buyColor;
		private SpreadIndicatorItem _currentTrade;

		private int _offset;
		private Color _sellColor;
		private int _spacing;
		private Color _textColor;
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Colors", Order = 1)]
		public System.Windows.Media.Color BuyColor
		{
			get => _buyColor.Convert();
			set => _buyColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Colors", Order = 3)]
		public System.Windows.Media.Color SellColor
		{
			get => _sellColor.Convert();
			set => _sellColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "TextColor", GroupName = "Colors", Order = 4)]
		public System.Windows.Media.Color TextColor
		{
			get => _textColor.Convert();
			set => _textColor = value.Convert();
		}

		[Display(ResourceType = typeof(Resources), Name = "Spacing", GroupName = "Common")]
		public int Spacing
		{
			get => _spacing;
			set
			{
				_spacing = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Offset", GroupName = "Common")]
		public int Offset
		{
			get => _offset;
			set
			{
				_offset = value;
				RedrawChart();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Width", GroupName = "Common")]
		public int Width
		{
			get => _width;
			set
			{
				_width = value;
				RedrawChart();
			}
		}

		#endregion

		#region ctor

		public SpreadVolume()
			: base(true)
		{
			DenyToChangePanel = true;
			_buyColor = Color.Green;
			_sellColor = Color.Red;
			_textColor = Color.Black;
			_width = 20;
			_offset = 1;
			DataSeries[0].IsHidden = true;
			EnableCustomDrawing = true;
			DrawAbovePrice = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
		}

		#endregion

		#region Overrides of ExtendedIndicator

		protected override void OnCumulativeTrade(CumulativeTrade trade)
		{
			if (trade.Direction == TradeDirection.Between)
				return;

			_trades.Add(trade);
			_prints.Clear();

			if (_trades.Count > 200)
				_trades.RemoveRange(0, 100);

			var askPrice = 0m;
			var bidPrice = 0m;

			foreach (var tradeItem in _trades)
			{
				if (tradeItem.PreviousAsk.Price != askPrice || tradeItem.PreviousBid.Price != bidPrice || _currentTrade == null)

				{
					askPrice = tradeItem.PreviousAsk.Price;
					bidPrice = tradeItem.PreviousBid.Price;
					_currentTrade = new SpreadIndicatorItem(bidPrice, askPrice);

					if (tradeItem.Direction == TradeDirection.Buy)
						_currentTrade.AskVol = tradeItem.Volume;
					else if (tradeItem.Direction == TradeDirection.Sell)
						_currentTrade.BidVol = tradeItem.Volume;
					_prints.Add(_currentTrade);
				}
				else
				{
					if (tradeItem.Direction == TradeDirection.Buy)
						_currentTrade.AskVol += tradeItem.Volume;
					else if (tradeItem.Direction == TradeDirection.Sell)
						_currentTrade.BidVol += tradeItem.Volume;
				}
			}
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var temp = _prints;

			var j = -1;

			var firstBarX = ChartInfo.PriceChartContainer.GetXByBar(CurrentBar - 1);

			for (var i = temp.Count - 1; i >= 0; i--)
			{
				j++;
				var trade = temp[i];

				var x = firstBarX - j * (Spacing + Width) - Offset;

				if (x < 0)
					return;

				var y1 = ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice, true);
				var h = y1 - ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice + InstrumentInfo.TickSize, true);

				if (h == 0)
					continue;

				var y2 = ChartInfo.PriceChartContainer.GetYByPrice(trade.BidPrice, true);

				var rect1 = new Rectangle(x, y1, Width, h);
				var rect2 = new Rectangle(x, y2, Width, h);

				if (trade.AskVol != 0)
				{
					context.FillRectangle(_buyColor, rect1);
					context.DrawString(trade.AskVol.ToString(), _font, _textColor, rect1, _textFormat);
				}

				if (trade.BidVol != 0)
				{
					context.FillRectangle(_sellColor, rect2);
					context.DrawString(trade.BidVol.ToString(), _font, _textColor, rect2, _textFormat);
				}
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		#endregion
	}
}