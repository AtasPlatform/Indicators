namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("TRIX")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45295-trix")]
	public class TRIX : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new() { Period = 10 };
		private readonly EMA _emaSecond = new() { Period = 10 };
        private readonly EMA _emaThird = new() { Period = 10 };
		
		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _emaFirst.Period;
			set
			{
				_emaFirst.Period = _emaSecond.Period = _emaThird.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TRIX()
		{
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, value);
			_emaThird.Calculate(bar, value);

			if (bar == 0)
				return;

			this[bar] = 100 * (_emaThird[bar] - _emaThird[bar - 1]) / _emaThird[bar - 1];
		}

		#endregion
	}
}