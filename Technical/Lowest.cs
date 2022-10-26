namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("Lowest")]
	[LocalizedDescription(typeof(Resources), "Lowest")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7073-lowest")]
	public class Lowest : Indicator
	{
		#region Fields

		private int _period = 10;

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