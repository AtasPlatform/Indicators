namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Bands/Envelope")]
	public class BandsEnvelope : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "Percent")]
			Percentage,

			[Display(ResourceType = typeof(Resources), Name = "PriceChange")]
			Value,

			[Display(ResourceType = typeof(Resources), Name = "Ticks")]
			Ticks
		}

		#endregion

		#region Fields

		private readonly RangeDataSeries _renderSeries = new RangeDataSeries(Resources.Visualization);
		private Mode _calcMode;
		private decimal _rangeFilter;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Mode", GroupName = "Settings", Order = 100)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "RangeFilter", GroupName = "Settings", Order = 110)]
		public decimal RangeFilter
		{
			get => _rangeFilter;
			set
			{
				if (value <= 0)
					return;

				if (_calcMode == Mode.Percentage && value > 100)
					return;

				_rangeFilter = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BandsEnvelope()
		{
			_calcMode = Mode.Percentage;
			_rangeFilter = 1;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			switch (_calcMode)
			{
				case Mode.Percentage:
					_renderSeries[bar].Upper = value + value * _rangeFilter * 0.01m;
					_renderSeries[bar].Lower = value - value * _rangeFilter * 0.01m;
					break;
				case Mode.Value:
					_renderSeries[bar].Upper = value + _rangeFilter;
					_renderSeries[bar].Lower = value - _rangeFilter;
					break;
				case Mode.Ticks:
					_renderSeries[bar].Upper = value + _rangeFilter * InstrumentInfo.TickSize;
					_renderSeries[bar].Lower = value - _rangeFilter * InstrumentInfo.TickSize;
					break;
			}
		}

		#endregion
	}
}