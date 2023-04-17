namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Connie Brown Composite Index")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43360-connie-brown-composite-index")]
	public class CBI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _cbi1Series = new(Resources.ShortPeriod) { IgnoredByAlerts = true };
		private readonly ValueDataSeries _cbi2Series = new(Resources.MiddleBand);
        private readonly ValueDataSeries _cbi3Series = new(Resources.LongPeriod) { IgnoredByAlerts = true };
        private readonly Momentum _momentum = new();

		private readonly RSI _rsi1 = new();
		private readonly RSI _rsi2 = new();
		private readonly SMA _sma1 = new();
		private readonly SMA _sma2 = new();
		private readonly SMA _sma3 = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "RSI", Order = 100)]
		public int Rsi1Period
		{
			get => _rsi1.Period;
			set
			{
				if (value <= 0)
					return;

				_rsi1.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "RSI", Order = 110)]
		public int Rsi2Period
		{
			get => _rsi2.Period;
			set
			{
				if (value <= 0)
					return;

				_rsi2.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Momentum", Order = 200)]
		public int MomentumPeriod
		{
			get => _momentum.Period;
			set
			{
				if (value <= 0)
					return;

				_momentum.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "SMA", Order = 300)]
		public int Sma1Period
		{
			get => _sma1.Period;
			set
			{
				if (value <= 0)
					return;

				_sma1.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "SMA", Order = 310)]
		public int Sma2Period
		{
			get => _sma2.Period;
			set
			{
				if (value <= 0)
					return;

				_sma2.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod3", GroupName = "SMA", Order = 320)]
		public int Sma3Period
		{
			get => _sma3.Period;
			set
			{
				if (value <= 0)
					return;

				_sma3.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CBI()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_momentum.Period = 9;
			_rsi1.Period = 3;
			_rsi2.Period = 14;

			_sma1.Period = 3;
			_sma2.Period = 13;
			_sma3.Period = 33;

			_cbi1Series.Color = DefaultColors.Red.Convert();
			_cbi2Series.Color = DefaultColors.Orange.Convert();
			_cbi3Series.Color = DefaultColors.Green.Convert();

			DataSeries[0] = _cbi1Series;
			DataSeries.Add(_cbi2Series);
			DataSeries.Add(_cbi3Series);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_cbi1Series[bar] = _momentum.Calculate(bar, _rsi1.Calculate(bar, value)) + _sma1.Calculate(bar, _rsi2.Calculate(bar, value));
			_cbi2Series[bar] = _sma2.Calculate(bar, _cbi1Series[bar]);
			_cbi3Series[bar] = _sma3.Calculate(bar, _cbi1Series[bar]);
		}

		#endregion
	}
}