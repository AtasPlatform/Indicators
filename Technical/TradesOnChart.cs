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
using Color = System.Drawing.Color;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using Pen = OFT.Rendering.Tools.RenderPen;

[FeatureId("NotReady")]
[DisplayName("Trades On Chart")]
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
    }

    #endregion

    #region Fields

    private readonly List<TradeObj> _trades = new();

    private Pen _buyPen;
    private Pen _sellPen;
    private Color _buyColor;
    private Color _sellColor;
    private float _lineWidth = 2f;
    private DashStyle _lineStyle = DashStyle.Dash;

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Show), GroupName = nameof(Strings.Visualization))]
    public bool ShowTrades { get; set; } = true;

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

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
       if(ChartInfo is null) return;

        if (ShowTrades)
            DrawTrades(context);
    }

    #endregion

    #region Private Methods

    private void AddHistoryMyTrade()
    {
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
        CreateTradePair(trade);
    }

    private void CreateTradePair(HistoryMyTrade trade)
    {
        var enterBar = GetBarByTime(trade.OpenTime);

        if (enterBar < 0) return;

        var exitBar = GetBarByTime(trade.CloseTime);

        var tradeObj = new TradeObj
        {
            OpenBar = enterBar,
            OpenPrice = trade.OpenPrice,
            CloseBar = exitBar,
            ClosePrice = trade.ClosePrice,
            Direction = trade.OpenVolume > 0 ? OrderDirections.Buy : OrderDirections.Sell,
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

    private void DrawTrades(RenderContext context)
    {
        foreach (var trade in _trades)
        {
            if (trade.OpenBar > LastVisibleBarNumber || trade.CloseBar < FirstVisibleBarNumber)
                continue;

            var x1 = ChartInfo.GetXByBar(trade.OpenBar, false);
            var y1=ChartInfo.GetYByPrice(trade.OpenPrice, false);
            var x2 = ChartInfo.GetXByBar(trade.CloseBar, false);
            var y2 = ChartInfo.GetYByPrice(trade.ClosePrice, false);
            var pen = GetPenByDirection(trade.Direction);

            context.DrawLine(pen, x1, y1, x2, y2);
            DrawMarker(context, new Point(x1, y1), trade.Direction, true);
            DrawMarker(context, new Point(x2, y2), trade.Direction, false);
        }
    }

    private void DrawMarker(RenderContext context, Point point, OrderDirections direction, bool isOpen)
    {
        var shift = MarkerSize * 5;
        var dir = direction == OrderDirections.Buy ? 1 : -1;
        var y2 = isOpen ? (point.Y + shift * dir) : (point.Y + shift * (-dir));
        var point2 = new Point(point.X - shift, y2);
        var point3 = new Point(point2.X + shift * 2, point2.Y);
        var color = GetMarkerColor(direction, isOpen);

        context.FillPolygon(color, new Point[] { point, point2, point3 });
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
