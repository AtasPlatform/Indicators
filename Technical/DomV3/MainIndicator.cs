#nullable enable annotations
namespace DomV10;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Timers;

using ATAS.DataFeedsCore;
using ATAS.Indicators;

using OFT.Attributes;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

using Utils.Common.Logging;

using MarketDataType = ATAS.DataFeedsCore.MarketDataType;
using Timer = System.Timers.Timer;

[DisplayName("MBO DOM")]
[HelpLink("https://help.atas.net/support/solutions/articles/72000633231")]
[Category(IndicatorCategories.OrderBook)]
public partial class MainIndicator : Indicator
{
    #region Nested types

    private sealed class VisualRowData
    {
        public int Y1;
        public int Y2;
        public readonly List<OrderInfo> Orders = new();
        public MarketDataType Type = MarketDataType.Trade;
        public decimal RowVol;
        public DataType DataType = DataType.Lvl3;

        public void Reset(int y1, int y2, MarketDataType type, decimal rowVol, DataType dataType)
        {
            Y1 = y1;
            Y2 = y2;
            Type = type;
            RowVol = rowVol;
            DataType = dataType;
            Orders.Clear();
        }
    }

    #endregion

    private MboGridController _gridController = new();
    private readonly object _renderLock = new();
    private readonly Dictionary<decimal, int> _tempSizeBuffer = new();
    private readonly List<VisualRowData> _visualRows = new();
    private readonly RenderFont _tooltipFont = new("Arial", 8);

	private Timer _timer = new();
	private MarketDataArg? _lastAsk = null;
    private MarketDataArg? _lastBid = null;
    private decimal _lastPrice = 0;

    public MainIndicator()
	    : base(true)
    {
	    ((ValueDataSeries)DataSeries[0]).IsHidden = true;
	    EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
        DenyToChangePanel = true;
    }

    protected override void OnDispose()
    {
        base.OnDispose();
        try
        {
            _timer.Enabled = false;
            _timer.Stop();
            _timer.Dispose();
        }
        catch
        {
            //ignored
        }
    }

    private void TickTok(object? sender, ElapsedEventArgs e)
    {
        _timer?.Stop();
        _gridController?.Tick();
        _timer?.Start();
    }

    protected override async void OnInitialize()
    {
        // Recreate the controller BEFORE subscribing: events that arrive during/after
        // the subscription must land in this fresh controller, not in one that is
        // about to be replaced (which would silently drop the initial snapshot).
        _gridController = new();
        await SubscribeMarketByOrderData();
        _timer = new Timer();
        _timer.Elapsed += TickTok;
        _timer.Interval = 1000;
        _timer.Enabled = true;
        _timer.Start();

        OrderSizeFilter.PropertyChanged += UpdateUi;
        MinBlockSize.PropertyChanged += UpdateUi;
        RowOrderVolume.PropertyChanged += UpdateUi;
        RowOrderCount.PropertyChanged += UpdateUi;
    }

    public override void Dispose()
    {
        base.Dispose();
        OrderSizeFilter.PropertyChanged -= UpdateUi;
        MinBlockSize.PropertyChanged -= UpdateUi;
        RowOrderVolume.PropertyChanged -= UpdateUi;
        RowOrderCount.PropertyChanged -= UpdateUi;
    }

    private void UpdateUi(object? sender, PropertyChangedEventArgs e) => RedrawChart(_emptyRedrawArg);

    protected override void OnApplyDefaultColors()
    {
        if (ChartInfo == null)
	        return;

        _bidColor = ChartInfo.ColorsStore.UpCandleColor;
        _askColor = ChartInfo.ColorsStore.DownCandleColor;
        _textColor = ChartInfo.ColorsStore.FootprintMaximumVolumeTextColor;

        RedrawChart();
    }

    protected override void OnMarketByOrdersChanged(IEnumerable<MarketByOrder> orders)
    {
        if (!_gridController.Update(orders)) _gridController.Load(MarketByOrders);
    }

