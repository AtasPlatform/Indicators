namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Daily HighLow")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/387-daily-highlow")]
	public class DailyHighLow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeres;
		private readonly ValueDataSeries _lowSeres = new ValueDataSeries("Low") { Color = Color.FromArgb(255, 135, 135, 135), VisualType = VisualMode.Square };
		private readonly ValueDataSeries _medianSeres = new ValueDataSeries("Median") { Color = Colors.Lime, VisualType = VisualMode.Square };

		private readonly ValueDataSeries _yesterdaymedianaSeres = new ValueDataSeries("Yesterday median")
			{ Color = Colors.Blue, VisualType = VisualMode.Square };

		private decimal _high;
		private bool _highSpecifyed;
		private DateTime _lastSessionTime;
		private decimal _low;
		private bool _lowSpecifyed;

		private decimal _yesterdaymediana;

		#endregion

		#region Properties

		private decimal Mediana => _low + (_high - _low) / 2;

		#endregion

		#region ctor

		public DailyHighLow()
			: base(true)
		{
			_highSeres = (ValueDataSeries)DataSeries[0];
			_highSeres.Name = "High";
			_highSeres.Color = Color.FromArgb(255, 135, 135, 135);
			_highSeres.VisualType = VisualMode.Square;
			DataSeries.Add(_lowSeres);
			DataSeries.Add(_medianSeres);
			DataSeries.Add(_yesterdaymedianaSeres);
		}

		#endregion

		#region Public methods

		#region Overrides of Indicator

		public override string ToString()
		{
			return "Daily HighLow";
		}

		#endregion

		#endregion

		#region Protected methods

		#region Overrides of Indicator

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_high = _low = _yesterdaymediana = 0;
				return;
			}

			var candle = GetCandle(bar);
			if (IsNewSession(bar))
			{
				if (_lastSessionTime != candle.Time)
				{
					_lastSessionTime = candle.Time;
					_yesterdaymediana = Mediana;
					_high = _low = 0;
					_highSpecifyed = _lowSpecifyed = false;
				}
			}

			if (candle.High > _high || !_highSpecifyed)
				_high = candle.High;
			if (candle.Low < _low || !_lowSpecifyed)
				_low = candle.Low;
			_highSpecifyed = _lowSpecifyed = true;
			_highSeres[bar] = _high;
			_lowSeres[bar] = _low;
			_medianSeres[bar] = Mediana;
			_yesterdaymedianaSeres[bar] = _yesterdaymediana;
		}

		#endregion

		#endregion
	}
}