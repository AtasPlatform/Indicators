namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("TRIX")]
	public class TRIX : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new EMA();
		private readonly EMA _emaSecond = new EMA();
		private readonly EMA _emaThird = new EMA();

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _emaFirst.Period;
			set
			{
				if (value <= 0)
					return;

				_emaFirst.Period = _emaSecond.Period = _emaThird.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TRIX()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_emaFirst.Period = _emaSecond.Period = _emaThird.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, value);
			_emaThird.Calculate(bar, value);

			if (bar == 0)
				return;

			_renderSeries[bar] = 100 * (_emaThird[bar] - _emaThird[bar - 1]) / _emaThird[bar - 1];
		}

		#endregion
	}
}