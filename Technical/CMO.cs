namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Chaikin Money Oscillator")]
	[Description("Chaikin Money Oscillator")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/21727-chaikin-money-oscillator")]
	public class CMO : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _ad = new ValueDataSeries("AdLine");
		private readonly ValueDataSeries _cmo = new ValueDataSeries("Oscillator");
		private decimal _dailyHigh;
		private decimal _dailyLow;
		private DateTime _lastSessionTime;
		private int _periodLong = 10;

		private int _periodShort = 3;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod")]
		public int PeriodLong
		{
			get => _periodLong;
			set
			{
				_periodLong = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod")]
		public int PeriodShort
		{
			get => _periodShort;
			set
			{
				_periodShort = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CMO()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0].IsHidden = true;
			DataSeries.Add(_cmo);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_dailyHigh = _dailyLow = 0;
				return;
			}

			var candle = GetCandle(bar);

			if (IsNewSession(bar))
			{
				if (_lastSessionTime != candle.Time)
				{
					_lastSessionTime = candle.Time;
					_dailyHigh = _dailyLow = 0;
				}
			}

			if (candle.High > _dailyHigh || _dailyHigh == 0)
				_dailyHigh = candle.High;

			if (candle.Low < _dailyLow || _dailyLow == 0)
				_dailyLow = candle.Low;

			if (_dailyHigh == _dailyLow)
				return;

			_ad[bar] = (candle.Close - _dailyLow - (_dailyHigh - candle.Close)) / (_dailyHigh - _dailyLow) * candle.Volume;

			if (bar < _periodLong)
				return;

			var emaLong = _ad[bar] * (2.0m / (1 + _periodLong)) + (1 - 2.0m / (1 + _periodLong)) * _ad[bar - 1];
			var emaShort = _ad[bar] * (2.0m / (1 + _periodShort)) + (1 - 2.0m / (1 + _periodShort)) * _ad[bar - 1];

			_cmo[bar] = emaLong - emaShort;
		}

		#endregion
	}
}