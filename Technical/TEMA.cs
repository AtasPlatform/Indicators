﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Triple Exponential Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.TEMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602492")]
	public class TEMA : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new() { Period = 10 };
		private readonly EMA _emaSecond = new() { Period = 10 };
        private readonly EMA _emaThird = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
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