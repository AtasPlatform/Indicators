namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("AC DC Histogram")]
	public class ACDC : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _ao = new ValueDataSeries("AO");

		private readonly ValueDataSeries _averPrice = new ValueDataSeries("Price");
		private readonly ValueDataSeries _downSeries = new ValueDataSeries("Down");

		private readonly SMA _sma1 = new SMA();
		private readonly SMA _sma2 = new SMA();
		private readonly SMA _sma3 = new SMA();
		private readonly SMA _sma4 = new SMA();

		private readonly ValueDataSeries _upSeries = new ValueDataSeries("Up");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod1", GroupName = "Settings", Order = 100)]
		public int SmaPeriod1
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

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod2", GroupName = "Settings", Order = 110)]
		public int SmaPeriod2
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

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod3", GroupName = "Settings", Order = 120)]
		public int SmaPeriod3
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

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod4", GroupName = "Settings", Order = 130)]
		public int SmaPeriod4
		{
			get => _sma4.Period;
			set
			{
				if (value <= 0)
					return;

				_sma4.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ACDC()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			_sma1.Period = 34;
			_sma2.Period = 5;
			_sma3.Period = 10;
			_sma4.Period = 5;

			_upSeries.VisualType = _downSeries.VisualType = VisualMode.Histogram;
			_upSeries.IsHidden = _downSeries.IsHidden = true;
			_upSeries.ShowZeroValue = _downSeries.ShowZeroValue = false;
			_upSeries.Color = Colors.Green;
			_downSeries.Color = Colors.Red;

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);
			_averPrice[bar] = (candle.High + candle.Low) / 2;
			_sma1.Calculate(bar, _averPrice[bar]);
			_sma2.Calculate(bar, _averPrice[bar]);

			_ao[bar] = _sma2[bar] - _sma1[bar];

			_sma4.Calculate(bar, _ao[bar]);
			_sma3.Calculate(bar, _ao[bar] - _sma4[bar]);

			if (bar > 0)
			{
				var lastValue = _upSeries[bar - 1] != 0 ? _upSeries[bar - 1] : _downSeries[bar - 1];

				if (_sma3[bar] - lastValue > 0)
					_upSeries[bar] = _sma3[bar];
				else if (_sma3[bar] - lastValue < 0)
					_downSeries[bar] = _sma3[bar];
				else if (_upSeries[bar - 1] != 0)
					_upSeries[bar] = _sma3[bar];
				else
					_downSeries[bar] = _sma3[bar];
			}
			else
			{
				if (_sma3[bar] > 0)
					_upSeries[bar] = _sma3[bar];
				else
					_downSeries[bar] = _sma3[bar];
			}
		}

		#endregion
	}
}