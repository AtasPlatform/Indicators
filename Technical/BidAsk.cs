namespace ATAS.Indicators.Technical
{
	using System;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	[DisplayName("Bid Ask")]
	[Category("Bid x Ask,Delta,Volume")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/457-bid-ask")]
	public class BidAsk : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _asks;
		private readonly ValueDataSeries _bids;

		#endregion

		[IsExpanded]
		[ExtenderProvidedProperty]
		public Times TimesSource { get; }  = new Times();

		[DisplayFormat(DataFormatString = "HH:mm:ss")]
		public Collection<DateTime> Timers { get; set; } = new Collection<DateTime>
		{
			new DateTime(1900, 1,1), 
			new DateTime(1900, 2, 1),
			new DateTime(1900, 3, 1)
		};

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

	public class Times
	{
		public bool Check { get; set; }

		[DisplayFormat(DataFormatString = "dd.MM.yyyy HH:mm:ss")]
		public DateTime StartDateTime { get; set; }

		[DisplayFormat(DataFormatString = "dd.MM.yyyy HH:mm:ss")]
		public DateTime EndDateTime { get; set; }
	}
}