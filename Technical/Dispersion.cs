﻿namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Dispersion")]
	public class Dispersion : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _diffSeries = new ValueDataSeries("Difference");

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);

		private readonly SMA _sma = new SMA();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 110)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Dispersion()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_sma.Period = 10;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_diffSeries[bar] = value - _sma.Calculate(bar, value);
			_diffSeries[bar] *= _diffSeries[bar];

			var diffSum = _diffSeries.CalcSum(_sma.Period - 1, bar);
			_renderSeries[bar] = diffSum / _sma.Period;
		}

		#endregion
	}
}