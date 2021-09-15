namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
	using OFT.Localization;

	[DisplayName("KeltnerChannel")]
	[Description(
		"The Keltner Channel is a similar indicator to Bollinger Bands. Here the midline is a standard moving average with the upper and lower bands offset by the SMA of the difference between the high and low of the previous bars. The offset multiplier as well as the SMA period is configurable.")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/6712-keltner-channel")]
	public class KeltnerChannel : Indicator
	{
		#region Fields

		private readonly ATR _atr = new();
		private readonly RangeDataSeries _keltner = new("BackGround");
		private readonly SMA _sma = new();
		private int _days;

		private decimal _koef;
		private int _targetBar;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "Days",
			GroupName = "Common",
			Order = 15)]
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

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = _atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Strings),
			Name = "OffsetMultiplier",
			GroupName = "Common",
			Order = 20)]
		[Parameter]
		public decimal Koef
		{
			get => _koef;
			set
			{
				if (value <= 0)
					return;

				_koef = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KeltnerChannel()
			: base(true)
		{
			_days = 20;

			DataSeries.Add(new ValueDataSeries("Upper")
			{
				VisualType = VisualMode.Line
			});

			DataSeries.Add(new ValueDataSeries("Lower")
			{
				VisualType = VisualMode.Line
			});
			DataSeries.Add(_keltner);
			Period = 34;
			Koef = 4;

			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
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
						((ValueDataSeries)DataSeries[0]).SetPointOfEndLine(_targetBar - 1);
						((ValueDataSeries)DataSeries[1]).SetPointOfEndLine(_targetBar - 1);
						((ValueDataSeries)DataSeries[2]).SetPointOfEndLine(_targetBar - 1);
					}
				}
			}

			var currentCandle = GetCandle(bar);
			var ema = _sma.Calculate(bar, currentCandle.Close);

			if (bar < _targetBar)
				return;

			var atr = _atr[bar];
			this[bar] = ema;
			DataSeries[1][bar] = ema + atr * Koef;
			DataSeries[2][bar] = ema - atr * Koef;
			_keltner[bar].Upper = ema + atr * Koef;
			_keltner[bar].Lower = ema - atr * Koef;
		}

		#endregion
	}
}