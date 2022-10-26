namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("MACD - Volume Weighted")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45417-macd-volume-weighted")]
	public class MacdVW : Indicator
	{
		#region Fields

		private readonly EMA _ema = new() { Period = 9 };

		private readonly ValueDataSeries _macdSeries = new(Resources.MACD)
		{
			Color = Colors.CadetBlue,
			VisualType = VisualMode.Histogram,
			UseMinimizedModeIfEnabled = true
		};

		private readonly ValueDataSeries _signalSeries = new(Resources.Signal) { UseMinimizedModeIfEnabled = true };

		private readonly ValueDataSeries _valVol = new("ValVol");
		private readonly ValueDataSeries _vol = new("Volume");
		private int _longPeriod = 26;
        private int _shortPeriod = 12;

        #endregion

        #region Properties

        [Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
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

		[Display(ResourceType = typeof(Resources), Name = "ShortPeriod", GroupName = "Settings", Order = 110)]
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

		[Display(ResourceType = typeof(Resources), Name = "LongPeriod", GroupName = "Settings", Order = 120)]
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