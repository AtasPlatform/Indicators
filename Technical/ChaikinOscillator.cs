namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Rendering.Settings;

	[DisplayName("Chaikin Oscillator")]
	public class ChaikinOscillator : Indicator
	{
		#region Fields

		private readonly EMA _emaLong = new EMA();
		private readonly EMA _emaShort = new EMA();

		private readonly LineSeries _overbought = new LineSeries("Overbought");
		private readonly LineSeries _oversold = new LineSeries("Oversold");
		private int _divisor;
		private decimal _exAd;
		private decimal _lastAd;
		private int _lastBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "LongPeriod", Order = 1)]
		public int LongAvg
		{
			get => _emaLong.Period;
			set
			{
				if (value <= 0)
					return;

				_emaLong.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "ShortPeriod", Order = 2)]
		public int ShortAvg
		{
			get => _emaShort.Period;
			set
			{
				if (value <= 0)
					return;

				_emaShort.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Divisor", Order = 2)]
		public int Divisor
		{
			get => _divisor;
			set
			{
				if (value <= 0)
					return;

				_divisor = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Overbought", Order = 2)]
		public decimal Overbought
		{
			get => _overbought.Value;
			set
			{
				_overbought.Value = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), GroupName = "Common", Name = "Oversold", Order = 2)]
		public decimal Oversold
		{
			get => _oversold.Value;
			set
			{
				_oversold.Value = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ChaikinOscillator()
			: base(true)
		{
			_emaLong.Period = 10;
			_emaShort.Period = 3;
			_divisor = 3;
			_lastBar = -1;

			Panel = IndicatorDataProvider.NewPanel;

			DataSeries[0] = new ValueDataSeries("ChaikinOscillator")
			{
				Color = Colors.Blue,
				LineDashStyle = LineDashStyle.Solid,
				VisualType = VisualMode.Line,
				Width = 2
			};

			_overbought = new LineSeries("Overbought")
			{
				Color = Colors.Red,
				Width = 1,
				Value = Overbought
			};

			_oversold = new LineSeries("Oversold")
			{
				Color = Colors.Red,
				Width = 1,
				Value = Oversold
			};

			LineSeries.Add(_overbought);

			LineSeries.Add(_oversold);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var currentCandle = GetCandle(bar);

			var ad = AccumulationDistributionBase(currentCandle);

			if (bar == 0)
				_exAd = ad;
			else
			{
				if (bar != _lastBar)
					_exAd = _lastAd;
				else
					_lastAd = ad;

				ad += _exAd;
			}

			var emaShort = _emaShort.Calculate(bar, ad);
			var emaLong = _emaLong.Calculate(bar, ad);

			var oscValue = (emaShort - emaLong) / Divisor;

			DataSeries[0][bar] = oscValue;

			_lastBar = bar;
		}

		#endregion

		#region Private methods

		private decimal AccumulationDistributionBase(IndicatorCandle candle)
		{
			var high = Convert.ToDouble(candle.High);
			var low = Convert.ToDouble(candle.Low);
			var close = Convert.ToDouble(candle.Close);
			var volume = Convert.ToDouble(candle.Volume);

			var ad = (close - low - (high - close)) / (high - low + Math.Pow(10, -9)) * volume;
			return Convert.ToDecimal(ad);
		}

		#endregion
	}
}