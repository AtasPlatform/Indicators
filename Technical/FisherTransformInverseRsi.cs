namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Inverse Fisher Transform with RSI")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45432-inverse-fisher-transform-with-rsi")]
	public class FisherTransformInverseRsi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _ift = new("Ift", Strings.Visualization);

		private readonly RSI _rsi = new() { Period = 10 };
		private readonly WMA _wma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.RSI), GroupName = nameof(Strings.Period), Order = 90)]
		[Range(1, 10000)]
		public int HighLowPeriod
		{
			get => _rsi.Period;
			set
			{
				_rsi.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WMA), GroupName = nameof(Strings.Period), Order = 100)]
		[Range(1, 10000)]
        public int WmaPeriod
		{
			get => _wma.Period;
			set
			{
				_wma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public FisherTransformInverseRsi()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _ift;
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			var rsiValue = (_rsi.Calculate(bar, value) - 50) / 10;
			var rsiSmoothed = _wma.Calculate(bar, rsiValue);

			var expValue = (decimal)Math.Exp((double)(2 * rsiSmoothed));

			_ift[bar] = (expValue - 1) / (expValue + 1);
		}

		#endregion
	}
}