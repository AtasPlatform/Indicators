namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Trade Volume Index")]
	public class TVI : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region ctor

		public TVI()
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var volume = GetCandle(bar).Volume;

			if (value - (decimal)SourceDataSeries[bar - 1] > InstrumentInfo.TickSize)
				_renderSeries[bar] = _renderSeries[bar - 1] + volume;
			else if (value - (decimal)SourceDataSeries[bar - 1] == InstrumentInfo.TickSize)
				_renderSeries[bar] = _renderSeries[bar - 1];
			else
				_renderSeries[bar] = _renderSeries[bar - 1] - volume;
		}

		#endregion
	}
}