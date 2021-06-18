namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Zero Lag Exponential Moving Average")]
	public class ZLEMA : Indicator
	{
		#region Fields

		private readonly EMA _ema = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _length;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _ema.Period;
			set
			{
				if (value <= 0)
					return;

				_ema.Period = value;
				_length = (int)Math.Ceiling((value - 1) / 2m);
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ZLEMA()
		{
			_ema.Period = 10;
			_length = (int)Math.Ceiling((10 - 1) / 2m);
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var startBar = Math.Max(0, bar - _length);

			var deLagged = 2 * value - (decimal)SourceDataSeries[startBar];

			_renderSeries[bar] = _ema.Calculate(bar, deLagged);
		}

		#endregion
	}
}