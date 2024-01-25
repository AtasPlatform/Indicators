namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Windows.Media;
    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Price Volume Trend")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.VolumeTrendDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602450")]
	public class VolumeTrend : Indicator
	{
		#region ctor

		public VolumeTrend()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			LineSeries.Add(new LineSeries("ZeroVal", Strings.ZeroValue) { Color = Colors.Gray, Value = 0, Width = 2 });
			DataSeries[0].UseMinimizedModeIfEnabled = true;
        }

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);

			this[bar] = candle.Close != 0
				? (candle.Close - prevCandle.Close) / candle.Close * candle.Volume + this[bar - 1]
				: this[bar - 1];
		}

		#endregion
	}
}