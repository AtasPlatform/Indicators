namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Standard Error Bands")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.StdErrBandsDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602232")]
	public class StdErrBands : Indicator
	{
        #region Fields

        private readonly LinearReg _linReg = new() { Period = 10 };
        private readonly SMA _sma = new() { Period = 10 };

        private readonly ValueDataSeries _botSeries = new("BotSeries", Strings.BottomBand)
		{
			Color = Colors.DodgerBlue,
            DescriptionKey = nameof(Strings.BottomBandDscription),
        };

		private readonly ValueDataSeries _topSeries = new("TopSeries", Strings.TopBand)
		{ 
			Color = Colors.DodgerBlue,
            DescriptionKey = nameof(Strings.TopBandDscription),
        };

		private int _stdDev = 1;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				_sma.Period = _linReg.Period = value;
				RecalculateValues();
			}
		}

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.StdDev), GroupName = nameof(Strings.Settings), Description = nameof(Strings.DeviationRangeDescription), Order = 110)]
		[Range(1, 10000)]
        public int StdDev
		{
			get => _stdDev;
			set
			{
				_stdDev = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public StdErrBands()
		{
			DataSeries[0] = _topSeries;
			DataSeries.Add(_botSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_sma.Calculate(bar, value);
			_linReg.Calculate(bar, value);

			if (bar == 0)
				DataSeries.ForEach(x => x.Clear());

			if (bar < Period)
				return;

			var diffSum = 0m;
			var kSum = 0m;
			var kDiffSum = 0m;

			for (var i = bar - Period; i < bar; i++)
			{
				var diff = (decimal)SourceDataSeries[i] - _sma[i];
				diffSum += diff * diff;

				var k = i - (Period - 1) / 2m;
				kSum += k * k;

				kDiffSum += k * diff;
			}

			var sum = (double)((diffSum - kDiffSum * kDiffSum) / ((Period - 2) * kSum));

			var sqrt = Math.Sqrt(Math.Abs(sum));

			var se = (decimal)sqrt;
			_topSeries[bar] = _linReg[bar] + _stdDev * se;
			_botSeries[bar] = _linReg[bar] - _stdDev * se;
		}

		#endregion
	}
}