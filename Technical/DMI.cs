namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Dynamic Momentum Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/49350-dynamic-momentum-index")]
	public class DMI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _negDiff = new("Diff");
		private readonly ValueDataSeries _posDiff = new("Diff");
		private readonly ValueDataSeries _renderSeries = new("DMI");

		private readonly SMA _sma = new();
		private readonly StdDev _std = new();
		private int _lastBar;
		private decimal _negSmma;
		private decimal _posSmma;
		private decimal _prevNegSmma;
		private decimal _prevPosSmma;
		private int _rsiMax;
		private int _rsiMin;
		private int _rsiPeriod;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "RSI", Order = 100)]
		[Range(1, 10000)]
		public int RsiPeriod
		{
			get => _rsiPeriod;
			set
			{
				_rsiPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinPeriod", GroupName = "RSI", Order = 110)]
		[Range(1, 10000)]
		public int RsiMin
		{
			get => _rsiMin;
			set
			{
				_rsiMin = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaxPeriod", GroupName = "RSI", Order = 120)]
		[Range(1, 10000)]
		public int RsiMax
		{
			get => _rsiMax;
			set
			{
				_rsiMax = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 200)]
		[Range(1, 10000)]
		public int StdPeriod
		{
			get => _std.Period;
			set
			{
				_std.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "StdDev", Order = 210)]
		public int SmaPeriod
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DMI()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_rsiMin = 3;
			_rsiMax = 30;
			_rsiPeriod = 14;

			_std.Period = 5;
			_sma.Period = 10;

			_renderSeries.Color = Colors.Blue;
			_renderSeries.Width = 2;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_std.Calculate(bar, value);
			_sma.Calculate(bar, _std[bar]);

			if (bar == 0)
			{
				_posSmma = _negSmma = 0;
				_prevPosSmma = _prevNegSmma = 0;
				return;
			}

			var diff = (decimal)SourceDataSeries[bar] - (decimal)SourceDataSeries[bar - 1];
			_posDiff[bar] = diff > 0 ? diff : 0;
			_negDiff[bar] = diff < 0 ? -diff : 0;

			if (_sma[bar] == 0 || _std[bar] == 0)
			{
				_renderSeries[bar] = _renderSeries[bar - 1];
				_lastBar = bar;
				return;
			}

			var vi = _std[bar] / _sma[bar];
			var td = RsiPeriod / vi;

			td = Math.Max(RsiMin, td);
			td = Math.Min(RsiMax, td);
			td = Math.Round(td);

			_renderSeries[bar] = RsiDynamic(bar, (int)td);

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private decimal RsiDynamic(int bar, int period)
		{
			_posSmma = (_prevPosSmma * (period - 1) + _posDiff[bar]) / period;
			_negSmma = (_prevNegSmma * (period - 1) + _negDiff[bar]) / period;

			if (_lastBar != bar)
			{
				_prevPosSmma = _posSmma;
				_prevNegSmma = _negSmma;
			}

			if (_negSmma != 0)
			{
				if (_negSmma == 0)
					return 0;

				var div = _posSmma / _negSmma;
				return div == 1 ? 0 : 100 - 100 / (1 + div);
			}

			return 100;
		}

		#endregion
	}
}