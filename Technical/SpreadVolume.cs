namespace ATAS.Indicators.Technical
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Controls.Primitives;

    using ATAS.Indicators.Technical.Properties;

    using OFT.Rendering.Context;
    using OFT.Rendering.Tools;

    using Color = System.Windows.Media.Color;

    [DisplayName("Spread Volumes")]
    public class SpreadVolume : Indicator
    {
	    public class SpreadIndicatorItem
	    {
		    public decimal BidPrice;
		    public decimal BidVol;
		    public decimal AskPrice;
		    public decimal AskVol;
	    }

        #region Fields

        private int _shift;
        private int _spacing;
        private int _width;
        private MarketDataArg _ask;
        private MarketDataArg _bid;
        private System.Drawing.Color _buyColor;
        private System.Drawing.Color _sellColor;
        private SpreadIndicatorItem _currentTrade;
       private List<SpreadIndicatorItem> _tradeList = new List<SpreadIndicatorItem>();

        private List<CumulativeTrade> _cumulativeTrades = new List<CumulativeTrade>();

        private readonly RenderFont _font = new RenderFont("Arial", 10);

        private readonly RenderStringFormat _textFormat = new RenderStringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Colors", Order = 1)]
        public System.Windows.Media.Color BuyColor
        {

            get => System.Windows.Media.Color.FromRgb(_buyColor.R, _buyColor.G, _buyColor.B);
            set => _buyColor = System.Drawing.Color.FromArgb(value.R, value.G, value.B);
        }

        [Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Colors", Order = 3)]
        public System.Windows.Media.Color SellColor
        {
            get => System.Windows.Media.Color.FromRgb(_sellColor.R, _sellColor.G, _sellColor.B);
            set => _sellColor = System.Drawing.Color.FromArgb(value.R, value.G, value.B);
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
            _buyColor = System.Drawing.Color.Green;
            _sellColor = System.Drawing.Color.Red;
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
	         
        }

        #endregion

        

        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
	        if (trade.Direction == TradeDirection.Between)
		        return;

	        if (trade.PreviousAsk.Price != _ask.Price || trade.PreviousBid.Price != _bid.Price || _currentTrade == null)
	        {
		        _ask = trade.PreviousAsk;
		        _bid = trade.PreviousBid;
                _currentTrade = new SpreadIndicatorItem
                {
	                AskPrice = _ask.Price,
                    BidPrice = _bid.Price
                };

                if (trade.Direction == TradeDirection.Buy)
	                _currentTrade.AskVol = trade.NewAsk.Volume;
                else
                {
	                _currentTrade.BidVol = trade.NewBid.Volume;
                }

                lock (_tradeList)
                {
	                _tradeList.Add(_currentTrade);
                }
	        }

	        lock (_tradeList)
	        {
		        if (_tradeList.Count > 400)
			        _tradeList = _tradeList.Skip(200).ToList();
	        }

            /*
	        if (trade.Direction != TradeDirection.Between)
	            _cumulativeTrades.Insert(0, trade);
	        */
        }


        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {


            //var oldTrade = _cumulativeTrades.FirstOrDefault(x => IsEqual(x, trade));
            //var renderTrade = new List<CumulativeTrade>();

            //if (oldTrade != default)
            //{
            //    if (trade.Volume == oldTrade.Volume)
            //        return;
            //    _cumulativeTrades.Remove(oldTrade);
            //}
            //_cumulativeTrades.Insert(0, trade);
            //RedrawChart();

        }


        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            //_cumulativeTrades = _cumulativeTrades.OrderByDescending(x => x.Time).ToList();
            
            /*
            _cumulativeTrades.ForEach(x =>
            {
	            if (x.Direction != TradeDirection.Between)
	            {
                    if(x.PreviousAsk.Price!=_ask||x.PreviousBid.Price!=_bid)


	            }
            });

            */
            /*
            if (_cumulativeTrades.Count == 0)
                return;


            if (_cumulativeTrades.Count > 400)
                _cumulativeTrades = _cumulativeTrades.Take(200).ToList();
            */
            var tickSize = InstrumentInfo.TickSize;
            int j = -1;

            foreach (var trade in _cumulativeTrades)
            {
                j++;
                var x = ChartInfo.PriceChartContainer.GetXByBar(CurrentBar - 1, true) - j * (Spacing + Width) - Shift;
                if (x < 0)
                    return;

                var y1 = ChartInfo.PriceChartContainer.GetYByPrice(trade.NewAsk.Price, true);
                var h = y1 - ChartInfo.PriceChartContainer.GetYByPrice(trade.NewAsk.Price + tickSize, true);
                if (h == 0)
                    continue;

                var y2 = ChartInfo.PriceChartContainer.GetYByPrice(trade.NewBid.Price, true);

                var buyRect = new Rectangle(x, y1, Width, h);
                var sellRect = new Rectangle(x, y2, Width, h);



                if (trade.Direction == TradeDirection.Buy)
                {
                    context.FillRectangle(_buyColor, buyRect);
                    context.DrawString($"{trade.Volume}", _font, System.Drawing.Color.Black, buyRect, _textFormat);
                }

                if (trade.Direction == TradeDirection.Sell)
                {
                    context.FillRectangle(_sellColor, sellRect);
                    context.DrawString($"{trade.Volume}", _font, System.Drawing.Color.Black, sellRect, _textFormat);
                }
            }
        }


        protected override void OnCalculate(int bar, decimal value)
        {

        }

        public static bool IsEqual(CumulativeTrade trade1, CumulativeTrade trade2)
        {
            return trade1.Time == trade2.Time && trade1.Direction == trade2.Direction && trade1.FirstPrice == trade2.FirstPrice;
        }

    }
}