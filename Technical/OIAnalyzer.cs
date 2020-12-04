using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Windows.Media;

    using ATAS.Indicators.Technical.Properties;

    using OFT.Rendering.Context;
    using OFT.Rendering.Tools;

    using Color = System.Windows.Media.Color;

    [Category("Order Flow")]
    [DisplayName("OI analyzer")]
    public class OIAnalyzer : Indicator
    {

        public enum CalcMode
        {
            [Display(ResourceType = typeof(Resources), Name = "CumulativeTrades")]
            CumulativeTrades,

            [Display(ResourceType = typeof(Resources), Name = "SeparatedTrades")]
            SeparatedTrades
        }

        public enum Mode
        {
            [Display(ResourceType = typeof(Resources), Name = "Buys")]
            Buys,

            [Display(ResourceType = typeof(Resources), Name = "Sells")]
            Sells
        }

        private const int _height = 15;
        private readonly RenderStringFormat _stringAxisFormat = new RenderStringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };
        private ValueDataSeries _test = new ValueDataSeries("Test");

        private CandleDataSeries _renderValues = new CandleDataSeries("Values");
        private RangeDataSeries _maxMin = new RangeDataSeries("MaxMin");
        private readonly RenderFont _font = new RenderFont("Arial", 9);
        private int _customDiapason;
        private int _gridStep;
        private Mode _mode;
        private CalcMode _calcMode;
        private bool _bigTradesIsReceived;
        private int _sessionBegin;
        private bool _requestWaiting;
        private bool _requestFailed;
        private int _lastBar;
        private bool _cumulativeMode;
        private readonly RenderPen _linePen = new RenderPen(System.Drawing.Color.Gray);
        private System.Drawing.Color _candlesColor;
        private int _lastCalculatedBar;
        private CumulativeTrade _prevTrade;
        private decimal _prevLastOi;
        private decimal _lastOi;
        private Candle _prevCandle;

        [Display(ResourceType = typeof(Resources), Name = "CustomDiapason", Order = 100)]
        public int CustomDiapason
        {
            get => _customDiapason;
            set
            {
                if (value < 0)
                    return;

                _customDiapason = value;
                RecalculateValues();
            }

        }

        [Display(ResourceType = typeof(Resources), Name = "GridStep", Order = 110)]
        public int GridStep
        {
            get => _gridStep;
            set
            {
                if (value <= 0)
                    return;

                _gridStep = value;
                RecalculateValues();
            }

        }

        [Display(ResourceType = typeof(Resources), Name = "Color", Order = 120)]
        public Color CandlesColor { get => _candlesColor.Convert(); set => _candlesColor = value.Convert(); }

        [Display(ResourceType = typeof(Resources), Name = "Mode", Order = 130)]
        public Mode OiMode
        {
            get => _mode;
            set
            {
                _mode = value;
                RecalculateValues();
            }


        }



        [Display(ResourceType = typeof(Resources), Name = "CalculationMode", Order = 140)]
        public CalcMode CalculationMode
        {
            get => _calcMode;
            set
            {
                _calcMode = value;
                RecalculateValues();
            }


        }

        [Display(ResourceType = typeof(Resources), Name = "ClustersMode", Order = 150)]
        public bool ClustersMode { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "CumulativeMode", Order = 160)]
        public bool CumulativeMode
        {
            get => _cumulativeMode;
            set
            {
                _cumulativeMode = value;
                RecalculateValues();
            }
        }


        [Display(ResourceType = typeof(Resources), Name = "Author", GroupName = "Copyright", Order = 200)]
        public string Author { get; }


        public OIAnalyzer()
            : base(true)
        {
            //EnableCustomDrawing = true;
            //SubscribeToDrawingEvents(DrawingLayouts.Final | DrawingLayouts.LatestBar);
            Panel = IndicatorDataProvider.NewPanel;

            Author = "Sotnikov Denis (sotnik)";

            _renderValues.BorderColor = CandlesColor;
            _renderValues.UpCandleColor = CandlesColor;
            _renderValues.DownCandleColor = Colors.Transparent;
            _gridStep = 1000;
            CandlesColor = Colors.Green;
            _renderValues.BorderColor = _renderValues.DownCandleColor = CandlesColor;
            _renderValues.UpCandleColor = Colors.White;
            _renderValues.ScaleIt = true;

            _mode = Mode.Buys;
            _calcMode = CalcMode.CumulativeTrades;
            CumulativeMode = true;

            DataSeries[0] = _renderValues;
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                _renderValues.Clear();

                _bigTradesIsReceived = false;

                var totalBars = ChartInfo.PriceChartContainer.TotalBars;
                _sessionBegin = totalBars;

                for (var i = totalBars; i >= 0; i--)
                {
                    if (!IsNewSession(i))
                        continue;

                    _sessionBegin = i;
                    break;
                }

                if (!_requestWaiting)
                {
                    _requestWaiting = true;
                    RequestForCumulativeTrades(new CumulativeTradesRequest(GetCandle(_sessionBegin).Time));
                }
                else
                    _requestFailed = true;
            }
		}

        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            _requestWaiting = false;

            if (!_requestFailed)
            {
                var trades = cumulativeTrades
                    .OrderBy(x => x.Time)
                    .ToList();

                CalculateHistory(trades);

                _bigTradesIsReceived = true;
            }
            else
            {
                _requestFailed = false;
                Calculate(0, 0);
                RedrawChart();
            }
        }

        protected override void OnCumulativeTrade(CumulativeTrade trade)
        {
            if (!_bigTradesIsReceived)
                return;

            CalculateTrade(trade, ChartInfo.PriceChartContainer.TotalBars);
        }

        protected override void OnUpdateCumulativeTrade(CumulativeTrade trade)
        {
            if (!_bigTradesIsReceived)
                return;

            CalculateTrade(trade,ChartInfo.PriceChartContainer.TotalBars,true);
        }

        private void CalculateHistory(List<CumulativeTrade> trades)
        {
            foreach (var trade in trades)
            {
                for (var i = _sessionBegin; i <= ChartInfo.PriceChartContainer.TotalBars; i++)
                {
                    var candle = GetCandle(i);

                    if (candle.Time > trade.Time || candle.LastTime < trade.Time)
                        continue;

                    CalculateTrade(trade, i);
                    break;
                }
            }
            RedrawChart();
        }

        private void CalculateTrade(CumulativeTrade trade, int bar, bool isUpdated = false)
        {
            if (_lastCalculatedBar != bar)
            {
                _lastBar = _lastCalculatedBar;
                _lastCalculatedBar = bar;
            }

            if (isUpdated)
            {
                if (trade.IsEqual(_prevTrade))
                {
                    _lastOi = _prevLastOi;
                }
            }
            else
            {
                _prevLastOi = _lastOi;
                _prevTrade = trade;
            }

            var open = 0m;

            if (_cumulativeMode && _lastBar > 0)
            {
                var prevValue = _renderValues[_lastBar];

                if (prevValue.Close != 0)
                {
                    open = prevValue.Close;
                }
            }


            var currentValue = _renderValues[bar];

            if (IsEmpty(currentValue))
            {
                _renderValues[bar] = new Candle()
                {
                    High = open,
                    Low = open,
                    Open = open,
                    Close = open
                };
            }
            else
            {
	            if (currentValue.Open == currentValue.Close && currentValue.Open == 0)
		            _renderValues[bar] = new Candle()
		            {
			            High = open,
			            Low = open,
			            Open = open,
			            Close = open
		            };
            }

            if (isUpdated && trade.IsEqual(_prevTrade))
            {
                _renderValues[bar] = _prevCandle;
            }
            else
                _prevCandle = _renderValues[bar];


            if (_calcMode == CalcMode.CumulativeTrades)
            {
	            if (_lastOi != 0)
	            {
		            var dOi = trade.Ticks.Last().OpenInterest - _lastOi;

		            if (dOi != 0)
		            {
			            if (_mode == Mode.Buys && trade.Direction == TradeDirection.Buy
				            ||
				            _mode == Mode.Sells && trade.Direction == TradeDirection.Sell)
			            {
				            var value = dOi > 0 ? trade.Volume : -trade.Volume;
				            _renderValues[bar].Close += value;

				            if (_renderValues[bar].Close > _renderValues[bar].High)
					            _renderValues[bar].High = _renderValues[bar].Close;
				            if (_renderValues[bar].Close < _renderValues[bar].Low)
					            _renderValues[bar].Low = _renderValues[bar].Close;
                        }
		            }
	            }

	            _lastOi = trade.Ticks.Last().OpenInterest;
            }
            else
            {
	            foreach (var tick in trade.Ticks)
	            {
		            if (_lastOi != 0)
		            {
			            var dOi = tick.OpenInterest - _lastOi;

			            if (dOi != 0)
			            {
				            if (_mode == Mode.Buys && tick.Direction == TradeDirection.Buy
					            ||
					            _mode == Mode.Sells && tick.Direction == TradeDirection.Sell)
				            {
					            var value = dOi > 0 ? tick.Volume : -tick.Volume;
					            _renderValues[bar].Close += value;

					            if (_renderValues[bar].Close > _renderValues[bar].High)
						            _renderValues[bar].High = _renderValues[bar].Close;
					            if (_renderValues[bar].Close < _renderValues[bar].Low)
						            _renderValues[bar].Low = _renderValues[bar].Close;
				            }
			            }
		            }

		            _lastOi = tick.OpenInterest;
                }
            }
            MaxMin(bar);
        }

        private bool IsEmpty(Candle candle)
        {
            return candle.High == 0 && candle.Low == 0 && candle.Open == 0 && candle.Close == 0;
        }
        /*
        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {


            if (ClustersMode && ChartInfo.ChartVisualMode != ChartVisualModes.Clusters)
                return;

            DrawGrid(context);

            var barWidth = ChartInfo.GetXByBar(1) - ChartInfo.GetXByBar(0);

            var firstBar = Math.Max(ChartInfo.PriceChartContainer.FirstVisibleBarNumber, _sessionBegin);
            var lastBar = ChartInfo.PriceChartContainer.LastVisibleBarNumber;

            var y = Container.Region.Y + 3;
            var strHeight = _height - 1;
            var maxX = 0;


            var pen = new RenderPen(_candlesColor);
            var mid = barWidth / 2;

            for (var i = firstBar; i <= lastBar; i++)
            {
                var x = ChartInfo.GetXByBar(i);
                var width = barWidth;

                if (width > 2)
                {
                    width--;
                    x++;
                }

                if (ClustersMode)
                {
                    maxX = Math.Max(x, maxX);
                    var y1 = y;
                    var rect = new Rectangle(x, y1, width, strHeight);
                    var diff = _renderValues[i].Close - _renderValues[i].Open;
                    context.DrawString(diff.ToString("+#;-#;0"), _font, _candlesColor, rect, _stringAxisFormat);
                    context.DrawLine(_linePen, x + width, y, x + width, y + 15);
                }
                else
                {
                    var high = ChartInfo.GetYByPrice(_renderValues[i].High);
                    var low = ChartInfo.GetYByPrice(_renderValues[i].Low);
                    var open = ChartInfo.GetYByPrice(_renderValues[i].Open);
                    var close = ChartInfo.GetYByPrice(_renderValues[i].Close);

                    var max = Math.Max(open, close);
                    var min = Math.Min(open, close);

                    var fillBody = _renderValues[i].Close <= _renderValues[i].Open;

                    if (fillBody)
                        context.DrawLine(pen, x + mid, high, x + mid, low);
                    else
                    {
                        context.DrawLine(pen, x + mid, Math.Max(high, low), x + mid, max);
                        context.DrawLine(pen, x + mid, Math.Min(high, low), x + mid, min);
                    }

                    var height = Math.Abs(max - min);

                    if (height == 0)
                        context.DrawLine(pen, x, max, x + width, max);
                    else
                    {
                        var rect = new Rectangle(x, min, width, height);

                        if (fillBody)
                            context.FillRectangle(_candlesColor, rect);
                        else
                            context.DrawRectangle(pen, rect);
                    }

                }

            }
        }
        */
        private void DrawGrid(RenderContext context)
        {
	       // var max = ChartInfo.GetYByPrice();
	            //Container.Region.Height - Container.Region.Height % _gridStep;
                /*
            while (max > Container.Region.Top)
            {
                context.DrawLine(_linePen, 0, max, Container.Region.Width, max);
                max -= _gridStep;
            }
                */
        }

        private void MaxMin(int bar)
        {
            if (_customDiapason != 0)
                return;

            var close = 0m;
            var lastCandle = _renderValues[bar];

            if (lastCandle != default)
                close = lastCandle.Close;


            var max = 0m;
            var min = 0m;

            for (var i = bar; i >= _sessionBegin; i--)
            {
                var valueCandle = _renderValues[i];

                if (valueCandle == default)
                    continue;

                if (valueCandle.High == 0 && valueCandle.Low == 0)
                    continue;

                if (valueCandle.Low < min || min == 0)
                    min = valueCandle.Low;

                if (valueCandle.High < max || max == 0)
                    max = valueCandle.High;

                if (Math.Abs(max - min) > _customDiapason)
                    break;

            }

            if (max == 0 || min == 0)
                return;


            if (max - close > close - min)
                max = min + _customDiapason;
            else
                min = max - _customDiapason;

            for (var i = _sessionBegin; i < bar; i++)
            {
                _maxMin[i] = new RangeValue() { Upper = max, Lower = min };
            }




        }
    }
}
