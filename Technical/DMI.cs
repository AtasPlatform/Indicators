namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Dynamic Momentum Index")]
	public class DMI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _negDiff = new("Diff");
		private readonly ValueDataSeries _posDiff = new("Diff");
		private readonly ValueDataSeries _renderSeries = new("DMI");

		private readonly RSI _rsi = new();
		private readonly SMA _sma = new();
		private readonly StdDev _std = new();
		private int _rsiMax;
		private int _rsiMin;
		private int _rsiPeriod;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "RSI", Order = 100)]
		public int RsiPeriod
		{
			get => _rsiPeriod;
			set
			{
				if (value <= 0)
					return;

				_rsiPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MinPeriod", GroupName = "RSI", Order = 110)]
		public int RsiMin
		{
			get => _rsiMin;
			set
			{
				if (value <= 0)
					return;

				_rsiMin = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "MaxPeriod", GroupName = "RSI", Order = 120)]
		public int RsiMax
		{
			get => _rsiMax;
			set
			{
				if (value <= 0)
					return;

				_rsiMax = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "StdDev", Order = 200)]
		public int StdPeriod
		{
			get => _std.Period;
			set
			{
				if (value <= 0)
					return;

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
				if (value <= 0)
					return;

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
				return;

			var diff = (decimal)SourceDataSeries[bar] - (decimal)SourceDataSeries[bar - 1];
			_posDiff[bar] = diff > 0 ? diff : 0;
			_negDiff[bar] = diff < 0 ? -diff : 0;

			var vi = _std[bar] / _sma[bar];
			var td = RsiPeriod / vi;

			td = Math.Max(RsiMin, td);
			td = Math.Min(RsiMax, td);
			td = Math.Round(td);

			_rsi.Period = (int)td;

			_renderSeries[bar] = RsiDynamic(bar, (int)td);
		}

		#endregion

		#region Private methods

		private decimal RsiDynamic(int bar, int period)
		{
			var posSmma = _posDiff[0];
			var negSmma = _negDiff[0];

			for (var i = 1; i <= bar; i++)
			{
				posSmma = (posSmma * (period - 1) + _posDiff[i]) / period;
				negSmma = (negSmma * (period - 1) + _negDiff[i]) / period;
			}

			if (negSmma != 0)
			{
				var div = posSmma / negSmma;
				return div == 1 ? 0 : 100 - 100 / (1 + div);
			}

			return 100;
		}

		#endregion
	}
}