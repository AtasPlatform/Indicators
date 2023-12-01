namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using OFT.Attributes;
    using OFT.Localization;

	[DisplayName("Lowest")]
    [Display(ResourceType = typeof(Strings), Description = nameof(Strings.LowestIndDescription))]
    [HelpLink("https://help.atas.net/en/support/solutions/articles/72000602417")]
	public class Lowest : Indicator
	{
		#region Fields

		private int _period = 10;

		#endregion

		#region Properties

		[Parameter]
		[Display(ResourceType = typeof(Strings),
			Name = nameof(Strings.Period),
			GroupName = nameof(Strings.Common),
            Description = nameof(Strings.PeriodDescription),
            Order = 20)]
		[Range(1, 10000)]
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

        #region Protected methods

        protected override void OnCalculate(int bar, decimal value)
		{
			var start = Math.Max(0, bar - Period + 1);
			var count = Math.Min(bar + 1, Period);

			var min = (decimal)SourceDataSeries[start];

			for (var i = start + 1; i < start + count; i++)
				min = Math.Min(min, (decimal)SourceDataSeries[i]);

			this[bar] = min;
		}

		#endregion
	}
}