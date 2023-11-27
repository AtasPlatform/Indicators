namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Elder Ray")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ElderRayDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602580")]
	public class ElderRay : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _bearSeries = new("BearSeries", Strings.Bearlish)
		{
			DescriptionKey=nameof(Strings.BearishMovementSettingsDescription)
		};

		private readonly ValueDataSeries _bullSeries = new("BullSeries", Strings.Bullish) 
		{ 
			Color = DefaultColors.Green.Convert(),
            DescriptionKey = nameof(Strings.BullishMovementSettingsDescription)
        };

		private readonly EMA _ema = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ElderRay()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _bullSeries;
			DataSeries.Add(_bearSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_ema.Calculate(bar, candle.Close);
			_bullSeries[bar] = candle.High - _ema[bar];
			_bearSeries[bar] = candle.Low - _ema[bar];
		}

		#endregion
	}
}