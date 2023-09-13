namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Dispersion")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45190-dispersion")]
	public class Dispersion : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _diffSeries = new("Difference");

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization) { UseMinimizedModeIfEnabled = true };

		private readonly SMA _sma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = "Period", GroupName = "Settings", Order = 110)]
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

		#endregion

		#region ctor

		public Dispersion()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_diffSeries[bar] = value - _sma.Calculate(bar, value);
			_diffSeries[bar] *= _diffSeries[bar];

			var diffSum = _diffSeries.CalcSum(_sma.Period - 1, bar);
			_renderSeries[bar] = diffSum / _sma.Period;
		}

		#endregion
	}
}