namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Weighted Average Oscillator")]
	[FeatureId("NotReady")]
	public class WAO : Indicator
	{
		#region Fields

		private readonly WMA _longWma = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		private readonly WMA _shortWma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _shortWma.Period;
			set
			{
				if (value <= 0)
					return;

				_shortWma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		public int LongPeriod
		{
			get => _longWma.Period;
			set
			{
				if (value <= 0)
					return;

				_longWma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public WAO()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_shortWma.Period = 10;
			_longWma.Period = 30;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _shortWma.Calculate(bar, value) - _longWma.Calculate(bar, value);
		}

		#endregion
	}
}