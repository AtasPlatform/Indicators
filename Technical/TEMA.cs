namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Triple Exponential Moving Average")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45290-triple-exponential-moving-average")]
	public class TEMA : Indicator
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

		public TEMA()
		{
			_emaFirst.Period = _emaSecond.Period = _emaThird.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, _emaFirst[bar]);
			_emaThird.Calculate(bar, _emaSecond[bar]);
			_renderSeries[bar] = 3 * _emaFirst[bar] - 3 * _emaSecond[bar] + _emaThird[bar];
		}

		#endregion
	}
}