namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using ATAS.DataFeedsCore;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Color = System.Drawing.Color;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Pen = OFT.Rendering.Tools.RenderPen;

[HelpLink("https://help.atas.net/en/support/solutions/articles/72000633119")]
[Category(IndicatorCategories.Trading)]
[DisplayName("Trades On Chart")]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.TradesOnChartDescription))]
public class TradesOnChart : Indicator
{
    #region Nested Types

    internal class TradeObj
    {
        internal int OpenBar { get; set; }
        internal decimal OpenPrice { get; set; }
        internal int CloseBar { get; set; }
        internal decimal ClosePrice { get; set; }
        internal OrderDirections Direction { get; set; }
		internal decimal PnL { get; set; }
		internal decimal PnLTicks { get; set; }
		internal DateTime OpenTime { get; set; }
		internal DateTime CloseTime { get; set; }
        internal decimal Volume { get; set; }
        internal string Security { get; set; }


		public TradeObj(HistoryMyTrade trade)
		{
			OpenPrice = trade.OpenPrice;
			ClosePrice = trade.ClosePrice;
			Direction = trade.OpenVolume > 0 ? OrderDirections.Buy : OrderDirections.Sell;
			PnL = trade.PnL;
			PnLTicks = trade.TicksPnL;
			OpenTime = trade.OpenTime;
			CloseTime = trade.CloseTime;
			Volume = Math.Abs(trade.OpenVolume);
			Security = trade.Security.Code;
		}
    }

    #endregion

    #region Fields

