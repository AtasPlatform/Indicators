namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("BollingerBands: Bandwidth")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/43437-bollingerbands-bandwidth")]
	public class BollingerBandsBandwidth : Indicator
	{
		#region Fields

		private readonly BollingerBands _bb = new();

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _bb.Period;
			set
			{
				if (value <= 0)
					return;

				_bb.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "BBandsWidth", GroupName = "Settings", Order = 110)]
		[Range(0.0, 999999)]
		public decimal Width
		{
			get => _bb.Width;
			set
			{
				_bb.Width = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public BollingerBandsBandwidth()
		{
			Panel = IndicatorDataProvider.NewPanel;
			_bb.Period = 10;
			_bb.Width = 1;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			_bb.Calculate(bar, value);
			var sma = ((ValueDataSeries)_bb.DataSeries[0])[bar];
			var top = ((ValueDataSeries)_bb.DataSeries[1])[bar];
			var bot = ((ValueDataSeries)_bb.DataSeries[2])[bar];

			if (sma == 0)
				return;

			_renderSeries[bar] = 100 * (top - bot) / sma;
		}

		#endregion
	}
}