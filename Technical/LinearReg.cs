namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("LinearReg")]
	[LocalizedDescription(typeof(Resources), "LinearReg")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/7069-linearreg")]
	public class LinearReg : Indicator
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

			var x = 0m;
			var y = 0m;
			var xy = 0m;
			var x2 = 0m;

			for (var i = start; i < start + count; i++)
			{
				var val = (decimal)SourceDataSeries[i];

				x += i;
				x2 += i * i;

				y += val;
				xy += i * val;
			}

			var k = count * x2 - x * x;

			k = k == 0
				? 0
				: (count * xy - x * y) / k;

			this[bar] = k * bar + (y - k * x) / count;
		}

		#endregion
	}
}