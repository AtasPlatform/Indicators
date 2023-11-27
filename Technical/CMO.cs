namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Chaikin Money Oscillator")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.CMODescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602299")]
	public class CMO : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _ad = new("AdLine");
		private readonly ValueDataSeries _cmo = new("Cmo", "Oscillator");
		private decimal _dailyHigh;
		private decimal _dailyLow;
		private DateTime _lastSessionTime;
		private int _periodLong = 10;

		private int _periodShort = 3;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), Description = nameof(Strings.LongPeriodDescription))]
		[Range(1, 10000)]
		public int PeriodLong
		{
			get => _periodLong;
			set
			{
				_periodLong = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), Description = nameof(Strings.ShortPeriodDescription))]
		[Range(1, 10000)]
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
			DataSeries[0] = _cmo;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
			{
				_cmo.Clear();
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