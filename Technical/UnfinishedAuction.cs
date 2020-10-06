namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.Linq;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	using Pen = System.Drawing.Pen;

	[DisplayName("Unfinished Auction")]
	[Description("Unfinished Auction")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/17466-unfinished-auction")]
	public class UnfinishedAuctionMod : Indicator
	{
		#region Fields

		private readonly PriceSelectionDataSeries _priceSelectionSeries = new PriceSelectionDataSeries("Clusters Selection");
		private int _askFilter = 20;

		private int _bidFilter = 20;
		private Color _highColor = Colors.Red;
		private Color _highLineColor = Colors.Crimson;
		private int _lineWidth = 20;
		private Color _lowColor = Colors.Blue;

		private Color _lowLineColor = Colors.Aqua;

		#endregion

		#region Properties

		[DisplayName("Bid Filter")]
		public int BidFilter
		{
			get => _bidFilter;
			set
			{
				_bidFilter = value;
				RecalculateValues();
			}
		}

		[DisplayName("Ask Filter")]
		public int AskFilter
		{
			get => _askFilter;
			set
			{
				_askFilter = value;
				RecalculateValues();
			}
		}

		[DisplayName("Line Width")]
		public int LineWidth
		{
			get => _lineWidth;
			set
			{
				_lineWidth = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Colors")]
		[DisplayName("Low Line Color")]
		public Color LowLineColor
		{
			get => _lowLineColor;
			set
			{
				_lowLineColor = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Colors")]
		[DisplayName("High Line Color")]
		public Color HighLineColor
		{
			get => _highLineColor;
			set
			{
				_highLineColor = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Colors")]
		[DisplayName("Low Color")]
		public Color LowColor
		{
			get => _lowColor;
			set
			{
				_lowColor = value;
				RecalculateValues();
			}
		}

		[LocalizedCategory(typeof(Resources), "Colors")]
		[DisplayName("High Color")]
		public Color HighColor
		{
			get => _highColor;
			set
			{
				_highColor = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public UnfinishedAuctionMod()
			: base(true)
		{
			DataSeries[0] = _priceSelectionSeries;
			DataSeries[0].IsHidden = true;
			_highLineColor.A = _highColor.A = _lowLineColor.A = _lowColor.A = 150;
			_lineWidth = 10;
			DenyToChangePanel = true;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			CalculateAuctionAt(bar);
		}

		#endregion

		#region Private methods

		private void CalculateAuctionAt(int bar)
		{
			HorizontalLinesTillTouch.RemoveAll(t => t.FirstBar == bar);

			var candle = GetCandle(bar);
			var priceSelectionValues = _priceSelectionSeries[bar];
			priceSelectionValues.Clear();

			//Ищем новые начала трендовых
			var candlePvLow = candle.GetPriceVolumeInfo(candle.Low);
			var candlePvHigh = candle.GetPriceVolumeInfo(candle.High);

			if (candlePvLow != null && candlePvLow.Ask > 0 && candlePvLow.Bid > _bidFilter)
			{
				var lowPenColor = System.Drawing.Color.FromArgb(_lowLineColor.A, _lowLineColor.R, _lowLineColor.G, _lowLineColor.B);
				var lowPen = new Pen(lowPenColor)
				{
					Width = _lineWidth
				};

				var tt = new LineTillTouch(bar, candle.Low, lowPen);
				HorizontalLinesTillTouch.Add(tt);
			}

			if (candlePvHigh != null && candlePvHigh.Ask > _askFilter && candlePvHigh.Bid > 0)
			{
				var highPenColor = System.Drawing.Color.FromArgb(_highLineColor.A, _highLineColor.R, _highLineColor.G, _highLineColor.B);
				var highPen = new Pen(highPenColor)
				{
					Width = _lineWidth
				};
				var tt = new LineTillTouch(bar, candle.High, highPen);
				HorizontalLinesTillTouch.Add(tt);
			}

			foreach (var trendLine in HorizontalLinesTillTouch.Where(t => t.FirstBar == bar || t.SecondBar == bar && t.SecondBar != CurrentBar && t.Finished))
			{
				var value = trendLine.FirstPrice;
				var cl = Colors.Black;
				if (trendLine.FirstBar == bar)
					cl = value == candle.Low ? _lowColor : _highColor;
				else
				{
					var val = _priceSelectionSeries[trendLine.FirstBar].FirstOrDefault(t => t.MinimumPrice == value);
					if (val != null)
						cl = val.ObjectColor;
				}

				priceSelectionValues.Add(new PriceSelectionValue(value)
				{
					VisualObject = ObjectType.OnlyCluster,
					ObjectColor = cl,
					Size = 100,
					PriceSelectionColor = cl
				});
			}
		}

		#endregion
	}
}