namespace ATAS.Indicators.Technical
{
    using System.ComponentModel.DataAnnotations;
    using System.Windows.Media;

    using ATAS.Indicators.Technical.Properties;

    public class HeatmapVolume : Indicator
    {
        private PaintbarsDataSeries _paint = new("paint");
        private ValueDataSeries _close = new("close")
        {
            VisualType = VisualMode.Histogram
        };

        private RangeDataSeries _range = new("range")
        {
            RangeColor = Colors.LightSeaGreen
        };

        private SMA _sma = new();
        private StdDev _stdDev = new();
        private decimal _thresholdExtraHigh = 4m;
        private decimal _thresholdHigh = 2.5m;
        private decimal _thresholdMedium = 1m;
        private decimal _thresholdNormal = -0.5m;
        private bool _showAsOscillator;

        private Highest _highestV = new();
        private Lowest _lowestV = new();
        private bool _coloredBars = true;
        private Color _heatmapExtraHigh = Colors.Red;
        private Color _heatmapHigh = Colors.Orange;
        private Color _heatmapMedium = Colors.Yellow;
        private Color _heatmapNormal = Colors.LightSkyBlue;
        private Color _heatmapLow = Colors.DodgerBlue;
        private Color _upExtraHigh = Colors.LawnGreen;
        private Color _upHigh = Colors.LimeGreen;
        private Color _upMedium = Colors.Green;
        private Color _upNormal = Colors.SeaGreen;
        private Color _upLow = Colors.LightGreen;
        private Color _downExtraHigh = Colors.Red;
        private Color _downHigh = Color.FromRgb(255,50,50);
        private Color _downMedium = Color.FromRgb(255, 100, 100);
        private Color _downNormal = Color.FromRgb(255, 150, 150);
        private Color _downLow = Color.FromRgb(255, 200, 200);

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "SMA", Order = 100)]
        public int SmaPeriod
        {
            get => _sma.Period;
            set
            {
                _sma.Period = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 110)]
        public int StdPeriod
        {
            get => _stdDev.Period;
            set
            {
                _stdDev.Period = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "ExtraHighVolumeThreshold", GroupName = "Settings", Order = 200)]
        public decimal ThresholdExtraHigh
        {
            get => _thresholdExtraHigh;
            set
            {
                _thresholdExtraHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "HighVolumeThreshold", GroupName = "Settings", Order = 210)]
        public decimal ThresholdHigh
        {
            get => _thresholdHigh;
            set
            {
                _thresholdHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "MediumVolumeThreshold", GroupName = "Settings", Order = 220)]
        public decimal ThresholdMedium
        {
            get => _thresholdMedium;
            set
            {
                _thresholdMedium = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "NormalVolumeThreshold", GroupName = "Settings", Order = 230)]
        public decimal ThresholdNormal
        {
            get => _thresholdNormal;
            set
            {
                _thresholdNormal = value;
                RecalculateValues();
            }
        }


        [Display(ResourceType = typeof(Resources), Name = "ShowAsOscillator", GroupName = "Settings", Order = 240)]
        public bool ShowAsOscillator
        {
            get => _showAsOscillator;
            set
            {
                _showAsOscillator = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "PaintBars", GroupName = "Visualization", Order = 300)]
        public bool ColoredBars
        {
            get => _coloredBars;
            set
            {
                _coloredBars = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "Heatmap", Order = 310)]
        public Color HeatmapExtraHigh
        {
            get => _heatmapExtraHigh;
            set
            {
	            _heatmapExtraHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "High", GroupName = "Heatmap", Order = 320)]
        public Color HeatmapHigh
        {
            get => _heatmapHigh;
            set
            {
	            _heatmapHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "Heatmap", Order = 330)]
        public Color HeatmapMedium
        {
            get => _heatmapMedium;
            set
            {
	            _heatmapMedium = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "Heatmap", Order = 340)]
        public Color HeatmapNormal
        {
            get => _heatmapNormal;
            set
            {
	            _heatmapNormal = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "Heatmap", Order = 350)]
        public Color HeatmapLow
        {
            get => _heatmapLow;
            set
            {
	            _heatmapLow = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "UpColor", Order = 400)]
        public Color UpExtraHigh
        {
            get => _upExtraHigh;
            set
            {
	            _upExtraHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "High", GroupName = "UpColor", Order = 410)]
        public Color UpHigh
        {
            get => _upHigh;
            set
            {
	            _upHigh = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "UpColor", Order = 420)]
        public Color UpMedium
        {
            get => _upMedium;
            set
            {
                _upMedium = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "UpColor", Order = 430)]
        public Color UpNormal
        {
            get => _upNormal;
            set
            {
	            _upNormal = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "UpColor", Order = 440)]
        public Color UpLow
        {
            get => _upLow;
            set
            {
                _upLow = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "ExtraHigh", GroupName = "DownColor", Order = 500)]
        public Color DownExtraHigh
        {
	        get => _downExtraHigh;
	        set
	        {
		        _downExtraHigh = value;
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "High", GroupName = "DownColor", Order = 510)]
        public Color DownHigh
        {
	        get => _downHigh;
	        set
	        {
                _downHigh = value;
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Medium", GroupName = "DownColor", Order = 520)]
        public Color DownMedium
        {
	        get => _downMedium;
	        set
	        {
                _downMedium = value;
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Normal", GroupName = "DownColor", Order = 530)]
        public Color DownNormal
        {
	        get => _downNormal;
	        set
	        {
                _downNormal = value;
		        RecalculateValues();
	        }
        }

        [Display(ResourceType = typeof(Resources), Name = "Low", GroupName = "DownColor", Order = 540)]
        public Color DownLow
        {
	        get => _downLow;
	        set
	        {
                _downLow = value;
		        RecalculateValues();
	        }
        }

        public HeatmapVolume()
            : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);

            _highestV.Period = _lowestV.Period = 300;

            DataSeries[0] = _range;

            DataSeries.Add(_close);
            DataSeries.Add(_paint);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
            }


            var candle = GetCandle(bar);
            var mean = _sma.Calculate(bar, candle.Volume);
            var std = _stdDev.Calculate(bar, candle.Volume);
            var stdBar = (candle.Volume - mean) / std;

            var dir = candle.Close > candle.Open;
            var v = ShowAsOscillator ? candle.Volume - mean : candle.Volume;
            var mosc = ShowAsOscillator ? 0 : mean;

            var tst = _highestV.Calculate(bar, v) * 9999;
            var ts0 = _lowestV.Calculate(bar, v) * 9999;
            var ts1 = std * ThresholdExtraHigh + mosc;
            var ts2 = std * ThresholdHigh + mosc;
            var ts3 = std * ThresholdMedium + mosc;
            var ts4 = std * ThresholdNormal + mosc;

            if (ColoredBars)
            {
                var barColor = stdBar > thresholdExtraHigh
                    ? dir
                        ? cthresholdExtraHighUp
                        : cthresholdExtraHighDn
                    : stdBar > thresholdHigh
                        ? dir
                            ? cthresholdHighUp
                            : cthresholdHighDn
                        : stdBar > thresholdMedium
                            ? dir
                                ? cthresholdMediumUp
                                : cthresholdMediumDn
                            : stdBar > thresholdNormal
                                ? dir
                                    ? cthresholdNormalUp
                                    : cthresholdNormalDn
                                : dir
                                    ? cthresholdLowUp
                                    : cthresholdLowDn;
            }

        }
    }
}
