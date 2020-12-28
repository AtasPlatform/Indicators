﻿namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Study Angle")]
	public class Angle : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _openCloseSeries = new ValueDataSeries("OpenClose");

		private readonly ValueDataSeries _renderSeries = new ValueDataSeries(Resources.Visualization);
		private int _period;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Angle()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 10;
			LineSeries.Add(new LineSeries(Resources.ZeroValue) { Color = Colors.Gray, Value = 0, Width = 2 });
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				_renderSeries.Clear();

			if (bar < _period)
				return;

			_renderSeries[bar] = (decimal)(Math.Atan((double)
				((value - (decimal)SourceDataSeries[bar - _period]) / _period)) * 180 / Math.PI);
		}

		#endregion
	}
}