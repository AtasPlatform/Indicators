namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("Highest")]
	[LocalizedDescription(typeof(Resources), "Highest")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/6716-highest")]
	public class Highest : Indicator
	{
		#region Fields

		private int _period;

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