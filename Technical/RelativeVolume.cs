

namespace ATAS.Indicators.Technical
{
    using ATAS.Indicators.Properties;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Windows.Media;
    using Utils.Common.Localization;

    [DisplayName("RelativeVolume")]
    [LocalizedDescription(typeof(Resources), "RelativeVolume")]
    public class RelativeVolume : Indicator
    {
        #region Fields
        private readonly ValueDataSeries _negative;
        private readonly ValueDataSeries _neutral;
        private readonly ValueDataSeries _positive;
        private readonly ValueDataSeries _averagePoints;
        private Queue<decimal> _periodVolume = new Queue<decimal>();
        private int _period=10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Resources),
            Name = "Period",
            GroupName = "Common",
            Order = 1)]
        public int Period
        {
            get => _period;
            set
            {                
                if (value <= 0)
                    return;
                
                _period = value;
                _periodVolume.Clear();
                RecalculateValues();                
            }
        }
        #endregion

        #region ctor
        public RelativeVolume():base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            
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
                Width=2,                
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
            var candle = GetCandle(bar);

            if (candle.Delta > 0)
            {
                _positive[bar] = candle.Ticks;
                _negative[bar] = _neutral[bar] = 0;
            }
            else if (candle.Delta < 0)
            {
                _negative[bar] = candle.Ticks;
                _positive[bar] = _neutral[bar] = 0;
            }
            else
            {
                _negative[bar] = candle.Ticks;
                _positive[bar] = _neutral[bar] = 0;
            }     

            if (_periodVolume.Count > Period)
                _periodVolume.Dequeue();

            if (_periodVolume.Any())
            {
                var avgVolume = _periodVolume.Average();
                _averagePoints[bar] = avgVolume;
            }

            _periodVolume.Enqueue(candle.Ticks);
        }
        #endregion
    }
}
