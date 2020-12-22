namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("Bill Williams AC")]
	public class ACBW : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _downSeries = new ValueDataSeries(Resources.Down);
		private readonly SMA _longSma = new SMA();
		private readonly ValueDataSeries _neutralSeries = new ValueDataSeries(Resources.Neutral);
		private readonly SMA _shortSma = new SMA();
		private readonly SMA _signalSma = new SMA();

		private readonly ValueDataSeries _upSeries = new ValueDataSeries(Resources.Up);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 100)]
		public int LongPeriod
		{
			get => _longSma.Period;
			set
			{
				if (value < 51 || value == ShortPeriod)
					return;

				_longSma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 110)]
		public int ShortPeriod
		{
			get => _shortSma.Period;
			set
			{
				if (value < 50 || value == LongPeriod)
					return;

				_shortSma.Period = value;

				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "SignalPeriod", GroupName = "Settings", Order = 120)]
		public int SignalPeriod
		{
			get => _signalSma.Period;
			set
			{
				if (value < 50)
					return;

				_signalSma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ACBW()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_shortSma.Period = _signalSma.Period = 50;
			_longSma.Period = 51;
			_upSeries.Color = Colors.Green;
			_downSeries.Color = Colors.Purple;
			_neutralSeries.Color = Colors.Gray;

			_upSeries.VisualType = _downSeries.VisualType = _neutralSeries.VisualType = VisualMode.Histogram;
			_upSeries.ShowZeroValue = _downSeries.ShowZeroValue = _neutralSeries.ShowZeroValue = false;

			DataSeries[0] = _upSeries;
			DataSeries.Add(_downSeries);
			DataSeries.Add(_neutralSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var diff = _shortSma.Calculate(bar, value) - _longSma.Calculate(bar, value);
			var ac = diff - _signalSma.Calculate(bar, diff);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			var prevValue = _neutralSeries[bar - 1];

			if (_upSeries[bar - 1] != 0)
				prevValue = _upSeries[bar - 1];
			else if (_downSeries[bar - 1] != 0)
				prevValue = _downSeries[bar - 1];

			if (ac > prevValue)
				_upSeries[bar] = ac;
			else if (ac < prevValue)
				_downSeries[bar] = ac;
			else
				_neutralSeries[bar] = ac;
		}

		#endregion
	}
}