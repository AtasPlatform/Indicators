namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;
	using System.Windows.Media;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Rendering.Settings;

	using Utils.Common.Localization;

	[DisplayName("CCI")]
	[LocalizedDescription(typeof(Resources), "CCI")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/6854-cci")]
	public class CCI : Indicator
	{
		#region Fields

		private readonly SMA _sma = new SMA();
		private readonly ValueDataSeries _typicalSeries = new ValueDataSeries("typical");

		#endregion

		#region Properties

		[Display(ResourceType = typeof(Resources),
			Name = "Period",
			GroupName = "Common",
			Order = 20)]
		public int Period
		{
			get => _sma.Period;
			set
			{
				if (value <= 0)
					return;

				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public CCI()
			: base(true)
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 10;

			LineSeries.Add(new LineSeries("100")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = 100,
				Width = 1,
				UseScale = true
			});
			LineSeries.Add(new LineSeries("-100")
			{
				Color = Colors.Orange,
				LineDashStyle = LineDashStyle.Dash,
				Value = -100,
				Width = 1,
				UseScale = true
			});
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var candle = GetCandle(bar);
			decimal mean = 0;
			var typical = (candle.High + candle.Low + candle.Close) / 3m;
			var sma0 = _sma.Calculate(bar, typical);
			_typicalSeries[bar] = typical;

			for (var i = bar; i > bar - Period && i >= 0; i--)
				mean += Math.Abs(_typicalSeries[i] - sma0);

			var res = 0.015m * (mean / Math.Min(Period, bar + 1));
			this[bar] = (typical - sma0) / (res <= 0.000000001m ? 1 : res);
		}

		#endregion
	}
}