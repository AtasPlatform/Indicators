namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Highest High/Lowest Low Over N Bars")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.HighLowIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602244")]
	public class HighLow : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _highSeries = new("High");
		private readonly ValueDataSeries _lowSeries = new("Low");

		private readonly ValueDataSeries _maxSeries = new("MaxSeries", Strings.Highest) { Color = DefaultColors.Green.Convert() };
        private readonly ValueDataSeries _minSeries = new("MinSeries", Strings.Lowest);
		private int _period = 15;

        #endregion

        #region Properties

        [Parameter]
		[Range(1, 10000)]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public HighLow()
			:base(true)
		{
			DenyToChangePanel = true;
			
			DataSeries[0] = _maxSeries;
			DataSeries.Add(_minSeries);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highSeries[bar] = candle.High;
			_lowSeries[bar] = candle.Low;

			_maxSeries[bar] = _highSeries.MAX(_period, bar);
			_minSeries[bar] = _lowSeries.MIN(_period, bar);
		}

		#endregion
	}
}