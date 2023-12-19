namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Rate of Change")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.ROCDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602454")]
	public class ROC : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Percent))]
			Percent,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.Ticks))]
			Ticks
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};
		private Mode _calcMode = Mode.Percent;
        private decimal _multiplier = 100;
        private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 90)]
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
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Multiplier), GroupName = nameof(Strings.Settings), Description = nameof(Strings.MultiplierDescription), Order = 110)]
		[Range(0.0000001, 10000000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ROC()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var calcBar = Math.Max(0, bar - _period);
			var roc = 0m;

			switch (_calcMode)
			{
				case Mode.Percent:
					if ((decimal)SourceDataSeries[calcBar] != 0)
						roc = _multiplier * (value - (decimal)SourceDataSeries[calcBar]) / (decimal)SourceDataSeries[calcBar];
					break;
				case Mode.Ticks:
					roc = (value - (decimal)SourceDataSeries[calcBar]) / InstrumentInfo.TickSize;
					break;
			}

			_renderSeries[bar] = roc;
		}

		#endregion
	}
}