    protected override void MarketDepthChanged(MarketDataArg depth)
    {
        if (MarketDepthInfo == null) return;
        if (!_gridController.Update(depth)) _gridController.Load(MarketDepthInfo.GetMarketDepthSnapshot());
    }

    protected override void OnNewTrade(MarketDataArg trade)
    {
        _gridController.UpdateTrade(trade);
    }

    protected override void OnBestBidAskChanged(MarketDataArg depth)
    {
        base.OnBestBidAskChanged(depth);
        if (depth.IsAsk) _lastAsk = depth;
        if (depth.IsBid) _lastBid = depth;
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        _lastPrice = GetCandle(bar).Close;
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
	    try
	    {
		    lock (_renderLock)
		    {
			    if (ChartInfo == null)
				    return;

			    if (Container == null)
				    return;

			    if (InstrumentInfo == null)
				    return;

			    if (_lastAsk is null || _lastBid is null)
				    return;

			    var step = ChartInfo.PriceChartContainer.Step;
			    var fixHigh = GetFixPrice(ChartInfo.PriceChartContainer.High, true, step);
			    var fixLow = GetFixPrice(ChartInfo.PriceChartContainer.Low, false, step);

			    if (fixLow >= fixHigh)
				    return;

			    // Optimization: limit range to prices with data only
			    var (dataMinPrice, dataMaxPrice, hasData) = _gridController.GetPriceRange();

			    if (!hasData)
				    return;

			    // Intersect visible range with data range
			    var effectiveHigh = Math.Min(fixHigh, dataMaxPrice + step);
			    var effectiveLow = Math.Max(fixLow, dataMinPrice - step);

			    if (effectiveLow >= effectiveHigh)
				    return;

			    var height = ChartInfo.PriceChartContainer.PriceRowHeight;

                var (fontSize, fontWidth) =
				    SetFontSize(context, height);

			    var showSeparately = height >= 4.5m;

				var showText = _lastHeight < height;

                var maxScreenSize = Container.RelativeRegion.Width * 0.5m;
			    var (maxVol, maxCount) = _gridController.MaxInView(effectiveHigh, effectiveLow, step, true);

			    var aggregationBaseRow = new Rectangle() { X = Container.RelativeRegion.Right - 1, Width = 0, };

			    if (showText && (ShowSum || ShowCount))
			    {
				    var maxWidth = (int)(((ShowSum ? 1 : 0) + (ShowCount ? 1 : 0)) * 6 * fontWidth);

				    if (maxWidth > 0)
				    {
					    aggregationBaseRow.X -= maxWidth;
					    aggregationBaseRow.Width = maxWidth;
				    }
			    }

			    _tempSizeBuffer.Clear();

			    Rectangle selectedRectangle = new();
			    var tooltip = "";

			    var pricesWithData = _gridController.GetPricesInRange(effectiveHigh, effectiveLow, _lastAsk, _lastBid);

			    // Phase 1: Group prices by visual row (handles Scale > 1 where multiple tick prices share one row)
			    _visualRows.Clear();
			    var lastPriceRowIndex = long.MinValue;

			    var tickSize = InstrumentInfo.TickSize;

			    foreach (var price in pricesWithData)
			    {
				    if (price < fixLow || price > fixHigh)
					    continue;

				    var blockInRow = _gridController.GetItemInRow(price, _lastAsk, _lastBid, !showSeparately && !showText);
				    var (rowVol, dataType) = _gridController.Volume(price, _lastAsk, _lastBid, _lastPrice);

				    if (blockInRow.Orders.Length == 0 && rowVol == 0)
					    continue;

				    if (rowVol > 0 && blockInRow.Orders.Length == 0)
					    rowVol = 0;

				    // Group by price grid position (deterministic, independent of pixel rounding)
				    var priceRowIndex = step > 0 ? (long)Math.Floor(price / step) : 0;

				    if (_visualRows.Count > 0 && priceRowIndex == lastPriceRowIndex)
				    {
					    // Same visual row - merge orders from adjacent tick prices
					    var lastVisualRow = _visualRows[^1];
					    lastVisualRow.Orders.AddRange(blockInRow.Orders);
					    lastVisualRow.RowVol += rowVol;

					    if (lastVisualRow.Type == MarketDataType.Trade)
						    lastVisualRow.Type = blockInRow.Type;
				    }
				    else
				    {
					    lastPriceRowIndex = priceRowIndex;

					    // Snap to step-aligned boundaries to prevent overlap between groups
					    var topTickPrice = (priceRowIndex + 1) * step - tickSize;
					    var y1 = ChartInfo.GetYByPrice(topTickPrice);
					    var y2 = ChartInfo.GetYByPrice(topTickPrice - step);

					    if (showSeparately)
						    y1 += 1;

					    var row = new VisualRowData();
					    row.Reset(y1, y2, blockInRow.Type, rowVol, dataType);
					    row.Orders.AddRange(blockInRow.Orders);
					    _visualRows.Add(row);
				    }
			    }

			    // Phase 2: Render visual rows
			    foreach (var visualRow in _visualRows)
			    {
				    var y1 = visualRow.Y1;
				    var realHeight = visualRow.Y2 - y1;

				    if (visualRow.DataType == DataType.Lvl2)
				    {
					    aggregationBaseRow.X += aggregationBaseRow.Width;
					    aggregationBaseRow.Width = 0;
				    }

				    var pen = visualRow.Type switch
				    {
					    MarketDataType.Ask => _askColorPen,
					    MarketDataType.Bid => _bidColorPen,
					    MarketDataType.Trade => _textColorPen,
					    _ => RenderPens.Transparent
				    };

				    var aggregationRow = aggregationBaseRow with { Y = y1, Height = realHeight };

				    if (aggregationRow.Height < 1)
					    aggregationRow.Height = 1;

				    if (aggregationRow.Width > 0)
				    {
					    context.DrawRectangle(pen, aggregationRow);

					    var pw = 0;

					    if (ShowSum)
					    {
						    var text = $"V {ChartInfo.TryGetMinimizedVolumeString(visualRow.RowVol)}";

						    var aggVolBox = aggregationRow with
						    {
							    Width = ShowCount ? (aggregationRow.Width / 2) : aggregationRow.Width
						    };
						    pw = aggVolBox.Width;

						    if (RowOrderVolume.Enabled && visualRow.Type != MarketDataType.Trade)
						    {
							    if (RowOrderVolume.Value <= visualRow.RowVol)
								    context.FillRectangle(pen.Color, aggVolBox);
						    }

						    context.DrawString(text, _font, TextColor, aggVolBox, _stringCenterFormat);
					    }

					    if (ShowCount)
					    {
						    var text = $"C {visualRow.Orders.Count}";

						    var aggCountBox = aggregationRow with
						    {
							    X = aggregationRow.X + pw, Width = aggregationRow.Width - pw
						    };

						    if (RowOrderCount.Enabled && visualRow.Type != MarketDataType.Trade)
						    {
							    if (RowOrderCount.Value <= visualRow.Orders.Count)
								    context.FillRectangle(pen.Color, aggCountBox);
						    }

						    context.DrawString(text, _font, TextColor, aggCountBox, _stringCenterFormat);
					    }
				    }

				    if (visualRow.Type is not MarketDataType.Trade && visualRow.Orders.Count > 0)
				    {
					    var minW = (int)Math.Max(height, 6);
					    var lastX = aggregationRow.X - 2;

					    foreach (var order in visualRow.Orders)
					    {
						    var vol = order.Order.Volume;

						    var needToFilterBlockSize = MinBlockSize.Enabled && MinBlockSize.Value > vol &&
							    visualRow.Type != MarketDataType.Trade;

						    var needToFillBox = OrderSizeFilter.Enabled && OrderSizeFilter.Value <= vol &&
							    visualRow.Type != MarketDataType.Trade;

						    if (needToFilterBlockSize)
							    continue;

						    // Cache width by volume to avoid recalculation for orders with same volume
						    if (!_tempSizeBuffer.TryGetValue(vol, out var ww))
						    {
							    ww = ItemWidthCalculation(vol, maxVol, (int)maxScreenSize / 2, maxCount, 0, minW);
							    _tempSizeBuffer[vol] = ww;
						    }

						    var orderBlockRow = aggregationRow with
						    {
							    X = lastX - ww, Width = ww, Height = realHeight
						    };

						    if (showSeparately)
						    {
							    if (!needToFilterBlockSize)
							    {
								    var textColor = TextColor;

								    if (IsPointInRectangle(orderBlockRow, MouseLocationInfo.LastPosition))
								    {
									    selectedRectangle = orderBlockRow;
									    context.FillRectangle(ChartInfo.ColorsStore.MouseBackground, orderBlockRow);
									    textColor = ChartInfo.ColorsStore.MouseTextColor;

									    if (ChartInfo.KeyboardInfo.PressedKey != null && ChartInfo.KeyboardInfo.PressedKey.Key == CrossKey.LeftCtrl)
									    {
										    tooltip = $"Price\t\t{ChartInfo.GetPriceString(order.Order.Price)}{Environment.NewLine}" +
										              $"Volume\t{order.Order.Volume}{Environment.NewLine}" +
										              $"Time\t\t{order.Order.Time:HH:mm:ss.fff}{Environment.NewLine}" +
										              $"Id\t\t{order.Order.ExchangeOrderId}{Environment.NewLine}" +
										              $"Priority\t{order.Order.Priority}{Environment.NewLine}";
									    }
								    }
								    else if (needToFillBox)
									    context.FillRectangle(pen.Color, orderBlockRow);
								    else
									    context.DrawRectangle(pen, orderBlockRow);

								    if (showText)
								    {
									    context.DrawString(ChartInfo.TryGetMinimizedVolumeString(vol), _font,
										    textColor, orderBlockRow,
										    visualRow.DataType is DataType.Lvl3 ? _stringCenterFormat : _stringRightFormat);
								    }
							    }
						    }

						    lastX = orderBlockRow.X - 1;
					    }

					    if (!showSeparately)
					    {
						    var end = aggregationRow.X - 1;

						    var orderBlockRow = aggregationRow with
						    {
							    X = lastX, Width = end - lastX
						    };

						    context.FillRectangle(pen.Color, orderBlockRow);
					    }
				    }
			    }

                if (!string.IsNullOrWhiteSpace(tooltip))
                {
                    var size = context.MeasureString(tooltip, _tooltipFont);

                    size = new Size(size.Width + 20, size.Height);
	                var rectangle = new Rectangle(new Point(selectedRectangle.X, selectedRectangle.Bottom + 1), size);

                    if (rectangle.Right > Container.RelativeRegion.Right)
	                {
		                rectangle.X += (Container.RelativeRegion.Right - rectangle.Right);
	                }
	                if (rectangle.Bottom > Container.RelativeRegion.Bottom)
	                {
		                rectangle.Y = selectedRectangle.Y - rectangle.Height - 1;
	                }

                    context.FillRectangle(ChartInfo.ColorsStore.MouseBackground, rectangle, 10);
	                rectangle.X += 10;
	                rectangle.Y += 9;
                    context.DrawString(tooltip, _tooltipFont, ChartInfo.ColorsStore.MouseTextColor, rectangle, _stringLeftFormat);
                }
		    }
	    }
	    catch (Exception es)
	    {
		    this.LogWarn(es.ToString());
	    }
    }

    private bool IsPointInRectangle(Rectangle rectangle, Point e)
    {
	    if (rectangle.Width == 0 || rectangle.Height == 0)
		    return false;
	    if (e.X >= rectangle.X && e.Y >= rectangle.Y && e.X <= rectangle.X + rectangle.Width && e.Y <= rectangle.Y + rectangle.Height)
		    return true;
	    return false;
    }
}
