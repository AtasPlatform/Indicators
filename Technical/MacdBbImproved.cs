namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using OFT.Rendering.Settings;

	[DisplayName("MACD Bollinger Bands - Improved")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.MacdBbImprovedDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602628")]
	public class MacdBbImproved : Indicator
	{
        #region Fields

        private readonly ValueDataSeries _topBand = new("TopBand", Strings.TopBand)
        {
            Color = System.Drawing.Color.Purple.Convert(),
            IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.TopBandDscription)
        };

        private readonly ValueDataSeries _bottomBand = new("BottomBand", Strings.BottomBand)
		{
			Color = System.Drawing.Color.Purple.Convert(),
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.BottomBandDscription)
        };

        private readonly MACD _macd = new()
        {
			LongPeriod = 26,
			ShortPeriod = 12,
			SignalPeriod = 9
        };

        private readonly SMA _sma = new() { Period = 9 };
		private readonly StdDev _stdDev = new() { Period = 9 };		
		private int _stdDevCount = 2;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
        public int MacdPeriod
		{
			get => _macd.SignalPeriod;
			set
			{
				_macd.SignalPeriod = _stdDev.Period = _sma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.ShortPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int MacdShortPeriod
		{
			get => _macd.ShortPeriod;
			set
			{
				_macd.ShortPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.LongPeriod), GroupName = nameof(Strings.Settings), Description = nameof(Strings.LongPeriodDescription), Order = 120)]
		[Range(1, 10000)]
        public int MacdLongPeriod
		{
			get => _macd.LongPeriod;
			set
			{
				_macd.LongPeriod = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StdDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.StdDevPeriodDescription), Order = 130)]
		[Range(1, 10000)]
        public int StdDev
		{
			get => _stdDevCount;
			set
			{
				_stdDevCount = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MacdBbImproved()
		{
			Panel = IndicatorDataProvider.NewPanel;
			((ValueDataSeries)_macd.DataSeries[1]).LineDashStyle = LineDashStyle.Solid;
			
			DataSeries[0] = _topBand;
			DataSeries.Add(_bottomBand);
			DataSeries.AddRange(_macd.DataSeries);
		}

		#endregion

		#region Protected methods
		
		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}
		
		protected override void OnCalculate(int bar, decimal value)
		{
			_macd.Calculate(bar, value);

			var deltaMacd = ((ValueDataSeries)_macd.DataSeries[0])[bar] - ((ValueDataSeries)_macd.DataSeries[1])[bar];
			_sma.Calculate(bar, Math.Abs(deltaMacd));

			var macdMa = ((ValueDataSeries)_macd.DataSeries[1])[bar];

			var stdDev = _stdDev.Calculate(bar, macdMa);

			_topBand[bar] = macdMa + _sma[bar] + _stdDevCount * stdDev;
			_bottomBand[bar] = macdMa - _sma[bar] - _stdDevCount * stdDev;
		}

		#endregion
	}
}