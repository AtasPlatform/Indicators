using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ATAS.Indicators.Drawing;
using OFT.Attributes;
using OFT.Localization;
using Color = System.Drawing.Color;

namespace ATAS.Indicators.Technical
{
    [DisplayName("Cumulative Daily Volume")]
    [Category("Bid x Ask,Delta,Volume")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CumulativeDailyVolumeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000618670")]
    public class CumulativeDailyVolume : Indicator
    {
        #region Fields

        private readonly ValueDataSeries _data = new("Data", Strings.Volume)
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
