namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("TRIX")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45295-trix")]
	public class TRIX : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new();
		private readonly EMA _emaSecond = new();
		private readonly EMA _emaThird = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

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