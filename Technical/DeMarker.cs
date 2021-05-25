namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("DeMarker")]
	public class DeMarker : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private readonly SMA _smaMax = new();
		private readonly SMA _smaMin = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _smaMax.Period;
			set
			{
				if (value <= 0)
					return;

				_smaMax.Period = _smaMin.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeMarker()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_smaMax.Period = _smaMin.Period = 10;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_smaMax.Calculate(bar, 0);
				_smaMin.Calculate(bar, 0);
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			var deMax = Math.Max(0, candle.High - prevCandle.High);
			var deMin = Math.Min(0, prevCandle.Low - candle.Low);

			_smaMax.Calculate(bar, deMax);
			_smaMin.Calculate(bar, deMin);

			if (_smaMax[bar] + _smaMin[bar] != 0)
				_renderSeries[bar] = _smaMax[bar] / (_smaMax[bar] + _smaMin[bar]);
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}