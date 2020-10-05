namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Windows.Media;
    using Properties;
    using Utils.Common.Localization;

    [DisplayName("Relative Volume")]
    [LocalizedDescription(typeof(Resources), "RelativeVolume")]
    public class RelativeVolume : Indicator
    {
        #region Fields

        private readonly SMA _sma = new SMA();
        private readonly ValueDataSeries _averagePoints;
        private readonly ValueDataSeries _negative;
        private readonly ValueDataSeries _neutral;
        private readonly ValueDataSeries _positive;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Resources),
            Name = "Period",
            GroupName = "Common",
            Order = 1)]
        public int Period
        {
            get => _sma.Period;
            set
            {
                if (value <= 0)
                    return;

                _sma.Period = value;
                RecalculateValues();
            }
        }

        #endregion

        #region ctor

        public RelativeVolume() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            _sma.Period = 10;

            _positive = new ValueDataSeries("Positive")
            {
                VisualType = VisualMode.Histogram,
                Color = Colors.Green,
                ShowZeroValue = false
            };

            _negative = new ValueDataSeries("Negative")
            {
                VisualType = VisualMode.Histogram,
                Color = Colors.Red,
                ShowZeroValue = false
            };

            _neutral = new ValueDataSeries("Neutral")
            {
                VisualType = VisualMode.Histogram,
                Color = Colors.Gray,
                ShowZeroValue = false
            };

            _averagePoints = new ValueDataSeries("AveragePoints")
            {
                VisualType = VisualMode.Dots,
                Color = Colors.Orange,
                Width = 2,
                ShowZeroValue = false
            };

            DataSeries[0] = _positive;
            DataSeries.Add(_negative);
            DataSeries.Add(_neutral);
            DataSeries.Add(_averagePoints);
        }

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
        {
            var currentCandle = GetCandle(bar);

            var candleVolume = currentCandle.Volume;

            if (currentCandle.Delta > 0)
            {
                _positive[bar] = candleVolume;
                _negative[bar] = _neutral[bar] = 0;
            }
            else if (currentCandle.Delta < 0)
            {
                _negative[bar] = candleVolume;
                _positive[bar] = _neutral[bar] = 0;
            }
            else
            {
                _negative[bar] = candleVolume;
                _positive[bar] = _neutral[bar] = 0;
            }

            var avgValue = _sma.Calculate(bar, candleVolume);
            _averagePoints[bar] = avgValue;
        }

        #endregion
    }
}