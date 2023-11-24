namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Coppock Curve")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CoppockCurveDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602602-coppock-curve")]
	public class CoppockCurve : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization)
		{
			VisualType = VisualMode.Histogram
		};

		private readonly ROC _rocLong = new()
		{
			Period = 14,
			CalcMode = ROC.Mode.Percent
		};
		private readonly ROC _rocShort = new()
		{
			Period = 11,
			CalcMode = ROC.Mode.Percent
		};
		private readonly WMA _wma = new()
		{
			Period = 10
		};

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
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

		public CoppockCurve()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var rocShort = _rocShort.Calculate(bar, value);
			var rocLong = _rocLong.Calculate(bar, value);

			_renderSeries[bar] = _wma.Calculate(bar, rocLong + rocShort);
		}

		#endregion
	}
}