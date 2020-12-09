namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Bar Difference")]
	public class BarDifference : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				if (value < 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BarDifference()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 1;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();

			if (_period == 0)
			{
				var candle = GetCandle(bar);
				_renderSeries[bar] = (candle.High - candle.Low) / InstrumentInfo.TickSize;
				return;
			}

			if (bar < _period)
				return;

			_renderSeries[bar] = (value - (decimal)SourceDataSeries[bar - _period]) / InstrumentInfo.TickSize;
		}

		#endregion
	}
}