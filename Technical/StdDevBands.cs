namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Standard Deviation Bands")]
	public class StdDevBands : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _botSeries = new ValueDataSeries(Resources.BottomBand);
		private readonly Highest _highest = new Highest();
		private readonly Lowest _lowest = new Lowest();
		private readonly StdDev _stdHigh = new StdDev();
		private readonly StdDev _stdLow = new StdDev();

		private readonly ValueDataSeries _topSeries = new ValueDataSeries(Resources.TopBand);
		private readonly ValueDataSeries _smaBotSeries = new ValueDataSeries(Resources.SMA1);
		private readonly ValueDataSeries _smaTopSeries = new ValueDataSeries(Resources.SMA2);

		private readonly SMA _smaHigh = new SMA();
		private readonly SMA _smaLow = new SMA();
		private int _width;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _stdHigh.Period;
			set
			{
				if (value <= 0)
					return;

				_stdHigh.Period = _stdLow.Period = _highest.Period = _lowest.Period =
					_smaHigh.Period = _smaLow.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "Settings", Order = 110)]
		public int SmaPeriod
		{
			get => _width;
			set
			{
				if (value <= 0)
					return;

				_width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StdDevBands()
		{
			_stdHigh.Period = _stdLow.Period = _highest.Period = _lowest.Period =
				_smaHigh.Period = _smaLow.Period = 10;
			_width = 2;

			_topSeries.Color = _botSeries.Color = Colors.DodgerBlue;
			DataSeries[0] = _topSeries;
			DataSeries.Add(_botSeries);
			DataSeries.Add(_smaTopSeries);
			DataSeries.Add(_smaBotSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var high = _highest.Calculate(bar, value);
			var low = _lowest.Calculate(bar, value);

			_topSeries[bar] = _smaHigh.Calculate(bar, high) + _width * _stdHigh.Calculate(bar, high);
			_botSeries[bar] = _smaLow.Calculate(bar, low) - _width * _stdLow.Calculate(bar, low);
			_smaTopSeries[bar] = _smaHigh[bar];
			_smaBotSeries[bar] = _smaLow[bar];
		}

		#endregion
	}
}