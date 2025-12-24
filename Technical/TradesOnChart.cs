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

[HelpLink("https://help.atas.net/support/solutions/articles/72000633119")]
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

    public enum LabelDisplayMode
    {
        [Display(Name = "Hide")]
        Hide,
        [Display(Name = "Short")]
        Short,
        [Display(Name = "Full")]
        Full
    }

    #endregion

    #region Fields

    private RenderFont _font = new RenderFont("Arial", 10F, FontStyle.Regular, GraphicsUnit.Point, 204);
    private RenderFont _labelFont = new RenderFont("Arial", 8F, FontStyle.Regular, GraphicsUnit.Point, 204);
    private RenderStringFormat _stringFormat = new RenderStringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private readonly List<TradeObj> _trades = new();
    private Pen _buyPen;
    private Pen _sellPen;
    private Color _buyColor;
    private Color _sellColor;
    private Color _profitColor;
    private Color _lossColor;
    private float _lineWidth = 2f;
    private DashStyle _lineStyle = DashStyle.Dash;
    private readonly List<Rectangle> _labelsAbove = new();
    private readonly List<Rectangle> _labelsBelow = new();

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLines), GroupName = nameof(Strings.Visualization))]
    public bool ShowLine { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDescription), GroupName = nameof(Strings.Visualization))]
    public bool ShowTooltip { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LabelDisplay), Description = nameof(Strings.LabelDisplayDescription), GroupName = nameof(Strings.Visualization))]
    public LabelDisplayMode LabelDisplay { get; set; } = LabelDisplayMode.Hide;

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

    [Display(ResourceType = typeof(Strings), Name = "Profit Color", GroupName = nameof(Strings.Visualization), Description = "Color for profitable trades result section")]
    public Color ProfitColor
    {
        get => _profitColor;
        set => _profitColor = value;
    }

    [Display(ResourceType = typeof(Strings), Name = "Loss Color", GroupName = nameof(Strings.Visualization), Description = "Color for losing trades result section")]
    public Color LossColor
    {
        get => _lossColor;
        set => _lossColor = value;
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

        OnRecalculate();
    }

    private void TradingManager_PortfolioSelected(Portfolio obj)
    {
	    OnRecalculate();
    }

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo is null) return;

        BuyColor = Color.FromArgb(0xFF, 0x2C, 0x4F, 0x3A);
        SellColor = Color.FromArgb(0xFF, 0x64, 0x27, 0x33);
        ProfitColor = Color.FromArgb(0xFF, 0x16, 0x7A, 0x3B);
        LossColor = Color.FromArgb(0xFF, 0xB0, 0x49, 0x4F);
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
        List<TradeObj> tooltipTrades = new();
        List<(TradeObj Trade, bool MouseOverMarker1, bool MouseOverMarker2)> tradeInfo = new();
        _labelsAbove.Clear();
        _labelsBelow.Clear();

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

            tradeInfo.Add((trade, mouseOver, mouseOver2));
        }

        foreach (var (trade, mouseOver, mouseOver2) in tradeInfo)
        {
            var mouseOverLabel = false;

            if (LabelDisplay != LabelDisplayMode.Hide)
            {
                var candle = GetCandle(trade.CloseBar);
                var isAbove = trade.Direction == OrderDirections.Buy;

                var (labelRect, labelHover) = DrawTradeLabel(context, trade, trade.CloseBar, candle, isAbove);
                mouseOverLabel = labelHover;

                if (isAbove)
                    _labelsAbove.Add(labelRect);
                else
                    _labelsBelow.Add(labelRect);
            }

            if (ShowTooltip && (mouseOver || mouseOver2 || mouseOverLabel))
            {
                tooltipTrades.Add(trade);
            }
        }

	    if (tooltipTrades.Any())
	    {
		    var y = MouseLocationInfo.LastPosition.Y;

            foreach (var trade in tooltipTrades)
		    {
			    DrawTooltip(context, trade, MouseLocationInfo.LastPosition.X, ref y);
			    y += 5;
		    }
        }
    }

    private void DrawTooltip(RenderContext context, TradeObj trade, int x, ref int y)
    {
        var directionColor = trade.Direction == OrderDirections.Buy ? _buyColor : _sellColor;
        var resultColor = trade.PnL > 0 ? _profitColor : _lossColor;
        var cornerRadius = 3;

        var direction = trade.Direction == OrderDirections.Buy ? "Long" : "Short";
        var openTime = trade.OpenTime.AddHours(InstrumentInfo.TimeZone);
        var closeTime = trade.CloseTime.AddHours(InstrumentInfo.TimeZone);

        var topText = $"{direction} {trade.Volume} {trade.Security}{Environment.NewLine}{Environment.NewLine}" +
                      $"Entry\t:  {ChartInfo.GetPriceString(trade.OpenPrice)}  {openTime:dd MMM HH:mm:ss}{Environment.NewLine}" +
                      $"Exit\t:  {ChartInfo.GetPriceString(trade.ClosePrice)}  {closeTime:dd MMM HH:mm:ss}";

        var bottomText = $"Result:  {(trade.PnL > 0 ? "+" : "")}{trade.PnL}  ({trade.PnLTicks} ticks)";

        var topSize = context.MeasureString(topText, _font);
        var bottomSize = context.MeasureString(bottomText, _font);

        var padding = 10;
        var width = (int)Math.Max(topSize.Width, bottomSize.Width) + padding * 2;
        var topHeight = (int)topSize.Height + padding * 2;
        var bottomHeight = (int)bottomSize.Height + padding * 2;

        var topRect = new Rectangle(x, y, width, topHeight + cornerRadius * 2);
        var bottomRect = new Rectangle(x, y + topHeight, width, bottomHeight);

        context.FillRectangle(directionColor, topRect, cornerRadius);
        context.FillRectangle(resultColor, bottomRect, cornerRadius);

        var overlapCover = new Rectangle(x, y + topHeight, width, cornerRadius * 2);
        context.FillRectangle(resultColor, overlapCover);

        var topTextRect = new Rectangle(x + padding, y + padding, width - padding * 2, topHeight - padding * 2);
        var bottomTextRect = new Rectangle(x + padding, y + topHeight + padding, width - padding * 2, bottomHeight - padding * 2);

        var textFormat = new RenderStringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

        context.DrawString(topText, _font, Color.White, topTextRect, textFormat);
        context.DrawString(bottomText, _font, Color.White, bottomTextRect, textFormat);

        y += topHeight + bottomHeight;
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

    private (Rectangle Rect, bool MouseOver) DrawTradeLabel(RenderContext context, TradeObj trade, int bar, IndicatorCandle candle, bool isAbove)
    {
        var direction = trade.Direction == OrderDirections.Buy ? "L" : "S";
        var pnlSign = trade.PnL > 0 ? "+" : "";

        string leftText, rightText;

        if (LabelDisplay == LabelDisplayMode.Full)
        {
            var entryPrice = ChartInfo.GetPriceString(trade.OpenPrice);
            var exitPrice = ChartInfo.GetPriceString(trade.ClosePrice);
            leftText = $"{direction} {trade.Volume} | {entryPrice}→{exitPrice}";
            rightText = $" {pnlSign}{trade.PnL} ({trade.PnLTicks}t)";
        }
        else
        {
            leftText = $"{direction} {trade.Volume}";
            rightText = $" {pnlSign}{trade.PnL} ({trade.PnLTicks}t)";
        }

        var leftSize = context.MeasureString(leftText, _labelFont);
        var rightSize = context.MeasureString(rightText, _labelFont);

        var padding = 3;
        var leftWidth = leftSize.Width + padding;
        var rightWidth = rightSize.Width + padding;
        var rectWidth = leftWidth + rightWidth;
        var rectHeight = Math.Max(leftSize.Height, rightSize.Height) + padding * 2;

        var candleX = ChartInfo.GetXByBar(bar, false);
        var barWidth = (int)ChartInfo.PriceChartContainer.BarsWidth;
        var labelX = candleX - barWidth / 2;

        var markerOffset = MarkerSize * 4;
        var baseY = isAbove
            ? ChartInfo.GetYByPrice(candle.High, false) - markerOffset - rectHeight
            : ChartInfo.GetYByPrice(candle.Low, false) + markerOffset;

        var spacing = 3;
        var stepSize = rectHeight + spacing;
        var yPosition = baseY;

        var testRect = new Rectangle(labelX, yPosition, rectWidth, rectHeight);
        var allLabels = _labelsAbove.Concat(_labelsBelow).ToList();

        while (allLabels.Any(r => r.IntersectsWith(testRect)))
        {
            var intersecting = allLabels.Where(r => r.IntersectsWith(testRect)).ToList();

            if (intersecting.Any())
            {
                if (isAbove)
                {
                    var topmost = intersecting.Min(r => r.Y);
                    yPosition = topmost - stepSize;
                }
                else
                {
                    var bottommost = intersecting.Max(r => r.Bottom);
                    yPosition = bottommost + spacing;
                }

                testRect = new Rectangle(labelX, yPosition, rectWidth, rectHeight);
            }
        }

        var directionColor = trade.Direction == OrderDirections.Buy ? _buyColor : _sellColor;
        var resultColor = trade.PnL > 0 ? _profitColor : _lossColor;
        var cornerRadius = 3;

        var leftSectionRect = new Rectangle(testRect.X, testRect.Y, leftWidth + cornerRadius * 2, testRect.Height);
        context.FillRectangle(directionColor, leftSectionRect, cornerRadius);

        var rightSectionRect = new Rectangle(testRect.X + leftWidth, testRect.Y, rightWidth, testRect.Height);
        context.FillRectangle(resultColor, rightSectionRect, cornerRadius);

        var overlapCover = new Rectangle(testRect.X + leftWidth, testRect.Y, cornerRadius * 2, testRect.Height);
        context.FillRectangle(resultColor, overlapCover);

        var leftTextRect = new Rectangle(testRect.X + padding, testRect.Y + padding, leftWidth - padding, testRect.Height - padding * 2);
        var rightTextRect = new Rectangle(testRect.X + leftWidth, testRect.Y + padding, rightWidth - padding, testRect.Height - padding * 2);

        var leftFormat = new RenderStringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        var rightFormat = new RenderStringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        context.DrawString(leftText, _labelFont, Color.White, leftTextRect, leftFormat);
        context.DrawString(rightText, _labelFont, Color.White, rightTextRect, rightFormat);

        var mouseOver = testRect.Contains(MouseLocationInfo.LastPosition);

        return (testRect, mouseOver);
    }

    #endregion

    #endregion

    #region Private Methods

    private void AddHistoryMyTrade()
    {
	    if (TradingManager?.Portfolio == null|| TradingManager?.Security == null)
            return;

	    var allTrades = TradingStatisticsProvider?.Realtime?.HistoryMyTrades
		    .Where(t => 
                t.AccountID == TradingManager.Portfolio.AccountID && 
                t.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase));

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
