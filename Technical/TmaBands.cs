namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	[DisplayName("TMA Bands")]
	public class TmaBands : Indicator
	{
		#region Fields

		private readonly ATR _atr = new()
		{
			Period = 75
		};

		private readonly WMA _wma = new()
		{
			Period = 35
		};

		private decimal _atrMultiplier = 4m;

		private ValueDataSeries _botSeries = new(Resources.BottomBand)
		{
			Color = Colors.Purple
		};

		private ValueDataSeries _midSeries = new(Resources.Middle)
		{
			Color = Colors.Maroon
		};

		private ValueDataSeries _topSeries = new(Resources.TopBand)
		{
			Color = Colors.Purple
		};

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "Settings", Name = "Period", Order = 100)]
		[Range(1, 100000)]
		public int Period
		{
			get => _wma.Period;
			set
			{
				_wma.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "ATR", Name = "Period", Order = 200)]
		[Range(1, 100000)]
		public int AtrPeriod
		{
			get => _atr.Period;
			set
			{
				_atr.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "ATR", Name = "Multiplier", Order = 220)]
		[Range(0.0001, 100000)]
		public decimal AtrMultiplier
		{
			get => _atrMultiplier;
			set
			{
				_atrMultiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public TmaBands()
		{
			Add(_atr);
			DataSeries[0] = _midSeries;
			DataSeries.Add(_topSeries);
			DataSeries.Add(_botSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			_wma.Calculate(bar, value);

			if (bar < 3)
				return;

			var tma = Swma(bar, (ValueDataSeries)_wma.DataSeries[0]);
			var range = _atr[bar] * AtrMultiplier;

			_midSeries[bar] = tma;
			_topSeries[bar] = tma + range;
			_botSeries[bar] = tma - range;
		}

		#endregion

		#region Private methods

		private decimal Swma(int bar, ValueDataSeries series)
		{
			var value = series[bar];
			var prevValue = series[bar - 1];
			var prev2Value = series[bar - 2];
			var prev3Value = series[bar - 3];

			return prev3Value / 6 + prev2Value * 2 / 6 + prevValue * 2 / 6 + value / 6;
		}

		#endregion
	}
}