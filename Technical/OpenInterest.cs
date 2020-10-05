namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Properties;

	using Utils.Common.Attributes;

	[DisplayName("Open Interest")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8560-open-interest-o")]
	public class OpenInterest : Indicator
	{
		#region Nested types

		public enum OpenInterestMode
		{
			[Display(ResourceType = typeof(Resources), Name = "ByBar")]
			ByBar,

			[Display(ResourceType = typeof(Resources), Name = "Session")]
			Session,

			[Display(ResourceType = typeof(Resources), Name = "Cumulative")]
			Cumulative
		}

		#endregion

		#region Fields

		private readonly CandleDataSeries _oi = new CandleDataSeries("Open interest");

		private OpenInterestMode _mode = OpenInterestMode.ByBar;

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources), Name = "Mode")]
		public OpenInterestMode Mode
		{
			get => _mode;
			set
			{
				_mode = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public OpenInterest()
			: base(true)
		{
			((ValueDataSeries)DataSeries[0]).VisualType = VisualMode.OnlyValueOnAxis;
			DataSeries[0].Name = "Value";
			DataSeries.Add(_oi);
			Panel = IndicatorDataProvider.NewPanel;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				return;

			var currentCandle = GetCandle(bar);
			if (currentCandle.OI == 0)
				return;

			var prevCandle = GetCandle(bar - 1);
			var currentOpen = prevCandle.OI;
			var candle = _oi[bar];

			switch (_mode)
			{
				case OpenInterestMode.ByBar:
					candle.Open = 0;
					candle.Close = currentCandle.OI - currentOpen;
					candle.High = currentCandle.MaxOI - currentOpen;
					candle.Low = currentCandle.MinOI - currentOpen;
					break;

				case OpenInterestMode.Cumulative:
					candle.Open = currentOpen;
					candle.Close = currentCandle.OI;
					candle.High = currentCandle.MaxOI;
					candle.Low = currentCandle.MinOI;
					break;

				default:
					var prevvalue = _oi[bar - 1].Close;
					var dOi = currentOpen - prevvalue;

					if (IsNewSession(bar))
						dOi = currentOpen;

					candle.Open = currentOpen - dOi;
					candle.Close = currentCandle.OI - dOi;
					candle.High = currentCandle.MaxOI - dOi;
					candle.Low = currentCandle.MinOI - dOi;
					break;
			}

			this[bar] = candle.Close;
		}

		#endregion
	}
}