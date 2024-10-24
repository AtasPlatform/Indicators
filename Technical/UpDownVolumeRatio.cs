﻿using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using Color = System.Drawing.Color;


namespace ATAS.Indicators.Technical
{
    [DisplayName("Up/Down Volume Ratio")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.UpDownVolumeRatioDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000619242")]
    public class UpDownVolumeRatio : Indicator
    {
        #region Nested Types

        public enum CalculationMode
        {
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UpDownVolume))]
            UpDownVolume,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.AskBidVolume))]
            AskBidVolume
        }

        public enum MovingType
        {
            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EMA))]
            Ema,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LinearReg))]
            LinReg,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WMA))]
            Wma,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA))]
            Sma,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WWMA))]
            Wwma,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SZMA))]
            Szma,

            [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMMA))]
            Smma
        }

        #endregion

        #region Fields

        private readonly ValueDataSeries _data = new("Data", Strings.Volume)
        {
            IsHidden = true,
            VisualType = VisualMode.Histogram,
            ShowZeroValue = false
        };

        private EMA _ema;
        private LinearReg _linReg;
        private WMA _wma;
        private SMA _sma;
        private WWMA _wwma;
        private SZMA _szma;
        private SMMA _smma;
        private Color _histogramColor = DefaultColors.Blue;

        private CalculationMode _calcMode;
        private int _period = 10;
        private MovingType _movType;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.CalculationModeDescription))]
        public CalculationMode CalcMode
        {
            get => _calcMode;
            set
            {
                _calcMode = value;
                RecalculateValues();
            }
        }

        [Parameter]
        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.PeriodDescription))]
        public int Period
        {
            get => _period;
            set
            {
                _period = value;
                SetPeriod(value);
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.MovingType), GroupName = nameof(Strings.Calculation), Description = nameof(Strings.MovingTypeDescription))]
        public MovingType MovType
        {
            get => _movType;
            set
            {
                _movType = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Color), GroupName = nameof(Strings.Visualization), Description = nameof(Strings.ColorDescription))]
        public Color HistogramColor
        {
            get => _histogramColor;
            set
            {
                _histogramColor = value;
                _data.Color = value.Convert();
            }
        }

        #endregion

        #region ctor

        public UpDownVolumeRatio() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = true;

            DataSeries[0] = _data;
            _data.Color = _histogramColor.Convert();

        }

        #endregion

        #region Protected Methods

        protected override void OnRecalculate()
        {
            switch (_movType)
            {
                case MovingType.Ema:
                    _ema = new();
                    _ema.Period = _period;
                    break;
                case MovingType.LinReg:
                    _linReg = new();
                    _linReg.Period = _period;
                    break;
                case MovingType.Wma:
                    _wma = new();
                    _wma.Period = _period;
                    break;
                case MovingType.Sma:
                    _sma = new();
                    _sma.Period = _period;
                    break;
                case MovingType.Wwma:
                    _wwma = new();
                    _wwma.Period = _period;
                    break;
                case MovingType.Szma:
                    _szma = new();
                    _szma.Period = _period;
                    break;
                case MovingType.Smma:
                    _smma = new();
                    _smma.Period = _period;
                    break;
            }
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            var candle = GetCandle(bar);
            var udRatio = GetUDRatio(candle);
            SetMovingAverage(bar, udRatio);
        }

        #endregion

        #region Private Methods

        private void SetMovingAverage(int bar, decimal ratio)
        {
            switch (_movType)
            {
                case MovingType.Ema:
                    _data[bar] = _ema.Calculate(bar, ratio);
                    break;
                case MovingType.LinReg:
                    _data[bar] = _linReg.Calculate(bar, ratio);
                    break;
                case MovingType.Wma:
                    _data[bar] = _wma.Calculate(bar, ratio);
                    break;
                case MovingType.Sma:
                    _data[bar] = _sma.Calculate(bar, ratio);
                    break;
                case MovingType.Wwma:
                    _data[bar] = _wwma.Calculate(bar, ratio);
                    break;
                case MovingType.Szma:
                    _data[bar] = _szma.Calculate(bar, ratio);
                    break;
                case MovingType.Smma:
                    _data[bar] = _smma.Calculate(bar, ratio);
                    break;
            }
        }

        private decimal GetUDRatio(IndicatorCandle candle)
        {
            var udr = 0m;

            switch (_calcMode)
            {
                case CalculationMode.UpDownVolume:
                    udr = UpDownVolumeCalc(candle);
                    break;
                case CalculationMode.AskBidVolume:
                    udr = AskBidVolumeCalc(candle); 
                    break;
            }

            return udr;
        }

        private decimal AskBidVolumeCalc(IndicatorCandle candle)
        {
            var ascs = candle.Ask;
            var bids = candle.Bid;

            return ascs + bids == 0
                 ? 0
                 : 100 * (ascs - bids) / (ascs + bids);
        }

        private decimal UpDownVolumeCalc(IndicatorCandle candle)
        {
            var upVolume = candle.Open < candle.Close ? candle.Volume : 0;
            var downVolume = candle.Open > candle.Close ? candle.Volume : 0;

            return upVolume + downVolume == 0
                 ? 0
                 : 100 * (upVolume - downVolume) / (upVolume + downVolume);
        }

        private void SetPeriod(int period)
        {
            switch (_movType)
            {
                case MovingType.Ema:
                    if (_ema != null)
                        _ema.Period = period;
                    break;
                case MovingType.LinReg:
                    if (_linReg != null)
                        _linReg.Period = period;
                    break;
                case MovingType.Wma:
                    if (_wma != null)
                        _wma.Period = period;
                    break;
                case MovingType.Sma:
                    if (_sma != null) 
                        _sma.Period = period;
                    break;
                case MovingType.Wwma:
                    if (_wma != null)
                        _wwma.Period = period;
                    break;
                case MovingType.Szma:
                    if (_szma != null)
                        _szma.Period = period;
                    break;
                case MovingType.Smma:
                    if (_smma != null)
                        _smma.Period = period;
                    break;
            }
        }

        #endregion
    }
}
