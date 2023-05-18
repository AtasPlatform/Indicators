using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators;
using ATAS.Indicators.Drawing;
using ATAS.Indicators.Technical.Properties;

using Color = System.Drawing.Color;

namespace ATAS.Indicators.Technical
{
    [DisplayName("Cumulative Daily Volume")]
    public class CumulativeDailyVolume : Indicator
    {
        #region Fields

        private readonly ValueDataSeries _data = new(Resources.Data)
        {
            IsHidden = true,
            VisualType = VisualMode.Histogram,
            ShowZeroValue = false
        };

        private int _lastBar = -1;
        private decimal _sum;
        private Color _histogramColor = DefaultColors.Blue;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Color", GroupName = "Visualization")]
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

        public CumulativeDailyVolume() : base(true)
        {
            Panel = IndicatorDataProvider.NewPanel;
            DenyToChangePanel = true;

            DataSeries[0] = _data;
            _data.Color = _histogramColor.Convert();
        }

        #endregion

        #region Protected Methods

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar != _lastBar)
            {
                if (IsNewSession(bar) || bar == 0)
                    _sum = 0;
                else
                    _sum = _data[bar - 1];
            }

            var candle = GetCandle(bar);
            var sum = _sum + candle.Volume;
            _data[bar] = sum;

            _lastBar = bar;
        }

        #endregion
    }
}
