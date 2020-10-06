

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
    using Utils.Common.Logging;

    [DisplayName("RelativeVolume")]
    [LocalizedDescription(typeof(Resources), "RelativeVolume")]
    public class RelativeVolume : Indicator
    {
        #region Fields
        private readonly SMA _sma = new SMA();
        private readonly ValueDataSeries _negative;
        private readonly ValueDataSeries _neutral;
        private readonly ValueDataSeries _positive;
        private readonly ValueDataSeries _averagePoints;
        private Dictionary<TimeSpan,AvgBar> _avgVolumes = new Dictionary<TimeSpan, AvgBar>();
        private int _lookBack;
        #endregion

        #region Properties

        [Parameter]        
        public int LookBack
        {
            get => _lookBack;
            set
            {                
                if (value <= 0)
                    return;

                _lookBack = value;
                RecalculateValues();      
            }
        }
        #endregion

        #region ctor
        public RelativeVolume():base(true)
        {
            LookBack = 20;
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
           
            var currentCandle = GetCandle(bar);
            var time = currentCandle.Time.TimeOfDay;
            var candleVolume = currentCandle.Volume;


            if (!_avgVolumes.ContainsKey(currentCandle.Time.TimeOfDay))            
            {
                _avgVolumes.Add(time, new AvgBar(LookBack));
            }

            _avgVolumes[currentCandle.Time.TimeOfDay].Add(candleVolume);

            
            
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


            _averagePoints[bar] = _avgVolumes[currentCandle.Time.TimeOfDay].Avg();
        }
        #endregion

        private class AvgBar
        {
            private int _lookBack;

            public Queue<decimal> Volume = new Queue<decimal>();
            
            public AvgBar(int lookBack)
            {
                _lookBack = lookBack;
            }

            public void Add(decimal volume)
            {
                Volume.Enqueue(volume);
                if (Volume.Count > _lookBack)
                    Volume.Dequeue();
            }

            public decimal Avg()
            {
                
                decimal sum = 0;

                foreach(var vol in Volume)
                { 
                    sum += vol;
                    
                }

                return sum / Volume.Count;
            }
        }
    }
}
