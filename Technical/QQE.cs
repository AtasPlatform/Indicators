using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Technical.Properties;

    [DisplayName("Qualitative Quantitative Estimation")]
    public class QQE : Indicator
    {
        private readonly RSI _rsi = new RSI();
        private readonly EMA _ema = new EMA();
        private readonly EMA _emaWilders = new EMA();
        private readonly EMA _emaAtrRsi = new EMA();
        private readonly ValueDataSeries _trLevelSlow = new ValueDataSeries("LevelSlow");
        private readonly ValueDataSeries _rsiMa = new ValueDataSeries("RsiMa");


        [Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Common")]
        public int RsiPeriod
        {
            get => _rsi.Period;
            set
            {
                if (value <= 0)
                    return;

                _rsi.Period = value;
                _emaWilders.Period = value * 2 - 1;
                _emaAtrRsi.Period = value * 2 - 1;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Resources), Name = "SlowFactor", GroupName = "Common")]
        public int SlowFactor
        {
            get => _ema.Period;
            set
            {
                if (value <= 0)
                    return;

                _ema.Period = value;
                RecalculateValues();
            }
        }

        public QQE()
        {
	        Panel = IndicatorDataProvider.NewPanel;

            _ema.Period = 5;
            _rsi.Period = 14;
            _emaWilders.Period = _emaAtrRsi.Period = _rsi.Period * 2 - 1;

            _trLevelSlow.LineDashStyle = LineDashStyle.Dash;
            DataSeries[0] = _trLevelSlow;
            DataSeries.Add(_rsiMa);
        }

        protected override void OnCalculate(int bar, decimal value)
        {
            if (_emaWilders.Period < SlowFactor && bar < SlowFactor
            || _emaWilders.Period >= SlowFactor && bar < _emaWilders.Period)
                return;

            var candle = GetCandle(bar);

            var rsiValue = _rsi.Calculate(bar, candle.Close);

            var emaRsiValue = _ema.Calculate(bar, rsiValue);
            _rsiMa[bar] = Math.Abs(_ema[bar - 1] - emaRsiValue);


            var maAtrRsi = _emaWilders.Calculate(bar, _rsiMa[bar]);

            var tr = _trLevelSlow[bar - 1];

            var rsi1 = _rsi[bar - 1];

            var dar = _emaAtrRsi.Calculate(bar - 1, maAtrRsi) * 4.236m;

            var dv = tr;

            if (rsi1 < tr)
            {
                tr = rsi1 + dar;

                if (rsi1 < dv && tr > dv)
	                tr = dv;
            }
            else if (rsi1 > tr)
            {
	            tr = rsi1 - dar;

	            if (rsi1 > dv && tr < dv)
		            tr = dv;
            }

            _trLevelSlow[bar - 1] = tr;

        }

    }
}
