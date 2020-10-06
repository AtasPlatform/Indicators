namespace ATAS.Indicators.Technical
{
	using System;
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	[DisplayName("Momentum")]
	[LocalizedDescription(typeof(Resources), "Momentum")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/7083-momentum")]
	public class Momentum : Indicator
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

		public Momentum()
		{
			Panel = IndicatorDataProvider.NewPanel;
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			var start = Math.Max(0, bar - Period + 1);
			this[bar] = value - (decimal)SourceDataSeries[start];
		}

		#endregion
	}
}