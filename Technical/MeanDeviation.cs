namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("MeanDev")]
	[LocalizedDescription(typeof(Resources), "MeanDeviation")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7080-meandev")]
	public class MeanDev : Indicator
	{
		#region Fields

		private readonly SMA _sma = new() { Period = 10 };

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
			get => _sma.Period;
			set
			{
				_sma.Period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public MeanDev()
		{
			Panel = IndicatorDataProvider.NewPanel;
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
				sum += Math.Abs((decimal)SourceDataSeries[i] - sma);

			this[bar] = sum / count;
		}

		#endregion
	}
}