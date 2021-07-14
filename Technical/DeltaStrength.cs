namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Delta Strength")]
	public class DeltaStrength : Indicator
	{
		#region Fields

		private ValueDataSeries _negSeries = new(Resources.Negative);
		private int _percentage;
		private ValueDataSeries _posSeries = new(Resources.Positive);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Percent", GroupName = "Settings", Order = 100)]
		public int Percentage
		{
			get => _percentage;
			set
			{
				if (value is < 0 or > 100)
					return;

				_percentage = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DeltaStrength()
			: base(true)
		{
			_percentage = 98;
			_posSeries.Color = Colors.Green;
			_negSeries.Color = Colors.Red;
			_posSeries.VisualType = _negSeries.VisualType = VisualMode.Dots;
			_posSeries.Width = _negSeries.Width = 4;
			DataSeries[0] = _posSeries;
			DataSeries.Add(_negSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			var candle = GetCandle(bar);

			if (candle.Delta < 0 && candle.MinDelta < 0 && candle.Delta <= candle.MinDelta * 0.01m * _percentage)
				_negSeries[bar] = candle.Low - InstrumentInfo.TickSize;
			else
				_negSeries[bar] = 0;

			if (candle.Delta > 0 && candle.MaxDelta > 0 && candle.Delta >= candle.MaxDelta * 0.01m * _percentage)
				_posSeries[bar] = candle.High + InstrumentInfo.TickSize;
			else
				_posSeries[bar] = 0;
		}

		#endregion
	}
}