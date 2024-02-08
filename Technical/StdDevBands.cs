namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Standard Deviation Bands")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StdDevBandsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602614")]
	public class StdDevBands : Indicator
	{
        #region Fields

        private readonly Highest _highest = new() { Period = 10 };
        private readonly Lowest _lowest = new() { Period = 10 };
        private readonly SMA _smaHigh = new() { Period = 10 };
        private readonly SMA _smaLow = new() { Period = 10 };
        private readonly StdDev _stdHigh = new() { Period = 10 };
        private readonly StdDev _stdLow = new() { Period = 10 };

		private readonly ValueDataSeries _smaBotSeries = new("SmaBotSeries", Strings.SMA1)
		{
            DescriptionKey = nameof(Strings.SmaSetingsDescription),
        };

		private readonly ValueDataSeries _smaTopSeries = new("SmaTopSeries", Strings.SMA2)
		{
			DescriptionKey = nameof(Strings.SmaSetingsDescription),
		};

        private readonly ValueDataSeries _botSeries = new("BotSeries", Strings.BottomBand)
		{
			Color = Colors.DodgerBlue,
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.BottomBandDscription),
        };
		
        private readonly ValueDataSeries _topSeries = new("TopSeries", Strings.TopBand)
        {
			Color = Colors.DodgerBlue,
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.TopBandDscription),
        };

		private int _width = 2;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdPeriodDescription), Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _stdHigh.Period;
			set
			{
				_stdHigh.Period = _stdLow.Period = _highest.Period = _lowest.Period =
					_smaHigh.Period = _smaLow.Period = value;

				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBandsWidth), GroupName = nameof(Strings.Settings), Description = nameof(Strings.SMAPeriodDescription), Order = 110)]
		[Range(1, 1000)]
        public int SmaPeriod
		{
			get => _width;
			set
			{
				_width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StdDevBands()
		{
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