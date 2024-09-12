﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;

    using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering;

    [Category(IndicatorCategories.ClustersProfilesLevels)]
    [DisplayName("Dynamic Levels Channel")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.DynamicLevelsChannelDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602381")]
    public class DynamicLevelsChannel : Indicator
    {
        #region Nested types

        private class VolumeInfo
        {
            #region Properties

            public decimal Price { get; set; }

            public int Bar { get; set; }

            public decimal Volume { get; set; }

            public decimal Bid { get; set; }

            public decimal Ask { get; set; }

            public int Time { get; set; }

            #endregion
        }

        private class Signal
        {
            #region Properties

            public TradeDirection Direction { get; set; }

            public decimal Price { get; set; }

            public decimal PocTicks { get; set; }

            #endregion
        }

        public enum CalculationMode
        {
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Volume))]
            Volume,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PositiveDelta))]
            PosDelta,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.NegativeDelta))]
            NegDelta,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Delta))]
            Delta,

            [Browsable(false)]
            [Obsolete]
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Time))]
            Time
        }

        #endregion

        #region Static and constants

        private const decimal _percent = 70m;
        private const int _priceInterval = 2;

        #endregion

        #region Fields

        private readonly RangeDataSeries _areaSeries = new("AreaSeries", "Range");
        private readonly ValueDataSeries _buySeries = new("BuySeries", Strings.Buys)
        {
            DescriptionKey = Strings.BuySignalSettingsDescription
        };
        private readonly ValueDataSeries _sellSeries = new("SellSeries", Strings.Sells)
        {
            DescriptionKey = Strings.SellSignalSettingsDescription
        };

        private readonly ValueDataSeries _downSeries = new("DownSeries", "VAL")
        {
            DescriptionKey = Strings.VALLineSettingsDescription
        };

        private readonly ValueDataSeries _pocSeries = new("PocSeries", "POC")
        {
            DescriptionKey = Strings.POCLineSettingsDescription
        };

        private readonly ValueDataSeries _upSeries = new("UpSeries", "VAH")
        {
            DescriptionKey = Strings.VAHLineSettingsDescription
        };

        private readonly List<VolumeInfo> _priceInfo = new();
        private readonly List<Signal> _signals = new();
        private CalculationMode _calculationMode;
        private int _days;
        private int _lastBar;
        private decimal _lastVah;
        private decimal _lastVal;
        private decimal _lastVol;
        private decimal _maxPrice;
        private int _lastAlertBar;
        private decimal _lastApproximateLevel;
        private int _lastPocAlert;
        private int _lastVahAlert;
        private int _lastValAlert;
        private decimal _prevClose;

        private int _period;
        private int _targetBar;
        private decimal _tickSize;
        private List<VolumeInfo> _volumeGroup = new();

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SourceTypeDescription), Order = 100)]
        public CalculationMode CalcMode
        {
            get => _calculationMode;
            set
            {
                _calculationMode = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 110)]
        public int Period
        {
            get => _period;
            set
            {
                if (value <= 0)
                    return;

                _period = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), GroupName = nameof(Strings.Calculation), Name = nameof(Strings.DaysLookBack), Order = int.MaxValue, Description = nameof(Strings.DaysLookBackDescription))]
        public int Days
        {
            get => _days;
            set
            {
                if (value < 0)
                    return;

                _days = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AreaColor), GroupName = nameof(Strings.Drawing), Description = nameof(Strings.AreaColorDescription))]
        public CrossColor AreaColor
        {
            get => _areaSeries.RangeColor;
            set => _areaSeries.RangeColor = value;
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.IsApproximationAlertDescription), Order = 300)]
        public bool UseApproximationAlert { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ApproximationFilter), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.ApproximationFilterDescription), Order = 310)]
        public int ApproximationFilter { get; set; } = 3;

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PocChangeAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UsePocChangeAlertDescription), Order = 320)]
        public bool UseAlerts { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PocAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UsePocTouchAlertDescription), Order = 330)]
        public bool UsePocTouchAlert { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ValAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseVALTouchAlertDescription), Order = 340)]
        public bool UseValTouchAlert { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.VahAlert), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseVAHTouchAlertDescription), Order = 350)]
        public bool UseVahTouchAlert { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 360)]
        public string AlertFile { get; set; } = "alert1";

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription), Order = 370)]
        public CrossColor AlertForeColor { get; set; } = CrossColor.FromArgb(255, 247, 249, 249);

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription), Order = 380)]
        public CrossColor AlertBGColor { get; set; } = CrossColor.FromArgb(255, 75, 72, 72);

        #endregion

        #region ctor

        public DynamicLevelsChannel()
            : base(true)
        {
            DenyToChangePanel = true;

            _days = 20;
            _period = 40;
            _lastBar = -1;

            _areaSeries.RangeColor = CrossColor.FromArgb(100, 255, 100, 100);
            _areaSeries.IsHidden = true;
            DataSeries[0] = _areaSeries;
            _upSeries.ShowZeroValue = _downSeries.ShowZeroValue = _pocSeries.ShowZeroValue = false;
            _upSeries.Width = _downSeries.Width = _pocSeries.Width = 2;
            _pocSeries.Color = System.Drawing.Color.Aqua.Convert();

            _buySeries.VisualType = VisualMode.UpArrow;
            _buySeries.Color = System.Drawing.Color.Green.Convert();
            _sellSeries.VisualType = VisualMode.DownArrow;
            _sellSeries.Color = System.Drawing.Color.Red.Convert();
            _buySeries.ShowZeroValue = _sellSeries.ShowZeroValue = false;

            DataSeries.Add(_upSeries);
            DataSeries.Add(_downSeries);
            DataSeries.Add(_pocSeries);
            DataSeries.Add(_buySeries);
            DataSeries.Add(_sellSeries);
        }

        #endregion

        #region Protected methods

        protected override void OnApplyDefaultColors()
        {
            if (ChartInfo is null)
                return;

            var downCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
            _upSeries.Color = _downSeries.Color = _sellSeries.Color = downCandleColor;
            _buySeries.Color = ChartInfo.ColorsStore.UpCandleColor.Convert();
            _pocSeries.Color = ChartInfo.ColorsStore.DrawingObjectColor.Convert();

            _areaSeries.RangeColor = ChartInfo.ColorsStore.DownCandleColor.SetTransparency(0.9m).Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
                _lastVah = 0;
                _lastVal = 0;
                _lastVol = 0;
                _tickSize = InstrumentInfo.TickSize;
                _priceInfo.Clear();
                _upSeries.SetPointOfEndLine(_period - 1);
                _downSeries.SetPointOfEndLine(_period - 1);
                _pocSeries.SetPointOfEndLine(_period - 1);
                _signals.Clear();

                _lastPocAlert = 0;
                _lastValAlert = 0;
                _lastVahAlert = 0;
                _lastAlertBar = 0;
                _prevClose = GetCandle(CurrentBar - 1).Close;

                _targetBar = 0;

                if (_days > 0)
                {
                    var days = 0;

                    for (var i = CurrentBar - 1; i >= 0; i--)
                    {
                        _targetBar = i;

                        if (!IsNewSession(i))
                            continue;

                        days++;

                        if (days == _days)
                            break;
                    }

                    if (_targetBar > 0)
                    {
                        _upSeries.SetPointOfEndLine(_targetBar - 1);
                        _downSeries.SetPointOfEndLine(_targetBar - 1);
                        _pocSeries.SetPointOfEndLine(_targetBar - 1);
                    }
                }

                return;
            }

            if (bar < _targetBar)
                return;

            var candle = GetCandle(bar - 1);

            if (bar == _lastBar)
                _priceInfo.RemoveAll(x => x.Bar == bar);

            for (var i = candle.Low; i <= candle.High; i += _tickSize)
            {
                var priceInfo = candle.GetPriceVolumeInfo(i);

                if (priceInfo != null)
                {
                    _priceInfo.Add(new VolumeInfo
                    {
                        Price = i,
                        Volume = priceInfo.Volume,
                        Bar = bar,
                        Ask = priceInfo.Ask,
                        Bid = priceInfo.Bid,
                        Time = priceInfo.Time
                    });
                }
            }

            _lastBar = bar;

            if (bar < _period)
                return;

            _priceInfo.RemoveAll(x => x.Bar == bar - Period);

            _volumeGroup = _priceInfo
                .GroupBy(x => x.Price)
                .Select(p => new VolumeInfo
                {
                    Price = p.First().Price,
                    Volume = p.Sum(v => v.Volume),
                    Time = p.Sum(t => t.Time),
                    Ask = p.Sum(a => a.Ask),
                    Bid = p.Sum(b => b.Bid)
                })
                .OrderByDescending(x => x.Volume)
                .ToList();

            var maxPriceInfo = _volumeGroup
                .FirstOrDefault();

            if (maxPriceInfo != null)
                _maxPrice = maxPriceInfo.Price;

            VolumeInfo pocValue;

            switch (_calculationMode)
            {
                case CalculationMode.Time:
                case CalculationMode.Volume:
                    _pocSeries[bar] = _maxPrice;
                    break;
                case CalculationMode.PosDelta:
                    pocValue = _volumeGroup
                        .OrderByDescending(x => x.Ask - x.Bid)
                        .FirstOrDefault();

                    if (pocValue != null && pocValue.Ask - pocValue.Bid > 0)
                        _pocSeries[bar] = pocValue.Price;
                    break;
                case CalculationMode.NegDelta:
                    pocValue = _volumeGroup
                        .OrderBy(x => x.Ask - x.Bid)
                        .FirstOrDefault();

                    if (pocValue != null && pocValue.Ask - pocValue.Bid < 0)
                        _pocSeries[bar] = pocValue.Price;
                    break;
                case CalculationMode.Delta:
                    pocValue = _volumeGroup
                        .OrderByDescending(x => Math.Abs(x.Ask - x.Bid))
                        .FirstOrDefault();

                    if (pocValue != null)
                        _pocSeries[bar] = pocValue.Price;
                    break;
            }

            GetArea();

            _areaSeries[bar] = new RangeValue
            { Lower = _lastVal, Upper = _lastVah };
            _upSeries[bar] = _lastVah;
            _downSeries[bar] = _lastVal;

            if (candle.High > _upSeries[bar] && candle.Low <= _upSeries[bar]
                ||
                candle.Low < _downSeries[bar] && candle.High >= _downSeries[bar])
            {
                var signal = new Signal
                {
                    Direction = Math.Abs(candle.High - _upSeries[bar]) < Math.Abs(candle.Low - _downSeries[bar])
                        ? TradeDirection.Buy
                        : TradeDirection.Sell
                };

                signal.Price = signal.Direction == TradeDirection.Buy
                    ? _upSeries[bar]
                    : _downSeries[bar];

                signal.PocTicks = Math.Abs(signal.Price - _pocSeries[bar]) / _tickSize;

                _signals.Add(signal);
            }

            if (candle.High > _upSeries[bar])
                _signals.RemoveAll(x => x.Direction == TradeDirection.Sell);

            if (candle.Low < _downSeries[bar])
                _signals.RemoveAll(x => x.Direction == TradeDirection.Buy);

            for (var i = _signals.Count - 1; i >= 0; i--)
            {
                var signal = _signals[i];

                if (signal.Direction == TradeDirection.Buy)
                {
                    if (Math.Abs(candle.High - _upSeries[bar]) / _tickSize >= signal.PocTicks && _buySeries[bar] == 0)
                    {
                        _buySeries[bar] = candle.Low - _tickSize * 2;
                        _signals.RemoveAt(i);
                    }
                }

                if (signal.Direction == TradeDirection.Sell)
                {
                    if (Math.Abs(_downSeries[bar] - candle.Low) / _tickSize >= signal.PocTicks && _sellSeries[bar] == 0)
                    {
                        _sellSeries[bar] = candle.High + _tickSize * 2;
                        _signals.RemoveAt(i);
                    }
                }
            }

            if (bar != CurrentBar - 1)
                return;

            var currentPocPrice = _pocSeries[bar];

            if (UseAlerts && bar > _lastAlertBar)
            {
                var prevPrice = _pocSeries[bar - 1];

                if (prevPrice > 0.000001m && Math.Abs(prevPrice - currentPocPrice) > _tickSize / 2)
                {
                    _lastAlertBar = bar;
                    AddAlert(AlertFile, InstrumentInfo.Instrument, $"Changed max level to {currentPocPrice}", AlertBGColor, AlertForeColor);
                }
            }

            if (UseApproximationAlert)
            {
                if (currentPocPrice != _lastApproximateLevel && Math.Abs(candle.Close - currentPocPrice) / _tickSize <= ApproximationFilter)
                {
                    _lastApproximateLevel = currentPocPrice;
                    AddAlert(AlertFile, InstrumentInfo.Instrument, $"Approximate to max Level {currentPocPrice}", AlertBGColor, AlertForeColor);
                }
            }

            if (UsePocTouchAlert && _lastPocAlert != bar)
            {
                if ((candle.Close >= currentPocPrice && _prevClose < currentPocPrice)
                    ||
                    (candle.Close <= currentPocPrice && _prevClose > currentPocPrice))
                {
                    AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached POC level: {currentPocPrice}", AlertBGColor, AlertForeColor);

                    _lastPocAlert = bar;
                }
            }

            if (UseValTouchAlert && _lastValAlert != bar)
            {
                if ((candle.Close >= _downSeries[bar] && _prevClose < _downSeries[bar])
                    ||
                    (candle.Close <= _downSeries[bar] && _prevClose > _downSeries[bar]))
                {
                    AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAL level: {_downSeries[bar]}", AlertBGColor, AlertForeColor);

                    _lastValAlert = bar;
                }
            }

            if (UseVahTouchAlert && _lastVahAlert != bar)
            {
                if ((candle.Close >= _upSeries[bar] && _prevClose < _upSeries[bar])
                    ||
                    (candle.Close <= _upSeries[bar] && _prevClose > _upSeries[bar]))
                {
                    AddAlert(AlertFile, InstrumentInfo.Instrument, $"Price reached VAH level: {_upSeries[bar]}", AlertBGColor, AlertForeColor);

                    _lastVahAlert = bar;
                }
            }

            _prevClose = candle.Close;
        }

        #endregion

        #region Private methods

        private void GetArea()
        {
            var totalVolume = _volumeGroup.Sum(x => x.Volume);

            if (totalVolume == _lastVol || totalVolume == 0)
                return;

            var vah = 0m;
            var val = 0m;
            var high = _volumeGroup.Max(x => x.Price);
            var low = _volumeGroup.Min(x => x.Price);

            if (high != 0 && low != 0)
            {
                vah = val = _maxPrice;

                var vol = _volumeGroup
                    .Where(x => x.Price == _maxPrice)
                    .Sum(x => x.Volume);

                var valueAreaVolume = totalVolume * _percent * 0.01m;

                while (vol <= valueAreaVolume)
                {
                    if (vah >= high && val <= low)
                        break;

                    var upperVol = 0m;
                    var lowerVol = 0m;
                    var upperPrice = vah;
                    var lowerPrice = val;

                    for (var i = 0; i <= _priceInterval; i++)
                    {
                        if (high > upperPrice + _tickSize)
                        {
                            upperPrice += _tickSize;

                            upperVol += _volumeGroup
                                .Where(x => x.Price == upperPrice)
                                .Sum(x => x.Volume);
                        }

                        if (low > lowerPrice - _tickSize)
                            continue;

                        lowerPrice -= _tickSize;

                        lowerVol += _volumeGroup
                            .Where(x => x.Price == lowerPrice)
                            .Sum(x => x.Volume);
                    }

                    if (lowerVol == 0 && upperVol == 0)
                    {
                        vah = Math.Min(upperPrice, high);
                        val = Math.Max(lowerPrice, low);
                        break;
                    }

                    if (upperVol >= lowerVol)
                    {
                        vah = upperPrice;
                        vol += upperVol;
                    }
                    else
                    {
                        val = lowerPrice;
                        vol += lowerVol;
                    }

                    if (vol >= valueAreaVolume)
                        break;
                }
            }

            _lastVol = totalVolume;
            _lastVah = vah;
            _lastVal = val;
        }

        #endregion
    }
}