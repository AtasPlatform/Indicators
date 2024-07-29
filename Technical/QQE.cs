﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

	[DisplayName("Qualitative Quantitative Estimation")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.QQEDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602629")]
	public class QQE : Indicator
	{
		#region Static and constants

		private const decimal QQEmultiplier = 4.236m;

		#endregion

		#region Fields

		private readonly EMA _emaAtrRsi = new();
		private readonly EMA _emaWilders = new();
		private readonly RSI _rsi = new() { Period = 14 };
		private readonly EMA _rsiEma = new() { Period = 5 };

		private readonly ValueDataSeries _rsiMa = new("RsiMaId", "RsiMa")
		{
			Color = DefaultColors.Navy.Convert(), 
			Width = 2,
            DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
        };

		private readonly ValueDataSeries _trLevelSlow = new("TrLevelSlow", "LevelSlow")
		{
			Color = System.Drawing.Color.DodgerBlue.Convert(),
			LineDashStyle = LineDashStyle.Dash,
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.EMALineSettingsDescription)
        };

        private int _lastBar = -1;
        private bool _lastBarCounted;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RSI), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription))]
        [Range(1, 10000)]
        public int RsiPeriod
        {
            get => _rsi.Period;
            set
            {
                _rsi.Period = value;
                _emaWilders.Period = value * 2 - 1;
                _emaAtrRsi.Period = value * 2 - 1;
                RecalculateValues();
            }
        }

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SlowFactor), GroupName = nameof(Strings.Settings), Description = nameof(Strings.EMAPeriodDescription))]
        [Range(1, 10000)]
        public int SlowFactor
        {
            get => _rsiEma.Period;
            set
            {
                _rsiEma.Period = value;
                RecalculateValues();
            }
        }

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.UseAlerts), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.UseAlertsDescription), Order = 0)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.AlertFile), GroupName = nameof(Strings.Alerts), Description = nameof(Strings.AlertFileDescription), Order = 1)]
		public string AlertFile { get; set; } = "alert1";

		#endregion

		#region ctor

		public QQE()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_emaWilders.Period = _emaAtrRsi.Period = _rsi.Period * 2 - 1;
			
			DataSeries[0] = _trLevelSlow;
			DataSeries.Add(_rsiMa);

			LineSeries.Add(new LineSeries("TargetLevelId", "TargetLevel")
			{
				Value = 50,
				Color = System.Drawing.Color.Aqua.Convert()
			});
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_lastBarCounted = false;
				return;
			}

			var candle = GetCandle(bar);

			var rsi = _rsi.Calculate(bar, candle.Close);
			var rsiMa = _rsiEma.Calculate(bar, rsi);
			var atrRsi = Math.Abs(_rsiEma[bar - 1] - rsiMa);
			var maAtrRsi = _emaWilders.Calculate(bar, atrRsi);

			if (_emaWilders.Period < SlowFactor && bar < SlowFactor
				|| _emaWilders.Period >= SlowFactor && bar < _emaWilders.Period)
				return;

			_trLevelSlow[bar] = _trLevelSlow[bar - 1];
			_rsiMa[bar] = rsiMa;

			var dar = _emaAtrRsi.Calculate(bar, maAtrRsi) * QQEmultiplier;

			var dv = _trLevelSlow[bar];

			if (rsiMa < _trLevelSlow[bar])
			{
				_trLevelSlow[bar] = rsiMa + dar;

				if (_rsiEma[bar - 1] < dv && _trLevelSlow[bar] > dv)
					_trLevelSlow[bar] = dv;
			}
			else if (rsiMa > _trLevelSlow[bar])
			{
				_trLevelSlow[bar] = rsiMa - dar;

				if (_rsiEma[bar - 1] > dv && _trLevelSlow[bar] < dv)
					_trLevelSlow[bar] = dv;
			}

			if (_lastBar != bar)
			{
				if (_lastBarCounted && UseAlerts)
				{
					if (_rsiMa[bar - 1] < LineSeries[0].Value && _rsiMa[bar - 2] > LineSeries[0].Value
						||
						_rsiMa[bar - 1] > LineSeries[0].Value && _rsiMa[bar - 2] < LineSeries[0].Value
					)
						AddAlert(AlertFile, "Target level is achieved");
				}

				_lastBar = bar;
			}
			else
			{
				if (!_lastBarCounted)
					_lastBarCounted = true;
			}
		}

		#endregion
	}
}