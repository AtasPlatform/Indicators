namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	[DisplayName("Schaff Trend Cycle")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/38254-schaff-trend-cycle")]
	public class SchaffTrendCycle : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _f1Series = new("f1");
		private readonly ValueDataSeries _f2Series = new("f2");

		private readonly Highest _highestMacd = new();
		private readonly Highest _highestPf = new();
		private readonly EMA _longMa = new();
		private readonly Lowest _lowestMacd = new();
		private readonly Lowest _lowestPf = new();
		private readonly MACD _macd = new();
		private readonly ValueDataSeries _pffSeries = new("pff");
		private readonly ValueDataSeries _pfSeries = new("pf");
		private readonly EMA _shortMa = new();

		private int _lastBar;

		private decimal _lastF1;
		private decimal _lastF2;
		private decimal _lastPf;
		private decimal _lastPff;
		private bool _drawLines = true;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Common")]
		public int Period
		{
			get => _highestMacd.Period;
			set
			{
				if (value <= 0)
					return;

				_highestMacd.Period = value;
				_lowestMacd.Period = value;
				_highestPf.Period = value;
				_lowestPf.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Common")]
		public int ShortPeriod
		{
			get => _shortMa.Period;
			set
			{
				if (value <= 0)
					return;

				_shortMa.Period = value;
				_macd.ShortPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Common")]
		public int LongPeriod
		{
			get => _longMa.Period;
			set
			{
				if (value <= 0)
					return;

				_longMa.Period = value;
				_macd.LongPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Show",
			GroupName = "Line",
			Order = 30)]
		public bool DrawLines
		{
			get => _drawLines;
			set
			{
				_drawLines = value;

				if (value)
				{
					if (LineSeries.Contains(UpLine))
						return;

					LineSeries.Add(UpLine);
					LineSeries.Add(DownLine);
				}
				else
				{
					LineSeries.Clear();
				}

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources),
			Name = "Up",
			GroupName = "Line",
			Order = 30)]
		public LineSeries UpLine { get; set; } = new("Up")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 75,
			Width = 1,
			IsHidden = true
		};

		[Display(ResourceType = typeof(Resources),
			Name = "Down",
			GroupName = "Line",
			Order = 30)]

		public LineSeries DownLine { get; set; } = new("Down")
		{
			Color = Colors.Orange,
			LineDashStyle = LineDashStyle.Dash,
			Value = 25,
			Width = 1,
			IsHidden = true
		};

        #endregion

        #region ctor

        public SchaffTrendCycle()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_lastBar = -1;
			_longMa.Period = _macd.LongPeriod = 50;
			_shortMa.Period = _macd.ShortPeriod = 23;
			_highestMacd.Period = _lowestMacd.Period = 10;
			_highestPf.Period = _lowestPf.Period = 10;

			LineSeries.Add(UpLine);
			LineSeries.Add(DownLine);
			((ValueDataSeries)DataSeries[0]).Width = 2;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar != _lastBar && bar > 0)
			{
				_lastBar = bar;

				_lastF1 = _f1Series[bar - 1];
				_lastF2 = _f2Series[bar - 1];
				_lastPf = _pfSeries[bar - 1];
				_lastPff = _pffSeries[bar - 1];
			}

			var candle = GetCandle(bar);

			var macd = _macd.Calculate(bar, candle.Close);

			var v1 = _lowestMacd.Calculate(bar, macd);
			var v2 = _highestMacd.Calculate(bar, macd) - v1;

			_f1Series[bar] = v2 > 0
				? (macd - v1) / v2 * 100.0m
				: _lastF1;

			_pfSeries[bar] = _lastPf == 0
				? _f1Series[bar]
				: _lastPf + 0.5m * (_f1Series[bar] - _lastPf);

			var v3 = _lowestPf.Calculate(bar, _pfSeries[bar]);
			var v4 = _highestPf.Calculate(bar, _pfSeries[bar]) - v3;

			_f2Series[bar] = v4 > 0
				? (_pfSeries[bar] - v3) / v4 * 100m
				: _lastF2;

			_pffSeries[bar] = _lastPff == 0
				? _f2Series[bar]
				: _lastPff + 0.5m * (_f2Series[bar] - _lastPff);

			this[bar] = _pffSeries[bar];
		}

		#endregion
	}
}