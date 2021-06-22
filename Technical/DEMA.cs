namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Double Exponential Moving Average")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45400-double-exponential-moving-average")]
	public class DEMA : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new();
		private readonly EMA _emaSecond = new();

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

				_emaFirst.Period = _emaSecond.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DEMA()
		{
			_emaFirst.Period = _emaSecond.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, _emaFirst[bar]);
			_renderSeries[bar] = 2 * _emaFirst[bar] - _emaSecond[bar];
		}

		#endregion
	}
}