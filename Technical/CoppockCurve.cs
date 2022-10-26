namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Coppock Curve")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43361-coppock-curve")]
	public class CoppockCurve : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization)
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

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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