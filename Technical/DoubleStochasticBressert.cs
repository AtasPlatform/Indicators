namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Double Stochastic - Bressert")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45192-double-stochastic-bressert")]
	public class DoubleStochasticBressert : Indicator
	{
		#region Fields

		private readonly DoubleStochastic _ds = new()
		{
			Period = 10,
			SmaPeriod = 10
		};

		private readonly EMA _ema = new() { Period = 10 };
		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _ds.Period;
			set
			{
				_ds.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "EMAPeriod", GroupName = "Settings", Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
		{
			get => _ds.SmaPeriod;
			set
			{
				_ds.SmaPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "Smooth", GroupName = "Settings", Order = 120)]
		[Range(1, 10000)]
        public int Smooth
		{
			get => _ema.Period;
			set
			{
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