    private RenderFont _font = new RenderFont("Arial", 10F, FontStyle.Regular, GraphicsUnit.Point, 204);
    private RenderStringFormat _stringFormat = new RenderStringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private readonly List<TradeObj> _trades = new();
    private Pen _buyPen;
    private Pen _sellPen;
    private Color _buyColor;
    private Color _sellColor;
    private float _lineWidth = 2f;
    private DashStyle _lineStyle = DashStyle.Dash;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLines), GroupName = nameof(Strings.Visualization))]
    public bool ShowLine { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDescription), GroupName = nameof(Strings.Visualization))]
    public bool ShowTooltip { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BuyColor), GroupName = nameof(Strings.Visualization))]
    public Color BuyColor 
    {
        get => _buyColor;
        set
        {
            _buyColor = value;
            _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SellColor), GroupName = nameof(Strings.Visualization))]
    public Color SellColor 
    { 
        get => _sellColor;
        set
        {
            _sellColor = value;
            _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);
        }
    }

    [Range(1, 20)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LineWidth), GroupName = nameof(Strings.Visualization))]
    public float LineWidth 
    { 
        get => _lineWidth; 
        set
        {
            _lineWidth = value;
            _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
            _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.DashStyle), GroupName = nameof(Strings.Visualization))]
    public DashStyle LineStyle 
    {
        get => _lineStyle;
        set
        {
            _lineStyle = value;
            _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
            _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);
        }
    }

    [Range(1, 10)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Size), GroupName = nameof(Strings.Visualization))]
    public int MarkerSize { get; set; } = 2;

    #endregion

    #region ctor

    public TradesOnChart() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

        SubscribeToDrawingEvents(DrawingLayouts.Final);
        EnableCustomDrawing = true;
    }

    #endregion

    #region Protected Methods

    protected override void OnInitialize()
    {
        TradingStatisticsProvider.Realtime.HistoryMyTrades.Added += OnTradeAdded;
        TradingManager.PortfolioSelected += TradingManager_PortfolioSelected;
    }

    private void TradingManager_PortfolioSelected(Portfolio obj)
    {
	    OnRecalculate();
    }

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null) return;

        BuyColor = ChartInfo.ColorsStore.BuyOrdersColor;
        SellColor = ChartInfo.ColorsStore.SellOrdersColor;
    }

    protected override void OnRecalculate()
    {
        _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
        _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);

        _trades.Clear();
        AddHistoryMyTrade();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
       
    }

    #region Rendering

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null) return;

        DrawTrades(context);
    }

    private void DrawTrades(RenderContext context)
    {
        List<(string Text, Color FillColor)> tooltips = new();

	    foreach (var trade in _trades)
	    {
	        if (trade.OpenBar > LastVisibleBarNumber || trade.CloseBar < FirstVisibleBarNumber)
                continue;

            var x1 = ChartInfo.GetXByBar(trade.OpenBar, false);
            var y1 = ChartInfo.GetYByPrice(trade.OpenPrice, false);
            var x2 = ChartInfo.GetXByBar(trade.CloseBar, false);
            var y2 = ChartInfo.GetYByPrice(trade.ClosePrice, false);
            var pen = GetPenByDirection(trade.Direction);

            if(ShowLine)
				context.DrawLine(pen, x1, y1, x2, y2);

            var mouseOver = DrawMarker(context, new Point(x1, y1), trade.Direction, true);

            var mouseOver2 = DrawMarker(context, new Point(x2, y2), trade.Direction, false);

            if (ShowTooltip && (mouseOver || mouseOver2))
            {
                var cl = trade.PnL > 0 ? _buyColor : _sellColor;

                var text = (trade.Direction == OrderDirections.Buy ? "Long" : "Short") + " " +
                    trade.Volume.ToString() + " " + trade.Security + Environment.NewLine + Environment.NewLine;

                text += $"Entry\t:  {ChartInfo.GetPriceString(trade.OpenPrice)} | {trade.OpenTime:dd MMM HH:mm:ss}{Environment.NewLine}";
                text += $"Exit\t:  {ChartInfo.GetPriceString(trade.ClosePrice)} | {trade.CloseTime:dd MMM HH:mm:ss}{Environment.NewLine}{Environment.NewLine}";
                text += $"Result\t: {(trade.PnL > 0 ? "+" : "")}{trade.PnL} ({trade.PnLTicks} ticks)";

                tooltips.Add((text, cl));
            }
        }

	    if (tooltips.Any())
	    {
		    var size = context.MeasureString(tooltips.First().Text, _font);
		    size = new Size(size.Width + 20, size.Height + 20);
		    var totalHeight = tooltips.Count * (size.Height + 5);

		    var y = MouseLocationInfo.LastPosition.Y;

            if(y + totalHeight> Container.Region.Height)
                y = Container.Region.Height- totalHeight;

            foreach (var tooltip in tooltips)
		    {
			    size = context.MeasureString(tooltip.Text, _font);
			    size = new Size(size.Width + 20, size.Height + 20);
			    var rectangle = new Rectangle(MouseLocationInfo.LastPosition.X, y, size.Width, size.Height);
			    context.FillRectangle(tooltip.FillColor, rectangle, 10);
			    rectangle.X += 10;
			    context.DrawString(tooltip.Text, _font, Color.AliceBlue, rectangle, _stringFormat);

			    y += size.Height + 5;
		    }
        }
    }

    private bool DrawMarker(RenderContext context, Point point, OrderDirections direction, bool isOpen)
    {
        var shift = MarkerSize * 4;
        var dir = direction == OrderDirections.Buy ? 1 : -1;
        var y2 = isOpen ? (point.Y + shift * dir) : (point.Y + shift * (-dir));
        var point2 = new Point(point.X - shift, y2);
        var point3 = new Point(point2.X + shift * 2, point2.Y);
        var color = GetMarkerColor(direction, isOpen);

        var points = new Point[] { point, point2, point3 };

        context.FillPolygon(color, points);

        context.DrawPolygon(ChartInfo.ColorsStore.Grid, points);

        if (IsPointInTriangle(MouseLocationInfo.LastPosition, point, point2, point3))
        {
            return true;
        }

        return false;
    }

    #endregion

    #endregion

    #region Private Methods

    private void AddHistoryMyTrade()
    {
	    if(TradingManager?.Portfolio == null|| TradingManager?.Security == null)
            return;

	    var allTrades = TradingStatisticsProvider?.Realtime?.HistoryMyTrades
		    .Where(t => t.AccountID == TradingManager.Portfolio.AccountID
			    && t.Security.Instrument == TradingManager.Security.Instrument);

	    foreach (var trade in allTrades)
	    {
		    CreateTradePair(trade);
	    }
    }

    private void OnTradeAdded(HistoryMyTrade trade)
    {
	    if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
		    return;

        if (trade.AccountID == TradingManager.Portfolio.AccountID && trade.Security.Instrument == TradingManager.Security.Instrument)
		    CreateTradePair(trade);        
    }

    private void CreateTradePair(HistoryMyTrade trade)
    {
        var enterBar = GetBarByTime(trade.OpenTime);

        if (enterBar < 0) return;

        var exitBar = GetBarByTime(trade.CloseTime);

        var tradeObj = new TradeObj(trade)
        {
            OpenBar = enterBar,
            CloseBar = exitBar,
        };

        _trades.Add(tradeObj);
    }

    private int GetBarByTime(DateTime time)
    {
        for (int i = CurrentBar - 1; i >= 0; i--) 
        {
            var candle = GetCandle(i);

            if (candle.Time <= time)
                return i;
        }

        return -1;
    }

    private bool IsPointInTriangle(Point p, Point p0, Point p1, Point p2)
    {
	    double area = TriangleArea(p0, p1, p2);
	    double area1 = TriangleArea(p, p0, p1);
	    double area2 = TriangleArea(p, p1, p2);
	    double area3 = TriangleArea(p, p2, p0);

	    return Math.Abs(area - (area1 + area2 + area3)) < 0.001;
    }

    private double TriangleArea(Point p0, Point p1, Point p2)
    {
	    return Math.Abs((p0.X * (p1.Y - p2.Y) + p1.X * (p2.Y - p0.Y) + p2.X * (p0.Y - p1.Y)) / 2.0);
    }

    private Color GetMarkerColor(OrderDirections direction, bool isOpen)
    {
        return direction switch
        {
            OrderDirections.Buy => isOpen ? _buyColor : _sellColor,
            OrderDirections.Sell => isOpen ? _sellColor : _buyColor,
            _ => Color.Transparent
        };
    }

    private Pen GetPenByDirection(OrderDirections directions)
    {
        return directions switch
        {
            OrderDirections.Buy => _buyPen,
            _ => _sellPen,
        };
    }

    private Pen GetNewPen(Color color, float lineWidth, DashStyle lineStyle)
    {
        return new Pen(color, lineWidth) { DashStyle = lineStyle };
    }

    #endregion
}
