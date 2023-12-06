namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Moving Average Envelope")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MaEnvelopeDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602431")]
	public class MaEnvelope : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.FixedValue))]
			FixedValue,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent))]
			Percentage
		}

        #endregion

        #region Fields

        private readonly SMA _sma = new() { Period = 10 };

        private readonly ValueDataSeries _botSeries = new("BotSeries", Strings.BottomBand)
        {
	        Color = DefaultColors.Blue.Convert(),
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.BottomChannelSettingsDescription)
        };
		private readonly ValueDataSeries _smaSeries = new("SmaSeries", Strings.MiddleBand)
		{
            DescriptionKey = nameof(Strings.MidChannelSettingsDescription)
        };

        private readonly ValueDataSeries _topSeries = new("TopSeries", Strings.TopBand)
        {
	        Color = DefaultColors.Blue.Convert(),
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.TopChannelSettingsDescription)
        };

        private Mode _calcMode = Mode.Percentage;
        private decimal _value = 1;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Mode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 110)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Value), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DeviationRangeDescription), Order = 120)]
		[Range(0.00001, 10000)]
        public decimal Value
		{
			get => _value;
			set
			{
				_value = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MaEnvelope()
		{
			DataSeries[0] = _botSeries;
			DataSeries.Add(_topSeries);
			DataSeries.Add(_smaSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_smaSeries[bar] = _sma[bar];

			if (_calcMode == Mode.FixedValue)
			{
				_topSeries[bar] = _sma[bar] + _value;
				_botSeries[bar] = _sma[bar] - _value;
			}
			else
			{
				_topSeries[bar] = _sma[bar] * (1 + 0.01m * _value);
				_botSeries[bar] = _sma[bar] * (1 - 0.01m * _value);
			}
		}

		#endregion
	}
}