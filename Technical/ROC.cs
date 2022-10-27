namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Rate of Change")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43357-rate-of-change")]
	public class ROC : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "Percent")]
			Percent,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
		{
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};
		private Mode _calcMode = Mode.Percent;
        private decimal _multiplier = 100;
        private int _period = 10;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 90)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Settings", Order = 110)]
		[Range(0, 10000000000)]
		public decimal Multiplier
		{
			get => _multiplier;
			set
			{
				if (value <= 0)
					return;

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