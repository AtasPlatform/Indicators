namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Starc Bands")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StarcBandsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602475")]
	public class StarcBands : Indicator
	{
		#region Fields

		private readonly ATR _atr = new() { Period = 10 };
        private readonly SMA _sma = new() { Period = 10 };

        private readonly ValueDataSeries _botSeries = new("BotSeries", Strings.BottomBand)
		{
			Color = System.Drawing.Color.DodgerBlue.Convert(),
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.BottomBandDscription),
        };

		private readonly ValueDataSeries _smaSeries = new("SmaSeries", Strings.SMA)
		{
			DescriptionKey = nameof(Strings.SmaSetingsDescription),
		};

		private readonly ValueDataSeries _topSeries = new("TopSeries", Strings.TopBand)
		{
			Color = System.Drawing.Color.DodgerBlue.Convert(),
			IgnoredByAlerts = true,
			DescriptionKey = nameof(Strings.TopBandDscription),
		};

		private decimal _botBand = 1;
        private decimal _topBand = 1;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 1000000)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ATR), GroupName = nameof(Strings.Settings), Description = nameof(Strings.AtrPeriodDescription), Order = 110)]
		[Range(1, 1000000)]
		public int SmaPeriod
		{
			get => _atr.Period;
			set
			{
				_atr.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ATRMultiplierDescription), Order = 120)]
		[Range(0.000001, 1000000)]
		public decimal TopBand
		{
			get => _topBand;
			set
			{
				_topBand = value;
				RecalculateValues();
			}
		}

        [Obsolete]
		[Browsable(false)]
		public decimal BotBand
		{
			get => _botBand;
			set
			{
				_botBand = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StarcBands()
		{
			Add(_atr);

			DataSeries[0] = _topSeries;
			DataSeries.Add(_botSeries);
			DataSeries.Add(_smaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);

			var bandValue = _topBand * _atr[bar];

			_topSeries[bar] = _sma[bar] + bandValue;
			_botSeries[bar] = _sma[bar] - bandValue;
			_smaSeries[bar] = _sma[bar];
		}

		#endregion
	}
}