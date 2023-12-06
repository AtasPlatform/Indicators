namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Kaufman Adaptive Moving Average")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.KAMADescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602525")]
	public class KAMA : Indicator
	{
		#region Fields

		private readonly List<decimal> _closeList = new();

		private int _efficiencyRatioPeriod = 10;
        private int _lastBar = -1;
        private int _longPeriod = 30;
        private int _shortPeriod = 2;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.EfficiencyRatioPeriod), GroupName = nameof(Strings.Common), Description = nameof(Strings.PeriodDescription))]
		[Range(1, 10000)]
		public int EfficiencyRatioPeriod
		{
			get => _efficiencyRatioPeriod;
			set
			{
				_efficiencyRatioPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Common), Description = nameof(Strings.ShortPeriodDescription))]
		[Range(1, 10000)]
        public int ShortPeriod
		{
			get => _shortPeriod;
			set
			{
				if (value > LongPeriod)
					return;

				_shortPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Common), Description = nameof(Strings.LongPeriodDescription))]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _longPeriod;
			set
			{
				if (value < ShortPeriod)
					return;

				_longPeriod = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KAMA()
			: base(true)
		{
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);
			var pastCandle = GetCandle(Math.Max(bar - EfficiencyRatioPeriod, 0));

			if (bar == 0)
			{
				_closeList.Clear();
				_closeList.Add(currentCandle.Close);
				this[bar] = currentCandle.Close;
				return;
			}

			if (_closeList.Count > EfficiencyRatioPeriod)
				_closeList.RemoveAt(0);

			var change = currentCandle.Close - pastCandle.Close;
			var volatilitySum = Math.Abs(currentCandle.Close - _closeList.LastOrDefault());

			for (var i = _closeList.Count - 1; i > 0; i--)
				volatilitySum += Math.Abs(_closeList[i] - _closeList[i - 1]);

			decimal er;

			if (volatilitySum == 0.0m)
				er = 1;
			else
				er = change / volatilitySum;

			var fastestConst = 2.0m / (ShortPeriod + 1.0m);
			var slowestConst = 2.0m / (LongPeriod + 1.0m);

			var sc = er * (fastestConst - slowestConst) + slowestConst;
			sc *= sc;

			this[bar] = this[bar - 1] + sc * (currentCandle.Close - this[bar - 1]);

			if (bar != _lastBar)
				_lastBar = bar;
			else
				_closeList.RemoveAt(_closeList.Count - 1);

			_closeList.Add(currentCandle.Close);
		}

		#endregion
	}
}