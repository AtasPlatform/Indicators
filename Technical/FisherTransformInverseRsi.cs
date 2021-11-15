namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	[DisplayName("Inverse Fisher Transform with RSI")]
	[FeatureId("NotReady")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/45432-inverse-fisher-transform-with-rsi")]
	public class FisherTransformInverseRsi : Indicator
	{
		#region Fields

		private readonly ValueDataSeries _ift = new(Resources.Visualization);

		private readonly RSI _rsi = new();
		private readonly WMA _wma = new();

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "RSI", GroupName = "Period", Order = 90)]
		public int HighLowPeriod
		{
			get => _rsi.Period;
			set
			{
				if (value <= 0)
					return;

				_rsi.Period = value;
				RecalculateValues();
			}
		}

		[Display(ResourceType = typeof(Resources), Name = "WMA", GroupName = "Period", Order = 100)]
		public int WmaPeriod
		{
			get => _wma.Period;
			set
			{
				if (value <= 0)
					return;

				_wma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public FisherTransformInverseRsi()
		{
			Panel = IndicatorDataProvider.NewPanel;

			_rsi.Period = _wma.Period = 10;

			DataSeries[0] = _ift;
		}

		#endregion

		#region Protected methods

		#region Overrides of BaseIndicator

		protected override void OnRecalculate()
		{
			DataSeries.ForEach(x => x.Clear());
		}

		#endregion

		protected override void OnCalculate(int bar, decimal value)
		{
			var rsiValue = (_rsi.Calculate(bar, value) - 50) / 10;
			var rsiSmoothed = _wma.Calculate(bar, rsiValue);

			var expValue = (decimal)Math.Exp((double)(2 * rsiSmoothed));

			_ift[bar] = (expValue - 1) / (expValue + 1);
		}

		#endregion
	}
}