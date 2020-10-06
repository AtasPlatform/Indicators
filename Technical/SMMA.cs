namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using Utils.Common.Attributes;
	using Utils.Common.Localization;

	[DisplayName("SMMA")]
	[LocalizedDescription(typeof(Resources), "SMMA")]
	[HelpLink("https://support.orderflowtrading.ru/knowledge-bases/2/articles/9198-smma")]
	public class SMMA : Indicator
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

		public SMMA()
		{
			Period = 10;
		}

		#endregion

		#region Protected methods

		protected override void OnCalculate(int bar, decimal value)
		{
			if (bar == 0)
				this[bar] = value;
			else
				this[bar] = (this[bar - 1] * (Period - 1) + value) / Period;
		}

		#endregion
	}
}