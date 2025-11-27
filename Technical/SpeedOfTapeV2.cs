namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Linq;
    using ATAS.Indicators;
    using ATAS.Indicators.Drawing;
    using ATAS.Indicators.Technical;
    using OFT.Attributes;
    using OFT.Rendering.Context;


    [DisplayName("Speed of Tape V2")]
    [Category("Order Flow")]
    [Description("Measures the real tape speed (High Water Mark) and highlights the price acceleration zone.")]
    public class SpeedOfTapeV2 : Indicator
    {
        #region Enums & Structs

        public enum SpeedType
        {
            [Display(Name = "Ticks (HFT)")] Ticks,
            [Display(Name = "Volume (Blocks)")] Volume,
            [Display(Name = "Delta (Aggression)")] Delta,
            [Display(Name = "Buy Volume")] Buys,
            [Display(Name = "Sell Volume")] Sells
        }

        private struct TickSnapshot
        {
            public DateTime Time;
            public decimal Volume;
            public int Direction; // 1 Buy, -1 Sell
            public decimal Price;
        }

        private struct SpeedState
        {
            public decimal Speed;
            public decimal Context;
            public decimal TotalVolume;
            public decimal HighRange;
            public decimal LowRange;
        }

        // Lightweight structure to store drawing data without affecting DataSeries scaling
        private struct ZoneDrawData
        {
            public decimal High;
            public decimal Low;
            public System.Drawing.Color Color;
            public bool IsActive;
        }

        #endregion

        #region Fields

        // Bottom Panel (Histogram)
        // Note: ScaleIt = true is correct here because it will only contain small values (speed)
        private readonly ValueDataSeries _renderSeries = new("Speed Histogram")
        {
            VisualType = VisualMode.Histogram,
            ShowZeroValue = false,
            UseMinimizedModeIfEnabled = false,
            ResetAlertsOnNewBar = true,
            ScaleIt = true
        };

        private readonly ValueDataSeries _thresholdSeries = new("Filter Line")
        {
            VisualType = VisualMode.Line,
            Color = System.Drawing.Color.Aqua.Convert(),
            Width = 2,
            ScaleIt = true,
            ShowZeroValue = false
        };

        // We NO LONGER use RangeDataSeries in the public DataSeries.
        // This prevents the panel from trying to scale up to the price (e.g., 4000).
        // We use this Dictionary to store drawing info instead.
        private Dictionary<int, ZoneDrawData> _zoneCache = new Dictionary<int, ZoneDrawData>();

        // Core Engine
        private readonly Queue<TickSnapshot> _tickQueue = new();
        private readonly Queue<decimal> _historyMaxSpeeds = new();
        private decimal _currentThreshold = 100;

        // State
        private int _lastBar = -1;
        private bool _historyLoaded = false;
        private SpeedState _currentBarMaxState;
        private int _lastAlertBar = -1;

        // Settings
        private int _timeWindow = 5;
        private int _filterPeriod = 10;
        private bool _autoFilter = true;
        private decimal _manualThreshold = 100;
        private System.Drawing.Color _buyColor = System.Drawing.Color.Lime;
        private System.Drawing.Color _sellColor = System.Drawing.Color.Red;
        private System.Drawing.Color _neutralColor = System.Drawing.Color.Gray;

        private List<CumulativeTrade> _cachedTrades = new List<CumulativeTrade>();

        #endregion

        #region Parameters

        [Display(Name = "Time Window (sec)", GroupName = "Calculation", Order = 10)]
        [Range(1, 600)]
        public int TimeWindow
        {
            get => _timeWindow;
            set { _timeWindow = value; RecalculateValues(); }
        }

        [Display(Name = "Data Type", GroupName = "Calculation", Order = 20)]
        public SpeedType DataType
        {
            get => _dataType;
            set
            {
                _dataType = value;
                if (_historyLoaded) RebuildHistoryFromCache();
                RecalculateValues();
            }
        }
        private SpeedType _dataType = SpeedType.Ticks;

        [Display(Name = "Use AutoFilter", GroupName = "Filter", Order = 40)]
        public bool UseAutoFilter
        {
            get => _autoFilter;
            set { _autoFilter = value; RecalculateValues(); }
        }

        [Display(Name = "AutoFilter Period", GroupName = "Filter", Order = 50)]
        [Range(1, 1000)]
        public int FilterPeriod
        {
            get => _filterPeriod;
            set { _filterPeriod = value; RecalculateValues(); }
        }

        [Display(Name = "Manual Threshold", GroupName = "Filter", Order = 60)]
        public decimal ManualThreshold
        {
            get => _manualThreshold;
            set { _manualThreshold = value; RecalculateValues(); }
        }

        [Display(Name = "Show Price Marker", GroupName = "Visuals", Order = 65)]
        public bool ShowPriceMarker
        {
            get => _showPriceMarker;
            set { _showPriceMarker = value; RedrawChart(); }
        }
        private bool _showPriceMarker = true;

        [Display(Name = "Use Smart Colors", GroupName = "Visuals", Order = 70)]
        public bool UseSmartColors
        {
            get => _useSmartColors;
            set { _useSmartColors = value; RedrawChart(); }
        }
        private bool _useSmartColors = true;

        [Display(Name = "Buy Color", GroupName = "Visuals", Order = 80)]
        public CrossColor BuyColor
        {
            get => _buyColor.Convert();
            set { _buyColor = value.Convert(); RedrawChart(); }
        }

        [Display(Name = "Sell Color", GroupName = "Visuals", Order = 90)]
        public CrossColor SellColor
        {
            get => _sellColor.Convert();
            set { _sellColor = value.Convert(); RedrawChart(); }
        }

        [Display(Name = "Neutral Color", GroupName = "Visuals", Order = 100)]
        public CrossColor NeutralColor
        {
            get => _neutralColor.Convert();
            set { _neutralColor = value.Convert(); RedrawChart(); }
        }

        [Display(Name = "Use Alerts", GroupName = "Alerts", Order = 110)]
        public bool UseAlerts { get; set; }

        [Display(Name = "Alert File", GroupName = "Alerts", Order = 120)]
        public string AlertFile { get; set; } = "alert1";

        #endregion

        public SpeedOfTapeV2() : base(true)
        {
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);

            Panel = IndicatorDataProvider.NewPanel;

            // ONLY add series that have correct SCALE values for this panel (Speed)
            DataSeries.Add(_renderSeries);
            DataSeries.Add(_thresholdSeries);

            // Range series are NOT added to DataSeries to avoid breaking scaling.
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                if (!_historyLoaded)
                {
                    DataSeries.ForEach(x => x.Clear());
                    _tickQueue.Clear();
                    _lastBar = -1;
                }
                // Clear manual cache
                _zoneCache.Clear();
            }
        }

        protected override void OnFinishRecalculate()
        {
            if (CurrentBar < 2) return;
            var startTime = GetCandle(0).Time;
            var endTime = GetCandle(CurrentBar - 2).LastTime;
            var request = new CumulativeTradesRequest(startTime, endTime, 0, 0);
            RequestForCumulativeTrades(request);
        }

        #region Core Logic

        private void UpdateVisuals(int bar)
        {
            var speed = _renderSeries[bar];
            var threshold = UseAutoFilter ? _currentThreshold : ManualThreshold;
            _thresholdSeries[bar] = threshold;

            var finalColor = CalculateSmartColor(_currentBarMaxState);
            _renderSeries.Colors[bar] = finalColor;

            // Store data in internal cache instead of DataSeries
            if (ShowPriceMarker && speed > threshold)
            {
                decimal totalVol = _currentBarMaxState.TotalVolume;
                double efficiency = totalVol == 0 ? 0 : (double)(Math.Abs(_currentBarMaxState.Context) / totalVol);
                bool isBuy = _currentBarMaxState.Context > 0;

                System.Drawing.Color zoneColor;

                if (UseSmartColors && efficiency < 0.3)
                {
                    zoneColor = _neutralColor;
                }
                else if (isBuy)
                {
                    zoneColor = _buyColor;
                }
                else
                {
                    zoneColor = _sellColor;
                }

                // Insert or update cache
                _zoneCache[bar] = new ZoneDrawData
                {
                    High = _currentBarMaxState.HighRange,
                    Low = _currentBarMaxState.LowRange,
                    Color = zoneColor,
                    IsActive = true
                };
            }
            else
            {
                // If it drops below threshold, ensure zone is removed if it existed
                if (_zoneCache.ContainsKey(bar))
                    _zoneCache.Remove(bar);
            }

            if (UseAlerts && bar == CurrentBar - 1 && speed > threshold && _lastAlertBar != bar)
            {
                AddAlert(AlertFile, $"Speed Alert: {speed:0}");
                _lastAlertBar = bar;
            }
        }

        private System.Drawing.Color CalculateSmartColor(SpeedState state)
        {
            bool isBuy = state.Context > 0;
            var baseColor = isBuy ? BuyColor : SellColor;

            if (!UseSmartColors) return baseColor.Convert();
            if (state.TotalVolume == 0) return NeutralColor.Convert();

            double efficiency = (double)(Math.Abs(state.Context) / state.TotalVolume);
            return Blend(NeutralColor.Convert(), baseColor.Convert(), efficiency);
        }

        private System.Drawing.Color Blend(System.Drawing.Color background, System.Drawing.Color foreground, double ratio)
        {
            if (ratio > 1) ratio = 1;
            if (ratio < 0) ratio = 0;
            var r = (int)(background.R + (foreground.R - background.R) * ratio);
            var g = (int)(background.G + (foreground.G - background.G) * ratio);
            var b = (int)(background.B + (foreground.B - background.B) * ratio);
            return System.Drawing.Color.FromArgb(r, g, b);
        }

        #endregion

        #region Event Handlers

        protected override void OnCumulativeTradesResponse(CumulativeTradesRequest request, IEnumerable<CumulativeTrade> cumulativeTrades)
        {
            _cachedTrades = cumulativeTrades?.ToList() ?? new List<CumulativeTrade>();
            RebuildHistoryFromCache();
        }

        private void RebuildHistoryFromCache()
        {
            if (_cachedTrades.Count == 0) return;

            _tickQueue.Clear();
            _historyMaxSpeeds.Clear();
            _currentThreshold = 100;
            _lastBar = -1;
            DataSeries.ForEach(x => x.Clear());
            _zoneCache.Clear(); // Clear zones

            foreach (var trade in _cachedTrades)
            {
                int tradeBar = -1;
                int searchStart = _lastBar < 0 ? 0 : _lastBar;
                for (int i = searchStart; i < CurrentBar; i++)
                {
                    var c = GetCandle(i);
                    if (trade.Time >= c.Time && trade.Time <= c.LastTime)
                    {
                        tradeBar = i;
                        break;
                    }
                }

                if (tradeBar < 0) continue;

                if (tradeBar != _lastBar)
                {
                    if (_lastBar >= 0)
                    {
                        _thresholdSeries[_lastBar] = _currentThreshold;
                        _renderSeries.Colors[_lastBar] = CalculateSmartColor(_currentBarMaxState);

                        // Save zone at historical candle close
                        if (_currentBarMaxState.Speed > _currentThreshold && ShowPriceMarker)
                        {
                            SaveZoneToCache(_lastBar, _currentBarMaxState);
                        }

                        _historyMaxSpeeds.Enqueue(_currentBarMaxState.Speed);
                        while (_historyMaxSpeeds.Count > FilterPeriod)
                            _historyMaxSpeeds.Dequeue();

                        if (_historyMaxSpeeds.Count > 0)
                            _currentThreshold = _historyMaxSpeeds.Average() * 1.5m;
                    }

                    _lastBar = tradeBar;
                    _currentBarMaxState = new SpeedState();
                    _renderSeries[tradeBar] = 0;
                    _thresholdSeries[tradeBar] = _currentThreshold;
                }

                _tickQueue.Enqueue(new TickSnapshot
                {
                    Time = trade.Time,
                    Volume = trade.Volume,
                    Direction = trade.Direction == TradeDirection.Buy ? 1 : -1,
                    Price = trade.FirstPrice
                });

                var cutoff = trade.Time.AddSeconds(-TimeWindow);
                while (_tickQueue.Count > 0 && _tickQueue.Peek().Time <= cutoff)
                    _tickQueue.Dequeue();

                decimal winVol = 0;
                decimal winDelta = 0;
                decimal winBuys = 0;
                decimal winSells = 0;
                int winTicks = _tickQueue.Count;
                decimal winHigh = decimal.MinValue;
                decimal winLow = decimal.MaxValue;

                foreach (var t in _tickQueue)
                {
                    winVol += t.Volume;
                    var d = (t.Direction == 1 ? t.Volume : -t.Volume);
                    winDelta += d;
                    if (t.Direction == 1) winBuys += t.Volume; else winSells += t.Volume;
                    if (t.Price > winHigh) winHigh = t.Price;
                    if (t.Price < winLow) winLow = t.Price;
                }

                if (winTicks == 0) { winHigh = trade.FirstPrice; winLow = trade.FirstPrice; }

                decimal currentSpeed = 0;
                decimal contextDelta = winDelta;

                switch (DataType)
                {
                    case SpeedType.Ticks: currentSpeed = winTicks; break;
                    case SpeedType.Volume: currentSpeed = winVol; break;
                    case SpeedType.Delta: currentSpeed = Math.Abs(winDelta); break;
                    case SpeedType.Buys: currentSpeed = winBuys; contextDelta = winBuys; break;
                    case SpeedType.Sells: currentSpeed = winSells; contextDelta = -winSells; break;
                }

                if (currentSpeed > _currentBarMaxState.Speed)
                {
                    _currentBarMaxState = new SpeedState
                    {
                        Speed = currentSpeed,
                        Context = contextDelta,
                        TotalVolume = winVol,
                        HighRange = winHigh,
                        LowRange = winLow
                    };
                }
                _renderSeries[tradeBar] = _currentBarMaxState.Speed;
            }

            if (_lastBar >= 0)
            {
                _thresholdSeries[_lastBar] = _currentThreshold;
                _renderSeries.Colors[_lastBar] = CalculateSmartColor(_currentBarMaxState);
                if (_currentBarMaxState.Speed > _currentThreshold && ShowPriceMarker)
                    SaveZoneToCache(_lastBar, _currentBarMaxState);
            }
            RedrawChart();
        }

        private void SaveZoneToCache(int bar, SpeedState state)
        {
            bool isBuy = state.Context > 0;
            System.Drawing.Color c = isBuy ? _buyColor : _sellColor;

            if (UseSmartColors && state.TotalVolume > 0 &&
               (Math.Abs(state.Context) / state.TotalVolume) < 0.3m)
            {
                c = _neutralColor;
            }
            else if (isBuy) { c = _buyColor; }
            else { c = _sellColor; }

            _zoneCache[bar] = new ZoneDrawData
            {
                High = state.HighRange,
                Low = state.LowRange,
                Color = c,
                IsActive = true
            };
        }

        protected override void OnNewTrade(MarketDataArg trade)
        {
            int bar = CurrentBar - 1;
            if (bar < 0) return;
            var now = DateTime.UtcNow;

            _tickQueue.Enqueue(new TickSnapshot
            {
                Time = now,
                Volume = trade.Volume,
                Direction = trade.Direction == TradeDirection.Buy ? 1 : -1,
                Price = trade.Price
            });

            var cutoff = now.AddSeconds(-TimeWindow);
            while (_tickQueue.Count > 0 && _tickQueue.Peek().Time <= cutoff)
                _tickQueue.Dequeue();

            if (bar != _lastBar)
            {
                if (_lastBar >= 0)
                {
                    _historyMaxSpeeds.Enqueue(_currentBarMaxState.Speed);
                    while (_historyMaxSpeeds.Count > FilterPeriod) _historyMaxSpeeds.Dequeue();
                    if (_historyMaxSpeeds.Count > 0) _currentThreshold = _historyMaxSpeeds.Average() * 1.5m;
                }
                _lastBar = bar;
                _currentBarMaxState = new SpeedState();
                _renderSeries[bar] = 0;
            }

            decimal winVol = 0;
            decimal winDelta = 0;
            decimal winBuys = 0;
            decimal winSells = 0;
            int winTicks = _tickQueue.Count;
            decimal winHigh = decimal.MinValue;
            decimal winLow = decimal.MaxValue;

            foreach (var t in _tickQueue)
            {
                winVol += t.Volume;
                var d = (t.Direction == 1 ? t.Volume : -t.Volume);
                winDelta += d;
                if (t.Direction == 1) winBuys += t.Volume; else winSells += t.Volume;
                if (t.Price > winHigh) winHigh = t.Price;
                if (t.Price < winLow) winLow = t.Price;
            }

            if (winTicks == 0) { winHigh = trade.Price; winLow = trade.Price; }

            decimal currentSpeed = 0;
            decimal contextDelta = winDelta;

            switch (DataType)
            {
                case SpeedType.Ticks: currentSpeed = winTicks; break;
                case SpeedType.Volume: currentSpeed = winVol; break;
                case SpeedType.Delta: currentSpeed = Math.Abs(winDelta); break;
                case SpeedType.Buys: currentSpeed = winBuys; contextDelta = winBuys; break;
                case SpeedType.Sells: currentSpeed = winSells; contextDelta = -winSells; break;
            }

            if (currentSpeed > _currentBarMaxState.Speed)
            {
                _currentBarMaxState = new SpeedState
                {
                    Speed = currentSpeed,
                    Context = contextDelta,
                    TotalVolume = winVol,
                    HighRange = winHigh,
                    LowRange = winLow
                };
            }

            _renderSeries[bar] = _currentBarMaxState.Speed;
            UpdateVisuals(bar);
        }

        #endregion

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            if (layout != DrawingLayouts.Final || ChartInfo == null || InstrumentInfo == null)
                return;

            context.SetClip(ChartInfo.PriceChartContainer.Region);

            for (int bar = FirstVisibleBarNumber; bar <= LastVisibleBarNumber; bar++)
            {
                // Read directly from internal cache
                if (_zoneCache.TryGetValue(bar, out ZoneDrawData zone) && zone.IsActive)
                {
                    DrawZone(context, bar, zone.High, zone.Low, zone.Color);
                }
            }

            context.ResetClip();
        }

        private void DrawZone(RenderContext context, int bar, decimal high, decimal low, System.Drawing.Color color)
        {
            if (high == low) high += InstrumentInfo.TickSize;

            var y1 = ChartInfo.GetYByPrice(high, true);
            var y2 = ChartInfo.GetYByPrice(low, false);
            var top = Math.Min(y1, y2);
            var height = Math.Abs(y2 - y1);
            if (height < 1) height = 1;

            var x = ChartInfo.GetXByBar(bar, true);
            var width = (int)Math.Round((double)ChartInfo.PriceChartContainer.BarsWidth);
            if (width < 1) width = 1;

            var rect = new Rectangle(x, top, width, height);
            var finalColor = System.Drawing.Color.FromArgb(150, color.R, color.G, color.B);
            context.FillRectangle(finalColor, rect);
        }
    }
}
