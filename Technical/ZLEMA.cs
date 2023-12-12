namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Zero Lag Exponential Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ZLEMAIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602640")]
	public class ZLEMA : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 10 };
		private int _length = 4;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription),Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				_length = (int)Math.Ceiling((value - 1) / 2m);
				RecalculateValues();
			}
		}

        #endregion

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			var startBar = Math.Max(0, bar - _length);

			var deLagged = 2 * value - (decimal)SourceDataSeries[startBar];

			this[bar] = _ema.Calculate(bar, deLagged);
		}

		#endregion
	}
}