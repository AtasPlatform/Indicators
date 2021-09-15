namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Localization;

	[DisplayName("Volume Oscillator")]
	public class VolumeOscillator : Indicator
	{
		#region Fields

		private readonly SMA _longSma = new();
		private readonly ValueDataSeries _renderSeries = new(Strings.Visualization);
		private readonly SMA _shortSma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = "ShortPeriod", GroupName = "Settings", Order = 100)]
		public int ShortPeriod
		{
			get => _shortSma.Period;
			set
			{
				_shortSma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings), Name = "LongPeriod", GroupName = "Settings", Order = 110)]
		public int LongPeriod
		{
			get => _longSma.Period;
			set
			{
				_longSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public VolumeOscillator()
		{
			Panel = IndicatorDataProvider.NewPanel;
			ShortPeriod = 20;
			LongPeriod = 60;

			LineSeries.Add(new LineSeries(Strings.BaseLine)
			{
				Color = Colors.DarkBlue,
				Value = 0
			});
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var volume = GetCandle(bar).Volume;
			_shortSma.Calculate(bar, volume);
			_longSma.Calculate(bar, volume);

			if (bar == 0)
			{
				_renderSeries.Clear();
				return;
			}

			if (_longSma[bar] != 0)
				_renderSeries[bar] = 100 * (_shortSma[bar] - _longSma[bar]) / _longSma[bar];
			else
				_renderSeries[bar] = _renderSeries[bar - 1];
		}

		#endregion
	}
}