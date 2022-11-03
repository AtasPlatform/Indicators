namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Triple Exponential Moving Average")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45290-triple-exponential-moving-average")]
	public class TEMA : Indicator
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
		
		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, _emaFirst[bar]);
			_emaThird.Calculate(bar, _emaSecond[bar]);
			this[bar] = 3 * _emaFirst[bar] - 3 * _emaSecond[bar] + _emaThird[bar];
		}

		#endregion
	}
}