namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;
    using Utils.Common.Localization;

	[DisplayName("Highest")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.HighestIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602627")]
	public class Highest : Indicator
	{
		#region Fields

		private int _period;

		#endregion

		#region Properties

		[Parameter]
        [Range(1, 10000)]
        [Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.PeriodDescription),
            Order = 20)]
		public int Period
		{
			get => _period;
			set
			{
				_period = value;
				RecalculateValues();
			}
		}

		#endregion

		#region ctor

		public Highest()
		{
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var start = Math.Max(0, bar - Period + 1);
			var count = Math.Min(bar + 1, Period);

			var max = (decimal)SourceDataSeries[start];

			for (var i = start + 1; i < start + count; i++)
				max = Math.Max(max, (decimal)SourceDataSeries[i]);

			this[bar] = max;
		}

		#endregion
	}
}