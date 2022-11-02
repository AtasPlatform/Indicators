namespace ATAS.Indicators.Technical
{
	using System.ComponentModel;
	using System.ComponentModel.DataAnnotations;

	using ATAS.Indicators.Technical.Properties;

	using OFT.Attributes;

	using Utils.Common.Localization;

	[DisplayName("SMMA")]
	[LocalizedDescription(typeof(Resources), "SMMA")]
	[HelpLink("https://support.atas.net/knowledge-bases/2/articles/9198-smma")]
	public class SMMA : Indicator
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
			this[bar] = bar == 0 
				? value 
				: this[bar] = (this[bar - 1] * (Period - 1) + value) / Period;
		}

		#endregion
	}
}