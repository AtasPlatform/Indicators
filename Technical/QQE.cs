namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Qualitative Quantitative Estimation")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38311-qualitative-quantitative-estimation-indicator")]
	public class QQE : Indicator
	{
		#region Static and constants

		private const decimal QQEmultiplier = 4.236m;

		#endregion

		#region Fields

		private readonly EMA _emaAtrRsi = new();
		private readonly EMA _emaWilders = new();
		private int _lastBar = -1;

		private readonly RSI _rsi = new() { Period = 14 };
		private readonly EMA _rsiEma = new() { Period = 5 };
		private readonly ValueDataSeries _rsiMa = new("RsiMa")
		{
			Color = Colors.DarkBlue, 
			Width = 2
		};
		private readonly ValueDataSeries _trLevelSlow = new("LevelSlow")
		{
			Color = Colors.DodgerBlue,
			LineDashStyle = LineDashStyle.Dash,
			IgnoredByAlerts = true
		};

		private bool _lastBarCounted;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "UseAlerts", GroupName = "Alerts", Order = 0)]
		public bool UseAlerts { get; set; }

		[Display(ResourceType = typeof(Resources), Name = "AlertFile", GroupName = "Alerts", Order = 1)]
		public string AlertFile { get; set; } = "alert1";

        [Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Common")]
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

		[Display(ResourceType = typeof(Resources), Name = "SlowFactor", GroupName = "Common")]
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

		#endregion

		#region ctor

		public QQE()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_emaWilders.Period = _emaAtrRsi.Period = _rsi.Period * 2 - 1;
			
			DataSeries[0] = _trLevelSlow;
			DataSeries.Add(_rsiMa);

			LineSeries.Add(new LineSeries("TargetLevel")
			{
				Value = 50,
				Color = Colors.Aqua
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