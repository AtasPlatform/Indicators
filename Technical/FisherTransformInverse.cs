namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Drawing;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Inverse Fisher Transform")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.FisherTransformInverseDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602407")]
	public class FisherTransformInverse : Indicator
	{
		#region Fields

		private readonly Highest _highest = new() { Period = 10 };
		private readonly Lowest _lowest = new() { Period = 10 };

        private readonly ValueDataSeries _ift = new("Ift", Strings.Indicator);
		private readonly ValueDataSeries _iftSmoothed = new("IftSmoothed", Strings.SMA)
		{
			Color = DefaultColors.Green.Convert(),
			IgnoredByAlerts = true,
            DescriptionKey = nameof(Strings.SmaSetingsDescription)
        };
		
		private readonly SMA _sma = new();
		private readonly WMA _wma = new();

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLow), GroupName = nameof(Strings.Period), Description = nameof(Strings.PeriodDescription), Order = 90)]
		[Range(1, 10000)]
		public int HighLowPeriod
		{
			get => _highest.Period;
			set
			{
				_highest.Period = _lowest.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.WMA), GroupName = nameof(Strings.Period), Description = nameof(Strings.WMAPeriodDescription), Order = 100)]
		[Range(1, 10000)]
        public int WmaPeriod
		{
			get => _wma.Period;
			set
			{
				_wma.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.SMA), GroupName = nameof(Strings.Period), Description = nameof(Strings.SMAPeriodDescription), Order = 110)]
		[Range(1, 10000)]
        public int SmaPeriod
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

		public FisherTransformInverse()
		{
			Panel = IndicatorDataProvider.NewPanel;
			
			DataSeries[0] = _ift;
			DataSeries.Add(_iftSmoothed);
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			var high = _highest.Calculate(bar, value);
			var low = _lowest.Calculate(bar, value);
			var eps = 0m;

			if (high != low)
				eps = 10m * (value - low) / (high - low) - 5;

			var epsSmoothed = _wma.Calculate(bar, eps);

			_ift[bar] = 0m;

			if (bar > 0)
			{
				if (high != low)
				{
					var expEps = (decimal)Math.Exp(Convert.ToDouble(2 * epsSmoothed));
					_ift[bar] = (expEps - 1) / (expEps + 1);
				}
				else
					_ift[bar] = _ift[bar - 1];
			}

			_iftSmoothed[bar] = _sma.Calculate(bar, _ift[bar]);
		}

		#endregion
	}
}