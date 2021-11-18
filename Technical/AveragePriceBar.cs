namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Average Price for Bar")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43409-average-price-for-bar")]
	public class AveragePriceBar : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Resources), Name = "HighLow2")]
			Hl2,

			[Display(ResourceType = typeof(Resources), Name = "HighLowClose3")]
			Hlc3,

			[Display(ResourceType = typeof(Resources), Name = "OpenHighLowClose4")]
			Ohlc4,

			[Display(ResourceType = typeof(Resources), Name = "HighLow2Close4")]
			Hl2c4
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private Mode _calcMode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "CalculationMode", GroupName = "Settings", Order = 110)]
		public Mode CalcMode
		{
			get => _calcMode;
			set
			{
				_calcMode = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public AveragePriceBar()
			: base(true)
		{
			_calcMode = Mode.Hlc3;
			_renderSeries.Color = Colors.Blue;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			var averValue = 0m;

			switch (_calcMode)
			{
				case Mode.Hl2:
					averValue = (candle.High + candle.Low) / 2;
					break;
				case Mode.Hlc3:
					averValue = (candle.High + candle.Low + candle.Close) / 3;
					break;
				case Mode.Ohlc4:
					averValue = (candle.Open + candle.High + candle.Low + candle.Close) / 4;
					break;
				case Mode.Hl2c4:
					averValue = (candle.High + candle.Low + 2 * candle.Close) / 4;
					break;
			}

			_renderSeries[bar] = averValue;
		}

		#endregion
	}
}