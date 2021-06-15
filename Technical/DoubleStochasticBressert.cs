namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Double Stochastic - Bressert")]
	public class DoubleStochasticBressert : Indicator
	{
		#region Fields

		private readonly DoubleStochastic _ds = new();

		private readonly EMA _ema = new();
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _ds.Period;
			set
			{
				if (value <= 0)
					return;

				_ds.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SMAPeriod", GroupName = "Settings", Order = 110)]
		public int SmaPeriod
		{
			get => _ds.SmaPeriod;
			set
			{
				if (value <= 0)
					return;

				_ds.SmaPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		public int Smooth
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

				_ema.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DoubleStochasticBressert()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_ema.Period = 10;
			_ds.Period = _ds.SmaPeriod = 10;

			Add(_ds);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _ema.Calculate(bar, _ds[bar]);
		}

		#endregion
	}
}