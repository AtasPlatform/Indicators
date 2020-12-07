namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("DI+")]
	[LocalizedDescription(typeof(Resources), "DIPos")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class DIPos : Indicator
	{
		#region Fields

		private readonly ATR _atr = new ATR();
		private readonly WMA _wma = new WMA();

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _wma.Period;
			set
			{
				if (value <= 0)
					return;

				_wma.Period = _atr.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DIPos()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = Colors.Blue;

			Period = 10;

			Add(_atr);
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar > 0)
			{
				var atr = _atr[bar];

				var currentCandle = GetCandle(bar);
				var prevCandle = GetCandle(bar - 1);

				var val = currentCandle.High > prevCandle.High && currentCandle.High - prevCandle.High > prevCandle.Low - currentCandle.Low
					? currentCandle.High - prevCandle.High
					: 0m;

				var wma = _wma.Calculate(bar, val);

				this[bar] = atr != 0m ? 100m * wma / atr : 0m;
			}
			else
				this[bar] = 0;
		}

		#endregion
	}
}