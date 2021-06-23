namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Market Facilitation Index")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/ru/knowledge-bases/2/articles/45433-market-facilitation-index")]
	public class MarketFacilitation : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _multiplier;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Multiplier", GroupName = "Settings", Order = 100)]
		public int Multiplier
		{
			get => _multiplier;
			set
			{
				if (value <= 0)
					return;

				_multiplier = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MarketFacilitation()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_multiplier = 1;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);

			if (candle.Volume != 0)
				_renderSeries[bar] = (candle.High - candle.Low) * _multiplier / candle.Volume;
			else
				_renderSeries[bar] = 0m;
		}

		#endregion
	}
}