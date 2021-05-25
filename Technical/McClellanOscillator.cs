namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("McClellan Oscillator")]
	public class McClellanOscillator : Indicator
	{
		#region Fields

		private readonly EMA _mEmaLong = new();
		private readonly EMA _mEmaShort = new();
		private readonly ValueDataSeries _renderSeries = new("McClellan Oscillator");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _mEmaShort.Period;
			set
			{
				if (value <= 0)
					return;

				_mEmaShort.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 110)]
		public int LongPeriod
		{
			get => _mEmaLong.Period;
			set
			{
				if (value <= 0)
					return;

				_mEmaLong.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public McClellanOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_renderSeries.Color = Colors.LimeGreen;
			_renderSeries.Width = 2;

			ShortPeriod = 19;
			LongPeriod = 39;

			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_renderSeries[bar] = _mEmaShort.Calculate(bar, value) - _mEmaLong.Calculate(bar, value);
		}

		#endregion
	}
}