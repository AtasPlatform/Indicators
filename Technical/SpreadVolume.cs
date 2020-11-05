namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Drawing;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Context;
	using OFT.Rendering.Tools;

	using Utils.Common.Logging;

	[DisplayName("Spread Volumes Indicator")]
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

		private readonly RenderStringFormat _textFormat = new RenderStringFormat
		{
			Alignment = StringAlignment.Center,
			LineAlignment = StringAlignment.Center
		};

		private MarketDataArg _bestAsk;

		private MarketDataArg _bestBid;
		private Color _buyColor;
		private SpreadIndicatorItem _currentTrade;
		private Color _sellColor;

		private int _shift;
		private int _spacing;
		private List<SpreadIndicatorItem> _tradeList = new List<SpreadIndicatorItem>();
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

		[Display(ResourceType = typeof(Resources), Name = "Shift", GroupName = "Common")]
		public int Shift
		{
			get => _shift;
			set
			{
				_shift = value;
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
			_width = 20;
			_shift = 1;
			DataSeries[0].IsHidden = true;
			EnableCustomDrawing = true;
			DrawAbovePrice = true;
			SubscribeToDrawingEvents(DrawingLayouts.LatestBar);
		}

		#endregion

		#region Overrides of ExtendedIndicator

		protected override void OnNewTrade(MarketDataArg trade)
		{
			if (_bestBid == null || _bestAsk == null || trade.Direction == TradeDirection.Between)
				return;

			var bidPrice = _bestBid.Price;
			var askPrice = _bestAsk.Price;

			if (trade.Direction == TradeDirection.Buy)
				askPrice = trade.Price;
			else if (trade.Direction == TradeDirection.Sell)
				bidPrice = trade.Price;

			if (_currentTrade == null || _currentTrade.AskPrice != askPrice || _currentTrade.BidPrice != bidPrice)
			{
				_currentTrade = new SpreadIndicatorItem(bidPrice, askPrice);

				lock (_tradeList)
				{
					_tradeList.Add(_currentTrade);

					if (_tradeList.Count > 400)
						_tradeList = _tradeList.Skip(200).ToList();
				}
			}

			if (trade.Direction == TradeDirection.Buy)
				_currentTrade.AskVol += trade.Volume;
			else if (trade.Direction == TradeDirection.Sell)
				_currentTrade.BidVol += trade.Volume;
		}

		protected override void OnBestBidAskChanged(MarketDataArg depth)
		{
			if (depth.DataType == MarketDataType.Ask)
				_bestAsk = depth;

			else if (depth.DataType == MarketDataType.Bid)
				_bestBid = depth;
		}

		protected override void OnRender(RenderContext context, DrawingLayouts layout)
		{
			var temp = _tradeList;

			var j = -1;

			var firstBarX = ChartInfo.PriceChartContainer.GetXByBar(CurrentBar - 1);

			for (var i = temp.Count - 1; i >= 0; i--)
			{
				j++;
				var trade = temp[i];

				var x = firstBarX - j * (Spacing + Width) - Shift;

				if (x < 0)
					return;

				var y1 = ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice, true);
				var h = y1 - ChartInfo.PriceChartContainer.GetYByPrice(trade.AskPrice + 1, true);

				if (h == 0)
					continue;

				var y2 = ChartInfo.PriceChartContainer.GetYByPrice(trade.BidPrice, true);

				var rect1 = new Rectangle(x, y1, Width, h);
				var rect2 = new Rectangle(x, y2, Width, h);

				if (trade.AskVol != 0)
				{
					context.FillRectangle(_buyColor, rect1);
					context.DrawString(trade.AskVol.ToString(), _font, Color.Black, rect1, _textFormat);
				}

				if (trade.BidVol != 0)
				{
					context.FillRectangle(_sellColor, rect2);
					context.DrawString(trade.BidVol.ToString(), _font, Color.Black, rect2, _textFormat);
				}
			}
		}

		protected override void OnCalculate(int bar, decimal value)
		{
		}

		#endregion
	}
}