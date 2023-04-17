namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("RVI")]
	[LocalizedDescription(typeof(Resources), "RVI")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7175-rvi")]
	public class RVI : Indicator
	{
		#region ctor

		public RVI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Green.Convert();

			DataSeries.Add(new ValueDataSeries("Signal")
			{
				VisualType = VisualMode.Line,
				IgnoredByAlerts = true
			});
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar > 3)
			{
				var c0 = GetCandle(bar - 3);
				var c1 = GetCandle(bar - 2);
				var c2 = GetCandle(bar - 1);
				var c3 = GetCandle(bar - 0);

				var valueNum = (c0.Close - c0.Open + 2 * (c1.Close - c1.Open) + 2 * (c2.Close - c2.Open) + (c3.Close - c3.Open)) / 6m;
				var valueDenum = (c0.High - c0.Low + 2 * (c1.High - c1.Low) + 2 * (c2.High - c2.Low) + (c3.High - c3.Low)) / 6m;

				DataSeries[0][bar] = valueDenum != 0 ? valueNum / valueDenum : valueNum;
				DataSeries[1][bar] = (this[bar - 3] + 2 * this[bar - 2] + 2 * this[bar - 1] + this[bar]) / 6m;
			}
			else
			{
				DataSeries[0][bar] = 0m;
				DataSeries[1][bar] = 0m;
			}
		}

		#endregion
	}
}