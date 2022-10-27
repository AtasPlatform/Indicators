namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Repulse")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45501-repulse")]
	public class Repulse : Indicator
	{
		#region Fields

		private readonly EMA _emaHigh = new();
		private readonly EMA _emaLow = new();
		private readonly ValueDataSeries _highSeries = new("High");
		private readonly ValueDataSeries _lowSeries = new("Low");

		private readonly ValueDataSeries _renderSeries = new(Resources.Visualization);
		private int _period = 10;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		[Range(1, 10000)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				_emaLow.Period = _emaHigh.Period = value * 5;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Repulse()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			DataSeries[0] = _renderSeries;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			_highSeries[bar] = candle.High;
			_lowSeries[bar] = candle.Low;

			if (bar < Period)
			{
				var firstCandle = GetCandle(0);
				var max = _highSeries.MAX(bar, bar);
				var min = _lowSeries.MIN(bar, bar);

				var bull = 100 * (3 * candle.Close - 2 * min - firstCandle.Open) / candle.Close;
				var bear = 100 * (firstCandle.Open + 2 * max - 3 * candle.Close) / candle.Close;
				_emaHigh.Calculate(bar, bull);
				_emaLow.Calculate(bar, bear);
			}
			else
			{
				var firstCandle = GetCandle(bar - Period);
				var max = _highSeries.MAX(Period, bar);
				var min = _lowSeries.MIN(Period, bar);

				var bull = 100 * (3 * candle.Close - 2 * min - firstCandle.Open) / candle.Close;
				var bear = 100 * (firstCandle.Open + 2 * max - 3 * candle.Close) / candle.Close;
				_emaHigh.Calculate(bar, bull);
				_emaLow.Calculate(bar, bear);
			}

			_renderSeries[bar] = _emaHigh[bar] - _emaLow[bar];
		}

		#endregion
	}
}