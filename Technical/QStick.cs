namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Q Stick")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.QStickDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602451")]
	public class QStick : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _openCloseSeries = new("OpenClose");

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization) { UseMinimizedModeIfEnabled = true };
		private int _period = 10;

        #endregion

        #region Properties

        [Parameter]
        [Display(ResourceType = typeof(Strings), Name = nameof(Strings.Period), GroupName = nameof(Strings.Settings), Description = nameof(Strings.PeriodDescription), Order = 100)]
		[Range(1, 10000)]
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

		public QStick()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			LineSeries.Add(new LineSeries("ZeroVal", Strings.ZeroValue)
			{
				Color = System.Drawing.Color.Gray.Convert(), 
				Value = 0, 
				Width = 2,
                DescriptionKey = nameof(Strings.ZeroLineDescription)
            });
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_openCloseSeries[bar] = candle.Close - candle.Open;

			if (bar < _period)
				return;

			_renderSeries[bar] = _openCloseSeries.CalcSum(_period, bar) / _period;
		}

		#endregion
	}
}