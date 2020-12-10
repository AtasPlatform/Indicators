namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Coppock Curve")]
	public class CoppockCurve : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);

		private readonly ROC _rocLong = new ROC();
		private readonly ROC _rocShort = new ROC();
		private readonly WMA _wma = new WMA();

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