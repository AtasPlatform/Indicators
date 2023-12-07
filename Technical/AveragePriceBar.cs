namespace ATAS.Indicators.Technical
{
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;

    using ATAS.Indicators.Drawing;

    using OFT.Attributes;
    using OFT.Localization;

    [DisplayName("Average Price for Bar")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.AveragePriceBarDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602324")]
	public class AveragePriceBar : Indicator
	{
		#region Nested types

		public enum Mode
		{
			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLow2))]
			Hl2,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLowClose3))]
			Hlc3,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.OpenHighLowClose4))]
			Ohlc4,

			[Display(ResourceType = typeof(Strings), Name = nameof(Strings.HighLow2Close4))]
			Hl2c4
		}

		#endregion

		#region Fields

		private readonly ValueDataSeries _renderSeries = new("RenderSeries", Strings.Visualization);
		private Mode _calcMode;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Strings), Name = nameof(Strings.CalculationMode), GroupName = nameof(Strings.Settings), Description = nameof(Strings.CalculationModeDescription), Order = 110)]
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