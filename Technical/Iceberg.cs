namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using MoreLinq.Extensions;

	public class Iceberg : Indicator
    {
	    private PriceSelectionDataSeries _icebergsRender = new(Resources.Values)
	    {
			IsHidden = true
	    };

	    private System.Drawing.Color _buyColor = System.Drawing.Color.FromArgb(128, System.Drawing.Color.LawnGreen);
	    private System.Drawing.Color _sellColor = System.Drawing.Color.FromArgb(128, System.Drawing.Color.DarkRed);
	    private CumulativeTrade _lastTrade = new();
	    private ObjectType _visualMode = ObjectType.Triangle;
	    private int _size = 10;
	    private object _locker = new();
	    private int _startBar;

	    [Display(ResourceType = typeof(Resources), Name = "BuyColor", GroupName = "Visualization", Order = 100)]
	    public Color BuyColor
	    {
		    get => _buyColor.Convert();
		    set => _buyColor = value.Convert();
	    }

	    [Display(ResourceType = typeof(Resources), Name = "SellColor", GroupName = "Visualization", Order = 110)]
	    public Color SellColor
        {
		    get => _sellColor.Convert();
		    set => _sellColor = value.Convert();
	    }

	    [Display(ResourceType = typeof(Resources), Name = "VisualMode", GroupName = "Visualization", Order = 120)]
	    public ObjectType VisualMode
        {
		    get => _visualMode;
		    set => _visualMode = value;
	    }

	    [Display(ResourceType = typeof(Resources), Name = "Size", GroupName = "Visualization", Order = 130)]
		[Range(1, 1000)]
	    public int Size
        {
		    get => _size;
		    set
		    {
			    _size = value;

			    for (var bar = 0; bar < _icebergsRender.Count; bar++)
			    {
					if (_icebergsRender[bar].Count == 0)
						continue;

					_icebergsRender[bar].ForEach(x=>x.Size = value);
			    }
		    }
        }



        public Iceberg()
		    : base(true)
	    {
		    DenyToChangePanel = true;

		    DataSeries[0] = _icebergsRender;
	    }
		
		protected override void OnCumulativeTrade(CumulativeTrade trade)
	    {
			if (InstrumentInfo is null)
			    return;

			lock(_locker)
				ProcessTrade(trade, CurrentBar - 1);

			_lastTrade = trade;
	    }

		protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
		{
			if (InstrumentInfo is null)
				return;

			lock (_locker)
                ProcessTrade(trade, CurrentBar - 1);

			_lastTrade = trade;
        }

        protected override void OnCalculate(int bar, decimal value)
	    {
		    if (bar == 0)
		    {
			    _startBar = CurrentBar - 1;

			    for (var i = _startBar; i >= 0; i--)
			    {
				    if (!IsNewSession(i))
					    continue;

				    _startBar = i;
				    break;
			    }

			    var startTime = GetCandle(_startBar).Time;
			    var endTime = GetCandle(CurrentBar - 1).LastTime;

				RequestForCumulativeTrades(new CumulativeTradesRequest(startTime, endTime, 0 , 0));
		    }
	    }
		
        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
	        var icebergTrades = cumulativeTrades
		        .Where(trade => 
		        trade.Direction == TradeDirection.Buy
		        && trade.NewAsk.Volume == trade.PreviousAsk.Volume
		        && trade.NewAsk.Price == trade.PreviousAsk.Price
		        && trade.FirstPrice == trade.Lastprice
		        ||
		        trade.Direction is TradeDirection.Sell
		        && trade.NewBid.Volume == trade.PreviousBid.Volume
		        && trade.NewBid.Price == trade.PreviousBid.Price
		        && trade.FirstPrice == trade.Lastprice)
		        .ToList();

	        var curBar = _startBar;

	        foreach (var trade in icebergTrades)
	        {
		        while (curBar <= CurrentBar - 1)
		        {
			        var candle = GetCandle(curBar);

			        if (trade.Time >= candle.Time && trade.Time <= candle.LastTime)
			        {
						lock(_locker)
							ProcessTrade(trade, curBar, true);
				        break;
			        }

			        curBar++;
		        }
	        }
        }

        private void ProcessTrade(CumulativeTrade trade, int bar, bool isHistory = false)
	    {
		    var updateTrade = _lastTrade.IsEqual(trade);

		    if (trade.Direction is TradeDirection.Buy 
		        && trade.NewAsk.Volume == trade.PreviousAsk.Volume 
		        && trade.NewAsk.Price == trade.PreviousAsk.Price 
		        && trade.FirstPrice == trade.Lastprice)
		    {
				if(!isHistory)
			    if (updateTrade && _icebergsRender[bar]
				        .Any(x => x.MinimumPrice == trade.FirstPrice && (TradeDirection)x.Context == TradeDirection.Buy))
			    {
				    var lastIdx = _icebergsRender[bar].Count - 1;

				    _icebergsRender[bar].RemoveAt(lastIdx);
			    }

			    _icebergsRender[bar].Add(new PriceSelectionValue(trade.FirstPrice)
			    {
				    PriceSelectionColor = BuyColor,
					ObjectColor = BuyColor,
				    Tooltip = "Direction: " + trade.Direction + Environment.NewLine + $"Volume: {trade.Volume:0.#######}",
				    VisualObject = VisualMode,
					MinimumPrice = trade.FirstPrice,
					MaximumPrice = trade.FirstPrice,
				    Context = trade.Direction,
					Size = _size
                });
			    return;
		    }


		    if (trade.Direction is TradeDirection.Sell 
		        && trade.NewBid.Volume == trade.PreviousBid.Volume 
		        && trade.NewBid.Price == trade.PreviousBid.Price 
		        && trade.FirstPrice == trade.Lastprice)
		    {
			    if (!isHistory)
                    if (updateTrade && _icebergsRender[bar]
				        .Any(x => x.MinimumPrice == trade.FirstPrice && (TradeDirection)x.Context == TradeDirection.Sell))
			    {
				    var lastIdx = _icebergsRender[bar].Count - 1;

				    _icebergsRender[bar].RemoveAt(lastIdx);
			    }

                _icebergsRender[bar].Add(new PriceSelectionValue(trade.FirstPrice)
			    {
				    PriceSelectionColor = SellColor,
					ObjectColor = SellColor,
				    Tooltip = "Direction: " + trade.Direction + Environment.NewLine + $"Volume: {trade.Volume:0.#######}",
				    VisualObject = VisualMode,
				    MinimumPrice = trade.FirstPrice,
				    MaximumPrice = trade.FirstPrice,
                    Context = trade.Direction,
                    Size = _size
                });
			    return;
		    }
        }
    }
}
