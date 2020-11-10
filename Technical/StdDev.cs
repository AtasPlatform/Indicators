namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;
	using OFT.Attributes.Editors;

	using Utils.Common.Localization;

	[DisplayName("StdDev")]
	[LocalizedDescription(typeof(Resources), "StdDev")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/7208-stddev")]
	public class StdDev : Indicator
	{
		#region Fields

		private readonly SMA _sma = new SMA();

		#endregion

		#region Properties

		[Parameter]
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

		public StdDev()
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var sma = _sma.Calculate(bar, value);

			var start = Math.Max(0, bar - Period + 1);
			var count = Math.Min(bar + 1, Period);

			var sum = 0m;

			for (var i = start; i < start + count; i++)
			{
				var tmp = Math.Abs((decimal)SourceDataSeries[i] - sma);
				sum += tmp * tmp;
			}

			this[bar] = (decimal)Math.Sqrt((double)(sum / count));
		}

		#endregion
	}
}