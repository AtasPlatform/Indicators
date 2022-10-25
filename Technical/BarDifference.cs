namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Bar Difference")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43418-bar-difference")]
	public class BarDifference : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period = 1;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BarDifference()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();
			
			if (bar < _period)
				return;

			_renderSeries[bar] = (value - (decimal)SourceDataSeries[bar - _period]) / InstrumentInfo.TickSize;
		}

		#endregion
	}
}