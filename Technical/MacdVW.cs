namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("MACD - Volume Weighted")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MacdVWDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602231")]
	public class MacdVW : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 9 };

		private readonly ValueDataSeries _macdSeries = new("MacdSeries", Strings.MACD)
		{
			Color = System.Drawing.Color.CadetBlue.Convert(),
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true,
            DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
        };

		private readonly ValueDataSeries _signalSeries = new("SignalSeries", Strings.Signal) 
		{ 
			UseMinimizedModeIfEnabled = true ,
            DescriptionKey = nameof(Strings.SignalLineSettingsDescription)
        };

		private readonly ValueDataSeries _valVol = new("ValVol");
		private readonly ValueDataSeries _vol = new("Volume");
		private int _longPeriod = 26;
        private int _shortPeriod = 12;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
        public int Period
		{
			get => _ema.Period;
			set
			{
				_ema.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int ShortPeriod
		{
			get => _shortPeriod;
			set
			{
				_shortPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.LongPeriodDescription), Order = 120)]
		[Range(1, 10000)]
        public int LongPeriod
		{
			get => _longPeriod;
			set
			{
				_longPeriod = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MacdVW()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _macdSeries;
			DataSeries.Add(_signalSeries);
		}

		#endregion

		#region Protected methods
		
		protected override void OnRecalculate()
		{
			_vol.Clear();
			_valVol.Clear();

			DataSeries.ForEach(x => x.Clear());
		}
		
		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			_vol[bar] = candle.Volume;
			_valVol[bar] = value * candle.Volume;

			var volSumShort = _vol.CalcSum(ShortPeriod, bar);
			var volSumLong = _vol.CalcSum(LongPeriod, bar);

			if (volSumShort == 0 || volSumLong == 0)
			{
				_ema.Calculate(bar, 0);
				return;
			}

			var vwShort = _valVol.CalcSum(ShortPeriod, bar) / volSumShort;
			var vwLong = _valVol.CalcSum(LongPeriod, bar) / volSumLong;

			var vwMacd = vwShort - vwLong;
			_signalSeries[bar] = _ema.Calculate(bar, vwMacd);
			_macdSeries[bar] = vwMacd;
		}

		#endregion
	}
}