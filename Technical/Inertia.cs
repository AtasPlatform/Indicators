namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Inertia")]
	public class Inertia : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly RVI2 _rvi = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RVI", GroupName = "Period", Order = 100)]
		public int RviPeriod
		{
			get => _rvi.Period;
			set
			{
				if (value <= 0)
					return;

				_rvi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LinearReg", GroupName = "Period", Order = 110)]
		public int LinearRegPeriod
		{
			get => _linReg.Period;
			set
			{
				if (value <= 0)
					return;

				_linReg.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Inertia()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_rvi.Period = 10;
			_linReg.Period = 14;

			Add(_rvi);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _linReg.Calculate(bar, _rvi[bar]);
		}

		#endregion
	}
}