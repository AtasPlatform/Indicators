namespace ATAS.Indicators.Technical;

using ATAS.DataFeedsCore;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
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

    public enum LabelHorizontalAnchor
    {
        [Display(Name = "Close bar")]
        CloseBar,

        [Display(Name = "Midpoint (multi-bar)")]
        Midpoint
    }

    public enum LabelVerticalReference
    {
        [Display(Name = "Operation range (min/max)")]
        OperationRange,

        [Display(Name = "Local window (±N) around anchor")]
        LocalWindow
    }


    #endregion

    #region Fields

    private RenderFont _font = new RenderFont("Arial", 10F, FontStyle.Regular, GraphicsUnit.Point, 204);
    private RenderFont _labelFont = new RenderFont("Arial", 8F, FontStyle.Regular, GraphicsUnit.Point, 204);
    private RenderStringFormat _stringFormat = new RenderStringFormat() { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
    private readonly List<TradeObj> _trades = new();
    private readonly object _tradesSync = new();
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
    private volatile int _historyLoadToken;
    private bool _isHistoryLoading;
    // Keep a persistent dedupe set per context; do NOT clear on every recalc.
    private readonly HashSet<string> _seenTradeKeys = new(StringComparer.InvariantCultureIgnoreCase);
    // Request signature caching.
    private readonly object _requestSync = new();
    private string _lastReqAcc;
    private string _lastReqSec;
    private DateTime _lastReqFrom;
    private DateTime _lastReqTo;
    // Context signature for "current" chart stats.
    private string _ctxAcc;
    private string _ctxSec;

    // Fast lookup to update already drawn trades when they become complete (Changed event)
    private readonly Dictionary<long, TradeObj> _tradeById = new();

    // Pending actions executed on OnCalculate (avoid UI-thread issues / race timing)
    private volatile bool _pendingSyncClosedTrades;
    private string _pendingSyncReason;
    private volatile bool _pendingRedraw;

    // Deferred sync retry (no timers). We retry until the provider snapshot actually includes the closed trade.
    private int _pendingSyncRetriesLeft;
    private DateTime _lastSnapshotMaxCloseTime = DateTime.MinValue;

    private bool _subscriptionsAttached;

    // Pending history reload executed on OnCalculate
    private volatile bool _pendingHistoryReload;
    private string _pendingHistoryReason;

    // Candle time cache to avoid O(N) scans in GetBarByTime.
    // Built on demand and reused until CurrentBar changes.
    private DateTime[] _candleTimesCache = Array.Empty<DateTime>();
    private int _candleTimesCacheSize;

    // Render buffers (avoid per-frame allocations).
    private readonly List<TradeObj> _tooltipTradesBuffer = new();
    private readonly List<(TradeObj Trade, Rectangle Rect)> _tooltipRectsBuffer = new();

    #endregion

    #region Properties

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowLines), GroupName = nameof(Strings.Visualization))]
    public bool ShowLine { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowDescription), GroupName = nameof(Strings.Visualization))]
    public bool ShowTooltip { get; set; } = true;

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LabelDisplay), Description = nameof(Strings.LabelDisplayDescription), GroupName = nameof(Strings.Visualization))]
    public LabelDisplayMode LabelDisplay { get; set; } = LabelDisplayMode.Hide;

    [Display(Name = "Label X anchor", GroupName = nameof(Strings.Visualization), Description = "Anchor labels horizontally at close bar or at the midpoint (multi-bar trades).")]
    public LabelHorizontalAnchor LabelXAnchor { get; set; } = LabelHorizontalAnchor.CloseBar;

    [Display(Name = "Label Y reference", GroupName = nameof(Strings.Visualization), Description = "Compute the candle range from the whole trade period or from a local window around the anchor.")]
    public LabelVerticalReference LabelYReference { get; set; } = LabelVerticalReference.LocalWindow;

    [Range(0, 10)]
    [Display(Name = "Label local window", GroupName = nameof(Strings.Visualization), Description = "Number of bars to look back/forward around the anchor when Y reference is Local window.")]
    public int LabelLocalWindow { get; set; } = 0;

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

        if (_subscriptionsAttached)
        {
            return;
        }

        _subscriptionsAttached = true;

        TradingManager.PortfolioSelected += TradingManager_PortfolioSelected;
        TradingManager.PositionChanged += TradingManager_PositionChanged;

        OnRecalculate();
    }

    protected override void OnDispose()
    {
        var rtHist = TradingStatisticsProvider?.Realtime?.HistoryMyTrades;
        if (rtHist != null)
        {
            rtHist.Added -= OnTradeAdded;
            rtHist.Changed -= OnTradeChanged;
            rtHist.Removed -= OnTradeRemoved;
            rtHist.Cleared -= OnTradesCleared;
        }
        else
        {
            var hist = TradingStatisticsProvider?.Statistics?.HistoryMyTrades;
            if (hist != null)
            {
                hist.Added -= OnTradeAdded;
                hist.Changed -= OnTradeChanged;
                hist.Removed -= OnTradeRemoved;
                hist.Cleared -= OnTradesCleared;
            }
        }

        TradingManager.PortfolioSelected -= TradingManager_PortfolioSelected;
        TradingManager.PositionChanged -= TradingManager_PositionChanged;

        _subscriptionsAttached = false;
        base.OnDispose();
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
        var acc = TradingManager?.Portfolio?.AccountID;
        var sec = TradingManager?.Security?.SecurityId;


        _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
        _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);

        // If we are already loading, do not start another load.
        if (_isHistoryLoading)
        {
            return;
        }

        // Only reset caches when context changed (account/security).
        var ctxChanged =
            !string.Equals(_ctxAcc, acc, StringComparison.InvariantCultureIgnoreCase) ||
            !string.Equals(_ctxSec, sec, StringComparison.InvariantCultureIgnoreCase);

        if (ctxChanged)
        {
            _ctxAcc = acc;
            _ctxSec = sec;

            lock (_tradesSync)
            {
                _trades.Clear();
                _seenTradeKeys.Clear();
                _candleTimesCache = Array.Empty<DateTime>();
                _candleTimesCacheSize = 0;
            }
        }

        RequestHistoryForChartRange();
    }


    protected override void OnCalculate(int bar, decimal value)
    {
        if (_pendingHistoryReload)
        {
            var reason = _pendingHistoryReason ?? "Unknown";
            _pendingHistoryReload = false;
            _pendingHistoryReason = null;

            RequestHistoryForChartRange();
        }

        // Execute deferred sync/redraw inside the indicator lifecycle (no timers).
        if (_pendingSyncClosedTrades)
        {
            var reason = _pendingSyncReason ?? "Unknown";

            var beforeMaxClose = _lastSnapshotMaxCloseTime;

            SyncClosedTradesFromHistorySnapshot(reason);

            // If provider snapshot did not advance yet, keep retrying on next OnCalculate (bounded, no timers).
            if (_lastSnapshotMaxCloseTime <= beforeMaxClose && _pendingSyncRetriesLeft > 0)
            {
                _pendingSyncRetriesLeft--;
                _pendingSyncClosedTrades = true; // keep pending
                _pendingRedraw = true;
            }
            else
            {
                _pendingSyncClosedTrades = false;
                _pendingSyncReason = null;
                _pendingSyncRetriesLeft = 0;
            }
        }

        if (_pendingRedraw)
        {
            _pendingRedraw = false;
            RedrawChart();
        }
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

        TradeObj[] tradesSnapshot;
        lock (_tradesSync)
            tradesSnapshot = _trades.ToArray();

        foreach (var trade in tradesSnapshot)
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

        // PERFORMANCE: Avoid building/iterating over all labels.
        // We only need to test against the most recent labels in each lane because we draw left->right by bar.
        const int maxCheckPerSide = 64;   // tuneable: 32/64 are usually enough
        const int maxRelocateAttempts = 6; // bound the cost on UI thread

        int attempts = 0;

        while (attempts < maxRelocateAttempts)
        {
            attempts++;

            bool intersects = false;

            // Check last N labels above
            for (int i = _labelsAbove.Count - 1, checkedCount = 0; i >= 0 && checkedCount < maxCheckPerSide; i--, checkedCount++)
            {
                if (_labelsAbove[i].IntersectsWith(testRect))
                {
                    intersects = true;
                    if (isAbove)
                    {
                        // Move further up above the intersecting label
                        yPosition = _labelsAbove[i].Y - stepSize;
                    }
                    else
                    {
                        // If we are placing below but intersect with above labels, push down
                        yPosition = _labelsAbove[i].Bottom + spacing;
                    }

                    testRect = new Rectangle(labelX, yPosition, rectWidth, rectHeight);
                    break;
                }
            }

            if (intersects)
                continue;

            // Check last N labels below
            for (int i = _labelsBelow.Count - 1, checkedCount = 0; i >= 0 && checkedCount < maxCheckPerSide; i--, checkedCount++)
            {
                if (_labelsBelow[i].IntersectsWith(testRect))
                {
                    intersects = true;
                    if (isAbove)
                    {
                        // If we are placing above but intersect with below labels, push up
                        yPosition = _labelsBelow[i].Y - stepSize;
                    }
                    else
                    {
                        // Move further down below the intersecting label
                        yPosition = _labelsBelow[i].Bottom + spacing;
                    }

                    testRect = new Rectangle(labelX, yPosition, rectWidth, rectHeight);
                    break;
                }
            }

            if (!intersects)
                break;
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
    private void OnTradeAdded(HistoryMyTrade trade)
    {
        if (_isHistoryLoading)
        {
            return;
        }

        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!string.Equals(trade.AccountID, TradingManager.Portfolio.AccountID, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        if (!trade.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }

        var key = GetTradeKey(trade);

        lock (_tradesSync)
        {
            if (!_seenTradeKeys.Add(key))
            {
                return;
            }
        }

        if (!trade.IsComplete)
        {
            return;
        }

        CreateTradePairNoDedupe(trade);

        _pendingRedraw = true;
    }



    private static int GetBarByTime(DateTime[] candleTimes, DateTime time)
    {
        var last = candleTimes.Length - 1;
        if (last <= 0)
            return 0;

        if (candleTimes[0] >= time)
            return 0;

        if (candleTimes[last] <= time)
            return last;

        var lo = 0;
        var hi = last;
        var result = 0;

        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (candleTimes[mid] <= time)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    private int GetBarByTime(DateTime time)
    {
        var candleTimes = EnsureCandleTimesCache();
        if (candleTimes.Length == 0)
            return -1;

        // If candle times are in chart timezone and trade times are in another,
        // normalize here if needed (keep as-is for now since your real-time works).
        return GetBarByTime(candleTimes, time);
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

    private bool TryGetChartTimeRange(out DateTime from, out DateTime to)
    {
        from = default;
        to = default;

        if (CurrentBar <= 0)
            return false;

        var first = GetCandle(0);
        var last = GetCandle(CurrentBar - 1);

        // Defensive: candles can be null on some initialization phases
        if (first == null || last == null)
            return false;

        from = first.Time;
        to = last.Time;

        // Ensure valid range
        if (to < from)
            (from, to) = (to, from);

        return true;
    }

    private async void RequestHistoryForChartRange()
    {
        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!TryGetChartTimeRange(out var from, out var to))
            return;

        var acc = TradingManager.Portfolio.AccountID;
        var sec = TradingManager.Security.SecurityId;

        // Normalize to seconds to avoid micro-deltas spamming requests.
        from = TruncateToSeconds(from);
        to = TruncateToSeconds(to);

        // 1) Signature guard FIRST (no token/flags touched).
        lock (_requestSync)
        {
            if (string.Equals(_lastReqAcc, acc, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(_lastReqSec, sec, StringComparison.InvariantCultureIgnoreCase) &&
                _lastReqFrom == from &&
                _lastReqTo == to)
            {
                return;
            }

            _lastReqAcc = acc;
            _lastReqSec = sec;
            _lastReqFrom = from;
            _lastReqTo = to;
        }

        // 2) Now we can mark a real load attempt.
        var token = ++_historyLoadToken;
        _isHistoryLoading = true;

        try
        {
            TradingStatisticsProvider.From = from;
            TradingStatisticsProvider.To = to;
            TradingStatisticsProvider.Accounts = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { acc };
            TradingStatisticsProvider.Securities = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { sec };

            // LoadHistoryAsync is obsolete in this build; we keep it scoped here because it is required to
            // force provider refresh for the chart time range using the current recommended statistics source.
            #pragma warning disable CS0618
            var stats = await TradingStatisticsProvider.LoadHistoryAsync(from, to, new[] { acc }, new[] { sec });
            #pragma warning restore CS0618

            if (token != _historyLoadToken)
            {
                return;
            }

            // Take a filtered snapshot FIRST. If it's empty, do NOT wipe existing drawn trades.
            // This prevents a race where LoadHistoryAsync completes but provider snapshot is not yet populated.
            var providerTrades = TradingStatisticsProvider.Statistics.HistoryMyTrades;
            var filtered = providerTrades
                .Where(t =>
                    string.Equals(t.AccountID, acc, StringComparison.InvariantCultureIgnoreCase) &&
                    t.Security.SecurityId.Equals(sec, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (filtered.Count == 0)
            {
                return;
            }

            lock (_tradesSync)
            {
                _trades.Clear();
                // _seenTradeKeys is kept to prevent duplicates across reloads.
            }

            int total = 0, matchedRaw = 0, matchedUnique = 0, duplicates = 0, created = 0;
            var snapshotKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var t in filtered)
            {
                total++;
                matchedRaw++;

                var key = GetTradeKey(t);

                if (!snapshotKeys.Add(key))
                {
                    duplicates++;
                    continue;
                }

                matchedUnique++;

                bool isNewKey;
                lock (_tradesSync)
                    isNewKey = _seenTradeKeys.Add(key);

                var before = _trades.Count;

                CreateTradePairNoDedupe(t);

                if (_trades.Count > before)
                    created++;
            }

            RedrawChart();

        }
        finally
        {
            if (token == _historyLoadToken)
                _isHistoryLoading = false;
        }
    }


    private string GetTradeKey(HistoryMyTrade trade)
    {
        // Prefer the unique trade id when available (long in this build).
        if (trade.Id != 0)
            return trade.Id.ToString();

        // Fallback: deterministic composite key (stringify everything)
        return string.Join("|", new[]
        {
        trade.AccountID ?? string.Empty,
        trade.Security?.SecurityId ?? string.Empty,
        trade.OpenTime.Ticks.ToString(),
        trade.CloseTime.Ticks.ToString(),
        trade.OpenPrice.ToString(),
        trade.ClosePrice.ToString(),
        trade.OpenVolume.ToString(),
        trade.CloseVolume.ToString(),
        trade.PnL.ToString(),
        trade.TicksPnL.ToString()
    });
    }

    private static DateTime TruncateToSeconds(DateTime dt)
    {
        var ticks = dt.Ticks - (dt.Ticks % TimeSpan.TicksPerSecond);
        return new DateTime(ticks, dt.Kind);
    }

    private void CreateTradePairNoDedupe(HistoryMyTrade trade)
    {
        var enterBar = GetBarByTime(trade.OpenTime);
        if (enterBar < 0)
        {
            return;
        }

        var exitBar = GetBarByTime(trade.CloseTime);
        if (exitBar < 0)
        {
            exitBar = enterBar;
        }

        var tradeObj = new TradeObj(trade)
        {
            OpenBar = enterBar,
            CloseBar = exitBar,
        };

        lock (_tradesSync)
        {
            _trades.Add(tradeObj);

            // Index by Id if available for in-place updates on Changed
            if (trade.Id != 0)
                _tradeById[trade.Id] = tradeObj;
        }
    }

    private void OnTradeChanged(HistoryMyTrade trade)
    {
        if (_isHistoryLoading)
            return;

        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!string.Equals(trade.AccountID, TradingManager.Portfolio.AccountID, StringComparison.InvariantCultureIgnoreCase))
            return;

        if (!trade.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase))
            return;

        if (!trade.IsComplete)
            return;

        bool updated = false;

        lock (_tradesSync)
        {
            if (trade.Id != 0 && _tradeById.TryGetValue(trade.Id, out var existing))
            {
                existing.ClosePrice = trade.ClosePrice;
                existing.CloseTime = trade.CloseTime;
                existing.PnL = trade.PnL;
                existing.PnLTicks = trade.TicksPnL;
                existing.CloseBar = Math.Max(GetBarByTime(trade.CloseTime), existing.OpenBar);
                updated = true;
            }
        }

        if (!updated)
            CreateTradePairNoDedupe(trade);

        _pendingRedraw = true;
    }


    private void OnTradeRemoved(HistoryMyTrade trade)
    {

        if (trade.Id == 0)
            return;

        lock (_tradesSync)
        {
            if (_tradeById.Remove(trade.Id, out var obj))
                _trades.Remove(obj);
        }

        _pendingRedraw = true;
    }

    private void OnTradesCleared()
    {

        lock (_tradesSync)
        {
            _trades.Clear();
            _tradeById.Clear();
            // Note: we intentionally do NOT clear _seenTradeKeys here because it is managed per context.
            // If we wanted a "cleared" event to allow a full rebuild from scratch, we could clear it,
            // but we are not doing that in this version to avoid duplicate churn.
        }

        _pendingRedraw = true;
    }

    private void TradingManager_PositionChanged(Position pos)
    {
        if (pos == null)
            return;

        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!string.Equals(pos.AccountID, TradingManager.Portfolio.AccountID, StringComparison.InvariantCultureIgnoreCase))
            return;

        if (!pos.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase))
            return;

        // We only care about closures (position flat). This is the earliest reliable signal.
        if (pos.Volume != 0)
            return;

        // Force next history request to NOT be skipped by the "same signature" guard.
        // We only invalidate the "to" component; "from" remains untouched.
        lock (_requestSync)
        {
            _lastReqTo = DateTime.MinValue;
        }

        // IMPORTANT: when position becomes flat, refresh provider range (To) so current candle is included
        _pendingHistoryReload = true;
        _pendingHistoryReason = "PositionFlat";
        _pendingRedraw = true;

        // Defer: HistoryMyTrades may not be updated yet at this instant.
        _pendingSyncClosedTrades = true;
        _pendingSyncReason = "PositionFlat";
        // Retry budget: enough to catch provider lag but bounded to avoid endless work.
        _pendingSyncRetriesLeft = 30;

        _pendingRedraw = true;
    }



    private void SyncClosedTradesFromHistorySnapshot(string reason)
    {
        if (_isHistoryLoading)
        {
            return;
        }

        // Prefer realtime history snapshot if available, else fallback to Statistics.
        var src = TradingStatisticsProvider?.Realtime?.HistoryMyTrades
                  ?? TradingStatisticsProvider?.Statistics?.HistoryMyTrades;

        if (src == null)
        {
            return;
        }

        int total = 0;
        int accepted = 0;
        int updated = 0;
        var snapshotKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        DateTime snapshotMaxClose = DateTime.MinValue;

        // Iterate snapshot and add only completed trades for current context.
        foreach (var t in src)
        {
            total++;

            if (!t.IsComplete)
                continue;

            if (!string.Equals(t.AccountID, TradingManager.Portfolio.AccountID, StringComparison.InvariantCultureIgnoreCase))
                continue;

            if (!t.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase))
                continue;

            var key = GetTradeKey(t);
            if (!snapshotKeys.Add(key))
                continue;

            if (t.CloseTime > snapshotMaxClose)
                snapshotMaxClose = t.CloseTime;

            bool wasUpdated = false;

            lock (_tradesSync)
            {
                // Update in-place if we already have it indexed.
                if (t.Id != 0 && _tradeById.TryGetValue(t.Id, out var existing))
                {
                    existing.ClosePrice = t.ClosePrice;
                    existing.CloseTime = t.CloseTime;
                    existing.PnL = t.PnL;
                    existing.PnLTicks = t.TicksPnL;
                    existing.CloseBar = Math.Max(GetBarByTime(t.CloseTime), existing.OpenBar);

                    wasUpdated = true;
                }
            }

            if (wasUpdated)
            {
                updated++;
                continue;
            }

            bool isNewKey;
            lock (_tradesSync)
                isNewKey = _seenTradeKeys.Add(key);

            if (!isNewKey)
                continue;

            CreateTradePairNoDedupe(t);
            accepted++;
        }

        if (accepted > 0 || updated > 0)
            RedrawChart();

        if (snapshotMaxClose > DateTime.MinValue)
            _lastSnapshotMaxCloseTime = snapshotMaxClose;
    }

    private DateTime[] EnsureCandleTimesCache()
    {
        var size = CurrentBar; // number of candles currently loaded
        if (size <= 0)
            return Array.Empty<DateTime>();

        // Rebuild only if the number of loaded candles changed
        if (_candleTimesCache.Length == size && _candleTimesCacheSize == size)
            return _candleTimesCache;

        var times = new DateTime[size];
        for (int i = 0; i < size; i++)
        {
            var c = GetCandle(i);
            times[i] = c?.Time ?? default;
        }

        _candleTimesCache = times;
        _candleTimesCacheSize = size;
        return _candleTimesCache;
    }

    private int GetLabelAnchorBar(TradeObj trade)
    {
        if (LabelXAnchor == LabelHorizontalAnchor.Midpoint && trade.CloseBar > trade.OpenBar)
            return (trade.OpenBar + trade.CloseBar) >> 1;

        return trade.CloseBar;
    }

    private (int FromBar, int ToBar, bool IsProvisional) GetLabelBandBars(TradeObj trade, int anchorBar)
    {
        // Use the full trade span.
        if (LabelYReference == LabelVerticalReference.OperationRange && trade.CloseBar >= trade.OpenBar)
            return (trade.OpenBar, trade.CloseBar, false);

        // Use a local window around the anchor bar.
        var w = Math.Max(0, LabelLocalWindow);

        var from = Math.Max(0, anchorBar - w);
        var lastBar = Math.Max(0, CurrentBar - 1);
        var to = Math.Min(lastBar, anchorBar + w);

        // Provisional if we couldn't include the intended "future" window.
        var provisional = (anchorBar + w) > to;

        return (from, to, provisional);
    }

    private bool TryGetMinMaxForBars(int fromBar, int toBar, out decimal minLow, out decimal maxHigh)
    {
        minLow = 0m;
        maxHigh = 0m;

        if (CurrentBar <= 0)
            return false;

        if (fromBar < 0 || toBar < 0)
            return false;

        if (toBar < fromBar)
            (fromBar, toBar) = (toBar, fromBar);

        bool found = false;

        for (int i = fromBar; i <= toBar; i++)
        {
            var c = GetCandle(i);
            if (c is null)
                continue;

            if (!found)
            {
                minLow = c.Low;
                maxHigh = c.High;
                found = true;
                continue;
            }

            if (c.Low < minLow)
                minLow = c.Low;

            if (c.High > maxHigh)
                maxHigh = c.High;
        }

        return found;
    }


    #endregion
}
