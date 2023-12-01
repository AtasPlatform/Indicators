namespace ATAS.Indicators.Technical
{
    using System;
    using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("KD - Fast")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.KdFastDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602411")]
	public class KdFast : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _dSeries = new("DSeries", Strings.SMA)
		{
			Color = DefaultColors.Green.Convert(),
			IgnoredByAlerts = true,
			DescriptionKey = nameof(Strings.SmaSetingsDescription)
		};
		
		private readonly ValueDataSeries _kSeries = new("KSeries", Strings.Line) 
		{
			Color = DefaultColors.Red.Convert(),
            DescriptionKey = nameof(Strings.BaseLineSettingsDescription)
        };

		private readonly Lowest _lowest = new() { Period = 10 };
        private readonly Highest _highest = new() { Period = 10 };
        private readonly SMA _sma = new() { Period = 10 };

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodK), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodKDescription), Order = 100)]
		[Range(1, 10000)]
        public int PeriodK
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.PeriodD), GroupName = nameof(Strings.Settings), Description = nameof(Strings.ShortPeriodDDescription), Order = 110)]
		[Range(1, 10000)]
		public int PeriodD
		{
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public KdFast()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _kSeries;
			DataSeries.Add(_dSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var high = _highest.Calculate(bar, candle.High);
			var low = _lowest.Calculate(bar, candle.Low);

			_kSeries[bar] = high != low
				? 100m * (candle.Close - low) / (high - low)
				: 100m;

			_dSeries[bar] = _sma.Calculate(bar, _kSeries[bar]);
		}

		#endregion
	}
}