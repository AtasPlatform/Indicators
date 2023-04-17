namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
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
			_renderSeries.Color = DefaultColors.Blue.Convert();
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			var averValue = _calcMode switch
			{
				Mode.Hl2 => (candle.High + candle.Low) / 2,
				Mode.Hlc3 => (candle.High + candle.Low + candle.Close) / 3,
				Mode.Ohlc4 => (candle.Open + candle.High + candle.Low + candle.Close) / 4,
				Mode.Hl2c4 => (candle.High + candle.Low + 2 * candle.Close) / 4,
				_ => 0m
			};

			_renderSeries[bar] = averValue;
		}

		#endregion
	}
}