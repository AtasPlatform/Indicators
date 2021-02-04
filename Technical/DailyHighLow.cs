namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Daily HighLow")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/387-daily-highlow")]
	public class DailyHighLow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeres;
		private readonly ValueDataSeries _lowSeres = new("Low") { Color = Color.FromArgb(255, 135, 135, 135), VisualType = VisualMode.Square };
		private readonly ValueDataSeries _medianSeres = new("Median") { Color = Colors.Lime, VisualType = VisualMode.Square };

		private readonly ValueDataSeries _yesterdaymedianaSeres = new("Yesterday median")
			{ Color = Colors.Blue, VisualType = VisualMode.Square };

		private int _days;

		private decimal _high;
		private bool _highSpecifyed;
		private DateTime _lastSessionTime;
		private decimal _low;
		private bool _lowSpecifyed;
		private int _targetBar;

		private decimal _yesterdaymediana;

		#endregion

		#region Properties

		private decimal Mediana => _low + (_high - _low) / 2;

		[Display(ResourceType = typeof(Resources), Name = "Days", GroupName = "Settings", Order = 100)]
		public int Days
		{
			get => _days;
			set
			{
				if (value < 0)
					return;

				_days = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DailyHighLow()
			: base(true)
		{
			_days = 20;
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
				if (_days == 0)
					_targetBar = 0;
				else
				{
					var days = 0;

					for (var i = CurrentBar - 1; i >= 0; i--)
					{
						_targetBar = i;

						if (!IsNewSession(i))
							continue;

						days++;

						if (days == _days)
							break;
					}
				}

				_high = _low = _yesterdaymediana = 0;
				_highSpecifyed = _lowSpecifyed = false;
				DataSeries.ForEach(x => x.Clear());
				return;
			}

			if (bar < _targetBar)
				return;

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