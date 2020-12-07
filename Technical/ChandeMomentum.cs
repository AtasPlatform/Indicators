namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Chande Momentum Oscillator")]
	public class ChandeMomentum : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downValues = new ValueDataSeries("Down");
		private int _period;
		private readonly ValueDataSeries _renderSeries = new ValueDataSeries("Momentum");

		private readonly ValueDataSeries _upValues = new ValueDataSeries("Up");

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

		public ChandeMomentum()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_period = 14;

			_renderSeries.Color = Colors.Blue;
			_renderSeries.Width = 2;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			_upValues[bar] = Math.Max(value - (decimal)SourceDataSeries[bar - 1], 0);
			_downValues[bar] = Math.Max((decimal)SourceDataSeries[bar - 1] - value, 0);

			if (bar < _period)
				return;

			var upSum = _upValues.CalcSum(_period, bar);
			var downSum = _downValues.CalcSum(_period, bar);

			if (upSum + downSum != 0)
				_renderSeries[bar] = 100m * (upSum - downSum) / (upSum + downSum);
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}