namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("RVI V2")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7175-rvirvi-v2")]
	public class RVI2 : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _rviSignal = new(Resources.RVI);
		private readonly ValueDataSeries _rviValues = new(Resources.Signal);

		private readonly SMA _smaHighLow = new();
		private readonly SMA _smaOpenClose = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Period", GroupName = "Settings", Order = 100)]
		public int Period
		{
			get => _smaOpenClose.Period;
			set
			{
				if (value <= 0)
					return;

				_smaOpenClose.Period = _smaHighLow.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public RVI2()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			_smaOpenClose.Period = _smaHighLow.Period = 10;

			DataSeries[0] = _rviSignal;
			DataSeries.Add(_rviValues);
		}

		#endregion

		#region Protected methods

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar < 3)
			{
				_smaHighLow.Calculate(bar, 0);
				_smaOpenClose.Calculate(bar, 0);
				return;
			}

			var candle = GetCandle(bar);
			var prevCandle = GetCandle(bar - 1);
			var prev2Candle = GetCandle(bar - 2);
			var prev3Candle = GetCandle(bar - 3);

			var closeOpen = (prev3Candle.Close - prev3Candle.Open +
				2 * (prev2Candle.Close - prev2Candle.Open) +
				2 * (prevCandle.Close - prevCandle.Open) +
				candle.Close - candle.Open) / 6m;

			var highLow = (prev3Candle.High - prev3Candle.Low +
				2 * (prev2Candle.High - prev2Candle.Low) +
				2 * (prevCandle.High - prevCandle.Low) +
				candle.High - candle.Low) / 6m;

			_rviValues[bar] = _smaOpenClose.Calculate(bar, closeOpen) / _smaHighLow.Calculate(bar, highLow);

			_rviSignal[bar] = (_rviValues[bar - 3] + 2 * _rviValues[bar - 2] + 2 * _rviValues[bar - 1] + _rviValues[bar]) / 6m;
		}

		#endregion
	}
}