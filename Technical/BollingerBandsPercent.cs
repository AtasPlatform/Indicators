namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Bollinger Bands: Percentage")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BollingerBandsPercentDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602336")]
	public class BollingerBandsPercent : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.BottomBand))]
			Bottom,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.MiddleBand))]
			Middle
		}

		#endregion

		#region Fields

		private readonly BollingerBands _bb = new();

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private Mode _calcMode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 100)]
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
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 110)]
		public int Period
		{
			get => _bb.Period;
			set
			{
				_bb.Period = _bb.Period = value;
				RecalculateValues();
			}
		}

        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.BBandsWidth), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DeviationRangeDescription), Order = 120)]
		public decimal Width
		{
			get => _bb.Width;
			set
			{
				_bb.Width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBandsPercent()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_bb.Period = 10;
			_bb.Width = 1;
			_calcMode = Mode.Bottom;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_bb.Calculate(bar, value);
			var top = ((ValueDataSeries)_bb.DataSeries[1])[bar];

			switch (_calcMode)
			{
				case Mode.Bottom:
					var bot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

					if (top - bot == 0)
						return;

					_renderSeries[bar] = 100 * (value - bot) / (top - bot);
					break;
				case Mode.Middle:
					var sma = ((ValueDataSeries)_bb.DataSeries[0])[bar];

					if (top - sma == 0)
						return;

					_renderSeries[bar] = 100 * (value - sma) / (top - sma);
					break;
			}
		}

		#endregion
	}
}