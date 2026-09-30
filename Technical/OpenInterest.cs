namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.DataFeedsCore.Valuation;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common;

    [DisplayName("Open Interest")]
    [Category(IndicatorCategories.VolumeOrderFlow)]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OpenInterestDescription))]
    [HelpLink("https://help.atas.net/support/solutions/articles/72000602439")]
	public class OpenInterest : Indicator
	{
        #region Nested types

        public enum OpenInterestMode
        {
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ByBar))]
            ByBar,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Session))]
            Session,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Cumulative))]
            Cumulative
        }

        #endregion

        #region Fields

        private readonly CandleDataSeries _filterSeries = new("FilterSeries", "Open interest filtered")
        {
            UpCandleColor = System.Drawing.Color.LightBlue.Convert(),
            DownCandleColor = System.Drawing.Color.LightBlue.Convert(),
            IsHidden = true,
            ScaleIt = false,
            ShowCurrentValue = false,
            ShowTooltip = false,
            UseMinimizedModeIfEnabled = true,
            ResetAlertsOnNewBar = true,
            HideZeroCandles = true
        };

        private readonly CandleDataSeries _oi = new("Oi", "OI")
        {
            UseMinimizedModeIfEnabled = true,
            ResetAlertsOnNewBar = true,
            DescriptionKey = nameof(Strings.OISettingsDescription),
            HideZeroCandles = true
        };

        private int _lastBar = -1;
        private bool _isAlerted;
        private decimal _filter;
        private bool _minimizedMode;

        private OpenInterestMode _mode = OpenInterestMode.ByBar;
        private decimal _changeSize;
        private bool _valuesInMoney;
        private VolumeValueFactor? _moneyFactor;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription))]
        public OpenInterestMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                UpdateTooltipSettings();
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MinimizedMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.HistogramMinimizedModeDescription))]
        public bool MinimizedMode
        {
            get => _minimizedMode;
            set
            {
                _minimizedMode = value;
                UpdateTooltipSettings();
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ValuesInMoney), GroupName = nameof(Strings.Settings), Description = nameof(Strings.OpenInterestValuesInMoneyDescription))]
        public bool ValuesInMoney
        {
            get => _valuesInMoney;
            set
            {
                _valuesInMoney = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Filter), GroupName = nameof(Strings.Filters), Description = nameof(Strings.MaximumFilterDescription))]
        [Range(0, 100000000)]
        public decimal Filter
        {
            get => _filter;
            set
            {
                _filter = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FilterColor), GroupName = nameof(Strings.Filters), Description = nameof(Strings.FilterCandleColorDescription))]
        public CrossColor FilterColor
        {
            get => _filterSeries.UpCandleColor;
            set => _filterSeries.UpCandleColor = _filterSeries.DownCandleColor = value;
        }

        #region Alerts

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription))]
        public bool UseAlerts { get; set; }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription))]
        public string AlertFile { get; set; } = "alert1";

        [Range(0, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RequiredChange), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFilterDescription))]
        public decimal ChangeSize
        {
            get => _changeSize;
            set
            {
                _changeSize = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.FontColor), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertTextColorDescription))]
        public CrossColor AlertForeColor { get; set; } = CrossColor.FromArgb(255, 247, 249, 249);

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BackGround), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFillColorDescription))]
        public CrossColor AlertBGColor { get; set; } = CrossColor.FromArgb(255, 75, 72, 72);

        #endregion

        #endregion

        #region ctor

        public OpenInterest()
            : base(true)
        {
            DataSeries[0].IsHidden = true;
            ((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.Hide;

            DataSeries.Add(_oi);
            DataSeries.Add(_filterSeries);
            Panel = IndicatorDataProvider.NewPanel;
        }

        #endregion

        #region Protected methods

        protected override void OnInitialize()
        {
	        _filterSeries.BorderColor = CrossColors.Transparent;
        }

        protected override void OnApplyDefaultColors()
        {
            if (ChartInfo is null)
                return;

            _oi.UpCandleColor = ChartInfo.ColorsStore.UpCandleColor.Convert();
            _oi.DownCandleColor = ChartInfo.ColorsStore.DownCandleColor.Convert();
            _oi.BorderColor = ChartInfo.ColorsStore.BarBorderPen.Color.Convert();
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
                UpdateMoneyFactor();

            var currentCandle = GetCandle(bar);

            if (currentCandle.OI == 0)
            {
                if(bar == 0 || _mode is OpenInterestMode.ByBar)
                    return;

                var close = _oi[bar - 1].Close;

                _oi[bar] = new Candle()
                {
	                Open = close,
	                Low = close,
	                Close = close,
	                High = close
                };
                return;
            }

            var currentOpen = bar == 0
	            ? currentCandle.OI
	            : GetCandle(bar - 1).OI;

            if (currentOpen is 0)
	            currentOpen = currentCandle.OI;

            // PLAT-5080: in money every open interest of the bar is valued at the bar's close price,
            // so a bar shows the value of the change in open interest, not the move of the price
            var oi = ToMoney(currentCandle.OI, currentCandle.Close);
            var maxOi = ToMoney(currentCandle.MaxOI, currentCandle.Close);
            var minOi = ToMoney(currentCandle.MinOI, currentCandle.Close);
            currentOpen = ToMoney(currentOpen, currentCandle.Close);

            var candle = _oi[bar];

            switch (_mode)
            {
                case OpenInterestMode.ByBar:
                    if (_minimizedMode)
                    {
                        candle.Low = 0;

                        if (oi > currentOpen)
                        {
                            candle.Open = 0;
                            candle.Close = oi - currentOpen;
                            candle.High = maxOi - currentOpen;
                        }
                        else
                        {
                            candle.Open = currentOpen - oi;
                            candle.Close = 0;
                            candle.High = currentOpen - minOi;
                        }
                    }
                    else
                    {
                        candle.Open = 0;
                        candle.Close = oi - currentOpen;
                        candle.High = maxOi - currentOpen;
                        candle.Low = minOi - currentOpen;
                    }

                    break;

                case OpenInterestMode.Cumulative:
                    candle.Open = currentOpen;
                    candle.Close = oi;
                    candle.High = maxOi;
                    candle.Low = minOi;
                    break;

                default:
                    var prevValue = _oi[bar - 1].Close;
                    var dOi = currentOpen - prevValue;

                    if (IsNewSession(bar))
                        dOi = currentOpen;

                    candle.Open = currentOpen - dOi;
                    candle.Close = oi - dOi;
                    candle.High = maxOi - dOi;
                    candle.Low = minOi - dOi;
                    break;
            }

            this[bar] = candle.Close;

            var oiValue = Math.Abs(candle.Close);

            if (oiValue < Filter || Filter == 0)
                _filterSeries[bar].Open = _filterSeries[bar].Close = _filterSeries[bar].High = _filterSeries[bar].Low = 0;
            else
                _filterSeries[bar] = candle.MemberwiseClone();

            if (bar != _lastBar)
            {
                _isAlerted = false;
            }

            if (bar == CurrentBar - 1)
            {
                if (UseAlerts && Math.Abs(this[bar]) >= _changeSize && !_isAlerted)
                {
                    AddAlert(AlertFile, InstrumentInfo.Instrument, "OI changed!", AlertBGColor, AlertForeColor);
                    _isAlerted = true;
                }
            }

            _lastBar = bar;
        }

        // a new exchange rate or display currency changes every value in money
        protected override void OnValuationChanged()
        {
            if (_valuesInMoney)
                DoActionInGuiThread(RecalculateValues);
        }

        #endregion

        #region Private methods

        // Open interest comes in the units of the instrument's volume (contracts, lots or coins), so the
        // valuation of volumes applies to it; the series then label the values with the currency sign
        private void UpdateMoneyFactor()
        {
            _moneyFactor = _valuesInMoney && InstrumentInfo?.Valuation.TryGetDisplayFactor(out var factor) == true
                ? factor
                : null;

            _oi.ValueCurrency = _filterSeries.ValueCurrency = _moneyFactor?.Currency;
        }

        private decimal ToMoney(decimal openInterest, decimal price)
        {
            return _moneyFactor is { } factor && openInterest != 0
                ? factor.ToMoney(openInterest, price)
                : openInterest;
        }

        private void UpdateTooltipSettings()
        {
            _oi.HideOpenCloseLabels = _mode is OpenInterestMode.ByBar;
            _oi.TooltipAnchor = _mode is OpenInterestMode.ByBar && _minimizedMode
                ? CandleTooltipAnchor.Top
                : CandleTooltipAnchor.Close;
        }

        #endregion
    }
}
