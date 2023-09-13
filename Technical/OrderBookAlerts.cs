namespace ATAS.Indicators.Technical;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using OFT.Localization;
using OFT.Rendering.Context;
using Color = System.Drawing.Color;

[DisplayName("Order Book Alerts")]
public class OrderBookAlerts : Indicator
{
    #region Nested Types

    public enum PriceOffsetMode
    {
        [Display(ResourceType = typeof(Strings), Name = "Percent")]
        Percent,

        [Display(ResourceType = typeof(Strings), Name = "Ticks")]
        Ticks
    }

    internal class PriceInfo
    {
        internal decimal Price { get; set; }
        internal decimal Volume { get; set; }
        internal DateTime AppearanceTime { get; set; } = DateTime.Now;
        internal DateTime LastAlertTime { get; set; } = default;
        internal bool IsAlerted { get; set; }
        internal bool IsActive { get; set; } = true;
    }

    #endregion

    #region Fields

    private readonly List<PriceInfo> _priceInfos = new();
    private decimal _lastPrice;
    private decimal _filter = 100;
    private PriceOffsetMode _pOMode;
    private int _priceOffset = 1;

    #endregion

    #region Properties

    [Parameter]
    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = "Filter", GroupName = "Filters")]
    public decimal Filter 
    { 
        get => _filter;
        set
        {
            _filter = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "TimeFilterSec", GroupName = "Filters")]
    public Filter TimeFilter { get; set; } = new Filter() { Enabled = false, Value = 1 };

    [Display(ResourceType = typeof(Strings), Name = "Mode", GroupName = "PriceOffset")]
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
    [Display(ResourceType = typeof(Strings), Name = "Offset", GroupName = "PriceOffset")]
    public int PriceOffset 
    { 
        get => _priceOffset; 
        set
        {
            _priceOffset = value;
            RecalculateValues();
        }
    }

    [Display(ResourceType = typeof(Strings), Name = "UseAlerts", GroupName = "Alerts")]
    public bool UseAlerts { get; set; }

    [Display(ResourceType = typeof(Strings), Name = "AlertFile", GroupName = "Alerts")]
    public string AlertFile { get; set; } = "alert1";

    [Display(ResourceType = typeof(Strings), Name = "FontColor", GroupName = "Alerts")]
    public Color AlertForeColor { get; set; } = Color.FromArgb(255, 247, 249, 249);

    [Display(ResourceType = typeof(Strings), Name = "BackGround", GroupName = "Alerts")]
    public Color AlertBGColor { get; set; } = Color.FromArgb(255, 75, 72, 72);

    [Display(ResourceType = typeof(Strings), Name = "ShowOnChart", GroupName = "Alerts")]
    public bool ShowOnChart { get; set; }

    [Range(1, int.MaxValue)]
    [Display(ResourceType = typeof(Strings), Name = "CoolDownPeriod", GroupName = "Alerts")]
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

    protected override void OnRecalculate()
    {
        _lastPrice = 0;
        _priceInfos.Clear();
    }

    protected override void OnCalculate(int bar, decimal value)
    {
        if (bar != CurrentBar - 1) return;

        var candle = GetCandle(bar);
        _lastPrice = candle.Close;
    }

    protected override void MarketDepthChanged(MarketDataArg depth)
    {
        if (_lastPrice == 0) return;

        var depths = MarketDepthInfo.GetMarketDepthSnapshot();
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

        foreach (var mdArg in depths)
        {
            if (mdArg.Price >= lowerPrice && mdArg.Price <= upperPrice && mdArg.Volume > _filter)
            {
                var priceInfo = _priceInfos.FirstOrDefault(p => p.Price == mdArg.Price)
                    ?? new PriceInfo();

                priceInfo.Price = mdArg.Price;
                priceInfo.Volume = mdArg.Volume;
                priceInfo.IsActive = true;

                if (!_priceInfos.Contains(priceInfo))
                    _priceInfos.Add(priceInfo);

                if (!priceInfo.IsAlerted && (DateTime.Now - priceInfo.LastAlertTime).TotalSeconds >= CoolDownPeriod) 
                {
                    var trueConditions = true;

                    if (TimeFilter.Enabled)
                        trueConditions = (decimal)(DateTime.Now - priceInfo.AppearanceTime).TotalSeconds >= TimeFilter.Value;

                    if (trueConditions)
                    {
                        priceInfo.LastAlertTime = DateTime.Now;

                        if (UseAlerts)
                        {
                            AddAlert(AlertFile, InstrumentInfo.Instrument, $"New Level: {priceInfo.Price}, Volume: {priceInfo.Volume}",
                                AlertBGColor.Convert(), AlertForeColor.Convert());
                            priceInfo.IsAlerted = true;
                        }   
                    }
                }
            }
            else
            {
                var priceInfo = _priceInfos.FirstOrDefault(p => p.Price == mdArg.Price);

                if (priceInfo is null) continue;

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
        foreach (var pInfo in _priceInfos)
        {
            if (!pInfo.IsActive) continue;

            var x = ChartInfo.GetXByBar(FirstVisibleBarNumber);
            var y = ChartInfo.GetYByPrice(pInfo.Price);
            var w = ChartInfo.Region.Width;
            var h = Math.Max(1, ChartInfo.PriceChartContainer.PriceRowHeight);
            var rec = new Rectangle(x, y, w, (int)h);
            var color = Color.FromArgb((int)(AlertBGColor.A * 0.7), AlertBGColor.R, AlertBGColor.G, AlertBGColor.B);

            context.FillRectangle(color, rec);
        }
    }

    #endregion
}
