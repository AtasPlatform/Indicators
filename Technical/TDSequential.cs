
namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Windows.Media;
    using ATAS.Indicators.Drawing;

    [DisplayName("TD Sequential")]
    public class TDSequential : Indicator
    {
        #region Fields

        private const int BARS_NUM = 4;
        private const int MAX_SIGNAL_NUM = 9;

        readonly ValueDataSeries _td = new("TD") { ShowZeroValue = false, IsHidden = true };
        readonly ValueDataSeries _ts = new("TS") { ShowZeroValue = false, IsHidden = true };
        readonly PaintbarsDataSeries _colorBars = new("Candles");
        readonly ValueDataSeries _up = new("Up") { ShowZeroValue = false, VisualType = VisualMode.DownArrow };
        readonly ValueDataSeries _down = new("Down") { ShowZeroValue = false, VisualType = VisualMode.UpArrow };
        readonly ValueDataSeries _sup = new("Support")
        {
            ShowZeroValue = false,
            Width = 2,
            VisualType = VisualMode.Line,
            LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dot,
            Color = Colors.Red
        };

        readonly ValueDataSeries _res = new("Resistance")
        {
            ShowZeroValue = false,
            Width = 2,
            VisualType = VisualMode.Line,
            LineDashStyle = OFT.Rendering.Settings.LineDashStyle.Dot,
            Color = Colors.Green
        };

        private bool _isNumbers = true;

        private Color _buyBarsColor = Colors.LightGreen;
        private Color _buyovershoot = (Color)ColorConverter.ConvertFromString("#D6FF5C");
        private Color _buyovershoot1 = (Color)ColorConverter.ConvertFromString("#D1FF47");
        private Color _buyovershoot2 = (Color)ColorConverter.ConvertFromString("#B8E62E");
        private Color _buyovershoot3 = (Color)ColorConverter.ConvertFromString("#8FB224");
        private Color _sellBarsColor = Colors.OrangeRed;
        private Color _sellovershoot = (Color)ColorConverter.ConvertFromString("#FF66A3");
        private Color _sellovershoot1 = (Color)ColorConverter.ConvertFromString("#FF3385");
        private Color _sellovershoot2 = (Color)ColorConverter.ConvertFromString("#FF0066");
        private Color _sellovershoot3 = (Color)ColorConverter.ConvertFromString("#CC0052");
        private bool _isSr = true;
        private bool _isBarcolor = true;

        #endregion

        #region Properties

        #region Commonn Settings

        [Display(Name = "Numbers", GroupName = "Commonn Settings")]
        public bool IsNumbers
        {
            get => _isNumbers;
            set
            {
                _isNumbers = value;
                RecalculateValues();
                _up.VisualType = value ? VisualMode.DownArrow : VisualMode.Hide;
                _down.VisualType = value ? VisualMode.UpArrow : VisualMode.Hide;
            }
        }

        [Display(Name = "SR", GroupName = "Commonn Settings")]
        public bool IsSr { get => _isSr; set { _isSr = value; RecalculateValues(); } }

        [Display(Name = "Barcolor", GroupName = "Commonn Settings")]
        public bool IsBarcolor { get => _isBarcolor; set { _isBarcolor = value; RecalculateValues(); } }
        #endregion

        #region Bars Colors

        [Display(Name = "Buy", GroupName = "Bars Colors")]
        public Color BuyBarsColor
        {
            get => _buyBarsColor;
            set { _buyBarsColor = value; RecalculateValues(); }
        }

        [Display(Name = "Buy overshoot", GroupName = "Bars Colors")]
        public Color Buyovershoot
        {
            get => _buyovershoot;
            set { _buyovershoot = value; RecalculateValues(); }
        }

        [Display(Name = "Buy overshoot 1", GroupName = "Bars Colors")]
        public Color Buyovershoot1
        {
            get => _buyovershoot1;
            set { _buyovershoot1 = value; RecalculateValues(); }
        }

        [Display(Name = "Buy overshoot 2", GroupName = "Bars Colors")]
        public Color Buyovershoot2
        {
            get => _buyovershoot2;
            set { _buyovershoot2 = value; RecalculateValues(); }
        }

        [Display(Name = "Buy overshoot 3", GroupName = "Bars Colors")]
        public Color Buyovershoot3
        {
            get => _buyovershoot3;
            set { _buyovershoot3 = value; RecalculateValues(); }
        }

        [Display(Name = "Sell", GroupName = "Bars Colors")]
        public Color SellBarsColor { get => _sellBarsColor; set { _sellBarsColor = value; RecalculateValues(); } }

        [Display(Name = "Sell overshoot", GroupName = "Bars Colors")]
        public Color Sellovershoot { get => _sellovershoot; set { _sellovershoot = value; RecalculateValues(); } }

        [Display(Name = "Sell overshoot 1", GroupName = "Bars Colors")]
        public Color Sellovershoot1 { get => _sellovershoot1; set { _sellovershoot1 = value; RecalculateValues(); } }

        [Display(Name = "Sell overshoot 2", GroupName = "Bars Colors")]
        public Color Sellovershoot2 { get => _sellovershoot2; set { _sellovershoot2 = value; RecalculateValues(); } }

        [Display(Name = "Sell overshoot 3", GroupName = "Bars Colors")]
        public Color Sellovershoot3 { get => _sellovershoot3; set { _sellovershoot3 = value; RecalculateValues(); } }

        #endregion

        #endregion

        #region ctor
        public TDSequential() : base(true)
        {
            DenyToChangePanel = true;
            DataSeries[0].IsHidden = true;
            ((ValueDataSeries)DataSeries[0]).ShowZeroValue = false;
            _up.Color = _buyBarsColor;
            _down.Color = _sellBarsColor;
            DataSeries.Add(_colorBars);
            DataSeries.Add(_td);
            DataSeries.Add(_ts);
            DataSeries.Add(_up);
            DataSeries.Add(_down);
            DataSeries.Add(_sup);
            DataSeries.Add(_res);

            _up.PropertyChanged += (s, e) => { RecalculateValues(); };
            _down.PropertyChanged += (s, e) => { RecalculateValues(); };
        }

        #endregion

        #region Protected Mrthods

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar < BARS_NUM) return;

            NumbersCalc(bar);

        }


        #endregion

        #region Private Mrthods

        #region Numbers
        private void NumbersCalc(int bar)
        {
            var currCandle = GetCandle(bar);
            var candle = GetCandle(bar - BARS_NUM);

            if (currCandle.Close > candle.Close)
                _td[bar] = _td[bar - 1] + 1;
            else _td[bar] = 0;

            if (currCandle.Close < candle.Close)
                _ts[bar] = _ts[bar - 1] + 1;
            else _ts[bar] = 0;

            var tdUp = _td[bar] - GetValueCurrentSmallerPrev(bar, _td, 2);
            var tdDown = _ts[bar] - GetValueCurrentSmallerPrev(bar, _ts, 2);

            SetSignal(bar, currCandle, tdUp, _up, _down, 1);
            SetSignal(bar, currCandle, tdDown, _down, _up, -1);

            if (_isBarcolor)
            {
                SetBarsColor(tdUp, bar, _sellBarsColor, _sellovershoot, _sellovershoot1, _sellovershoot2, _sellovershoot3);
                SetBarsColor(tdDown, bar, _buyBarsColor, _buyovershoot, _buyovershoot1, _buyovershoot2, _buyovershoot3);
            }

            if (!_isSr) return;

            if (tdUp == MAX_SIGNAL_NUM)
            {
                _res.SetPointOfEndLine(bar - 1);
                _res[bar] = currCandle.High;
            }
            else
            {
                _res[bar] = _res[bar - 1];
            }

            if (tdDown == MAX_SIGNAL_NUM)
            {
                _sup.SetPointOfEndLine(bar - 1);
                _sup[bar] = currCandle.Low;
            }
            else
            {
                _sup[bar] = _sup[bar - 1];
            }
        }

        private void SetBarsColor(decimal td, int bar, Color color9, Color color13, Color color14, Color color15, Color color16)
        {
            switch (td)
            {
                case 9:
                    _colorBars[bar] = color9;
                    break;
                case 13:
                    _colorBars[bar] = color13;
                    break;
                case 14:
                    _colorBars[bar] = color14;
                    break;
                case 15:
                    _colorBars[bar] = color15;
                    break;
                case 16:
                    _colorBars[bar] = color16;
                    break;
            }
        }

        private void SetSignal(int bar, IndicatorCandle candle, decimal tdValue, ValueDataSeries series, ValueDataSeries altSeries, int direction)
        {
            if (tdValue < 1 || tdValue > MAX_SIGNAL_NUM) return;

            var markerPlace = direction > 0 ? candle.High : candle.Low;
            series[bar] = markerPlace;
            altSeries[bar] = 0;

            if (_isNumbers)
            {
                var textSize = 10;
                var offsetY = direction > 0 ? -textSize * 2 : textSize * 3;
                var color = direction > 0 ? Colors.Green : Colors.Red;

                AddText(bar.ToString(), tdValue.ToString(), true, bar, markerPlace, offsetY * series.Width, 0,
                   color.Convert(), System.Drawing.Color.Transparent, System.Drawing.Color.Transparent, textSize,
                   DrawingText.TextAlign.Center);
            }
        }

        private decimal GetValueCurrentSmallerPrev(int bar, ValueDataSeries series, int amount)
        {
            var count = 0;

            for (int i = bar; i > 0; i--)
            {
                if (series[i] < series[i - 1])
                {
                    count++;

                    if (count == amount)
                        return series[i];
                }
            }

            return series[0];
        }

        #endregion

        #region Drawing


        #endregion

        #endregion
    }
}
