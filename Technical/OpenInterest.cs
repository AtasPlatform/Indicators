namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common;
    
    [DisplayName("Open Interest")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.OpenInterestDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602439")]
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
            ResetAlertsOnNewBar = true
        };

        private readonly CandleDataSeries _oi = new("Oi", "OI")
        {
            UseMinimizedModeIfEnabled = true,
            ResetAlertsOnNewBar = true,
            DescriptionKey=nameof(Strings.OISettingsDescription)
        };

        private int _lastBar = -1;
        private bool _isAlerted;
        private decimal _filter;
        private bool _minimizedMode;

        private OpenInterestMode _mode = OpenInterestMode.ByBar;
        private decimal _changeSize;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription))]
        public OpenInterestMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
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
                return;

            var currentCandle = GetCandle(bar);

            if (currentCandle.OI == 0)
                return;

            var prevCandle = GetCandle(bar - 1);
            var currentOpen = prevCandle.OI;
            var candle = _oi[bar];

            switch (_mode)
            {
                case OpenInterestMode.ByBar:
                    if (_minimizedMode)
                    {
                        if (currentCandle.OI > currentOpen)
                        {
                            candle.Open = 0;
                            candle.Close = currentCandle.OI - currentOpen;
                            candle.High = currentCandle.MaxOI - currentOpen;
                        }
                        else
                        {
                            candle.Open = currentOpen - currentCandle.OI;
                            candle.Close = 0;
                            candle.High = currentOpen - currentCandle.MinOI;
                        }
                    }
                    else
                    {
                        candle.Open = 0;
                        candle.Close = currentCandle.OI - currentOpen;
                        candle.High = currentCandle.MaxOI - currentOpen;
                        candle.Low = currentCandle.MinOI - currentOpen;
                    }

                    break;

                case OpenInterestMode.Cumulative:
                    candle.Open = currentOpen;
                    candle.Close = currentCandle.OI;
                    candle.High = currentCandle.MaxOI;
                    candle.Low = currentCandle.MinOI;
                    break;

                default:
                    var prevValue = _oi[bar - 1].Close;
                    var dOi = currentOpen - prevValue;

                    if (IsNewSession(bar))
                        dOi = currentOpen;

                    candle.Open = currentOpen - dOi;
                    candle.Close = currentCandle.OI - dOi;
                    candle.High = currentCandle.MaxOI - dOi;
                    candle.Low = currentCandle.MinOI - dOi;
                    break;
            }

            this[bar] = candle.Close;

            var oiValue = Math.Abs(candle.Close);

            if (oiValue < Filter || Filter == 0)
                _filterSeries[bar].Open = _filterSeries[bar].Close = _filterSeries[bar].High = _filterSeries[bar].Low = candle.Open;
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

        #endregion
    }
}