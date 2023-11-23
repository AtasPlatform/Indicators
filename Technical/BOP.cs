namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Balance of Power")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.BOPDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602623")]
	public class BOP : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _bop = new("BOP");
		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private readonly SMA _sma = new()
		{
			Period = 14
		};

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
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BOP()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var highLow = candle.High - candle.Low;

			_bop[bar] = highLow == 0 ? 0 : (candle.Close - candle.Open) / highLow;
			
			_renderSeries[bar] = _sma.Calculate(bar, _bop[bar]);
		}

		#endregion
	}
}