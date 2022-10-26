namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Inertia")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45246-inertia")]
	public class Inertia : Indicator
	{
		#region Fields

		private readonly LinearReg _linReg = new() { Period = 14 };

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly RVI2 _rvi = new() { Period = 10 };

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RVI", GroupName = "Period", Order = 100)]
		[Range(1, 10000)]
        public int RviPeriod
		{
			get => _rvi.Period;
			set
			{
				_rvi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LinearReg", GroupName = "Period", Order = 110)]
		[Range(1, 10000)]
        public int LinearRegPeriod
		{
			get => _linReg.Period;
			set
			{
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