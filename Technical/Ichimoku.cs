namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("Ichimoku Kinko Hyo")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/16981-ichimoku-kinko-hyo")]
	public class Ichimoku : Indicator
	{
		#region Fields

		private readonly Highest _baseHigh = new();

		private readonly ValueDataSeries _baseLine = new("Base");
		private readonly Lowest _baseLow = new();

		private readonly Highest _conversionHigh = new();

		private readonly ValueDataSeries _conversionLine = new("Conversion");
		private readonly Lowest _conversionLow = new();
		private readonly RangeDataSeries _downSeries = new("Down");
		private readonly ValueDataSeries _laggingSpan = new("Lagging Span");
		private readonly ValueDataSeries _leadLine1 = new("Lead1");
		private readonly ValueDataSeries _leadLine2 = new("Lead2");

		private readonly Highest _spanHigh = new();
		private readonly Lowest _spanLow = new();
		private readonly RangeDataSeries _upSeries = new("Up");

		private int _days;
		private int _displacement;
		private int _targetBar;

		#endregion

		#region Properties

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Days")]
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

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Tenkan-sen")]
		public int Tenkan
		{
			get => _conversionHigh.Period;
			set
			{
				if (value <= 0)
					return;

				_conversionHigh.Period = _conversionLow.Period = value;

				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Kijun-sen")]
		public int Kijun
		{
			get => _baseHigh.Period;
			set
			{
				if (value <= 0)
					return;

				_baseHigh.Period = _baseLow.Period = value;

				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Senkou Span B")]
		public int Senkou
		{
			get => _spanHigh.Period;
			set
			{
				if (value <= 0)
					return;

				_spanHigh.Period = _spanLow.Period = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Settings")]
		[DisplayName("Displacement")]
		public int Displacement
		{
			get => _displacement;
			set
			{
				if (value <= 0)
					return;

				_displacement = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Ichimoku()
			: base(true)
		{
			DenyToChangePanel = true;

			_conversionHigh.Period = _conversionLow.Period = 9;
			_baseHigh.Period = _baseLow.Period = 26;
			_spanHigh.Period = _spanLow.Period = 52;
			_displacement = 26;

			_conversionLine.Color = Color.FromRgb(4, 150, 255);
			_baseLine.Color = Color.FromRgb(153, 21, 21);
			_laggingSpan.Color = Color.FromRgb(69, 153, 21);

			_leadLine1.Color = Colors.Green;
			_leadLine2.Color = Colors.Red;
			_upSeries.RangeColor = Color.FromArgb(100, 0, 255, 0);
			_downSeries.RangeColor = Color.FromArgb(100, 255, 0, 0);
			DataSeries[0] = _conversionLine;
			DataSeries.Add(_laggingSpan);
			DataSeries.Add(_baseLine);
			DataSeries.Add(_leadLine1);
			DataSeries.Add(_leadLine2);
			DataSeries.Add(_upSeries);
			DataSeries.Add(_downSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (bar == 0)
			{
				DataSeries.ForEach(x => x.Clear());
				_targetBar = 0;

				if (_days > 0)
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

					if (_targetBar > 0)
					{
						_conversionLine.SetPointOfEndLine(_targetBar - 1);
						_laggingSpan.SetPointOfEndLine(_targetBar - _displacement);
						_baseLine.SetPointOfEndLine(_targetBar - 1);
						_leadLine1.SetPointOfEndLine(_targetBar + _displacement - 2);
						_leadLine2.SetPointOfEndLine(_targetBar + _displacement - 2);
					}
				}
			}

			_conversionHigh.Calculate(bar, candle.High);
			_conversionLow.Calculate(bar, candle.Low);

			_baseHigh.Calculate(bar, candle.High);
			_baseLow.Calculate(bar, candle.Low);

			_spanHigh.Calculate(bar, candle.High);
			_spanLow.Calculate(bar, candle.Low);

			if (bar < _targetBar)
				return;

			_baseLine[bar] = (_baseHigh[bar] + _baseLow[bar]) / 2;
			_conversionLine[bar] = (_conversionHigh[bar] + _conversionLow[bar]) / 2;

			if (bar + _displacement <= CurrentBar)
			{
				var targetBar = bar + Displacement - 1;
				_leadLine1[targetBar] = (_conversionLine[bar] + _baseLine[bar]) / 2;

				_leadLine2[targetBar] = (_spanHigh[bar] + _spanLow[bar]) / 2;
			}
			else
			{
				_leadLine1[bar] = (_conversionLine[bar] + _baseLine[bar]) / 2;

				_leadLine2[bar] = (_spanHigh[bar] + _spanLow[bar]) / 2;
			}

			if (bar - _displacement + 1 >= 0)
			{
				var targetBar = bar - _displacement + 2;
				_laggingSpan[targetBar] = candle.Close;

				if (bar == CurrentBar - 1)
				{
					for (var i = targetBar + 1; i < CurrentBar; i++)
						_laggingSpan[i] = candle.Close;
				}
			}
			
			if (_leadLine1[bar] == 0 || _leadLine2[bar] == 0)
				return;

			if (_leadLine1[bar] > _leadLine2[bar])
			{
				_upSeries[bar] = new RangeValue
					{ Upper = _leadLine1[bar], Lower = _leadLine2[bar] };

				if (_leadLine1[bar - 1] < _leadLine2[bar - 1])
					_downSeries[bar] = _upSeries[bar];
			}
			else
			{
				_downSeries[bar] = new RangeValue
					{ Upper = _leadLine2[bar], Lower = _leadLine1[bar] };

				if (_leadLine1[bar - 1] > _leadLine2[bar - 1])
					_upSeries[bar] = _downSeries[bar];
			}
		}

		#endregion
	}
}