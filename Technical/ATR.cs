namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("ATR")]
	[LocalizedDescription(typeof(Resources), "ATR")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/6726-atr")]
	public class ATR : Indicator
	{
		#region Fields

		private int _period = 14;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				if (value <= 0)
					return;

				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public ATR()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 10;
			
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0&& ChartInfo!=null)
			{
				((ValueDataSeries)DataSeries[0]).StringFormat = ChartInfo.StringFormat;
			}
			
			var candle = GetCandle(bar);
			var high0 = candle.High;
			var low0 = candle.Low;

			if (bar == 0)
			{
				this[bar] = high0 - low0;
			}
			else
			{
				var close1 = GetCandle(bar - 1).Close;
				var trueRange = Math.Max(Math.Abs(low0 - close1), Math.Max(high0 - low0, Math.Abs(high0 - close1)));
				this[bar] = ((Math.Min(CurrentBar + 1, Period) - 1) * this[bar - 1] + trueRange) / Math.Min(CurrentBar + 1, Period);
			}
		}

		#endregion
	}
}