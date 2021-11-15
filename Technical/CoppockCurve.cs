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

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		private readonly ROC _rocLong = new();
		private readonly ROC _rocShort = new();
		private readonly WMA _wma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _wma.Period;
			set
			{
				if (value <= 0)
					return;

				_wma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CoppockCurve()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_rocLong.CalcMode = _rocShort.CalcMode = ROC.Mode.Percent;
			_rocLong.Period = 14;
			_rocShort.Period = 11;

			_wma.Period = 10;
			_renderSeries.VisualType = VisualMode.Histogram;
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