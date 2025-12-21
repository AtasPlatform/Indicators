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
using Utils.Common.Logging;
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
    private int _recalcCount;
    private int _historyRequestCount;
    private int _statsAddedCount;
    // Request signature caching.
    private readonly object _requestSync = new();
    private string _lastReqAcc;
    private string _lastReqSec;
    private DateTime _lastReqFrom;
    private DateTime _lastReqTo;
    // Context signature for "current" chart stats.
    private string _ctxAcc;
    private string _ctxSec;


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

    [Display(Name = "Debug logs", GroupName = "Debug", Description = "Enable verbose diagnostic logs for TradesOnChart.")]
    public bool DebugLogs { get; set; } = false;

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
        Dbg("OnInitialize: debug logging enabled.");
        TradingStatisticsProvider.Statistics.HistoryMyTrades.Added += OnTradeAdded;
        TradingManager.PortfolioSelected += TradingManager_PortfolioSelected;

        OnRecalculate();
    }

    protected override void OnDispose()
    {
        TradingStatisticsProvider.Statistics.HistoryMyTrades.Added -= OnTradeAdded;
        TradingManager.PortfolioSelected -= TradingManager_PortfolioSelected;

        base.OnDispose();
    }

    private void TradingManager_PortfolioSelected(Portfolio obj)
    {
        Dbg($"PortfolioSelected: {obj?.AccountID ?? "null"}");
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
        _recalcCount++;
        var acc = TradingManager?.Portfolio?.AccountID;
        var sec = TradingManager?.Security?.SecurityId;

        Dbg($"OnRecalculate #{_recalcCount}. CurrentBar={CurrentBar}, Acc={acc ?? "null"}, SecId={sec ?? "null"}, Code={TradingManager?.Security?.Code ?? "null"}");

        _buyPen = GetNewPen(_buyColor, _lineWidth, _lineStyle);
        _sellPen = GetNewPen(_sellColor, _lineWidth, _lineStyle);

        // If we are already loading, do not start another load.
        if (_isHistoryLoading)
        {
            Dbg("OnRecalculate ignored: history loading in progress.");
            return;
        }

        // Only reset caches when context changed (account/security).
        var ctxChanged =
            !string.Equals(_ctxAcc, acc, StringComparison.InvariantCultureIgnoreCase) ||
            !string.Equals(_ctxSec, sec, StringComparison.InvariantCultureIgnoreCase);

        if (ctxChanged)
        {
            Dbg($"Context changed -> reset caches. oldAcc={_ctxAcc ?? "null"}, newAcc={acc ?? "null"}, oldSec={_ctxSec ?? "null"}, newSec={sec ?? "null"}");

            _ctxAcc = acc;
            _ctxSec = sec;

            lock (_tradesSync)
            {
                _trades.Clear();
                _seenTradeKeys.Clear();
            }
        }

        RequestHistoryForChartRange();
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
    private void OnTradeAdded(HistoryMyTrade trade)
    {
        _statsAddedCount++;
        Dbg($"Statistics.Added #{_statsAddedCount}: id={trade.Id}, acc={trade.AccountID}, secId={trade.Security?.SecurityId}, code={trade.Security?.Code}, open={trade.OpenTime:O}, close={trade.CloseTime:O}, pnl={trade.PnL}, vol={trade.OpenVolume}");

        if (_isHistoryLoading)
        {
            Dbg("Statistics.Added ignored: history loading.");
            return;
        }

        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!string.Equals(trade.AccountID, TradingManager.Portfolio.AccountID, StringComparison.InvariantCultureIgnoreCase))
        {
            Dbg("Statistics.Added ignored: account mismatch.");
            return;
        }

        if (!trade.Security.SecurityId.Equals(TradingManager.Security.SecurityId, StringComparison.InvariantCultureIgnoreCase))
        {
            Dbg("Statistics.Added ignored: security mismatch.");
            return;
        }

        var key = GetTradeKey(trade);

        lock (_tradesSync)
        {
            if (!_seenTradeKeys.Add(key))
            {
                Dbg($"Statistics.Added duplicate ignored: key={key}, id={trade.Id}");
                return;
            }
        }

        CreateTradePairNoDedupe(trade);

        Dbg("Statistics.Added accepted: trade created, RedrawChart requested.");
        RedrawChart();
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

        Dbg($"ChartRange: CurrentBar={CurrentBar}, from={from:O}, to={to:O}");
        return true;
    }

    private async void RequestHistoryForChartRange()
    {
        _historyRequestCount++;
        Dbg($"RequestHistoryForChartRange #{_historyRequestCount} START. CurrentBar={CurrentBar}");

        if (TradingManager?.Portfolio == null || TradingManager?.Security == null)
            return;

        if (!TryGetChartTimeRange(out var from, out var to))
            return;

        var acc = TradingManager.Portfolio.AccountID;
        var sec = TradingManager.Security.SecurityId;

        // Normalize to seconds to avoid micro-deltas spamming requests.
        from = TruncateToSeconds(from);
        to = TruncateToSeconds(to);

        Dbg($"RequestHistoryForChartRange #{_historyRequestCount} Range: from={from:O}, to={to:O}");

        // 1) Signature guard FIRST (no token/flags touched).
        lock (_requestSync)
        {
            if (string.Equals(_lastReqAcc, acc, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(_lastReqSec, sec, StringComparison.InvariantCultureIgnoreCase) &&
                _lastReqFrom == from &&
                _lastReqTo == to)
            {
                Dbg($"RequestHistoryForChartRange skipped: same signature acc={acc}, secId={sec}, from={from:O}, to={to:O}");
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

            Dbg($"LoadHistoryAsync BEGIN token={token}, acc={acc}, secId={sec}");

#pragma warning disable CS0618
            var stats = await TradingStatisticsProvider.LoadHistoryAsync(from, to, new[] { acc }, new[] { sec });
#pragma warning restore CS0618

            Dbg($"LoadHistoryAsync END token={token}, currentToken={_historyLoadToken}, statsNull={(stats is null)}, globalHistoryCount={TradingStatisticsProvider.Statistics.HistoryMyTrades.Count()}");

            if (token != _historyLoadToken)
            {
                Dbg("LoadHistoryAsync ignored: outdated token.");
                return;
            }

            // NOTE: do not clear _seenTradeKeys here unless you want rebuild to re-add everything.
            // In Phase 1, we rebuild trades list but keep dedupe to avoid re-adding duplicates from re-emissions.
            lock (_tradesSync)
            {
                _trades.Clear();
                // _seenTradeKeys is kept to prevent duplicates across reloads.
            }

            Dbg("Rebuild BEGIN from TradingStatisticsProvider.Statistics.HistoryMyTrades");

            int total = 0, matchedRaw = 0, matchedUnique = 0, duplicates = 0, created = 0;
            var snapshotKeys = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var t in TradingStatisticsProvider.Statistics.HistoryMyTrades)
            {
                total++;

                if (!string.Equals(t.AccountID, acc, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                if (!t.Security.SecurityId.Equals(sec, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                matchedRaw++;

                var key = GetTradeKey(t);

                // Snapshot duplicates (provider returned same trade multiple times)
                if (!snapshotKeys.Add(key))
                {
                    duplicates++;
                    continue;
                }

                matchedUnique++;

                // Cross-reload duplicates (ATAS re-emits; we already saw it earlier)
                bool isNewKey;
                lock (_tradesSync)
                    isNewKey = _seenTradeKeys.Add(key);

                if (!isNewKey)
                {
                    // We still want it on chart after rebuild, so we must recreate the visual trade object
                    // BUT without increasing dedupe counts. We'll create it anyway.
                    // If you prefer, you can allow recreation always during rebuild.
                }

                var before = _trades.Count;

                CreateTradePairNoDedupe(t); // see next section

                if (_trades.Count > before)
                    created++;
            }

            Dbg($"Rebuild END total={total}, matchedRaw={matchedRaw}, matchedUnique={matchedUnique}, duplicates={duplicates}, created={created}");

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

    private void Dbg(string message)
    {
        if (!DebugLogs)
            return;

        // Official ATAS logging mechanism (shown in ATAS log window)
        this.LogInfo($"[TradesOnChart] {message}");
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
            Dbg($"CreateTradePairNoDedupe aborted: enterBar<0 id={trade.Id}, open={trade.OpenTime:O}, CurrentBar={CurrentBar}");
            return;
        }

        var exitBar = GetBarByTime(trade.CloseTime);
        if (exitBar < 0)
        {
            Dbg($"CreateTradePairNoDedupe: exitBar<0 -> using enterBar. id={trade.Id}, close={trade.CloseTime:O}");
            exitBar = enterBar;
        }

        var tradeObj = new TradeObj(trade)
        {
            OpenBar = enterBar,
            CloseBar = exitBar,
        };

        lock (_tradesSync)
            _trades.Add(tradeObj);
    }




    #endregion
}
