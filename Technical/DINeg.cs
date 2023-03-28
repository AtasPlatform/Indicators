namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Drawing;
	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	using Utils.Common.Localization;

	[DisplayName("DI-")]
	[LocalizedDescription(typeof(Resources), "DINeg")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/8526-adx-di-di-")]
	public class DINeg : Indicator
	{
		#region Fields

		private readonly ATR _atr = new() { Period = 10 };
		private readonly WMA _wma = new() { Period = 10 };

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		[Range(1, 10000)]
		public int Period
		{
			get => _wma.Period;
			set
			{
				_wma.Period = _atr.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public DINeg()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;

			((ValueDataSeries)DataSeries[0]).Color = DefaultColors.Red.Convert();
			((ValueDataSeries)DataSeries[0]).LineDashStyle = LineDashStyle.Dash;
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

				var val = currentCandle.Low < prevCandle.Low && currentCandle.High - prevCandle.High < prevCandle.Low - currentCandle.Low
					? prevCandle.Low - currentCandle.Low
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