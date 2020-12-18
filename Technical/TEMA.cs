﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Triple Exponential Moving Average")]
	public class TEMA : Indicator
	{
		#region Fields

		private readonly EMA _emaFirst = new EMA();
		private readonly EMA _emaSecond = new EMA();
		private readonly EMA _emaThird = new EMA();

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _emaFirst.Period;
			set
			{
				if (value <= 0)
					return;

				_emaFirst.Period = _emaSecond.Period = _emaThird.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TEMA()
		{
			_emaFirst.Period = _emaSecond.Period = _emaThird.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_emaFirst.Calculate(bar, value);
			_emaSecond.Calculate(bar, _emaFirst[bar]);
			_emaThird.Calculate(bar, _emaSecond[bar]);
			_renderSeries[bar] = 3 * _emaFirst[bar] - 3 * _emaSecond[bar] + _emaThird[bar];
		}

		#endregion
	}
}