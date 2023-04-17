namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Double Exponential Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45400-double-exponential-moving-average")]
	public class DEMA : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new()
		{
			Period = 10
		};
		private readonly EMA _emaSecond = new()
		{
			Period = 10
		};

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization) { UseMinimizedModeIfEnabled = true };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _emaFirst.Period;
			set
			{
				_emaFirst.Period = _emaSecond.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DEMA()
		{
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