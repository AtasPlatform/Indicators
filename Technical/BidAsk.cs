namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Windows.Media;

	using OFT.Attributes;

	[DisplayName("Bid Ask")]
	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/457-bid-ask")]
	public class BidAsk : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _asks;
		private readonly ValueDataSeries _bids;

		#endregion

		#region ctor

		public BidAsk()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_bids = (ValueDataSeries)DataSeries[0];
			_bids.Color = Colors.Red;
			_bids.VisualType = VisualMode.Histogram;
			_bids.Name = "Bid";

			_asks = new ValueDataSeries("Ask")
			{
				VisualType = VisualMode.Histogram,
				Color = Colors.Green
			};
			DataSeries.Add(_asks);
		}

		#endregion

		#region Public methods

		public override string ToString()
		{
			return "Bid Ask";
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_bids[bar] = -candle.Bid;
			_asks[bar] = candle.Ask;
		}

		#endregion
	}
}