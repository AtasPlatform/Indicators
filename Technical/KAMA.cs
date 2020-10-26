namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Linq;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Kaufman Adaptive Moving Average")]
	public class KAMA : Indicator
	{
		#region Fields

		private readonly List<decimal> _closeList = new List<decimal>();

		private int _efficiencyRatioPeriod;
		private int _lastBar;
		private int _longPeriod;
		private int _shortPeriod;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "EfficiencyRatioPeriod")]
		public int EfficiencyRatioPeriod
		{
			get => _efficiencyRatioPeriod;
			set
			{
				if (value <= 0)
					return;

				_efficiencyRatioPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod")]
		public int ShortPeriod
		{
			get => _shortPeriod;
			set
			{
				if (value <= 0 || value > LongPeriod)
					return;

				_shortPeriod = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod")]
		public int LongPeriod
		{
			get => _longPeriod;
			set
			{
				if (value <= 0 || value < ShortPeriod)
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
			_lastBar = -1;
			_efficiencyRatioPeriod = 10;
			_shortPeriod = 2;
			_longPeriod = 30;
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
			sc = sc * sc;

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