namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
using OFT.Attributes;
using OFT.Localization;
using OFT.Rendering.Context;
using Utils.Common.Logging;
using Color = System.Drawing.Color;

[DisplayName("Order Book Alerts")]
[Category(IndicatorCategories.OrderBook)]
[Display(ResourceType = typeof(Strings), Description = nameof(Strings.OrderBookAlertsIndDescription))]
[HelpLink("https://help.atas.net/support/solutions/articles/72000619055")]
public class OrderBookAlerts : Indicator
{
    #region Nested Types

    public enum PriceOffsetMode
    {
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent))]
        Percent,

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
        Ticks
    }

    internal class PriceInfo
    {
        internal decimal Price { get; set; }
        internal decimal Volume { get; set; }
        internal DateTime AppearanceTime { get; set; }
        internal DateTime LastAlertTime { get; set; }
        internal bool IsAlerted { get; set; }
        internal bool IsActive { get; set; } = true;
    }

    #endregion

    #region Fields

    private readonly ConcurrentDictionary<decimal, PriceInfo> _priceInfos = new();
    private readonly object _locker = new();
    private SortedDictionary<decimal, MarketDataArg> _mDepth = [];
    private decimal _lastPrice;
    private decimal _filter = 100;
    private PriceOffsetMode _pOMode;
    private int _priceOffset = 1;

    private CrossColor _alertForeColor;
    private CrossColor _alertBgColor;
    private Color _chartFillColor = Color.FromArgb((int)(255 * 0.7), 75, 72, 72);

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MinVolumeFilterCommonDescription))]
    public decimal Filter 
    { 
        get => _filter;
        set
        {
            _filter = value;
            RecalculateValues();
        }
    }

    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.TimeFilterSec), GroupName = nameof(Strings.Filters), Description = nameof(Strings.LevelValidTimeFilterDescription))]
    public Filter TimeFilter { get; set; } = new Filter() { Enabled = false, Value = 1 };

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.PriceOffset), Description = nameof(Strings.CalculationModeDescription))]
    public PriceOffsetMode POMode 
    { 
        get => _pOMode;
        set
        {
            _pOMode = value;
            RecalculateValues();
        }
    }

    [Parameter]
    [Range(0, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Offset), GroupName = nameof(Strings.PriceOffset), Description = nameof(Strings.PriceLevelsCountDescription))]
    public int PriceOffset 
    { 
        get => _priceOffset; 
        set
        {
            _priceOffset = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription))]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription))]
    public Color AlertForeColor
    {
        get => field;
        set
        {
            if (value.Equals(field))
                return;

            field = value;
            _alertForeColor = value.Convert();
        }
    } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription))]
    public Color AlertBGColor
    {
        get => field;
        set
        {
            if (value.Equals(field))
                return;

            field = value;
            _alertBgColor = value.Convert();
            _chartFillColor = Color.FromArgb((int)(value.A * 0.7), value.R, value.G, value.B);
        }
    } = Color.FromArgb(255, 75, 72, 72);

    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShowOnChart), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.ShowLevelsOnChartDescription))]
    public bool ShowOnChart { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CoolDownPeriod), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.CoolDownAlertPeriodDescription))]
    public float CoolDownPeriod { get; set; } = 1f;

    #endregion

    #region ctor

    public OrderBookAlerts() : base(true)
    {
        DenyToChangePanel = true;
        DataSeries[0].IsHidden = true;
        ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;

        EnableCustomDrawing = true;
        SubscribeToDrawingEvents(DrawingLayouts.Final);
    }

    #endregion

    #region Protected Methods

    protected override void OnDispose()
    {
        lock (_locker)
        {
            _mDepth?.Clear();
        }
    }

    protected override void OnRecalculate()
    {
        _lastPrice = 0;
        _priceInfos.Clear();

        lock (_locker)
        {
            _mDepth.Clear();
        }
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar == 0)
        {
            lock (_locker)
            {
                var depths = MarketDepthInfo.GetMarketDepthSnapshot();
                var mDepth = new SortedDictionary<decimal, MarketDataArg>();

                foreach (var depth in depths)
                {
                    try
                    {
                        mDepth.Add(depth.Price, depth);
                    }
                    catch (ArgumentException)
                    {
                        // catch duplicates in snapshot
                    }
                }

                _mDepth = mDepth;
            }

            return;
        }

        if (bar != CurrentBar - 1) return;

        var candle = GetCandle(bar);
        _lastPrice = candle.Close;
    }

    protected override void MarketDepthChanged(MarketDataArg depth)
    {
        if (_lastPrice == 0) return;

        lock (_locker)
        {
            // Update internal market depth state
            if (depth.Volume != 0)
            {
                _mDepth[depth.Price] = depth;
            }
            else
            {
                _mDepth.Remove(depth.Price);
            }
        }

        var lowerPrice = 0m;
        var upperPrice = 0m;

        switch (POMode)
        {
            case PriceOffsetMode.Percent:
                lowerPrice = _lastPrice - _lastPrice / 100 * _priceOffset;
                upperPrice = _lastPrice + _lastPrice / 100 * _priceOffset;
                break;
            case PriceOffsetMode.Ticks:
                lowerPrice = _lastPrice - _priceOffset * InstrumentInfo.TickSize;
                upperPrice = _lastPrice + _priceOffset * InstrumentInfo.TickSize;
                break;
        }

        // Check if the changed level is in our range
        if (depth.Price < lowerPrice || depth.Price > upperPrice)
        {
            // Price outside range - deactivate if exists
            if (_priceInfos.TryGetValue(depth.Price, out var priceInfo))
            {
                priceInfo.IsAlerted = false;
                priceInfo.IsActive = false;
            }
            return;
        }

        // Process the changed level
        if (depth.Volume > _filter)
        {
            if (!_priceInfos.TryGetValue(depth.Price, out var priceInfo))
            {
                priceInfo = new PriceInfo { AppearanceTime = MarketTime };
                _priceInfos[depth.Price] = priceInfo;
            }

            priceInfo.Price = depth.Price;
            priceInfo.Volume = depth.Volume;
            priceInfo.IsActive = true;

            if (!priceInfo.IsAlerted && (MarketTime - priceInfo.LastAlertTime).TotalSeconds >= CoolDownPeriod)
            {
                var trueConditions = true;

                if (TimeFilter.Enabled)
                    trueConditions = (decimal)(MarketTime - priceInfo.AppearanceTime).TotalSeconds >= TimeFilter.Value;

                if (trueConditions)
                {
                    priceInfo.LastAlertTime = MarketTime;

                    if (UseAlerts)
                    {
                        AddAlert(AlertFile, InstrumentInfo.Instrument, $"New Level: {priceInfo.Price}, Volume: {priceInfo.Volume}",
                            _alertBgColor, _alertForeColor);
                        priceInfo.IsAlerted = true;
                    }
                }
            }
        }
        else
        {
            if (_priceInfos.TryGetValue(depth.Price, out var priceInfo))
            {
                priceInfo.IsAlerted = false;
                priceInfo.IsActive = false;
            }
        }
    }

    protected override void OnRender(RenderContext context, DrawingLayouts layout)
    {
        if (ChartInfo is null) return;

        if (ShowOnChart)
            DrawPriceLevel(context);
    }

    #endregion

    #region Private Methods

    private void DrawPriceLevel(RenderContext context)
    {
        var color = _chartFillColor;

        foreach (var pInfo in _priceInfos.Values)
        {
            if (!pInfo.IsActive) continue;

            var x = ChartInfo.GetXByBar(FirstVisibleBarNumber);
            var y = ChartInfo.GetYByPrice(pInfo.Price);
            var w = ChartInfo.Region.Width;
            var h = Math.Max(1, ChartInfo.PriceChartContainer.PriceRowHeight);
            var rec = new Rectangle(x, y, w, (int)h);
            context.FillRectangle(color, rec);
        }
    }

    #endregion
}